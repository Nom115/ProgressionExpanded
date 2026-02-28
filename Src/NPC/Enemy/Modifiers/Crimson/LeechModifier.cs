using Terraria;
using Microsoft.Xna.Framework;
using ProgressionExpanded.Utils;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Crimson
{
	/// <summary>
	/// Leech modifier - Steals life from players on hit
	/// Restricted to Crimson biome until Brain of Cthulhu is defeated
	/// </summary>
	public class LeechModifier : IModifier
	{
		public string GetPrefix() => "Leeching";

		public void Apply(Terraria.NPC npc) { }

		public void OnHit(Terraria.NPC npc, Player player)
		{
			// Calculate life steal amount (10% of damage dealt)
			int lifeSteal = npc.damage / 10;
			
			if (lifeSteal > 0)
			{
				// Heal the NPC
				npc.life += lifeSteal;
				if (npc.life > npc.lifeMax)
					npc.life = npc.lifeMax;

				// Visual effect - healing numbers
				CombatText.NewText(new Rectangle((int)npc.position.X, (int)npc.position.Y, 
					npc.width, npc.height), Color.Red, lifeSteal, false, false);
			}
		}

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.0f;

		public int GetSpawnWeight() => 45; // Rare

		/// <summary>
		/// Check if this modifier can be applied to the NPC
		/// </summary>
		public static bool CanApply(Terraria.NPC npc)
		{
			// If Brain of Cthulhu is defeated, can apply to any enemy
			if (BossKillTracker.DownedEvilBoss)
				return true;

			// Otherwise, only in Crimson biome
			// Find nearest player to check biome
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player player = Main.player[i];
				if (player.active && npc.Distance(player.Center) < 1000f)
				{
					return player.ZoneCrimson;
				}
			}

			return false;
		}
	}
}
