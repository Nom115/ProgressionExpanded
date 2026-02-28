using Terraria;
using Terraria.ID;
using Microsoft.Xna.Framework;
using ProgressionExpanded.Utils.DataManagers;
using ProgressionExpanded.Src.Levels.WorldLevel;

namespace ProgressionExpanded.Src.Levels.PlayerSystems
{
	/// <summary>
	/// Manages player level and experience points.
	/// Handles leveling up, XP gains, and level-related calculations.
	/// </summary>
	public static class PlayerLevelManager
	{
		// Constants - Power Curve Formula: A + B*level + C*level^2
		private const int FORMULA_A = 100;  // Base XP
		private const int FORMULA_B = 25;   // Linear scaling
		private const int FORMULA_C = 10;   // Quadratic scaling
		private const int MAX_LEVEL = 100;

		#region Level Management

		/// <summary>
		/// Get the player's current level
		/// </summary>
		public static int GetLevel(Terraria.Player player)
		{
			return PlayerDataManager.GetInt(player, "level", 1);
		}

		/// <summary>
		/// Set the player's level
		/// </summary>
		public static void SetLevel(Terraria.Player player, int level)
		{
			int clampedLevel = System.Math.Clamp(level, 1, MAX_LEVEL);
			PlayerDataManager.SetInt(player, "level", clampedLevel);
		}

		/// <summary>
		/// Get the player's current XP
		/// </summary>
		public static int GetXP(Terraria.Player player)
		{
			return PlayerDataManager.GetInt(player, "xp", 0);
		}

		/// <summary>
		/// Set the player's XP
		/// </summary>
		public static void SetXP(Terraria.Player player, int xp)
		{
			PlayerDataManager.SetInt(player, "xp", System.Math.Max(0, xp));
		}

		/// <summary>
		/// Add XP to the player and handle level ups
		/// </summary>
		/// <param name="player">The player to add XP to</param>
		/// <param name="amount">Amount of XP to add</param>
		/// <returns>True if the player leveled up</returns>
		public static bool AddXP(Terraria.Player player, int amount)
		{
			if (amount <= 0) return false;

			int currentLevel = GetLevel(player);
			if (currentLevel >= MAX_LEVEL) return false;

			// Show floating XP text in purple
			if (Main.netMode != NetmodeID.Server)
			{
				CombatText.NewText(player.getRect(), Color.Purple, $"+{amount} XP", false, false);
			}

			int currentXP = GetXP(player);
			int newXP = currentXP + amount;
			
			bool leveledUp = false;
			int xpRequired = GetXPRequiredForLevel(currentLevel);

			// Check for level up(s)
			while (newXP >= xpRequired && currentLevel < MAX_LEVEL)
			{
				newXP -= xpRequired;
				currentLevel++;
				leveledUp = true;
				xpRequired = GetXPRequiredForLevel(currentLevel);

				OnLevelUp(player, currentLevel);
			}

			SetLevel(player, currentLevel);
			SetXP(player, newXP);

			return leveledUp;
		}

		#endregion

		#region XP Calculations

		/// <summary>
		/// Uses power curve formula: A + B*level + C*level^2
		/// </summary>
		public static int GetXPRequiredForLevel(int currentLevel)
		{
			if (currentLevel >= MAX_LEVEL) return int.MaxValue;
			
			// Power curve XP scaling: 100 + 25*level + 10*level^2
			int levelSquared = currentLevel * currentLevel;
			return FORMULA_A + (FORMULA_B * currentLevel) + (FORMULA_C * levelSquared);
		}

		/// <summary>
		/// Get XP progress as a percentage (0-1)
		/// </summary>
		public static float GetXPProgress(Terraria.Player player)
		{
			int currentLevel = GetLevel(player);
			if (currentLevel >= MAX_LEVEL) return 1f;

			int currentXP = GetXP(player);
			int requiredXP = GetXPRequiredForLevel(currentLevel);

			return (float)currentXP / requiredXP;
		}

		/// <summary>
		/// Get total XP earned across all levels
		/// </summary>
		public static int GetTotalXP(Terraria.Player player)
		{
			int currentLevel = GetLevel(player);
			int currentXP = GetXP(player);
			int totalXP = currentXP;

			// Add XP from previous levels
			for (int i = 1; i < currentLevel; i++)
			{
				totalXP += GetXPRequiredForLevel(i);
			}

			return totalXP;
		}

		#endregion

		#region Level Events

		/// <summary>
		/// Called when a player levels up
		/// </summary>
		private static void OnLevelUp(Terraria.Player player, int newLevel)
		{
			// Store the new level
			SetLevel(player, newLevel);

			// Award 1 passive point per level
			PassivePointManager.AwardPlayerPoints(player, 1);

			// Increase world level when player levels up
			WorldLevelManager.IncreaseWorldLevel(1);

			// Show level up message in chat
			if (Main.netMode != NetmodeID.Server)
			{
				string levelUpMessage = $"Level Up! You are now level {newLevel}! (+1 Passive Point)";
				Main.NewText(levelUpMessage, new Color(100, 255, 100)); // Green text
				
				// Show floating combat text
				CombatText.NewText(player.getRect(), new Color(255, 215, 0), $"LEVEL UP!", true, false);
			}

			// Reset XP overflow is handled in AddXP
			
			// TODO: Add level up rewards/bonuses here
			// Example: Heal player, grant bonus stats, visual effects, etc.
		}

		#endregion

		#region Utility

		/// <summary>
		/// Reset player level and XP
		/// </summary>
		public static void ResetProgress(Terraria.Player player)
		{
			SetLevel(player, 1);
			SetXP(player, 0);
		}

		/// <summary>
		/// Get max level cap
		/// </summary>
		public static int GetMaxLevel()
		{
			return MAX_LEVEL;
		}

		/// <summary>
		/// Check if player is at max level
		/// </summary>
		public static bool IsMaxLevel(Terraria.Player player)
		{
			return GetLevel(player) >= MAX_LEVEL;
		}

		#endregion
	}
}
