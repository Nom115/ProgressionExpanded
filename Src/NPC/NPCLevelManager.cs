using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProgressionExpanded.Src.Levels;
using ProgressionExpanded.Src.Levels.PlayerSystems;
using ProgressionExpanded.Src.Levels.WorldLevel;
using ProgressionExpanded.Src.NPCs.Enemy;
using ProgressionExpanded.Utils;
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
		// Flat base reward (matches the design's flat-monster-XP curve). The amount each
		// player actually receives is scaled afterwards by how far this enemy's level sits
		// from that player's "sweet spot" (see GetLevelDifferenceXPMultiplier).
		private const int BASE_XP_REWARD = 10;

		// Level-difference reward curve. Reward peaks (x1.0) when the enemy is exactly
		// OPTIMAL_LEVEL_DIFFERENCE levels above the player, and falls off on both sides so
		// that fighting enemies around the sweet spot is the most XP-efficient.
		private const int OPTIMAL_LEVEL_DIFFERENCE = 5;
		private const float BELOW_OPTIMAL_FALLOFF = 0.125f; // per level under the sweet spot (+1 -> 0.5x)
		private const float ABOVE_OPTIMAL_FALLOFF = 0.05f;  // per level over the sweet spot (gentler)
		private const float MIN_XP_MULTIPLIER = 0.1f;
		private const float MAX_XP_MULTIPLIER = 1.0f;

		// Level-difference COMBAT scaling.
		// Player -> enemy: you deal extra damage to enemies below your level (gentle ramp).
		private const float PLAYER_DAMAGE_PER_LEVEL_BELOW = 0.10f; // +10% per level the enemy is below you
		private const float PLAYER_DAMAGE_MAX_MULTIPLIER = 3.0f;   // cap (reached ~20 levels below)
		// Enemy -> player: enemies above your level hit much harder — scales hard and fast.
		private const float ENEMY_DAMAGE_PER_LEVEL_ABOVE = 0.20f;  // +20% per level the enemy is above you
		private const float ENEMY_DAMAGE_MAX_MULTIPLIER = 5.0f;    // cap so it stays survivable

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

			ApplyPlayerDamageScaling(npc, player, ref modifiers);
		}

		public override void ModifyHitByProjectile(Terraria.NPC npc, Projectile projectile, ref Terraria.NPC.HitModifiers modifiers)
		{
			// Ensure level is initialized
			if (!levelInitialized)
			{
				InitializeLevel(npc);
			}

			// Scale by the owning player's level (covers weapons, minions, etc.).
			if (projectile.friendly && projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
			{
				ApplyPlayerDamageScaling(npc, Main.player[projectile.owner], ref modifiers);
			}
		}

		public override void ModifyHitPlayer(Terraria.NPC npc, Terraria.Player target, ref Terraria.Player.HurtModifiers modifiers)
		{
			// Ensure level is initialized
			if (!levelInitialized)
			{
				InitializeLevel(npc);
			}

			// Pinnacle bosses ignore level entirely — their damage is untouched.
			if (BossProgressionTracker.IsPinnacleEncounter(npc)) return;

			int playerLevel = PlayerLevelManager.GetLevel(target);
			float multiplier = GetEnemyDamageMultiplier(npcLevel, playerLevel);
			if (multiplier != 1.0f)
			{
				modifiers.FinalDamage *= multiplier;
			}
		}

		/// <summary>
		/// Apply the player-vs-enemy level-difference damage multiplier to an outgoing hit.
		/// Skipped entirely for pinnacle bosses (level has no bearing on their damage taken).
		/// </summary>
		private void ApplyPlayerDamageScaling(Terraria.NPC npc, Terraria.Player player, ref Terraria.NPC.HitModifiers modifiers)
		{
			if (BossProgressionTracker.IsPinnacleEncounter(npc)) return;

			int playerLevel = PlayerLevelManager.GetLevel(player);
			float multiplier = GetPlayerDamageMultiplier(npcLevel, playerLevel);
			if (multiplier != 1.0f)
			{
				modifiers.FinalDamage *= multiplier;
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

			// Pinnacle boss (first-ever kill): leave its health completely untouched.
			if (BossProgressionTracker.IsPinnacleEncounter(npc)) return;

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

			// Flat base reward. The per-player level-difference scaling is applied later
			// in EnemyXPRewards, once we know which player is being awarded.
			float xpReward = BASE_XP_REWARD;

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

		/// <summary>
		/// Reward multiplier based on how far this enemy's level sits above the player's.
		/// Peaks at x1.0 when the enemy is exactly OPTIMAL_LEVEL_DIFFERENCE (+5) levels above
		/// the player, and tapers off on both sides so the sweet spot is the most XP-efficient.
		/// Example, level 10 player: a +5 (level 15) enemy -> x1.0; a +1 (level 11) enemy -> x0.5.
		/// </summary>
		public static float GetLevelDifferenceXPMultiplier(int enemyLevel, int playerLevel)
		{
			int difference = enemyLevel - playerLevel;

			float multiplier;
			if (difference <= OPTIMAL_LEVEL_DIFFERENCE)
			{
				// Ramp up to the sweet spot: each level below it costs BELOW_OPTIMAL_FALLOFF.
				multiplier = MAX_XP_MULTIPLIER - BELOW_OPTIMAL_FALLOFF * (OPTIMAL_LEVEL_DIFFERENCE - difference);
			}
			else
			{
				// Taper off above the sweet spot (gentler, but still keeps +5 the peak).
				multiplier = MAX_XP_MULTIPLIER - ABOVE_OPTIMAL_FALLOFF * (difference - OPTIMAL_LEVEL_DIFFERENCE);
			}

			return System.Math.Clamp(multiplier, MIN_XP_MULTIPLIER, MAX_XP_MULTIPLIER);
		}

		#endregion

		#region Combat Scaling

		/// <summary>
		/// Multiplier for damage the PLAYER deals to this enemy, based on level difference.
		/// Enemies below the player take progressively more damage (a gentle ramp, so that
		/// e.g. an enemy 5 levels lower takes ~1.5x); enemies at or above the player take
		/// normal damage. Capped at PLAYER_DAMAGE_MAX_MULTIPLIER.
		/// </summary>
		public static float GetPlayerDamageMultiplier(int enemyLevel, int playerLevel)
		{
			int levelsBelow = playerLevel - enemyLevel;
			if (levelsBelow <= 0) return 1.0f; // no bonus against equal-or-higher enemies

			float multiplier = 1.0f + PLAYER_DAMAGE_PER_LEVEL_BELOW * levelsBelow;
			return System.Math.Min(multiplier, PLAYER_DAMAGE_MAX_MULTIPLIER);
		}

		/// <summary>
		/// Multiplier for damage this ENEMY deals to the player, based on level difference.
		/// Enemies above the player hit much harder and it scales up hard and fast for each
		/// level above (e.g. +5 -> 2x, +10 -> 3x); at or below the player it is normal.
		/// Capped at ENEMY_DAMAGE_MAX_MULTIPLIER so it stays survivable.
		/// </summary>
		public static float GetEnemyDamageMultiplier(int enemyLevel, int playerLevel)
		{
			int levelsAbove = enemyLevel - playerLevel;
			if (levelsAbove <= 0) return 1.0f; // no bonus when at or below the player

			float multiplier = 1.0f + ENEMY_DAMAGE_PER_LEVEL_ABOVE * levelsAbove;
			return System.Math.Min(multiplier, ENEMY_DAMAGE_MAX_MULTIPLIER);
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
