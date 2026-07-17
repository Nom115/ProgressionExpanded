using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints;

namespace ProgressionExpanded.Src.Levels.PlayerSystems
{
	/// <summary>
	/// Stats every player of a class has, before any talent or mastery. Currently: the warrior's
	/// baseline damage reduction and knockback resistance.
	///
	/// <b>This is the first mechanical effect PlayerClass has ever had.</b> Until now class only
	/// chose which mastery tree the UI opened (CLAUDE.md §8). Note that only warrior_tree.json
	/// exists and every class currently opens it, so in practice this reads as global — the gate is
	/// here so that Ranged/Magic/Summoner get their own baselines when they are authored, rather
	/// than silently inheriting the warrior's.
	///
	/// <b>Why this exists (play-test, 2026-07-17).</b> Reported: "every tank apart from Stagger feels
	/// incredibly squishy — against bosses Stagger is the only one that has a chance, but against
	/// white mobs they all do fine." Both halves were correct, and the cause was structural rather
	/// than a bad constant.
	///
	/// The three pinnacles used to be spread across DefensePercent from x0.7 (Vengeance) to x2.5
	/// (Stagger). Defense is <b>subtractive</b>, and vanilla floors damage to 1 <i>before</i>
	/// FinalDamage applies (Player.HurtModifiers.GetDamage:255), so a defense spread does not
	/// produce a proportional outcome spread — it produces a cliff. Measured at WL20 against
	/// Plantera's contact damage, that 3.6x stat gap became a 3.8x damage-taken gap; at WL8 it
	/// became unbounded, because Stagger's 188 defense simply exceeded the raw hit and it took 1
	/// while Vengeance took 168. Against trash everyone clears the floor and everyone is fine, which
	/// is exactly why the complaint was boss-only.
	///
	/// So the defense multipliers were removed from all three pinnacles and replaced by this, a
	/// single class-wide Endurance. Endurance is <b>multiplicative</b>, so it has no floor to fall
	/// off and reads identically at every world level and against every hit size. The spread
	/// collapses from 3.8x to 1.2x — and it does so at <i>any</i> value of this constant, because
	/// the compression comes from the removal, not from the number. The pinnacles are now separated
	/// by their mechanics (mana pool / hit cap / leech ramp) rather than by a stat, which is what §6
	/// says the slot is for.
	///
	/// Model: <c>.scripts/mitigation_model.py</c>. Juggernaut keeps its Strength-driven defense
	/// (JuggernautTalent.cs) — that one is a reward for an attribute investment, not a flat
	/// multiplier on the talent, and it is what still separates it from the other two on defense.
	/// </summary>
	public class ClassBaselines : ModPlayer
	{
		/// <summary>
		/// Flat damage reduction for the warrior, before anything else.
		///
		/// <b>The break-even is the reason this is 0.50 and not lower.</b> Each pinnacle surrendered
		/// a different amount of defense, so each needs a different Endurance merely to match what it
		/// had: Stagger ~70%, Juggernaut ~40%, and Vengeance is better at any value because it was
		/// losing a <i>penalty</i>. <b>Below ~40% Juggernaut would come out worse than before</b> —
		/// which is half of what this change exists to fix. At 0.50: Juggernaut +19%, Vengeance
		/// +2.3x, Stagger -1.7x.
		///
		/// Stagger is knowingly nerfed, and it is the yardstick, so expect to feel it. It should
		/// still lead: its mana pool eats 55% of every hit before life is touched, leaving it at ~47
		/// effective against everyone else's 85-104. That is the intended end state — Stagger stays
		/// first because of its <i>mechanic</i>, which is finite and drains, rather than because of a
		/// stat that never runs out. If it ends up too weak, move ManaPerMaxLifeFraction; do not put
		/// the defense back.
		///
		/// 70% was the alternative — nobody would have been nerfed at all — and was rejected because
		/// it consumes the whole of Fargo's 0.75 endurance clamp, which would make every Endurance
		/// roll on gear and the Endurance stat key dead on this modpack. 0.50 leaves 25 points of
		/// headroom for gear to still matter.
		///
		/// ⚠️ Untested in play. This is the single biggest defensive change the mod has made.
		/// </summary>
		public const float WarriorEndurance = 0.50f;

		/// <summary>
		/// Multiplier on knockback the warrior takes. 0.50 = knocked back half as far.
		///
		/// <b>Multiplicative, not additive, and that is a safety property rather than a preference.</b>
		/// Player.HurtModifiers.Knockback is a StatModifier, so <c>*= 0.5f</c> lands in Multiplicative
		/// ("more" — compounds) while <c>-= 0.5f</c> would land in Additive ("increased" — sums; see
		/// §5). Two additive halves would sum to zero and silently grant full knockback immunity,
		/// which is a talent-tier effect (BoneArmor) and must not fall out of a class baseline by
		/// accident. Multiplied, it can approach zero but never reach it.
		///
		/// <b>Interaction with BoneArmor is already handled by vanilla, not by us.</b> GetKnockback
		/// does <c>Math.Max(Knockback.ApplyTo(base), 0f)</c> and only THEN, if knockbackImmune, does
		/// <c>num *= 1f - KnockbackImmunityEffectiveness.Value</c> — which defaults to 1, so immunity
		/// zeroes the result outright and this multiplier becomes a no-op for that pick. No guard
		/// needed here; the two compose correctly by construction.
		///
		/// ⚠️ Untested in play. Note this also quietly helps the reported "stuck in a boss" problem
		/// (todo.md §5b): less knockback means less being juggled, so a Juggernaut at -50% movement
		/// keeps more of the control its mobility already cost it.
		/// </summary>
		public const float WarriorKnockbackTaken = 0.50f;

		/// <summary>
		/// Halve incoming knockback for the warrior.
		///
		/// ModifyHurt is the only hook that sees HurtModifiers, and it runs per hit, so there is no
		/// reset/ordering hazard of the kind PostUpdateMiscEffects has — the struct is built fresh for
		/// each hit.
		/// </summary>
		public override void ModifyHurt(ref Player.HurtModifiers modifiers)
		{
			if (!IsWarrior)
				return;

			modifiers.Knockback *= WarriorKnockbackTaken;
		}

		private bool IsWarrior =>
			ClassSelectionManager.GetSelectedClass(Player) == ClassSelectionManager.PlayerClass.Melee;

		public override void PostUpdateMiscEffects()
		{
			if (!IsWarrior)
				return;

			// Contributed here, not ResetEffects, for the reason §8 gives for the whole contributor
			// pattern: ResetEffects is where Player.endurance is zeroed, so adding there races the
			// wipe. tModLoader runs every ModPlayer's ResetEffects before any PostUpdateMiscEffects,
			// so this ordering is safe rather than lucky. Same phase as StatApplier's Endurance case,
			// which writes this identical field.
			Player.endurance += WarriorEndurance;

			// Matches StatApplier's clamp. Note 1.0 would mean every hit floored to vanilla's
			// minimum of 1, which is near-invulnerability — the clamp is a backstop against a stack
			// of sources, not a design target.
			if (Player.endurance > 1f)
				Player.endurance = 1f;
		}
	}
}
