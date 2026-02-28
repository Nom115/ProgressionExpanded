using Terraria;
using Terraria.ID;
using Microsoft.Xna.Framework;
using ProgressionExpanded.Utils;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Corruption
{
	/// <summary>
	/// Vile Spit modifier - Periodically fires corruption projectiles
	/// Restricted to Corruption biome until Eater of Worlds is defeated
	/// </summary>
	public class VileSpitModifier : IModifier
	{
		private int shootTimer = 0;
		private const int SHOOT_COOLDOWN = 180; // 3 seconds at 60 FPS

		public string GetPrefix() => "Vile";

		public void Apply(Terraria.NPC npc) { }

		public void OnHit(Terraria.NPC npc, Player player) { }

		public void Update(Terraria.NPC npc)
		{
			// Only fire on server/singleplayer
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;

			shootTimer++;

			if (shootTimer >= SHOOT_COOLDOWN)
			{
				shootTimer = 0;

				// Find nearest player
				Player target = null;
				float nearestDistance = 800f; // Max range

				for (int i = 0; i < Main.maxPlayers; i++)
				{
					Player player = Main.player[i];
					if (player.active && !player.dead)
					{
						float distance = npc.Distance(player.Center);
						if (distance < nearestDistance)
						{
							nearestDistance = distance;
							target = player;
						}
					}
				}

				// Fire projectile at nearest player
				if (target != null)
				{
					Vector2 velocity = target.Center - npc.Center;
					velocity.Normalize();
					velocity *= 8f; // Projectile speed

					int damage = npc.damage / 3; // Reduced damage for projectile
					int projectileType = ProjectileID.VilethornBase; // Corruption projectile

					Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, velocity, 
						projectileType, damage, 1f, Main.myPlayer);
				}
			}
		}

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.0f;

		public int GetSpawnWeight() => 40; // Rare

		/// <summary>
		/// Check if this modifier can be applied to the NPC
		/// </summary>
		public static bool CanApply(Terraria.NPC npc)
		{
			// If EoW is defeated, can apply to any enemy
			if (BossKillTracker.DownedEvilBoss)
				return true;

			// Otherwise, only in Corruption biome
			// Find nearest player to check biome
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player player = Main.player[i];
				if (player.active && npc.Distance(player.Center) < 1000f)
				{
					return player.ZoneCorrupt;
				}
			}

			return false;
		}
	}
}
