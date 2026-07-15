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
			+ "deal as life. Gain 1.5% increased damage for every 1% of your maximum life taken in the "
			+ "last 4 seconds, up to +100%. Fight hurt or fight for nothing.";

		private const float WindowSeconds = 4f;
		private const float DamagePerLifeFraction = 1.5f;
		private const float MaxBonus = 1.0f;
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

		/// <summary>Damage taken inside the window, as a fraction of max life.</summary>
		private float recentDamageFraction;

		public override void OnDeactivate(Player player)
		{
			recentDamageFraction = 0f;
		}

		public override void OnRespawn(Player player)
		{
			recentDamageFraction = 0f;
		}

		public override void PostHurt(Player player, Player.HurtInfo info)
		{
			if (player.statLifeMax2 <= 0)
				return;

			recentDamageFraction += info.Damage / (float)player.statLifeMax2;
		}

		public override void PostUpdate(Player player)
		{
			if (recentDamageFraction <= 0f)
				return;

			// Linear decay over the window: a hit's contribution is fully gone WindowSeconds after it
			// landed. Decaying continuously rather than expiring a queue of timestamps keeps the
			// bonus smooth — it ramps down as you stop being hit instead of falling off a cliff.
			recentDamageFraction -= 1f / (WindowSeconds * 60f);
			if (recentDamageFraction < 0f)
				recentDamageFraction = 0f;
		}

		public override void ResetEffects(Player player)
		{
			float bonus = recentDamageFraction * DamagePerLifeFraction;
			if (bonus <= 0f)
				return;

			if (bonus > MaxBonus)
				bonus = MaxBonus;

			player.GetDamage(DamageClass.Generic) *= 1f + bonus;
		}

		/// <summary>Current bonus as a fraction, for the UI to show live.</summary>
		public float GetCurrentBonus()
		{
			float bonus = recentDamageFraction * DamagePerLifeFraction;
			return bonus > MaxBonus ? MaxBonus : bonus;
		}
	}
}
