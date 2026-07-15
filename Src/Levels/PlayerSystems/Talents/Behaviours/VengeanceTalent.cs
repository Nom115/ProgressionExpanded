using Terraria;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Talents.Behaviours
{
	/// <summary>
	/// Pinnacle: Vengeance. The more you have just been hurt, the harder you hit.
	///
	/// Stagger's opposite number. Stagger smooths incoming damage into a flat bleed; Vengeance is
	/// paid in spikes, so the two want contradictory things from the same fight — taking a big hit
	/// is what turns Vengeance on, and Stagger is what stops big hits from happening. Picking one
	/// should genuinely close the door on the other's playstyle.
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
			"Gain 1.5% increased damage for every 1% of your maximum life taken in the last 4 seconds, "
			+ "up to +100%. Fight hurt or fight for nothing.";

		private const float WindowSeconds = 4f;
		private const float DamagePerLifeFraction = 1.5f;
		private const float MaxBonus = 1.0f;

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
