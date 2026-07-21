using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Src.NPCs;

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
			"Your critical strikes deal triple damage instead of double.";

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
			"While below half your maximum life: +50% damage and +30% movement speed.";

		private const float LifeThreshold = 0.5f;
		private const float DamageBonus = 0.50f;
		private const float MoveSpeedBonus = 0.30f;

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
	}

	/// <summary>
	/// Terror spreads. Your hits build Dread on any enemy, doing nothing until it reaches ten stacks —
	/// then the enemy erupts, dealing a share of YOUR maximum life to everything nearby and spreading
	/// Dread to them, which can chain into a cascade of eruptions. A freshly-erupted enemy is briefly
	/// immune to Dread so a cascade cannot loop forever.
	///
	/// The old Dread Gaze was a single-target ramp: it wanted you to fixate on one foe, which is
	/// impractical against the crowds Terraria throws at you. This rework rewards spreading damage across
	/// a pack instead — the more enemies you tag, the bigger the chain.
	///
	/// Per-enemy state lives on the DreadNPC GlobalNPC, not here — a stack count belongs on the enemy,
	/// not the player. This behaviour just drives it from the on-hit hook.
	/// </summary>
	public class DreadGazeTalent : TalentBehaviour
	{
		public override string Id => "dread_gaze";
		public override string SlotKey => "eye";
		public override string DisplayName => "Dread Gaze";

		public override string Description =>
			"Your hits build Dread on enemies. At 10 stacks the enemy erupts, dealing 10% of your "
			+ "maximum life to everything nearby and spreading Dread to them. A freshly-erupted enemy "
			+ "cannot gain Dread for 4 seconds.";

		public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
		{
			DreadNPC.Apply(player, target, DreadNPC.PlayerDreadPerHit, 0);
		}
	}
}
