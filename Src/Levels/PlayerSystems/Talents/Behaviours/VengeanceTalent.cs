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
	/// <b>The BONUS is a FLAT amount equal to 3% of the ABSOLUTE damage you have taken in the window</b>
	/// (reworked 2026-07-21), NOT a percentage and NOT a fraction of max life. Take 600 damage in the
	/// last 10 seconds and you add +18 flat damage to every hit and +18 to every leech proc (you heal
	/// 18 + the leech). That is the Mists-of-Pandaria idea in absolute terms: a harder-hitting boss
	/// hands you a bigger flat number, on both offense and sustain, regardless of how large your life
	/// pool has grown. See BonusFractionOfDamageTaken.
	///
	/// The flat heal rides the shared leech proc rather than being its own on-hit heal (see
	/// PostUpdateMiscEffects and CombatEffectStats.LifeLeechFlatBonus), so leech is still the ONLY
	/// healing path — the bonus just lands on top of each leech.
	///
	/// History: the bonus used to be a fraction of MAX life (damage ÷ statLifeMax2) applied as a percent
	/// multiplier, with a separate "Vengeful Recovery" heal-over-time and a life-regen amplifier. The HoT
	/// and the regen were removed on 2026-07-21 (leech is the only heal); the driver was switched to
	/// absolute damage the same day; and the multiplier was switched to a FLAT add (2026-07-21b) after the
	/// percent version read as too big a nerf.
	/// </summary>
	public class VengeanceTalent : TalentBehaviour
	{
		public override string Id => "vengeance";
		public override string SlotKey => "pinnacle";
		public override string DisplayName => "Vengeance";

		/// <summary>
		/// Every balance number in this text is derived from the constants below (LifeMultiplier,
		/// LeechFraction, BonusFractionOfDamageTaken, WindowSeconds) so the tooltip can never drift
		/// from the code — change a const and the description follows. The only literal is the "600
		/// damage" worked example, whose result (+X) is itself computed from the constant.
		/// </summary>
		public override string Description
		{
			get
			{
				const float exampleDamageTaken = 600f;
				float exampleBonus = exampleDamageTaken * BonusFractionOfDamageTaken;
				return $"Your maximum life is multiplied by {1f + LifeMultiplier:0.##}. Leech "
					+ $"{LeechFraction * 100f:0}% of the damage you deal as life. Add "
					+ $"{BonusFractionOfDamageTaken * 100f:0}% of the damage you have taken in the last "
					+ $"{WindowSeconds:0} seconds to your hits AND to each leech — take {exampleDamageTaken:0} "
					+ $"damage and you hit for +{exampleBonus:0} and heal for {exampleBonus:0} on top of the "
					+ "leech. Vengeance grants no life regeneration: you heal only by fighting.";
			}
		}

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
		/// ⚠️ This is also a DAMAGE buff, not only a leech-uptime fix — the bonus adds flat damage, so a
		/// longer window means both higher uptime and a bigger banked total (see BonusFractionOfDamageTaken):
		/// a wider window holds more recent damage, so the flat add is larger. If Vengeance over-delivers,
		/// reach for BonusFractionOfDamageTaken first; shortening this window is the second lever.
		///
		/// Public because the HUD states this window to the player: when it was 4s here and a hardcoded
		/// "(4s)" there, raising it to 10s left the readout lying. Read it from the talent, never restate it.
		/// </summary>
		public const float WindowSeconds = 10f;

		/// <summary>
		/// The bonus is a FLAT amount equal to this fraction of the ABSOLUTE damage taken in the window,
		/// added to both your hits and your leech heals. 0.03 → take 600 damage in the last 10s and you
		/// add +18 flat damage to every hit and +18 to every leech proc (so you heal 18 + the leech).
		///
		/// <b>Flat, not a multiplier (reworked 2026-07-21b).</b> The first cut of this rework made it a
		/// percent multiplier on damage &amp; healing, which read as a big nerf against the old design; the
		/// player asked for a flat add instead. Because it is flat, a fast/multi-hit weapon adds the full
		/// amount to EVERY hit, so it is kept small — 10% was tuned down to 3% on 2026-07-21b as too strong.
		/// This and WindowSeconds are the balance dials; watch it in play.
		/// </summary>
		private const float BonusFractionOfDamageTaken = 0.03f;

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
		/// Note the interaction with the doubled life: because the bonus is now driven by ABSOLUTE damage
		/// taken (see GetBonusAmount, reworked 2026-07-21), doubling max life is a pure survivability
		/// gain — it does NOT shrink the bonus the way the old fraction-of-max driver did, where a bigger
		/// pool made each hit a smaller share and so a smaller bonus. Bigger boss hits mean a bigger flat
		/// bonus regardless of pool size, which is the whole point of the absolute driver.
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
			float bonus = GetBonusAmount(player);
			if (bonus <= 0f)
				return;

			// Flat added damage — +18 to every hit at 600 damage taken, NOT a percentage. On Generic so it
			// covers whatever the warrior swings; note a multi-hit weapon adds the full amount per hit.
			player.GetDamage(DamageClass.Generic).Flat += bonus;
		}

		public override void PostUpdateMiscEffects(Player player)
		{
			float bonus = GetBonusAmount(player);
			if (bonus <= 0f)
				return;

			// The healing half is a FLAT add to each leech proc, routed through the shared leech pool's
			// flat-bonus channel so it rides the SAME ~0.3s cooldown, valid-target check and not-at-full
			// gating rather than becoming a second on-hit heal (CLAUDE.md §8 — "never write a second on-hit
			// heal"). So a leech proc heals its normal amount PLUS this: 18 + leech at 600 damage taken. You
			// heal only by fighting; there is no passive drip. Vengeance always carries 30% leech, so the
			// applier never early-outs on a zero pool and this always has a proc to ride.
			CombatEffectStats.Get(player).LifeLeechFlatBonus += bonus;
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
		/// The current FLAT bonus: 3% of the absolute damage taken in the window. Added as flat damage to
		/// every hit (ResetEffects) and as a flat heal to every leech proc (PostUpdateMiscEffects →
		/// CombatEffectStats.LifeLeechFlatBonus). Not a percentage and not capped — it simply tracks how
		/// hard you have been hit lately, in raw HP. What the HUD prints.
		/// </summary>
		public float GetBonusAmount(Player player)
		{
			return GetRecentDamage() * BonusFractionOfDamageTaken;
		}

		private class DamageEntry
		{
			public float Damage { get; set; }
			public float TimeRemaining { get; set; }
		}
	}
}
