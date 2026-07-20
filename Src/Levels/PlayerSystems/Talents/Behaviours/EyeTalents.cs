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
	/// Fix your gaze on a single foe. Hitting the SAME enemy builds Dread on it — each stack makes it
	/// take more damage from you and shreds its defense; switching targets resets the ramp, and it
	/// decays if you stop attacking. A single-target hunter's identity: it rewards committing to one
	/// enemy and scales with attack speed (fast weapons stack faster), distinct from Unblinking Eye's
	/// crit build and Second Phase's low-life window.
	///
	/// The old Dread Gaze was a flat +25% damage in disguise: "within 30 tiles" is nearly the whole
	/// screen, so a melee was always in range. This rework replaces it with a mechanic that changes
	/// how you fight rather than just a number.
	///
	/// Per-instance state on the shared behaviour, single-player focused (same pattern as StaggerTalent).
	/// </summary>
	public class DreadGazeTalent : TalentBehaviour
	{
		public override string Id => "dread_gaze";
		public override string SlotKey => "eye";
		public override string DisplayName => "Dread Gaze";

		public override string Description =>
			"Hitting the same enemy builds Dread on it: each stack makes it take 8% more damage from "
			+ "you and shreds 3 defense, up to 6 stacks. Switching targets resets your Dread, and it "
			+ "fades if you stop attacking. Rewards hunting one foe.";

		private const int MaxStacks = 6;
		private const float DamagePerStack = 0.08f; // +48% at full stacks
		private const int DefensePerStack = 3;      // -18 at full stacks
		private const int DecayTicks = 180;         // 3s of not attacking the target clears it

		private int dreadTargetWho = -1;
		private int dreadStacks;
		private int decayTimer;

		public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers)
		{
			// Same target keeps ramping (capped); a different target restarts the ramp at one stack.
			if (target.whoAmI == dreadTargetWho)
			{
				if (dreadStacks < MaxStacks)
					dreadStacks++;
			}
			else
			{
				dreadTargetWho = target.whoAmI;
				dreadStacks = 1;
			}

			decayTimer = DecayTicks;

			// The hit that adds the stack also benefits from it. FinalDamage (multiplicative,
			// post-defense) so it composes with Stagger/Second Phase and has no floor-at-1 cliff;
			// Defense.Base -= is the flat shred the old version used.
			modifiers.FinalDamage *= 1f + dreadStacks * DamagePerStack;
			modifiers.Defense.Base -= dreadStacks * DefensePerStack;
		}

		public override void ResetEffects(Player player)
		{
			// Dispatched every frame to the active talent; drop the ramp if the target goes untouched.
			if (decayTimer > 0 && --decayTimer == 0)
			{
				dreadStacks = 0;
				dreadTargetWho = -1;
			}
		}
	}
}
