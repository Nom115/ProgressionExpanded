using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Src.Levels.PlayerSystems.Talents;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints.NotablePassives
{
	// Playstyle-enhancing masteries. Unlike the talent slots (which redefine a build), these reward a
	// way of PLAYING — positioning, aggression, target choice — with proportional, never-falls-off
	// bonuses. All follow the notable pattern: read the node tier in ResetEffects (early-out at 0),
	// then apply. Bonuses use GetDamage / FinalDamage (multiplicative, post-defense), so there is no
	// subtractive-defense cliff (see CLAUDE.md §5b/§12). Single-player focused, same as the other
	// notables (per-player state on a per-player ModPlayer instance).

	/// <summary>
	/// Close Quarters — a brawler's risk/reward. While an enemy is within 10 tiles, you deal more
	/// damage AND take more damage, per tier. Rewards fighting nose-to-nose instead of kiting.
	/// </summary>
	public class CloseQuarters : ModPlayer
	{
		private const string TreeId = "warrior_tree";
		private const string NodeId = "close_quarters_notable";

		private const float RangeTiles = 10f;
		private const float RangePixels = RangeTiles * 16f;
		private const float DamageDealtPerTier = 0.10f;
		private const float DamageTakenPerTier = 0.10f;

		private int tier;
		private bool enemyNearby;

		public override void ResetEffects()
		{
			tier = Player.GetModPlayer<PassiveTreeManager>().GetNodeTier(TreeId, NodeId);
			enemyNearby = false;
			if (tier <= 0)
				return;

			// Any hostile NPC within range flips the stance on. Squared distance to avoid the sqrt.
			float rangeSq = RangePixels * RangePixels;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.dontTakeDamage)
					continue;
				if (Vector2.DistanceSquared(npc.Center, Player.Center) <= rangeSq)
				{
					enemyNearby = true;
					break;
				}
			}

			if (enemyNearby)
				Player.GetDamage(DamageClass.Generic) *= 1f + tier * DamageDealtPerTier;
		}

		public override void ModifyHurt(ref Player.HurtModifiers modifiers)
		{
			if (tier <= 0 || !enemyNearby)
				return;

			// FinalDamage (multiplicative, post-defense) so the penalty has no floor-at-1 cliff.
			modifiers.FinalDamage *= 1f + tier * DamageTakenPerTier;
		}
	}

	/// <summary>
	/// Momentum — reward hit-and-run. While moving, you deal more damage per tier; nothing while
	/// standing still.
	/// </summary>
	public class Momentum : ModPlayer
	{
		private const string TreeId = "warrior_tree";
		private const string NodeId = "momentum_notable";

		private const float DamagePerTier = 0.08f;

		private int tier;

		public override void ResetEffects()
		{
			tier = Player.GetModPlayer<PassiveTreeManager>().GetNodeTier(TreeId, NodeId);
			if (tier <= 0)
				return;

			// Vanilla's own "am I actually moving" check (|velocity| >= 0.05 on either axis).
			if (!Player.IsStandingStillForSpecialEffects)
				Player.GetDamage(DamageClass.Generic) *= 1f + tier * DamagePerTier;
		}
	}

	/// <summary>
	/// Executioner — reward finishing. You deal more damage per tier to enemies below 35% life.
	/// </summary>
	public class Executioner : ModPlayer
	{
		private const string TreeId = "warrior_tree";
		private const string NodeId = "executioner_notable";

		private const float LifeThreshold = 0.35f;
		private const float DamagePerTier = 0.10f;

		private int tier;

		public override void ResetEffects()
		{
			tier = Player.GetModPlayer<PassiveTreeManager>().GetNodeTier(TreeId, NodeId);
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			if (tier <= 0)
				return;

			// target.life is the pre-hit value here, so this reads the enemy's current health fraction.
			if (target.lifeMax > 0 && target.life <= target.lifeMax * LifeThreshold)
				modifiers.FinalDamage *= 1f + tier * DamagePerTier;
		}
	}

	/// <summary>
	/// Adrenaline — reward chaining kills. Each kill grants a short attack-speed burst that refreshes
	/// on every kill, so cutting through a pack keeps you fast.
	/// </summary>
	public class Adrenaline : ModPlayer
	{
		private const string TreeId = "warrior_tree";
		private const string NodeId = "adrenaline_notable";

		private const float AttackSpeedPerTier = 0.05f;
		private const int BuffDurationTicks = 240; // 4 seconds

		private int tier;
		private int buffTicks;

		public override void ResetEffects()
		{
			tier = Player.GetModPlayer<PassiveTreeManager>().GetNodeTier(TreeId, NodeId);
			if (tier <= 0)
			{
				buffTicks = 0;
				return;
			}

			if (buffTicks > 0)
			{
				// Juggernaut suppresses all attack-speed increases; honour it like the AttackSpeed key.
				if (!TalentPlayer.Get(Player).Suppresses(TalentSuppression.AttackSpeedIncreases))
					Player.GetAttackSpeed(DamageClass.Generic) += tier * AttackSpeedPerTier;
				buffTicks--;
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (tier <= 0)
				return;

			// Killing blow refreshes the burst (same killing-blow test Detonate uses).
			if (target.life <= 0 && !target.friendly)
				buffTicks = BuffDurationTicks;
		}
	}
}
