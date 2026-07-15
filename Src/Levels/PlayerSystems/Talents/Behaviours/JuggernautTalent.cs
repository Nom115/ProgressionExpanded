using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Talents.Behaviours
{
	/// <summary>
	/// Pinnacle: Juggernaut. Twice the life, half the options.
	///
	/// Reads as strictly the best pick until you notice it kills two of your three attributes:
	/// Dexterity does nothing (attack speed cannot be increased) and Intellect does nothing (no
	/// mana). It forces an all-Strength bruiser and turns every attack-speed talent below it into
	/// dead weight. That is the cost, and it is meant to be a real one — a pinnacle choice should
	/// close doors, not just hand out stats.
	///
	/// The suppression is declared via Suppresses rather than enforced here. Contributors ask
	/// TalentPlayer.Suppresses(...) before applying themselves, which means there is no dependency
	/// on this talent's hooks running before theirs.
	/// </summary>
	public class JuggernautTalent : TalentBehaviour
	{
		public override string Id => "juggernaut";
		public override string SlotKey => "pinnacle";
		public override string DisplayName => "Juggernaut";

		public override string Description =>
			"+100% maximum life. +50% damage. -15% attack speed, and attack speed cannot be increased. "
			+ "-10% movement speed. You permanently have no mana, and Intellect grants none.";

		private const float LifeBonus = 1.00f;
		private const float DamageBonus = 0.50f;
		private const float AttackSpeedPenalty = 0.15f;
		private const float MoveSpeedPenalty = 0.10f;

		private static readonly Dictionary<string, float> percentBonuses = new Dictionary<string, float>
		{
			{ "MaxLifePercent", LifeBonus },
			{ "GenericDamage", DamageBonus },
			{ "MovementSpeed", -MoveSpeedPenalty },
		};

		public override IReadOnlyDictionary<string, float> PercentBonuses => percentBonuses;

		public override TalentSuppression Suppresses =>
			TalentSuppression.AttackSpeedIncreases | TalentSuppression.ManaFromIntellect;

		public override void PostUpdateMiscEffects(Player player)
		{
			// The attack-speed penalty is applied here rather than declared as a PercentBonus,
			// because StatApplier's AttackSpeed case refuses to run while AttackSpeedIncreases is
			// suppressed — which is correct for everyone else and would silently swallow our own
			// penalty. Suppression blocks increases; this is a decrease, and it still applies.
			ApplyAttackSpeedPenalty(player, DamageClass.Melee);
			ApplyAttackSpeedPenalty(player, DamageClass.Ranged);
			ApplyAttackSpeedPenalty(player, DamageClass.Magic);
			ApplyAttackSpeedPenalty(player, DamageClass.Summon);
			ApplyAttackSpeedPenalty(player, DamageClass.Generic);

			// No mana, permanently. statManaMax2 is recomputed every frame, so this has to be
			// reasserted every frame rather than set once.
			player.statManaMax2 = 0;
			player.statMana = 0;
		}

		private static void ApplyAttackSpeedPenalty(Player player, DamageClass damageClass)
		{
			// GetAttackSpeed throws if the result lands at or below zero, so never let it.
			ref float speed = ref player.GetAttackSpeed(damageClass);
			speed -= AttackSpeedPenalty;
			if (speed < 0.05f)
				speed = 0.05f;
		}
	}
}
