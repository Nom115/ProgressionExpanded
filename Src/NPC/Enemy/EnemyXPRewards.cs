using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Src.Levels.PlayerLevel;
using ProgressionExpanded.Src.NPCs;
using ProgressionExpanded.Src.NPCs.Enemy;

namespace ProgressionExpanded.Src.NPCs.Enemy
{
	/// <summary>
	/// Handles XP rewards when enemies are killed.
	/// Awards XP to players who dealt damage to the enemy.
	/// </summary>
	public class EnemyXPRewards : GlobalNPC
	{
		public override void OnKill(Terraria.NPC npc)
		{
			// Get the NPC level manager to check if this NPC should give XP
			var npcManager = npc.GetGlobalNPC<NPCLevelManager>();
			
			if (!npcManager.ShouldGiveXP(npc))
			{
				return;
			}

			// Calculate base XP reward based on NPC level
			int baseXP = npcManager.CalculateXPReward(npc);

			// Apply rarity and modifier multipliers
			var modifierSystem = npc.GetGlobalNPC<EnemyModifierSystem>();
			float totalMultiplier = modifierSystem.GetTotalXPMultiplier();
			int finalXP = (int)(baseXP * totalMultiplier);

			if (finalXP <= 0) return;

			// Award XP to nearby players who likely participated in the kill
			AwardXPToNearbyPlayers(npc, finalXP);
		}

		/// <summary>
		/// Award XP to all players within a reasonable range who likely participated
		/// </summary>
		private void AwardXPToNearbyPlayers(Terraria.NPC npc, int xpReward)
		{
			const float XP_RANGE = 1000f; // Range in pixels to award XP
			
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Terraria.Player player = Main.player[i];
				
				// Check if player is active and nearby
				if (player.active && !player.dead && player.Distance(npc.Center) < XP_RANGE)
				{
					// Award XP to player
					PlayerLevelManager.AddXP(player, xpReward);
				}
			}
		}

		/// <summary>
		/// Award XP to a specific player (useful for single-player or direct kills)
		/// </summary>
		public static void AwardXPToPlayer(Terraria.Player player, Terraria.NPC npc)
		{
			var npcManager = npc.GetGlobalNPC<NPCLevelManager>();
			
			if (!npcManager.ShouldGiveXP(npc))
			{
				return;
			}

			int baseXP = npcManager.CalculateXPReward(npc);
			
			// Apply rarity and modifier multipliers
			var modifierSystem = npc.GetGlobalNPC<EnemyModifierSystem>();
			float totalMultiplier = modifierSystem.GetTotalXPMultiplier();
			int finalXP = (int)(baseXP * totalMultiplier);
			
			if (finalXP > 0 && player.active && !player.dead)
			{
				PlayerLevelManager.AddXP(player, finalXP);
			}
		}
	}
}
