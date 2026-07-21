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

		/// <summary>
		/// Flat HP added to EACH leech proc, on top of the percentage leech. Exists so a source that wants
		/// a flat on-hit heal (Vengeance's damage-taken bonus) can ride the shared leech proc — its ~0.3s
		/// cooldown, its valid-target and not-at-full-life gating — instead of writing a SECOND on-hit heal
		/// with its own cooldown, which CLAUDE.md §8 forbids. <see cref="LifeLeechApplier"/> is the sole
		/// consumer; it adds this after the per-hit leech cap (this is not leech, so the cap must not bound
		/// it) and amplifies it by <see cref="HealingPercent"/> alongside the leech. Whole HP, not a percent.
		/// </summary>
		public float LifeLeechFlatBonus;

		/// <summary>Percent increase to healing you receive (our heal procs + potions).</summary>
		public float HealingPercent;

		/// <summary>
		/// Percent increase to healing from CONSUMABLES only. <see cref="GetHealLife"/> is its sole
		/// consumer — <see cref="LifeLeechApplier"/> deliberately does NOT read it.
		///
		/// This channel exists because of one specific hazard. A source that already scales leech
		/// through <c>damageDone</c> must not ALSO scale the payout, or the same bonus enters the
		/// calculation twice and the result is squared rather than doubled. Vengeance USED to be exactly
		/// that source: its old percent ramp multiplied GetDamage in ResetEffects, so by the time
		/// LifeLeechApplier took its 30% of damageDone the ramp was already baked in, and contributing it
		/// to HealingPercent as well made a full-ramp heal 4x instead of the intended 2x. (Vengeance is a
		/// flat add now and no longer uses this channel; Juggernaut's +50% potion healing still does.)
		///
		/// So the rule is: if your bonus already reaches leech via the damage you dealt, contribute
		/// HERE. If it is a genuine "healing received" bonus that leech should also see (the item
		/// "Healing" roll), contribute to <see cref="HealingPercent"/>. See CLAUDE.md §8.
		/// </summary>
		public float ConsumableHealingPercent;

		/// <summary>
		/// Percent increase to converted elemental damage. <see cref="ElementalConversionApplier"/>
		/// multiplies the elemental slice of each hit by <c>1 + AilmentPercent/100</c>, so it is the
		/// payoff stat for a conversion build (the "increased Elemental Damage" weapon roll).
		///
		/// <b>Repurposed.</b> Before the true-conversion rework this scaled DoT DURATION — meaningless
		/// once the elemental damage became instant. It was renamed in intent (not in StatKey, to avoid
		/// item save/JSON churn — the key is still "AilmentEffect") to scale magnitude instead.
		/// </summary>
		public float AilmentPercent;

		/// <summary>Percent increase to area-of-effect (explosion radius/damage).</summary>
		public float AreaPercent;

		/// <summary>
		/// Percent of the damage you deal that is CONVERTED from armour-facing physical into instant
		/// elemental damage of each element, facing that element's resistance instead of armour. Indexed
		/// by <c>(int)DamageElement</c>. A trade, not a bonus: see <see cref="ElementalConversionApplier"/>
		/// and CLAUDE.md §10.
		///
		/// Every conversion source pools here — the five elemental masteries, Plaguebearer, and rolled
		/// item modifiers — and <see cref="ElementalConversionApplier"/> is the sole consumer, exactly as
		/// <see cref="LifeLeechApplier"/> is for LifeLeechPercent. It owns the split (physical shrunk via
		/// TargetDamageMultiplier, elemental added via ModifyHitInfo), the resistance lookup and the 100%
		/// total cap. Grant conversion by adding here.
		///
		/// Readonly and cleared rather than reallocated: this runs every frame for every player.
		/// </summary>
		public readonly float[] ElementalConversion = new float[DamageElementInfo.Count];

		/// <summary>
		/// Percent of each element's resistance this player ignores, indexed by <c>(int)DamageElement</c>.
		/// Rolled onto weapons/accessories and consumed once, by <see cref="ElementalConversionApplier"/>,
		/// which passes it as the <c>penetration</c> argument to <c>ElementalResistance.Get</c>.
		///
		/// <b>This is a NEW stat, deliberately separate from vanilla ArmorPenetration.</b> Armour pen
		/// already flows into <c>damageDone</c> via <c>NPC.HitModifiers.ArmorPenetration</c>, so routing
		/// it here as well would count it twice (the Vengeance-style double-dip the resistance class warns
		/// about). Nothing here ever touches <c>player.GetArmorPenetration</c>.
		///
		/// <c>ElementalPenetration[Bleed]</c> stays 0 in practice — no Bleed-penetration modifiers are
		/// authored, so Bleed continues to face armour alone.
		///
		/// Whole percents (a 15 here is 15 resistance points); the consumer divides by 100 because
		/// resistance is a fraction. Cleared, not reallocated, for the same reason as the pool above.
		/// </summary>
		public readonly float[] ElementalPenetration = new float[DamageElementInfo.Count];

		public override void ResetEffects()
		{
			LifeLeechPercent = 0f;
			LifeLeechFlatBonus = 0f;
			HealingPercent = 0f;
			ConsumableHealingPercent = 0f;
			AilmentPercent = 0f;
			AreaPercent = 0f;
			Array.Clear(ElementalConversion, 0, ElementalConversion.Length);
			Array.Clear(ElementalPenetration, 0, ElementalPenetration.Length);
		}

		/// <summary>Convenience accessor for the item applier / notables.</summary>
		public static CombatEffectStats Get(Player player) => player.GetModPlayer<CombatEffectStats>();

		/// <summary>
		/// Amplify potion (and other item) healing, so "healing" gear affects all self-healing, not
		/// just the Bloodthirst proc.
		///
		/// Reads BOTH channels: this is the one site where a general healing bonus and a
		/// leech-scaling one mean the same thing, because a potion has no damageDone for the latter to
		/// have already ridden in on. See <see cref="ConsumableHealingPercent"/> for why they are
		/// otherwise separate.
		/// </summary>
		public override void GetHealLife(Item item, bool quickHeal, ref int healValue)
		{
			float total = HealingPercent + ConsumableHealingPercent;
			if (total != 0f && healValue > 0)
				healValue += (int)Math.Round(healValue * total / 100f);
		}
	}
}
