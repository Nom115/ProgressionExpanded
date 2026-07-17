using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints.NotablePassives
{
	/// <summary>
	/// Notable Passive: Detonate — the player-side mirror of the enemy "Explosive" affix.
	/// When you land the killing blow on an enemy, it has a tier-scaled chance to erupt for a
	/// fraction of its own maximum life, damaging nearby enemies. Gated on the node tier.
	/// Single-player focused (applies damage directly).
	/// </summary>
	public class Detonate : ModPlayer
	{
		private const string TreeId = "warrior_tree";
		private const string NodeId = "detonate_notable";

		/// <summary>Chance to detonate at tier 1 before the per-tier term.</summary>
		private const float BaseChance = 0.20f;
		private const float ChancePerTier = 0.02f;

		/// <summary>Blast damage as a fraction of the slain enemy's maximum life.</summary>
		private const float DamageFractionOfMaxLife = 0.10f;

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

			if (Main.rand.NextFloat() >= BaseChance + ChancePerTier * tier) // tier 1..5 -> 22%..30%
				return;

			// lifeMax carries the mod's level/rarity scaling, so the blast keeps pace with the world.
			int damage = (int)(target.lifeMax * DamageFractionOfMaxLife);
			if (damage <= 0)
				return;

			// Area-of-effect scaling from gear/passives (CombatEffectStats), uncapped by tier.
			float areaMul = 1f + Player.GetModPlayer<CombatEffectStats>().AreaPercent / 100f;
			float radius = (96f + 24f * tier) * areaMul; // tier 1..5 -> 120..216 px before the area bonus
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
