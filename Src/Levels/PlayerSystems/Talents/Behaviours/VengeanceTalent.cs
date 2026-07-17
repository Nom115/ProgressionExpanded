using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Talents.Behaviours
{
	/// <summary>
	/// Pinnacle: Vengeance. The more you have just been hurt, the harder you hit — and hitting is
	/// how you heal.
	///
	/// Stagger's opposite number. Stagger smooths incoming damage into a bleed and drains it from a
	/// mana pool; Vengeance takes hits whole, banks a doubled life pool to absorb them, and buys the
	/// life back by attacking. The two want contradictory things from the same fight — taking a big
	/// hit is what turns Vengeance on, and Stagger is what stops big hits from happening. Picking one
	/// closes the door on the other's playstyle.
	///
	/// It used to also shed 30% defense, and that was removed on 2026-07-17 — see percentBonuses.
	/// The short version: in a subtractive system that penalty was several times more expensive than
	/// it read, and it deleted the melee range this talent's entire loop depends on.
	///
	/// It has no passive sustain on purpose: the life pool is a budget, not a wall, and the only way
	/// to refill it is to keep swinging. Disengaging to regenerate is what this talent is built to
	/// punish, which is also why Fleshless (no regeneration, 40% DR) is a natural partner rather
	/// than the trap it is next to Stagger.
	///
	/// Tracked as a fraction of MAX life rather than raw damage, so it stays meaningful at every
	/// point in progression instead of scaling out of reach.
	/// </summary>
	public class VengeanceTalent : TalentBehaviour
	{
		public override string Id => "vengeance";
		public override string SlotKey => "pinnacle";
		public override string DisplayName => "Vengeance";

		public override string Description =>
			"Your maximum life is doubled. Leech 30% of the damage you "
			+ "deal as life. For every 1% of your maximum life taken in the last 10 seconds, gain 1.5% "
			+ "increased damage AND 1.5% increased healing from all sources, up to +100%. "
			+ "Fight hurt or fight for nothing.";

		/// <summary>
		/// How long a hit stays in the ramp. Raised 4s -> 10s on 2026-07-17 from play-test.
		///
		/// <b>The ramp is anti-synergistic by construction, and 4s made that fatal.</b> It BUILDS
		/// while you are being hurt but only PAYS OUT while you are attacking — so it peaks at exactly
		/// the moment you most need to leave, and then decays while you are away. Reported: "you
		/// instinctively move out of the damage, then you need to run back into the fray and at which
		/// point you miss 75% if not more of the leech."
		///
		/// That 75% is not an estimate; it falls out of the arithmetic. A sliding window drops a hit
		/// after WindowSeconds, so a 3-second disengage costs (3/window) of the ramp: at 4s that is
		/// exactly 75%, at 10s it is 30%. The model and the play-test agree on the mechanism, which is
		/// why this is 10 and not a guess.
		///
		/// ⚠️ This is also a DAMAGE buff, not only a leech-uptime fix — the ramp multiplies GetDamage,
		/// so a longer window means higher peak uptime on the offensive half too. It is deliberately
		/// NOT paired with a cut to DamagePerLifeFraction: the "spikey" feel is the point of the
		/// talent, and the fix is meant to let the spike survive one engage/disengage cycle rather
		/// than to flatten it. If Vengeance now over-delivers on damage, this constant is what to
		/// look at first.
		///
		/// Public for the same reason as MaxRampBonus: the HUD states this window to the player, and
		/// when it was 4s here and a hardcoded "(4s)" there, raising it to 10s left the readout lying.
		/// </summary>
		public const float WindowSeconds = 10f;

		private const float DamagePerLifeFraction = 1.5f;

		/// <summary>Ceiling on the ramp, as a fraction. Public so the HUD can colour the maxed state
		/// off the real number instead of hardcoding its own copy of it.</summary>
		public const float MaxRampBonus = 1.0f;

		private const float LifeMultiplier = 1.00f;
		private const float LeechFraction = 0.30f;

		/// <summary>
		/// The whole loadout is one loop: take hits on a doubled pool, let them ramp you, and convert
		/// the resulting damage bonus back into life through leech. Each piece is a liability without
		/// the others — the pool is padding unless something is spending it, and the ramp is what
		/// spends it.
		///
		/// "More" life, not "increased" — MaxLifeMore lands in StatModifier.Multiplicative, so the
		/// doubling compounds with Fortitude/Royal Jelly/Avatar of Flesh instead of summing into the
		/// same additive bucket they share.
		///
		/// Note the deliberate self-tension: the damage bonus is driven by damage taken as a fraction
		/// of MAX life, so doubling max life also doubles the absolute damage needed to ramp. The
		/// life is a straight survivability gain and a straight ramp-rate loss. Do not "fix" this by
		/// tracking raw damage; that is what the fraction-of-max design exists to avoid (it would
		/// fall off across progression).
		///
		/// LifeLeech is declarative rather than a bespoke OnHitNPC heal because LifeLeechApplier
		/// already owns the payout, the ~0.3s cooldown and the 15%-of-max-life per-hit cap. Rolling
		/// our own would mean a second cooldown running alongside it — two independent leech procs
		/// per hit, which is exactly the stacking the shared pool exists to prevent.
		///
		/// Unit trap: these are all FRACTIONS. StatApplier's percent path multiplies LifeLeech by 100
		/// on the way into CombatEffectStats (which stores whole percents), so 0.30 here is 30%.
		/// </summary>
		private static readonly Dictionary<string, float> percentBonuses = new Dictionary<string, float>
		{
			{ "MaxLifeMore", LifeMultiplier },
			// The "30% less defense" that used to sit here was removed on 2026-07-17. It read as a
			// modest price and was in fact the most expensive line in the slot: defense is subtractive
			// and vanilla floors damage to 1 before FinalDamage, so shedding it near that floor is
			// violently non-linear. At WL20 it meant taking 238 per Plantera contact against Stagger's
			// 62 — a 3.8x gap from a 3.6x stat difference — and at WL8 it was 168 against Stagger's 1,
			// i.e. unbounded. Play-test: "with the 30% less defense you are also just taking so much
			// damage", and it deleted the melee range the leech needs to function at all.
			//
			// Its replacement is not a smaller penalty — softening x0.70 to x0.85 only moves 3.8x to
			// 3.6x, because the cliff eats it. All three pinnacles gave up their defense multipliers
			// and mitigation moved to ClassBaselines.WarriorEndurance, which is multiplicative and so
			// has no cliff to fall off. See .scripts/mitigation_model.py.
			{ "LifeLeech", LeechFraction },
		};

		public override IReadOnlyDictionary<string, float> PercentBonuses => percentBonuses;

		/// <summary>
		/// Each hit taken, held for exactly WindowSeconds and then dropped. Raw damage, not a
		/// fraction, so the HUD can state the actual number and the bonus can be derived from it.
		///
		/// This used to be a single float decayed by a flat 1/(WindowSeconds*60) per tick, which did
		/// NOT mean "damage taken in the last WindowSeconds" despite the description saying so: the
		/// decrement was absolute rather than proportional, so a hit for 50% of max life decayed away
		/// in half the window and a hit for 10% in a tenth of it. Only a hit for exactly 100% of max
		/// life lasted the stated duration. Putting the number on screen made that discrepancy the
		/// player's problem, so the window is now literal. Same shape as StaggerTalent's instance list.
		///
		/// That the window is literal is what makes the 2026-07-17 4s -> 10s change mean what it says:
		/// a 3-second disengage now costs 30% of the ramp instead of 75%.
		/// </summary>
		private readonly List<DamageEntry> recentHits = new List<DamageEntry>();

		public override void OnDeactivate(Player player)
		{
			recentHits.Clear();
		}

		public override void OnRespawn(Player player)
		{
			// Never carry a ramp across a life.
			recentHits.Clear();
		}

		public override void PostHurt(Player player, Player.HurtInfo info)
		{
			if (info.Damage <= 0)
				return;

			recentHits.Add(new DamageEntry { Damage = info.Damage, TimeRemaining = WindowSeconds });
		}

		public override void PostUpdate(Player player)
		{
			if (recentHits.Count == 0)
				return;

			const float deltaTime = 1f / 60f;
			for (int i = recentHits.Count - 1; i >= 0; i--)
			{
				recentHits[i].TimeRemaining -= deltaTime;
				if (recentHits[i].TimeRemaining <= 0f)
					recentHits.RemoveAt(i);
			}
		}

		public override void ResetEffects(Player player)
		{
			float bonus = GetCurrentBonus(player);
			if (bonus <= 0f)
				return;

			player.GetDamage(DamageClass.Generic) *= 1f + bonus;
		}

		public override void PostUpdateMiscEffects(Player player)
		{
			float bonus = GetCurrentBonus(player);
			if (bonus <= 0f)
				return;

			// The ramp pays out twice: harder hits AND bigger heals, by the same percentage. Healing
			// is the half that makes the life pool a resource rather than a countdown — you spend it
			// getting hit and buy it back by swinging, and the more you are losing the better the
			// exchange rate gets.
			//
			// <b>ConsumableHealingPercent, NOT HealingPercent, and the distinction is the whole bug.</b>
			// Leech does not need this to ramp — it ALREADY does, because ResetEffects above multiplies
			// GetDamage by the same (1 + bonus), so the ramp is baked into the damageDone that
			// LifeLeechApplier takes its 30% of. Contributing to HealingPercent as well fed the ramp in
			// a SECOND time on the payout, making a full-ramp heal 0.30 * baseHit * (1+r)^2 — 4x, where
			// this talent's own docs promise 2x. Potions are the one heal with no damageDone to have
			// ridden in on, which is why they get their own channel rather than losing the bonus.
			CombatEffectStats.Get(player).ConsumableHealingPercent += bonus * 100f;
		}

		/// <summary>
		/// The regeneration half of the ramp.
		///
		/// <b>This hook, not PostUpdateMiscEffects.</b> It used to live there, alongside the healing
		/// contribution, which made it very nearly dead code: PostUpdateMiscEffects runs immediately
		/// BEFORE UpdateLifeRegen, so at that point lifeRegen holds nothing but StatApplier's own flat
		/// "LifeRegen" case — none of the gear or buff regeneration this is supposed to be scaling has
		/// been added yet. TalentBehaviour.UpdateLifeRegen documents this, and StaggerTalent already
		/// follows it. The old placement justified itself by matching StatApplier's LifeRegenPercent
		/// case, which has the same bug and is logged in todo.md as a separate fix.
		///
		/// Guarded on > 0 for the reason StatApplier's LifeRegenPercent case is: lifeRegen goes
		/// NEGATIVE while a DoT ticks, so an unguarded multiply would make Bleeding/Poison/On Fire
		/// proportionally more lethal the closer to death you got — the exact inverse of this talent.
		/// Note the guard only became MEANINGFUL with the move: from PostUpdateMiscEffects it was
		/// protecting against a negative that could not exist yet.
		/// </summary>
		public override void UpdateLifeRegen(Player player)
		{
			float bonus = GetCurrentBonus(player);
			if (bonus <= 0f || player.lifeRegen <= 0)
				return;

			player.lifeRegen += (int)Math.Round(player.lifeRegen * bonus);
		}

		/// <summary>Raw damage taken inside the window. What the HUD prints.</summary>
		public float GetRecentDamage()
		{
			float total = 0f;
			for (int i = 0; i < recentHits.Count; i++)
				total += recentHits[i].Damage;
			return total;
		}

		/// <summary>
		/// Current bonus as a fraction, applied to both damage dealt and healing received.
		///
		/// Derived from CURRENT max life rather than banked at hit time, so the doubling this talent
		/// grants is already priced in — which is the self-tension noted above, not an oversight.
		/// </summary>
		public float GetCurrentBonus(Player player)
		{
			if (player.statLifeMax2 <= 0)
				return 0f;

			float bonus = (GetRecentDamage() / player.statLifeMax2) * DamagePerLifeFraction;
			return bonus > MaxRampBonus ? MaxRampBonus : bonus;
		}

		private class DamageEntry
		{
			public float Damage { get; set; }
			public float TimeRemaining { get; set; }
		}
	}
}
