using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ProgressionExpanded.Items.Modifiers;
using ProgressionExpanded.Items.Rarity;
using ProgressionExpanded.Src.Levels.WorldLevel;

namespace ProgressionExpanded.Items
{
	/// <summary>
	/// Per-item rarity + rolled-modifier state. The item-side analogue of
	/// <c>EnemyModifierSystem</c>: it self-persists (SaveData/LoadData/Clone), renders the item's
	/// rarity/modifiers in the tooltip, and (in later phases) applies their stats via the weapon /
	/// equip hooks. Storage uses the same nested/JSON-string idiom proven in
	/// <c>ItemDataManager</c> and <c>PassiveTreeManager.SaveData</c>.
	/// </summary>
	public class ItemModifierSystem : GlobalItem
	{
		public override bool InstancePerEntity => true;

		// Per-item instance state.
		private ItemRarity rarity = ItemRarity.Normal;
		private int itemLevel = 0;
		private List<RolledModifier> mods = new List<RolledModifier>();

		private static readonly Color ModLineColor = new Color(120, 190, 255);

		#region Accessors / mutation (used by the roller, orbs, and UI)

		public static ItemModifierSystem GetState(Item item) => item.GetGlobalItem<ItemModifierSystem>();

		public ItemRarity GetRarity() => rarity;
		public int GetItemLevel() => itemLevel;
		public List<RolledModifier> GetMods() => mods;
		public bool HasRolled => rarity != ItemRarity.Normal || (mods != null && mods.Count > 0);

		/// <summary>Replace the full rolled state. Callers own building the mod list.</summary>
		public void SetState(ItemRarity newRarity, int newItemLevel, List<RolledModifier> newMods)
		{
			rarity = newRarity;
			itemLevel = newItemLevel;
			mods = newMods ?? new List<RolledModifier>();
		}

		/// <summary>
		/// Lock this item's level to the current world level the first time it is created — for ANY
		/// rarity, white items included. Item level is a creation-time property from then on: orbs roll
		/// against it rather than re-stamping the current world level, so gear is tied to when you got
		/// it (see OrbActions). Idempotent — itemLevel == 0 means "unset". Only eligible items
		/// (weapons/armor/accessories) carry a level, since it only gates modifier tiers.
		/// </summary>
		public void EnsureItemLevel(Item item)
		{
			if (itemLevel != 0)
				return;
			if (!ItemCategoryClassifier.IsEligible(item))
				return;
			itemLevel = WorldLevelManager.GetWorldLevel();
		}

		#endregion

		#region Drop rolling

		public override void OnSpawn(Item item, IEntitySource source)
		{
			// Rarity/mod rolls are authoritative on the server / in single-player. (MP client sync
			// is a known out-of-scope limitation.)
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;

			// Only enemy-loot drops get rolled; crafting/other spawns stay untouched.
			if (source is EntitySource_Loot loot && loot.Entity is NPC npc)
				ItemModifierRoller.RollDrop(item, npc);

			// Lock the item level at creation for every eligible item, regardless of rarity. A Normal
			// drop (RollDrop returns early without setting state) still gets its level stamped here.
			EnsureItemLevel(item);
		}

		/// <summary>Crafted / bought / recipe-group items: lock their level in at creation too.</summary>
		public override void OnCreated(Item item, ItemCreationContext context)
		{
			EnsureItemLevel(item);
		}

		#endregion

		#region Tooltip

		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
			bool rolled = rarity != ItemRarity.Normal || (mods != null && mods.Count > 0);

			// A plain, unleveled item has nothing to show.
			if (!rolled && itemLevel == 0)
				return;

			int nameIndex = tooltips.FindIndex(l => l.Mod == "Terraria" && l.Name == "ItemName");
			var insert = new List<TooltipLine>();

			if (rolled)
			{
				var info = ItemRarityConfig.GetInfo(rarity);

				// Recolor the item name to its rarity colour.
				if (nameIndex >= 0)
					tooltips[nameIndex].OverrideColor = info.Color;

				// A rarity label, then one line per rolled modifier.
				insert.Add(new TooltipLine(Mod, "PE_Rarity", $"{info.Name} Item") { OverrideColor = info.Color });

				if (mods != null)
				{
					foreach (var rolledMod in mods)
					{
						var def = ItemModifierLoader.GetDefinition(rolledMod.PoolId, rolledMod.ModId);
						string text = def != null ? def.Format(rolledMod.Value) : $"+{rolledMod.Value} {rolledMod.ModId}";
						insert.Add(new TooltipLine(Mod, "PE_Mod_" + rolledMod.ModId, text) { OverrideColor = ModLineColor });
					}
				}
			}

