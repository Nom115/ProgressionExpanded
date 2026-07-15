using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Talents.Behaviours
{
	/// <summary>
	/// Pinnacle: Juggernaut. An armoured wall that cannot chase anything down.
	///
	/// Its life is deliberately "increased" rather than "more" — it lands in StatModifier.Additive
	/// and merely sums with Fortitude/Royal Jelly/Avatar of Flesh, where Vengeance's doubling
	/// compounds against them. Juggernaut used to be the default pick on raw life alone; the
	/// defense is what it trades for, and it pays in mobility.
	///
	/// It also kills two of your three attributes: Dexterity does nothing (attack speed cannot be
	/// increased) and Intellect does nothing (no mana). Since the mastery gate now keys off TOTAL
	/// attribute points rather than Strength, that dead weight is a real cost to build breadth and
	/// not just to the stat line.
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
			"+50% maximum life. 50% more defense. 65% more damage. -15% attack speed, and attack speed "
			+ "cannot be increased. -50% movement speed. You permanently have no mana, and Intellect "
			+ "grants none.";

		private const float LifeBonus = 0.50f;
		private const float DefenseBonus = 0.50f;

		/// <summary>
		/// Note this is "more", not "increased": StatApplier's GenericDamage percent case does
		/// GetDamage(Generic) *= 1f + value, which lands in StatModifier.Multiplicative and compounds
		/// with gear rather than summing into the additive bucket. The old description said
		/// "+50% damage", which understated what it was already doing.
		/// </summary>
		private const float DamageBonus = 0.65f;

		private const float AttackSpeedPenalty = 0.15f;
		private const float MoveSpeedPenalty = 0.50f;

		private static readonly Dictionary<string, float> percentBonuses = new Dictionary<string, float>
		{
			{ "MaxLifePercent", LifeBonus },
			// DefensePercent multiplies a Player.DefenseStat, which collects adds and multiplies
			// separately — so this is already "more" defense and compounds with Bone Armor and
			// Avatar of Flesh rather than summing with them.
			{ "DefensePercent", DefenseBonus },
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
