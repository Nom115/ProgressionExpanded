using System;
using Terraria;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems
{
	/// <summary>
	/// Shared per-player accumulator for "effect" stats that scale the mod's OWN effects (notable
	/// passives, potion healing) rather than a vanilla <see cref="Player"/> field: life leech,
	/// healing effectiveness, on-hit ailment effect, and area-of-effect.
	///
	/// Both item modifiers (via <c>ItemModifierApplier</c>) and passive-tree nodes (via
	/// <c>PassiveTreeManager.ApplyPercentBonusToStat</c>) add into these each frame; the notable
	/// passives (Bloodthirst, the on-hit ailments, Detonate) read them when computing their effect,
	/// so gear/passives can scale an effect <b>past its tier ceiling</b> — and it is generic, not
	/// tied 1:1 to any single passive.
	///
	/// Convention: values are WHOLE PERCENTS (e.g. <see cref="LifeLeechPercent"/> == 3 means 3%);
	/// consumers divide by 100. This ModPlayer persists NOTHING — it is fully recomputed every frame
	/// in <see cref="ResetEffects"/> — so it defaults to 0 for any existing save and never touches
	/// save data (no <c>Initialize</c>/<c>LoadData</c> ordering concern).
	/// </summary>
	public class CombatEffectStats : ModPlayer
	{
		/// <summary>Percent of damage dealt returned as health on hit (read by leech passives).</summary>
		public float LifeLeechPercent;

		/// <summary>Percent increase to healing you receive (our heal procs + potions).</summary>
		public float HealingPercent;

		/// <summary>Percent increase to on-hit ailment/DoT effect (debuff durations).</summary>
		public float AilmentPercent;

		/// <summary>Percent increase to area-of-effect (explosion radius/damage).</summary>
		public float AreaPercent;

		public override void ResetEffects()
		{
			LifeLeechPercent = 0f;
			HealingPercent = 0f;
			AilmentPercent = 0f;
			AreaPercent = 0f;
		}

		/// <summary>Convenience accessor for the item applier / notables.</summary>
		public static CombatEffectStats Get(Player player) => player.GetModPlayer<CombatEffectStats>();

		/// <summary>
		/// Amplify potion (and other item) healing by <see cref="HealingPercent"/>, so "healing"
		/// gear affects all self-healing, not just the Bloodthirst proc.
		/// </summary>
		public override void GetHealLife(Item item, bool quickHeal, ref int healValue)
		{
			if (HealingPercent != 0f && healValue > 0)
				healValue += (int)Math.Round(healValue * HealingPercent / 100f);
		}
	}
}
