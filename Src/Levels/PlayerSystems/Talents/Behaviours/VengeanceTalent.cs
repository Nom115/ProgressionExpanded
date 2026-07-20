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
	/// <b>Sustain is LEECH and only leech (reworked 2026-07-21).</b> Vengeance grants NO life
	/// regeneration and NO passive heal — the single way it puts life back is by attacking. The whole
	/// loadout is one loop: take hits on a doubled pool, let them ramp you, and convert the resulting
	/// damage bonus back into life through leech. That ties sustain to the fight, not to safety: the
	/// moment you disengage the ramp decays and there is nothing to leech, so retreating to heal is
	/// what this talent punishes — which is why Fleshless (no regeneration, 40% DR) is a natural partner
	/// rather than the trap it is next to Stagger.
	///
	/// <b>The RAMP is driven by the ABSOLUTE damage you have taken in the window</b> (reworked
	/// 2026-07-21), NOT by damage as a fraction of max life. That is the Mists-of-Pandaria idea: a
	/// harder-hitting boss ramps you faster and sustains you more, because a big raw hit is a big raw
	/// number regardless of how large your life pool has grown. It is deliberately UNCAPPED so that
	/// "bigger boss = more sustain" keeps holding instead of plateauing at a low ceiling — the ramp
	/// multiplies both damage dealt (so leech, being 30% of damage dealt, scales with it) and healing
	/// received (potions, via ConsumableHealingPercent). See RampPerDamage.
	///
	/// History: the ramp used to be a fraction of MAX life (damage ÷ statLifeMax2), and there used to
	/// be a separate "Vengeful Recovery" heal-over-time plus a life-regen amplifier. All three were
	/// removed on 2026-07-21 — the recovery HoT and the regen because the player wanted leech to be the
	/// only heal, and the fraction-of-max driver because they wanted absolute damage taken to drive it.
	/// </summary>
	public class VengeanceTalent : TalentBehaviour
	{
		public override string Id => "vengeance";
		public override string SlotKey => "pinnacle";
		public override string DisplayName => "Vengeance";

		public override string Description =>
			"Your maximum life is doubled. Leech 30% of the damage you deal as life. For every 100 "
			+ "damage you take in the last 10 seconds, gain +10% increased damage AND +10% increased "
			+ "healing from all sources, with no ceiling — the harder a boss hits you, the harder you hit "
			+ "and heal back. Vengeance grants no life regeneration: you heal only by fighting.";

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
		/// so a longer window means higher peak uptime on the offensive half too. The window also sets
		/// how much damage banks into the now-uncapped absolute ramp (see RampPerDamage): a wider window
		/// holds more damage, so it scales the ramp up as well. If Vengeance over-delivers on damage,
		/// reach for RampPerDamage first; shortening this window is the second lever.
		///
		/// Public for the same reason as MaxRampBonus: the HUD states this window to the player, and
		/// when it was 4s here and a hardcoded "(4s)" there, raising it to 10s left the readout lying.
		/// </summary>
		public const float WindowSeconds = 10f;

		/// <summary>
		/// Ramp rate: fraction of increased damage &amp; healing granted per point of ABSOLUTE damage
		/// taken inside the window. 0.001 → +0.1% per damage, i.e. <b>+10% per 100 damage taken</b>.
		///
		/// ⚠️ <b>This is THE balance dial for Vengeance, and the primary risk of the 2026-07-21 rework.</b>
		/// It is deliberately UNCAPPED (see GetCurrentBonus) so a harder-hitting boss keeps ramping you —
		/// and it multiplies DAMAGE DEALT, not just healing, on the pinnacle slot that is the whole mod's
		/// balance reference (CLAUDE.md §6). A long boss fight banks thousands of damage in the 10s window,
		/// so this can reach several-hundred-% on both offense and sustain. First guess only — expect to
		/// LOWER it after the first Build+Reload. If Vengeance over-delivers, this is the number to move,
		/// not the leech fraction or the life multiplier.
		/// </summary>
		private const float RampPerDamage = 0.001f;

		/// <summary>
		/// Display-only threshold: the HUD colours the readout gold once the ramp reaches this, to signal
		/// "you're really cooking". It is <b>no longer a functional ceiling</b> — the 2026-07-21 rework made
		/// the ramp uncapped (GetCurrentBonus does not clamp to it). Public so the HUD reads the real number
		/// instead of hardcoding its own copy.
		/// </summary>
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
		/// Note the interaction with the doubled life: because the ramp is now driven by ABSOLUTE damage
		/// taken (see GetCurrentBonus, reworked 2026-07-21), doubling max life is a pure survivability
		/// gain — it does NOT slow the ramp the way the old fraction-of-max driver did, where a bigger
		/// pool made each hit a smaller share and so ramped less. Bigger boss hits ramp you faster
		/// regardless of pool size, which is the whole point of the absolute driver.
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
			AgeRecentHits();
		}

		private void AgeRecentHits()
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

		/// <summary>Raw damage taken inside the window. What the HUD prints.</summary>
		public float GetRecentDamage()
		{
			float total = 0f;
			for (int i = 0; i < recentHits.Count; i++)
				total += recentHits[i].Damage;
			return total;
		}

		/// <summary>
		/// Current bonus as a fraction, applied to both damage dealt (ResetEffects) and healing received
		/// (PostUpdateMiscEffects → potions; leech rides in via the ramped damage dealt).
		///
		/// <b>Driven by ABSOLUTE damage taken, and UNCAPPED.</b> This is the 2026-07-21 rework: it used
		/// to be (GetRecentDamage / statLifeMax2) * 1.5 clamped to MaxRampBonus — a fraction of max life,
		/// which fell off across progression because a fixed raw hit is a shrinking share of a growing
		/// pool. Keying off the raw damage number instead makes a harder-hitting boss ramp you harder, and
		/// removing the clamp lets that keep holding instead of plateauing. See RampPerDamage — it is now
		/// the whole balance surface, and it multiplies OFFENSE as well as healing, so watch it.
		/// </summary>
		public float GetCurrentBonus(Player player)
		{
			return GetRecentDamage() * RampPerDamage;
		}

		private class DamageEntry
		{
			public float Damage { get; set; }
			public float TimeRemaining { get; set; }
		}
	}
}
