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
	/// Stagger's opposite number. Stagger smooths incoming damage into a flat bleed and stacks
	/// defense to shrink it; Vengeance sheds defense so hits land hard and fast, banks a huge life
	/// pool to absorb them, and buys the life back by attacking. The two want contradictory things
	/// from the same fight — taking a big hit is what turns Vengeance on, and Stagger is what stops
	/// big hits from happening. Picking one closes the door on the other's playstyle.
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
			"Your maximum life is doubled, but you have 30% less defense. Leech 30% of the damage you "
			+ "deal as life. For every 1% of your maximum life taken in the last 4 seconds, gain 1.5% "
			+ "increased damage AND 1.5% increased healing from all sources, up to +100%. "
			+ "Fight hurt or fight for nothing.";

		private const float WindowSeconds = 4f;
		private const float DamagePerLifeFraction = 1.5f;

		/// <summary>Ceiling on the ramp, as a fraction. Public so the HUD can colour the maxed state
		/// off the real number instead of hardcoding its own copy of it.</summary>
		public const float MaxRampBonus = 1.0f;

		private const float LifeMultiplier = 1.00f;
		private const float DefensePenalty = 0.30f;
		private const float LeechFraction = 0.30f;

		/// <summary>
		/// The whole loadout is one loop: shed defense so hits land hard, survive them on a doubled
		/// pool, convert the resulting damage bonus back into life through leech. Each piece is a
		/// liability without the others — the defense penalty is what turns the big pool from padding
		/// into a resource you actually spend.
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
			{ "DefensePercent", -DefensePenalty },
			{ "LifeLeech", LeechFraction },
		};

		public override IReadOnlyDictionary<string, float> PercentBonuses => percentBonuses;

		/// <summary>
		/// Each hit taken, held for exactly WindowSeconds and then dropped. Raw damage, not a
		/// fraction, so the HUD can state the actual number and the bonus can be derived from it.
		///
		/// This used to be a single float decayed by a flat 1/(WindowSeconds*60) per tick, which did
		/// NOT mean "damage taken in the last 4 seconds" despite the description saying so: the
		/// decrement was absolute rather than proportional, so a hit for 50% of max life decayed away
		/// in 2s and a hit for 10% in 0.4s. Only a hit for exactly 100% of max life lasted the stated
		/// 4. Putting the number on screen made that discrepancy the player's problem, so the window
		/// is now literal. Same shape as StaggerTalent's instance list.
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
