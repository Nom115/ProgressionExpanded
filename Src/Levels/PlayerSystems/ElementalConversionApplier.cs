using System;
using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Src.NPCs.Enemy.Elemental;

namespace ProgressionExpanded.Src.Levels.PlayerSystems
{
	/// <summary>
	/// True (PoE-style) elemental conversion. The sole consumer of
	/// <see cref="CombatEffectStats.ElementalConversion"/> and
	/// <see cref="CombatEffectStats.ElementalPenetration"/>.
	///
	/// <b>Conversion is a trade, not a bonus.</b> A converted slice of the hit is REMOVED from the
	/// normal armour-facing damage and dealt instead as elemental damage that faces the target's
	/// per-element resistance (<see cref="ElementalResistance"/>) instead of its armour. So converting
	/// into an element the enemy resists LOWERS the hit; into one it is weak to, RAISES it — fully
	/// double-edged by design. The hover panel (<c>EnemyResistPanel</c>) and elemental penetration are
	/// how you play around it. This replaced an "added on top" damage-over-time model; see CLAUDE.md §10.
	///
	/// <b>How the split is expressed inside a single hit</b> (mechanism verified against the decompiled
	/// tModLoader assembly — there is no .cs source on disk):
	/// <list type="number">
	/// <item>Shrink the armour-facing portion: <c>modifiers.TargetDamageMultiplier *= (1 - total)</c>.
	/// The physical remainder still faces vanilla Defense / ArmorPenetration / DefenseEffectiveness
	/// normally. <c>TargetDamageMultiplier</c> deliberately does NOT touch <c>HitInfo.SourceDamage</c>,
	/// so step 2 still sees the full pre-mitigation base.</item>
	/// <item>Add the resistance-adjusted elemental amount in a <c>ModifyHitInfo</c> callback, which runs
	/// at the very end of <c>ToHitInfo</c> — after defense, crit and FinalDamage. There
	/// <c>info.SourceDamage</c> is the full pre-mitigation scaled base; the elemental is
	/// <c>base x conversion x (1 - resistance)</c>, scaled to match the physical hit's crit and
	/// FinalDamage so it differs from the physical part ONLY in facing resistance instead of armour.</item>
	/// </list>
	///
	/// <b>Why ModifyHitNPC and not OnHitNPC.</b> <c>OnHitNPC</c> only sees <c>damageDone</c>, which is
	/// already post-armour — converting there would still pay armour, defeating the point. This hook runs
	/// inside the real hit calc, before defense, and fires for melee, projectile AND minion hits — but
	/// NOT for <c>SimpleStrikeNPC</c>, so mod-dealt AoE (Detonate, Devourer, Corrupted Blood) correctly
	/// does not convert.
	/// </summary>
	public class ElementalConversionApplier : ModPlayer
	{
		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			// Critters and the like — matches LifeLeechApplier.IsLeechableTarget and the old applier's gate.
			if (target.friendly || target.lifeMax <= 5)
				return;

			CombatEffectStats effects = CombatEffectStats.Get(Player);

			// Total conversion fraction across all elements (the pool is whole percents).
			float total = 0f;
			for (int e = 0; e < DamageElementInfo.Count; e++)
				total += effects.ElementalConversion[e] / 100f;

			if (total <= 0f)
				return;

			// You cannot convert more than the whole hit. Above 100% total, scale every element down
			// proportionally so the physical remainder in step 1 never goes negative.
			float scale = total > 1f ? 1f / total : 1f;
			float cappedTotal = total > 1f ? 1f : total;

			// AilmentEffect (repurposed from the old DoT-duration stat) now amplifies converted elemental
			// damage — the payoff stat for a conversion build. See CombatEffectStats.AilmentPercent.
			float ailmentMul = 1f + effects.AilmentPercent / 100f;

			// The resistance-adjusted fraction of the base that lands as elemental damage. Resistance is
			// per-element and depends on the target, which we have here; ElementalResistance.Get is reused
			// unchanged. Penetration lowers the target's resistance (a stat separate from vanilla armour
			// pen — see CombatEffectStats.ElementalPenetration). Bleed's branch faces def/(|def|+50).
			float elemFrac = 0f;
			for (int e = 0; e < DamageElementInfo.Count; e++)
			{
				float conversion = effects.ElementalConversion[e] / 100f * scale;
				if (conversion <= 0f)
					continue;

				var element = (DamageElement)e;
				float penetration = effects.ElementalPenetration[e] / 100f;
				float resistance = ElementalResistance.Get(target, element, penetration);

				// Fully double-edged: a resisted element (positive resistance) makes (1 - resistance) < 1
				// and shrinks this slice; a weakness (negative resistance) makes it > 1 and grows it.
				elemFrac += conversion * (1f - resistance);
			}

			elemFrac *= ailmentMul;

			// 1) Remove the converted slice from the armour-facing (physical) portion.
			modifiers.TargetDamageMultiplier *= (1f - cappedTotal);

			// 2) Add the elemental slice AFTER the whole vanilla calc. Captured by value because the
			//    ModifyHitInfo callback cannot reach the ref-struct modifiers; StatModifier is a value
			//    type, so these snapshot the crit/FinalDamage multipliers as they stand at our hook. (A
			//    contributor running after us would not be reflected — an accepted SP-first approximation;
			//    the multipliers involved are small level-scaling / playstyle factors.)
			StatModifier finalDamage = modifiers.FinalDamage;
			StatModifier critDamage = modifiers.CritDamage;
			StatModifier nonCritDamage = modifiers.NonCritDamage;

			modifiers.ModifyHitInfo += (ref NPC.HitInfo info) =>
			{
				// The full pre-mitigation scaled base, unaffected by step 1's TargetDamageMultiplier.
				float baseDamage = info.SourceDamage;

				// Match the physical part's crit + FinalDamage, so the elemental differs only in mitigation.
				// Additive*Multiplicative is the pure multiplicative channel; Base/Flat are flat adds that
				// were already spent on the physical number and must not be re-applied here.
				float critScale = info.Crit
					? critDamage.Additive * critDamage.Multiplicative
					: nonCritDamage.Additive * nonCritDamage.Multiplicative;
				float finalScale = finalDamage.Additive * finalDamage.Multiplicative;

				int elemental = (int)MathF.Round(baseDamage * elemFrac * critScale * finalScale);

				info.Damage = Math.Max(info.Damage + elemental, 1);
			};
		}
	}
}
