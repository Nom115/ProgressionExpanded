using System;
using System.Collections.Generic;

namespace ProgressionExpanded.Items.Modifiers
{
	/// <summary>
	/// One rollable magnitude band of a modifier. A modifier has up to 10 of these; higher tiers
	/// carry bigger values and are gated behind a higher world level (the item-level gate).
	/// </summary>
	public class ItemModifierTier
	{
		/// <summary>Inclusive lower bound of the rolled value.</summary>
		public float MinValue { get; set; }

		/// <summary>Inclusive upper bound of the rolled value.</summary>
		public float MaxValue { get; set; }

		/// <summary>Minimum world level (item level) required for this tier to be rollable.</summary>
		public int RequiredWorldLevel { get; set; } = 1;

		/// <summary>Selection weight among the eligible tiers.</summary>
		public int Weight { get; set; } = 100;
	}

	/// <summary>
	/// JSON-defined modifier template (the item-side analogue of a passive-tree node / an
	/// <c>IModifier</c>). Loaded by <see cref="ItemModifierLoader"/> and rolled into a
	/// <see cref="RolledModifier"/> per item.
	/// </summary>
	public class ItemModifierDefinition
	{
		/// <summary>Unique id within its pool (e.g. "increased_armor").</summary>
		public string ModId { get; set; }

		/// <summary>
		/// Tooltip template. Use "{value}" as the placeholder for the rolled magnitude,
		/// e.g. "{value}% increased Armour" or "+{value} Maximum Life".
		/// </summary>
		public string DisplayName { get; set; }

		/// <summary>"increased" (additive pool), "more" (compounding multiplier), or "flat".</summary>
		public string Keyword { get; set; } = "increased";

		/// <summary>Which stat this affects — reuses the passive-tree stat key set (+ "ArmorIncrease").</summary>
		public string StatKey { get; set; }

		/// <summary>
		/// Optional mutual-exclusion group. At most one modifier of a given non-empty group can roll
		/// onto a single item (the roller drops the whole group once one member is picked). Used to keep
		/// an item to one elemental conversion and one penetration mod at a time. Empty/null = no group.
		/// </summary>
		public string Group { get; set; }

		/// <summary>Weighted-selection weight when rolling which mods land on an item.</summary>
		public int Weight { get; set; } = 100;

		/// <summary>Magnitude bands, index 0 = tier 1. Up to 10 entries.</summary>
		public List<ItemModifierTier> Tiers { get; set; } = new List<ItemModifierTier>();

		/// <summary>Highest tier number (1-based). 0 if no tiers defined.</summary>
		public int MaxTier => Tiers?.Count ?? 0;

		/// <summary>Get a tier band by 1-based tier number, or null if out of range.</summary>
		public ItemModifierTier GetTier(int tier)
		{
			if (Tiers == null || tier < 1 || tier > Tiers.Count) return null;
			return Tiers[tier - 1];
		}

		/// <summary>
		/// The 1-based tier numbers whose <see cref="ItemModifierTier.RequiredWorldLevel"/> is met
		/// by the given item level. Used to gate high tiers behind world progression.
		/// </summary>
		public List<int> GetEligibleTiers(int itemLevel)
		{
			var result = new List<int>();
			if (Tiers == null) return result;
			for (int i = 0; i < Tiers.Count; i++)
			{
				if (Tiers[i].RequiredWorldLevel <= itemLevel)
					result.Add(i + 1);
			}
			return result;
		}

		/// <summary>
		/// Render the tooltip line for a rolled value. Substitutes "{value}" in
		/// <see cref="DisplayName"/>, or falls back to "+value StatKey".
		/// </summary>
		public string Format(float value)
		{
			string valStr = FormatValue(value);
			if (!string.IsNullOrEmpty(DisplayName) && DisplayName.Contains("{value}"))
				return DisplayName.Replace("{value}", valStr);

			string label = string.IsNullOrEmpty(DisplayName) ? (StatKey ?? ModId) : DisplayName;
			return $"+{valStr} {label}";
		}

		private static string FormatValue(float value)
		{
			return Math.Abs(value - (float)Math.Round(value)) < 0.001f
				? ((int)Math.Round(value)).ToString()
				: value.ToString("0.#");
		}
	}
}