			// Item level (transparency about tier gating) — shown on every eligible item, even white
			// ones, since the level is now locked at creation and drives what an orb can roll.
			insert.Add(new TooltipLine(Mod, "PE_ItemLevel", $"Item Level {itemLevel}")
			{
				OverrideColor = new Color(150, 150, 150)
			});

			int insertAt = nameIndex >= 0 ? nameIndex + 1 : 0;
			tooltips.InsertRange(insertAt, insert);
		}

		// Never merge a rolled item with another stack (it would lose its unique modifiers).
		// Equipment is maxStack 1 anyway; this guards modded maxStack>1 gear that gets rolled.
		public override bool CanStack(Item destination, Item source)
		{
			if (GetState(destination).HasRolled || GetState(source).HasRolled)
				return false;
			return base.CanStack(destination, source);
		}

		#endregion

		#region Stat application

		// Weapon per-hit stats (only fire for the item being used).
		public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
		{
			if (mods != null && mods.Count > 0)
				ItemModifierApplier.ApplyWeaponDamage(mods, ref damage);
		}

		public override void ModifyWeaponCrit(Item item, Player player, ref float crit)
		{
			if (mods != null && mods.Count > 0)
				ItemModifierApplier.ApplyWeaponCrit(mods, ref crit);
		}

		public override void ModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
		{
			if (mods != null && mods.Count > 0)
				ItemModifierApplier.ApplyWeaponKnockback(mods, ref knockback);
		}

		// Player-wide stats while worn (armor slot).
		public override void UpdateEquip(Item item, Player player)
		{
			if (mods != null && mods.Count > 0)
				ItemModifierApplier.ApplyEquipMods(player, item, mods);
		}

		// Player-wide stats while equipped as an accessory.
		public override void UpdateAccessory(Item item, Player player, bool hideVisual)
		{
			if (mods != null && mods.Count > 0)
				ItemModifierApplier.ApplyEquipMods(player, item, mods);
		}

		#endregion

		#region Persistence

		public override void SaveData(Item item, TagCompound tag)
		{
			try
			{
				// Persist if the item is rolled OR carries a locked creation-time level (white items
				// now keep their level, so an orb later rolls against the item's origin, not "now").
				if (rarity == ItemRarity.Normal && (mods == null || mods.Count == 0) && itemLevel == 0)
					return;

				tag["pe_rarity"] = (int)rarity;
				tag["pe_ilvl"] = itemLevel;

				if (mods != null && mods.Count > 0)
					tag["pe_mods"] = JsonConvert.SerializeObject(mods);
			}
			catch (Exception ex)
			{
				Mod.Logger.Error($"Failed to save item modifier data: {ex.Message}");
			}
		}

		public override void LoadData(Item item, TagCompound tag)
		{
			try
			{
				rarity = ItemRarityConfig.FromInt(tag.GetInt("pe_rarity"));
				itemLevel = tag.GetInt("pe_ilvl");
				mods = new List<RolledModifier>();

				if (tag.ContainsKey("pe_mods"))
				{
					string json = tag.GetString("pe_mods");
					if (!string.IsNullOrEmpty(json))
					{
						var loaded = JsonConvert.DeserializeObject<List<RolledModifier>>(json);
						if (loaded != null)
							mods = loaded;
					}
				}

				// Strip state from anything that should never have been rolled in the first place.
				// Ammo and coins used to classify as weapons (they carry a damage value for the
				// weapon that fires them), so existing saves have stacks of currency with real
				// modifiers baked onto them. Fixing the classifier alone would leave those in place
				// forever, because rolled state is stored per item.
				if ((rarity != ItemRarity.Normal || mods.Count > 0 || itemLevel > 0)
					&& !ItemCategoryClassifier.IsEligible(item))
				{
					rarity = ItemRarity.Normal;
					itemLevel = 0;
					mods.Clear();
				}
			}
			catch (Exception ex)
			{
				Mod.Logger.Error($"Failed to load item modifier data: {ex.Message}");
				rarity = ItemRarity.Normal;
				itemLevel = 0;
				mods = new List<RolledModifier>();
			}
		}

		public override GlobalItem Clone(Item from, Item to)
		{
			var clone = (ItemModifierSystem)base.Clone(from, to);
			clone.rarity = rarity;
			clone.itemLevel = itemLevel;
			clone.mods = new List<RolledModifier>();
			if (mods != null)
			{
				foreach (var m in mods)
					clone.mods.Add(m.Copy());
			}
			return clone;
		}

		#endregion
	}
}
