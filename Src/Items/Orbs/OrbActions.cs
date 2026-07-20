using System.Collections.Generic;
using System.Linq;
using Terraria;
using ProgressionExpanded.Items.Modifiers;
using ProgressionExpanded.Items.Rarity;
using ProgressionExpanded.Src.Levels.WorldLevel;

namespace ProgressionExpanded.Items.Orbs
{
	/// <summary>Result of applying an orb, with a user-facing message for the UI.</summary>
	public struct OrbResult
	{
		public bool Success;
		public string Message;

		public static OrbResult Ok(string message) => new OrbResult { Success = true, Message = message };
		public static OrbResult Fail(string message) => new OrbResult { Success = false, Message = message };
	}

	/// <summary>
	/// The PoE-style crafting operations orbs perform on an item's <see cref="ItemModifierSystem"/>
	/// state. All operations validate first and only mutate on success. Item level is LOCKED at the
	/// item's creation (see <see cref="ItemModifierSystem.EnsureItemLevel"/>): every craft rolls
	/// against that fixed level, not the current world level, so gear is tied to when you obtained it
	/// and does not "catch up" by re-rolling. Items that predate the lock are stamped on first use.
	/// </summary>
	public static class OrbActions
	{
		#region Queries (used by orbs for CanApplyTo / UI enabling)

		public static bool IsEligible(Item item) => ItemCategoryClassifier.IsEligible(item);

		/// <summary>
		/// The item's locked (creation-time) level. Falls back to stamping the current world level once
		/// for items that predate the lock (e.g. worldgen-chest loot never observed at creation).
		/// </summary>
		private static int LockedItemLevel(Item item, ItemModifierSystem state)
		{
			state.EnsureItemLevel(item);
			int ilvl = state.GetItemLevel();
			return ilvl > 0 ? ilvl : WorldLevelManager.GetWorldLevel();
		}

		public static ItemRarity GetRarity(Item item)
		{
			var state = ItemModifierSystem.GetState(item);
			return state != null ? state.GetRarity() : ItemRarity.Normal;
		}

		public static int ModCount(Item item)
		{
			var state = ItemModifierSystem.GetState(item);
			return state?.GetMods()?.Count ?? 0;
		}

		public static int MaxModsFor(Item item) => ItemRarityConfig.GetModCount(GetRarity(item));

		#endregion

		#region Upgrade orbs — raise rarity one step, keep existing mods, add new ones

		public static OrbResult UpgradeTo(Item item, ItemRarity from, ItemRarity to)
		{
			if (!IsEligible(item))
				return OrbResult.Fail("This item can't be modified.");

			var state = ItemModifierSystem.GetState(item);
			if (state == null)
				return OrbResult.Fail("This item can't be modified.");

			if (state.GetRarity() != from)
				return OrbResult.Fail($"Requires a {ItemRarityConfig.GetInfo(from).Name} item.");

			int itemLevel = LockedItemLevel(item, state);
			var mods = new List<RolledModifier>(state.GetMods());

			int targetCount = ItemRarityConfig.GetModCount(to);
			int toAdd = targetCount - mods.Count;
			if (toAdd > 0)
			{
				var existingIds = mods.Select(m => m.ModId).ToHashSet();
				mods.AddRange(ItemModifierRoller.RollModifiers(ItemCategoryClassifier.GetPoolId(item), toAdd, itemLevel, existingIds));
			}

			state.SetState(to, itemLevel, mods);
			return OrbResult.Ok($"Upgraded to {ItemRarityConfig.GetInfo(to).Name}!");
		}

		#endregion

		#region Reroll / value / annul / augment

		/// <summary>Verdant Paradox — reroll every current modifier for a fresh set (same rarity).</summary>
		public static OrbResult Reroll(Item item)
		{
			var state = ItemModifierSystem.GetState(item);
			if (state == null || !IsEligible(item))
				return OrbResult.Fail("This item can't be modified.");

			var mods = state.GetMods();
			if (mods == null || mods.Count == 0)
				return OrbResult.Fail("This item has no modifiers to reroll.");

			int itemLevel = LockedItemLevel(item, state);
			var rerolled = ItemModifierRoller.RollModifiers(ItemCategoryClassifier.GetPoolId(item), mods.Count, itemLevel);
			state.SetState(state.GetRarity(), itemLevel, rerolled);
			return OrbResult.Ok("Modifiers rerolled!");
		}

		/// <summary>Divine Orb — reroll the numeric values of the existing modifiers only.</summary>
		public static OrbResult DivineReroll(Item item)
		{
			var state = ItemModifierSystem.GetState(item);
			if (state == null)
				return OrbResult.Fail("This item can't be modified.");

			var mods = state.GetMods();
			if (mods == null || mods.Count == 0)
				return OrbResult.Fail("This item has no modifiers to reroll.");

			ItemModifierRoller.RerollValues(mods);
			return OrbResult.Ok("Modifier values rerolled!");
		}

		/// <summary>Orb of Annulment — remove one random modifier.</summary>
		public static OrbResult Annul(Item item)
		{
			var state = ItemModifierSystem.GetState(item);
			if (state == null)
				return OrbResult.Fail("This item can't be modified.");

			var mods = state.GetMods();
			if (mods == null || mods.Count == 0)
				return OrbResult.Fail("This item has no modifiers to remove.");

			int index = Main.rand.Next(mods.Count);
			mods.RemoveAt(index);
			state.SetState(state.GetRarity(), state.GetItemLevel(), mods);
			return OrbResult.Ok("A modifier was annulled.");
		}

		/// <summary>Orb of Augmentation — add one modifier if below the rarity's max mod count.</summary>
		public static OrbResult Augment(Item item)
		{
			var state = ItemModifierSystem.GetState(item);
			if (state == null || !IsEligible(item))
				return OrbResult.Fail("This item can't be modified.");

			if (state.GetRarity() == ItemRarity.Normal)
				return OrbResult.Fail("Transmute the item first.");

			var mods = new List<RolledModifier>(state.GetMods());
			int maxCount = ItemRarityConfig.GetModCount(state.GetRarity());
			if (mods.Count >= maxCount)
				return OrbResult.Fail($"This item is already full ({mods.Count}/{maxCount}).");

			int itemLevel = LockedItemLevel(item, state);
			var existingIds = mods.Select(m => m.ModId).ToHashSet();
			var extra = ItemModifierRoller.RollModifiers(ItemCategoryClassifier.GetPoolId(item), 1, itemLevel, existingIds);
			if (extra.Count == 0)
				return OrbResult.Fail("No new modifier available to add.");

			mods.AddRange(extra);
			state.SetState(state.GetRarity(), itemLevel, mods);
			return OrbResult.Ok("A modifier was added!");
		}

		#endregion
	}
}
