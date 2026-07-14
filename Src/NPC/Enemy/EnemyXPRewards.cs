using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Src.Levels.PlayerSystems;
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

			// Calculate base XP reward (flat base + boss/rarity), before the per-player
			// level-difference scaling.
			int baseXP = npcManager.CalculateXPReward(npc);

			// Apply rarity and modifier (affix) multipliers
			var modifierSystem = npc.GetGlobalNPC<EnemyModifierSystem>();
			float totalMultiplier = modifierSystem.GetTotalXPMultiplier();
			float preScaledXP = baseXP * totalMultiplier;

			if (preScaledXP <= 0f) return;

			// Award XP to nearby players who likely participated in the kill, scaled per
			// player by how far this enemy's level sits from their optimal (+5) sweet spot.
			AwardXPToNearbyPlayers(npc, npcManager, preScaledXP);
		}

		/// <summary>
		/// Award XP to all players within a reasonable range who likely participated.
		/// Each player's reward is scaled by their own level difference from the enemy,
		/// so killing enemies ~5 levels above you is the most XP-efficient.
		/// </summary>
		private void AwardXPToNearbyPlayers(Terraria.NPC npc, NPCLevelManager npcManager, float preScaledXP)
		{
			const float XP_RANGE = 1000f; // Range in pixels to award XP

			int enemyLevel = npcManager.GetLevel(npc);

			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Terraria.Player player = Main.player[i];

				// Check if player is active and nearby
				if (player.active && !player.dead && player.Distance(npc.Center) < XP_RANGE)
				{
					int playerLevel = PlayerLevelManager.GetLevel(player);
					float levelDiffMultiplier = NPCLevelManager.GetLevelDifferenceXPMultiplier(enemyLevel, playerLevel);

					int finalXP = (int)(preScaledXP * levelDiffMultiplier);
					if (finalXP <= 0) continue;

					// Award XP to player
					PlayerLevelManager.AddXP(player, finalXP);
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

			// Scale by this player's level difference from the enemy (peaks at +5).
			int playerLevel = PlayerLevelManager.GetLevel(player);
			int enemyLevel = npcManager.GetLevel(npc);
			float levelDiffMultiplier = NPCLevelManager.GetLevelDifferenceXPMultiplier(enemyLevel, playerLevel);

			int finalXP = (int)(baseXP * totalMultiplier * levelDiffMultiplier);

			if (finalXP > 0 && player.active && !player.dead)
			{
				PlayerLevelManager.AddXP(player, finalXP);
			}
		}
	}
}
