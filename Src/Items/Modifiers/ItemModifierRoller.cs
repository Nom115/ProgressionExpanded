using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using ProgressionExpanded.Items.Rarity;
using ProgressionExpanded.Src.NPCs;
using ProgressionExpanded.Src.NPCs.Enemy;
using ProgressionExpanded.Src.Levels.WorldLevel;

namespace ProgressionExpanded.Items.Modifiers
{
	/// <summary>
	/// Rolls item rarity and modifiers. Mirrors the enemy-side <c>EnemyRarityConfig.RollRarity</c> +
	/// <c>ModifierPool.RollModifiers</c> (weighted, no-dupe), but adds item-level tier gating so
	/// low-level items can only roll low tiers. The <c>itemLevel</c> passed in is the item's LOCKED
	/// creation-time level: for a drop that is the world level at drop time (== creation); orbs pass
	/// the item's stored level. It is not the "current" world level after the item exists.
	/// </summary>
	public static class ItemModifierRoller
	{
		// Chance a dropped eligible item is upgraded at all (before enemy scaling). Tunable.
		private const float BaseUpgradeChance = 0.12f;
		private const float MaxUpgradeChance = 0.60f;

		#region Rarity

		/// <summary>
		/// Roll the rarity of a dropped item. Higher/rarer source enemies both upgrade more often and
		/// skew toward higher tiers; each tier is gated behind a minimum world level.
		/// </summary>
		public static ItemRarity RollRarity(NPC source, int worldLevel)
		{
			float enemyMult = 1f;
			if (source != null)
			{
				int lvl = source.GetGlobalNPC<NPCLevelManager>().GetLevel(source);
				var enemyRarity = source.GetGlobalNPC<EnemyModifierSystem>().GetRarity();
				enemyMult += lvl * 0.01f;                 // +1% per enemy level
				enemyMult += (int)enemyRarity * 0.25f;    // rarer enemies drop better gear
			}

			float chance = Math.Min(BaseUpgradeChance * enemyMult, MaxUpgradeChance);
			if (Main.rand.NextFloat() >= chance)
				return ItemRarity.Normal;

			// Candidate tiers gated by world level: (rarity, minWorldLevel, baseWeight).
			var gates = new (ItemRarity rarity, int gate, float weight)[]
			{
				(ItemRarity.Uncommon,  1,  1000f),
				(ItemRarity.Magic,     5,  480f),
				(ItemRarity.Rare,      15, 200f),
				(ItemRarity.Mythic,    30, 60f),
				(ItemRarity.Legendary, 55, 12f),
			};

			var pick = new List<(ItemRarity rarity, float weight)>();
			float total = 0f;
			foreach (var g in gates)
			{
				if (worldLevel < g.gate) continue;
				// Stronger enemies skew toward higher tiers (weight * enemyMult^tierIndex).
				int tierIndex = (int)g.rarity - 1;
				float w = g.weight * (float)Math.Pow(enemyMult, tierIndex);
				pick.Add((g.rarity, w));
				total += w;
			}

			if (pick.Count == 0)
				return ItemRarity.Uncommon;

			float roll = Main.rand.NextFloat(total);
			float cur = 0f;
			foreach (var p in pick)
			{
				cur += p.weight;
				if (roll < cur) return p.rarity;
			}
			return pick[pick.Count - 1].rarity;
		}

		#endregion

		#region Modifiers

