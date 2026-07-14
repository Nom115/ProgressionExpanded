using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints.NotablePassives
{
	/// <summary>
	/// Notable Passive: Detonate — the player-side mirror of the enemy "Explosive" affix.
	/// When you land the killing blow on an enemy, it erupts, dealing tier-scaled AoE damage to
	/// nearby enemies. Gated on the node tier. Single-player focused (applies damage directly).
	/// </summary>
	public class Detonate : ModPlayer
	{
		private const string TreeId = "warrior_tree";
		private const string NodeId = "detonate_notable";

		private int tier;

		public override void ResetEffects()
		{
			tier = Player.GetModPlayer<PassiveTreeManager>().GetNodeTier(TreeId, NodeId);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (tier <= 0)
				return;

			// Only detonate on a killing blow.
			if (target.life > 0 || target.friendly)
				return;

			float radius = 96f + 24f * tier; // tier 1..5 -> 120..216 px
			int damage = 20 + 15 * tier;     // tier 1..5 -> 35..95
			Explode(target.Center, radius, damage);
		}

		private void Explode(Vector2 center, float radius, int damage)
		{
			SoundEngine.PlaySound(SoundID.Item14, center); // bomb explosion

			// Explosion FX.
			for (int i = 0; i < 24; i++)
			{
				Dust dust = Dust.NewDustPerfect(center, DustID.Torch,
					Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(2f, 6f));
				dust.noGravity = true;
			}

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC other = Main.npc[i];
				if (!other.active || other.friendly || other.dontTakeDamage || other.immortal)
					continue;

				if (Vector2.Distance(other.Center, center) > radius)
					continue;

				int hitDirection = other.Center.X < center.X ? -1 : 1;
				other.SimpleStrikeNPC(damage, hitDirection, false, 2f, DamageClass.Generic);
			}
		}
	}
}
