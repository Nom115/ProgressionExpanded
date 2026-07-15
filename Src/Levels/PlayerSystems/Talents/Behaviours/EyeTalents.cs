using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Talents.Behaviours
{
	/// <summary>
	/// Eye of Cthulhu row: precision, desperation, and terror.
	/// </summary>
	public class UnblinkingEyeTalent : TalentBehaviour
	{
		public override string Id => "unblinking_eye";
		public override string SlotKey => "eye";
		public override string DisplayName => "Unblinking Eye";

		public override string Description =>
			"+25% critical strike chance, and your critical strikes deal triple damage instead of double.";

		private const int CritChanceBonus = 25;

		private static readonly Dictionary<string, float> flatBonuses = new Dictionary<string, float>
		{
			{ "CritChance", CritChanceBonus },
		};

		public override IReadOnlyDictionary<string, float> FlatBonuses => flatBonuses;

		public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers)
		{
			// CritDamage defaults to +1f additive, which is vanilla's "crits deal double". Another
			// +1f makes it +200% — triple damage. There is no player-wide crit-damage stat in this
			// version of tModLoader, so this has to be done per-hit.
			modifiers.CritDamage += 1f;
		}
	}

	/// <summary>
	/// The Eye transforms at half life and comes at you far harder. So do you.
	/// </summary>
	public class SecondPhaseTalent : TalentBehaviour
	{
		public override string Id => "second_phase";
		public override string SlotKey => "eye";
		public override string DisplayName => "Second Phase";

		public override string Description =>
			"While below half your maximum life: +50% damage, +30% movement speed, and you take 25% less damage.";

		private const float LifeThreshold = 0.5f;
		private const float DamageBonus = 0.50f;
		private const float MoveSpeedBonus = 0.30f;
		private const float DamageReduction = 0.25f;

		private static bool IsTransformed(Player player)
		{
			return player.statLifeMax2 > 0 && player.statLife <= player.statLifeMax2 * LifeThreshold;
		}

		public override void ResetEffects(Player player)
		{
			if (!IsTransformed(player))
				return;

			player.GetDamage(DamageClass.Generic) *= 1f + DamageBonus;
			player.moveSpeed *= 1f + MoveSpeedBonus;
		}

		public override void ModifyHurt(Player player, ref Player.HurtModifiers modifiers)
		{
			if (!IsTransformed(player))
				return;

			// FinalDamage rather than endurance, so this composes multiplicatively with Stagger,
			// which also multiplies FinalDamage. Stagger's split is computed from the post-mitigation
			// number, so it correctly staggers 55% of the already-reduced hit.
			modifiers.FinalDamage *= 1f - DamageReduction;
		}
	}

	/// <summary>
	/// Everything close enough to see you is already dying.
	/// </summary>
	public class DreadGazeTalent : TalentBehaviour
	{
		public override string Id => "dread_gaze";
		public override string SlotKey => "eye";
		public override string DisplayName => "Dread Gaze";

		public override string Description =>
			"Enemies within 30 tiles take 25% more damage from you and have 15 less defense. "
			+ "Rewards fighting at knife-range.";

		private const float RangeInTiles = 30f;
		private const float DamageBonus = 0.25f;
		private const int DefenseReduction = 15;

		public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers)
		{
			float range = RangeInTiles * 16f;
			if (player.Distance(target.Center) > range)
				return;

			modifiers.FinalDamage *= 1f + DamageBonus;

			// Base is the enemy's defense value itself; subtracting from it is a flat shred.
			modifiers.Defense.Base -= DefenseReduction;
		}
	}
}
