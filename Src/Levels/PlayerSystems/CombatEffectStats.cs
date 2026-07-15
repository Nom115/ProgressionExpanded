using System;
using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Src.NPCs.Enemy.Elemental;

namespace ProgressionExpanded.Src.Levels.PlayerSystems
{
	/// <summary>
	/// Shared per-player accumulator for "effect" stats that scale the mod's OWN effects (notable
	/// passives, potion healing) rather than a vanilla <see cref="Player"/> field: life leech,
	/// healing effectiveness, on-hit ailment effect, and area-of-effect.
	///
	/// Both item modifiers (via <c>ItemModifierApplier</c>) and passive-tree nodes (via
	/// <c>PassiveTreeManager.ApplyPercentBonusToStat</c>) add into these each frame; the effects
	/// themselves (<c>LifeLeechApplier</c>, the on-hit ailments, Detonate) read them when computing
	/// their result, so gear/passives can scale an effect <b>past its tier ceiling</b> — and it is
	/// generic, not tied 1:1 to any single passive.
	///
	/// Each field needs a consumer that runs whether or not any particular passive is allocated.
	/// LifeLeechPercent used to be read ONLY inside Bloodthirst, which quietly made leech rolled onto
	/// an item do nothing at all unless that one mastery happened to be allocated. If you add a field
	/// here, give it an owner — an accumulator with a conditional reader is a stat that lies.
	///
	/// Convention: values are WHOLE PERCENTS (e.g. <see cref="LifeLeechPercent"/> == 3 means 3%);
	/// consumers divide by 100. This ModPlayer persists NOTHING — it is fully recomputed every frame
	/// in <see cref="ResetEffects"/> — so it defaults to 0 for any existing save and never touches
	/// save data (no <c>Initialize</c>/<c>LoadData</c> ordering concern).
	/// </summary>
	public class CombatEffectStats : ModPlayer
	{
		/// <summary>
		/// Percent of damage dealt returned as health on hit. Every leech source in the mod (gear
		/// modifiers, Bloodthirst, Devourer, Vengeance) pools into this one number, and
		/// <see cref="LifeLeechApplier"/> is its sole consumer — it owns the cooldown and the
		/// per-hit cap. Add here to grant leech; do not write another on-hit heal.
		/// </summary>
		public float LifeLeechPercent;

		/// <summary>Percent increase to healing you receive (our heal procs + potions).</summary>
		public float HealingPercent;

		/// <summary>Percent increase to on-hit ailment/DoT effect (debuff durations).</summary>
		public float AilmentPercent;

		/// <summary>Percent increase to area-of-effect (explosion radius/damage).</summary>
		public float AreaPercent;

		/// <summary>
		/// Percent of the damage you deal that is added, per second, as damage-over-time of each
		/// element. Indexed by <c>(int)DamageElement</c>.
		///
		/// Every conversion source pools here — the five elemental masteries, Plaguebearer, and
		/// (Phase 3) rolled item modifiers — and <see cref="ElementalDotApplier"/> is the sole
		/// consumer, exactly as <see cref="LifeLeechApplier"/> is for LifeLeechPercent. It owns the
		/// duration, the resistance lookup, the stack cap and the buff icon. Grant conversion by
		/// adding here; do not apply a debuff yourself, because a debuff on its own now deals
		/// nothing at all.
		///
		/// Readonly and cleared rather than reallocated: this runs every frame for every player.
		/// </summary>
		public readonly float[] ElementalConversion = new float[DamageElementInfo.Count];

		public override void ResetEffects()
		{
			LifeLeechPercent = 0f;
			HealingPercent = 0f;
			AilmentPercent = 0f;
			AreaPercent = 0f;
			Array.Clear(ElementalConversion, 0, ElementalConversion.Length);
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
