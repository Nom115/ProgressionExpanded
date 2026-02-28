using Terraria;
using Terraria.ID;
using Terraria.Audio;
using Microsoft.Xna.Framework;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Core
{
	/// <summary>
	/// Explosive modifier - Explodes on death
	/// </summary>
	public class ExplosiveModifier : IModifier
	{
		public string GetPrefix() => "Explosive";

		public void Apply(Terraria.NPC npc) { }

		public void OnHit(Terraria.NPC npc, Player player) { }

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
			{
				// Create explosion damage
				int explosionRadius = 150;
				int explosionDamage = npc.damage / 2;

				// Damage players in radius
				for (int i = 0; i < Main.maxPlayers; i++)
				{
					Player player = Main.player[i];
					if (player.active && !player.dead && player.Distance(npc.Center) < explosionRadius)
					{
						player.Hurt(Terraria.DataStructures.PlayerDeathReason.ByNPC(npc.whoAmI), 
							explosionDamage, 0);
					}
				}

				// Visual effects: smoke and fire dust
				for (int i = 0; i < 30; i++)
				{
					Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
					Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, 
						DustID.Smoke, velocity.X, velocity.Y, 100, default, 2f);
					dust.noGravity = true;
				}

				for (int i = 0; i < 20; i++)
				{
					Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
					Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, 
						DustID.Torch, velocity.X, velocity.Y, 100, default, 1.5f);
					dust.noGravity = true;
				}
			}

			// Play explosion sound
			SoundEngine.PlaySound(SoundID.Item14, npc.Center);
		}

		public float GetXPBonus() => 0.0f;

		public int GetSpawnWeight() => 50; // Rare
	}
}
