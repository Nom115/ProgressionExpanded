using Microsoft.Xna.Framework;
using System;
using Terraria.ID;

namespace ProgressionExpanded.Src.NPCs.Enemy.Elemental
{
	/// <summary>
	/// The five damage elements a hit can be converted into.
	///
	/// Fire, Cold, Lightning and Poison are elemental and face elemental resistance. Bleed is
	/// physical: it ignores the rolled/warded elemental resistance and is limited by the enemy's armour
	/// instead, which makes it the reliable answer to a heavily-resistant target. See
	/// <see cref="ElementalResistance"/>.
	///
	/// The numbering is load-bearing. Each value doubles as an index into CombatEffectStats'
	/// ElementalConversion and ElementalPenetration pools, so those arrays are sized by
	/// <see cref="DamageElementInfo.Count"/>. Append only — inserting a value in the middle silently
	/// reindexes every one of them.
	/// </summary>
	public enum DamageElement
	{
		Fire = 0,
		Cold = 1,
		Lightning = 2,
		Poison = 3,
		Bleed = 4,
	}

	/// <summary>
	/// Per-element facts: the vanilla buff that carries each element's icon, its display colour, and
	/// the item-stat-key convention.
	/// </summary>
	public static class DamageElementInfo
	{
		/// <summary>Number of elements. Sizes every per-element array in the mod.</summary>
		public const int Count = 5;

		/// <summary>
		/// The vanilla buff id that matches this element, kept as a per-element fact.
		///
		/// Currently unused by the applier: since the true-conversion rework the elemental damage is
		/// instant and folded into the hit number, so no carrier debuff is applied. Retained because it
		/// is the natural home for the mapping if a future brief hit-flash icon or UI wants it, and it
		/// stays in sync with the enemy affixes (BleedingModifier, IgnitingModifier, …) that use the
		/// same ids on the player side.
		/// </summary>
		public static int BuffIdFor(DamageElement element)
		{
			switch (element)
			{
				case DamageElement.Fire: return BuffID.OnFire;
				case DamageElement.Cold: return BuffID.Frostburn;
				case DamageElement.Lightning: return BuffID.Electrified;
				case DamageElement.Poison: return BuffID.Venom;
				default: return BuffID.Bleeding;
			}
		}

		/// <summary>
		/// A display colour for this element, so the hover panel and any future UI colour their rows
		/// consistently. Lives here because this file is the single home for per-element facts; there
		/// is no other element-colour table in the mod.
		/// </summary>
		public static Color ColorFor(DamageElement element)
		{
			switch (element)
			{
				case DamageElement.Fire: return new Color(255, 120, 40);
				case DamageElement.Cold: return new Color(110, 200, 255);
				case DamageElement.Lightning: return new Color(255, 235, 90);
				case DamageElement.Poison: return new Color(150, 220, 70);
				default: return new Color(220, 70, 70); // Bleed — physical, dark red.
			}
		}

		/// <summary>
		/// Parse an item modifier's stat key of the form <c>"Conversion&lt;Element&gt;"</c> or
		/// <c>"Penetration&lt;Element&gt;"</c> (e.g. <c>"ConversionFire"</c>, <c>"PenetrationCold"</c>)
		/// into its element and family. Returns false for any key that is not one of these, so the
		/// item applier can fall through to its normal "unknown key ignored" behaviour.
		///
		/// Five distinct keys per family were chosen over one key with an element field precisely so
		/// that this — and the whole item pipeline — needs no schema or save-format change: the key
		/// string carries the element. This is the single place that convention is decoded.
		/// </summary>
		public static bool TryParseItemStatKey(string key, out DamageElement element, out bool isPenetration)
		{
			element = default;
			isPenetration = false;
			if (string.IsNullOrEmpty(key))
				return false;

			string remainder;
			if (key.StartsWith("Conversion", StringComparison.Ordinal))
			{
				isPenetration = false;
				remainder = key.Substring("Conversion".Length);
			}
			else if (key.StartsWith("Penetration", StringComparison.Ordinal))
			{
				isPenetration = true;
				remainder = key.Substring("Penetration".Length);
			}
			else
			{
				return false;
			}

			return Enum.TryParse(remainder, out element) && Enum.IsDefined(typeof(DamageElement), element);
		}
	}
}
