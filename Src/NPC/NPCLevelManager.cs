using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProgressionExpanded.Src.Levels;
using ProgressionExpanded.Src.Levels.WorldLevel;
using ProgressionExpanded.Src.NPCs.Enemy;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.NPCs
{
	/// <summary>
	/// Manages NPC levels and scaling based on world level.
	/// Each NPC gets a level assigned based on the current world level.
	/// </summary>
	public class NPCLevelManager : GlobalNPC
	{
		// Constants
		private const int BASE_XP_REWARD = 10;
		private const float XP_SCALING_FACTOR = 1.0f; // XP scales with NPC level

		public override bool InstancePerEntity => true;

		// Per-NPC instance data
		private int npcLevel = 1;
		private bool levelInitialized = false;

		#region Level Management

		/// <summary>
		/// Get the level of this NPC
		/// </summary>
		public int GetLevel(Terraria.NPC npc)
		{
			if (!levelInitialized)
			{
				InitializeLevel(npc);
			}
			return npcLevel;
		}

		/// <summary>
		/// Set the level of this NPC
		/// </summary>
		public void SetLevel(Terraria.NPC npc, int level)
		{
			npcLevel = System.Math.Max(1, level);
			levelInitialized = true;
			ApplyLevelScaling(npc);
		}

		/// <summary>
		/// Initialize NPC level based on world level when spawned
		/// Uses 60/40 distribution: 60% within ±3 levels, 40% 5-10 levels above
		/// </summary>
		private void InitializeLevel(Terraria.NPC npc)
		{
			if (levelInitialized) return;

			// Base level is the world level
			int worldLevel = WorldLevelManager.GetWorldLevel();
			
			int variance;
			// 60% chance: within ±3 levels of world level
			if (Main.rand.NextFloat() < 0.6f)
			{
				variance = Main.rand.Next(-3, 4); // -3 to +3
			}
			// 40% chance: 5-10 levels above world level
			else
			{
				variance = Main.rand.Next(5, 11); // 5 to 10
			}
			
			int finalLevel = System.Math.Max(1, worldLevel + variance);

			SetLevel(npc, finalLevel);
		}

		#endregion

		#region NPC Hooks

		public override void OnSpawn(Terraria.NPC npc, IEntitySource source)
		{
			// Initialize level when NPC spawns
			if (!npc.friendly && npc.lifeMax > 5) // Only for hostile NPCs with reasonable health
			{
				InitializeLevel(npc);
			}
		}

		public override void ModifyHitByItem(Terraria.NPC npc, Terraria.Player player, Item item, ref Terraria.NPC.HitModifiers modifiers)
		{
			// Ensure level is initialized
			if (!levelInitialized)
			{
				InitializeLevel(npc);
			}
		}

		public override void ModifyHitByProjectile(Terraria.NPC npc, Projectile projectile, ref Terraria.NPC.HitModifiers modifiers)
		{
			// Ensure level is initialized
			if (!levelInitialized)
			{
				InitializeLevel(npc);
			}
		}

		public override void PostDraw(Terraria.NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			// Draw level tag and modified name above NPC
			if (levelInitialized)
			{
				var modifierSystem = npc.GetGlobalNPC<EnemyModifierSystem>();
				var rarityInfo = modifierSystem.GetRarityInfo();
				string displayName = modifierSystem.GetDisplayName();
				
				// Draw level with appropriate rarity color
				LevelDisplay.DrawNPCLevelTag(npc, npcLevel, displayName, rarityInfo.Color);
			}
		}

		#endregion

		#region Scaling

		/// <summary>
		/// Apply stat scaling based on NPC level
		/// </summary>
		private void ApplyLevelScaling(Terraria.NPC npc)
		{
			if (npc.lifeMax <= 5 || npc.friendly) return; // Skip townspeople and critters

			// Get world level scaling multipliers
			float healthMultiplier = WorldLevelManager.GetEnemyHealthMultiplier();
			float damageMultiplier = WorldLevelManager.GetEnemyDamageMultiplier();

			// Apply additional NPC-specific level scaling
			int levelDifference = npcLevel - 1; // Difference from base level 1
			
			// Health scales with level (5% per level)
			float levelHealthMultiplier = 1.0f + (levelDifference * 0.05f);
			npc.lifeMax = (int)(npc.lifeMax * healthMultiplier * levelHealthMultiplier);
			npc.life = npc.lifeMax;

			// Damage scales with level (4% per level)
			float levelDamageMultiplier = 1.0f + (levelDifference * 0.04f);
			npc.damage = (int)(npc.damage * damageMultiplier * levelDamageMultiplier);

			// Defense scales with level (2% per level)
			float levelDefenseMultiplier = 1.0f + (levelDifference * 0.02f);
			npc.defense = (int)(npc.defense * levelDefenseMultiplier);
		}

		#endregion

		#region XP Calculations

		/// <summary>
		/// Calculate XP reward for killing this NPC
		/// </summary>
		public int CalculateXPReward(Terraria.NPC npc)
		{
			if (!levelInitialized)
			{
				InitializeLevel(npc);
			}

			// Base XP scales with NPC level
			float xpReward = BASE_XP_REWARD * (1.0f + (npcLevel - 1) * XP_SCALING_FACTOR);

			// Bosses give more XP
			if (npc.boss)
			{
				xpReward *= 10f;
			}

			// Rare enemies give bonus XP
			if (npc.rarity > 0)
			{
				xpReward *= (1f + (npc.rarity * 0.5f));
			}

			return (int)xpReward;
		}

		#endregion

		#region Utility

		/// <summary>
		/// Check if this NPC should give XP rewards
		/// </summary>
		public bool ShouldGiveXP(Terraria.NPC npc)
		{
			// Don't give XP for friendly NPCs, critters, or very weak enemies
			if (npc.friendly || npc.lifeMax <= 5 || npc.townNPC)
			{
				return false;
			}

			// Don't give XP for statue-spawned enemies (to prevent farming)
			if (npc.SpawnedFromStatue)
			{
				return false;
			}

			return true;
		}

		#endregion

		#region Cloning

		protected override bool CloneNewInstances => false;

		public override GlobalNPC Clone(Terraria.NPC from, Terraria.NPC to)
		{
			var fromGlobal = from.GetGlobalNPC<NPCLevelManager>();
			var toGlobal = (NPCLevelManager)base.Clone(from, to);
			
			toGlobal.npcLevel = fromGlobal.npcLevel;
			toGlobal.levelInitialized = fromGlobal.levelInitialized;
			
			return toGlobal;
		}

		#endregion
	}
}