		/// <summary>
		/// Roll <paramref name="count"/> distinct modifiers from a pool, each at a world-level-gated
		/// tier with a value rolled in that tier's band.
		/// </summary>
		public static List<RolledModifier> RollModifiers(string poolId, int count, int itemLevel, ICollection<string> excludeModIds = null)
		{
			var result = new List<RolledModifier>();
			var pool = ItemModifierLoader.GetPool(poolId);
			if (pool == null || count <= 0)
				return result;

			var available = excludeModIds == null || excludeModIds.Count == 0
				? new List<ItemModifierDefinition>(pool.Modifiers)
				: pool.Modifiers.Where(m => !excludeModIds.Contains(m.ModId)).ToList();

			for (int i = 0; i < count && available.Count > 0; i++)
			{
				var selected = WeightedPick(available, d => Math.Max(1, d.Weight));
				if (selected == null) break;
				available.Remove(selected);

				var eligible = selected.GetEligibleTiers(itemLevel);
				if (eligible.Count == 0)
					eligible.Add(1); // always allow the lowest tier

				int tierNum = WeightedTierPick(selected, eligible);
				var tier = selected.GetTier(tierNum);
				float value = tier != null ? RollValue(tier) : 0f;

				result.Add(new RolledModifier(poolId, selected.ModId, selected.StatKey, selected.Keyword, tierNum, value));
			}

			return result;
		}

		/// <summary>
		/// Roll a rarity's worth of modifiers onto an item and store the state. Shared by the drop
		/// path and by the crafting orbs.
		/// </summary>
		public static void ApplyRarity(Item item, ItemRarity rarity, int itemLevel)
		{
			string poolId = ItemCategoryClassifier.GetPoolId(item);
			if (poolId == null) return;

			var state = ItemModifierSystem.GetState(item);
			if (state == null) return;

			int count = ItemRarityConfig.GetModCount(rarity);
			var rolled = RollModifiers(poolId, count, itemLevel);
			state.SetState(rarity, itemLevel, rolled);
		}

		/// <summary>Full drop-time roll: classify, roll rarity from the source enemy, roll mods.</summary>
		public static void RollDrop(Item item, NPC source)
		{
			if (!ItemCategoryClassifier.IsEligible(item))
				return;

			string poolId = ItemCategoryClassifier.GetPoolId(item);
			if (ItemModifierLoader.GetPool(poolId) == null)
				return;

			int worldLevel = WorldLevelManager.GetWorldLevel();
			ItemRarity rarity = RollRarity(source, worldLevel);
			if (rarity == ItemRarity.Normal)
				return;

			ApplyRarity(item, rarity, worldLevel);
		}

		/// <summary>Divine-orb behaviour: re-roll each modifier's value within its current tier band.</summary>
		public static void RerollValues(List<RolledModifier> mods)
		{
			if (mods == null) return;
			foreach (var mod in mods)
			{
				var tier = ItemModifierLoader.GetDefinition(mod.PoolId, mod.ModId)?.GetTier(mod.Tier);
				if (tier != null)
					mod.Value = RollValue(tier);
			}
		}

		#endregion

		#region Helpers

		private static ItemModifierDefinition WeightedPick(List<ItemModifierDefinition> defs, Func<ItemModifierDefinition, int> weight)
		{
			int total = defs.Sum(weight);
			if (total <= 0) return defs.Count > 0 ? defs[0] : null;

			int roll = Main.rand.Next(total);
			int cur = 0;
			foreach (var def in defs)
			{
				cur += weight(def);
				if (roll < cur) return def;
			}
			return defs[defs.Count - 1];
		}

		private static int WeightedTierPick(ItemModifierDefinition def, List<int> eligibleTiers)
		{
			int total = 0;
			foreach (int t in eligibleTiers)
				total += Math.Max(1, def.GetTier(t)?.Weight ?? 1);

			int roll = Main.rand.Next(total);
			int cur = 0;
			foreach (int t in eligibleTiers)
			{
				cur += Math.Max(1, def.GetTier(t)?.Weight ?? 1);
				if (roll < cur) return t;
			}
			return eligibleTiers[eligibleTiers.Count - 1];
		}

		private static float RollValue(ItemModifierTier tier)
		{
			int min = (int)Math.Round(tier.MinValue);
			int max = (int)Math.Round(tier.MaxValue);
			if (max < min) (min, max) = (max, min);
			return Main.rand.Next(min, max + 1);
		}

		#endregion
	}
}
