using Terraria;
using ProgressionExpanded.Src.Levels.PlayerSystems;
using ProgressionExpanded.Src.Levels.WorldLevel;

namespace ProgressionExpanded.Src.Levels
{
	/// <summary>
	/// Central manager that coordinates between player and world level systems.
	/// Acts as a facade for level-related operations without containing the logic itself.
	/// </summary>
	public static class LevelManager
	{
		#region Player Level Access

		/// <summary>
		/// Get the player's current level
		/// </summary>
		public static int GetPlayerLevel(Terraria.Player player)
		{
			return PlayerLevelManager.GetLevel(player);
		}

		/// <summary>
		/// Get the player's current XP
		/// </summary>
		public static int GetPlayerXP(Terraria.Player player)
		{
			return PlayerLevelManager.GetXP(player);
		}

		/// <summary>
		/// Add XP to player (handles level ups automatically)
		/// </summary>
		public static bool AddPlayerXP(Terraria.Player player, int amount)
		{
			return PlayerLevelManager.AddXP(player, amount);
		}

		/// <summary>
		/// Get player's XP progress to next level (0-1)
		/// </summary>
		public static float GetPlayerXPProgress(Terraria.Player player)
		{
			return PlayerLevelManager.GetXPProgress(player);
		}

		#endregion

		#region World Level Access

		/// <summary>
		/// Get the current world level
		/// </summary>
		public static int GetWorldLevel()
		{
			return WorldLevelManager.GetWorldLevel();
		}

		/// <summary>
		/// Get the world level variance
		/// </summary>
		public static float GetWorldVariance()
		{
			return WorldLevelManager.GetVariance();
		}

		/// <summary>
		/// Get effective world level (including variance)
		/// </summary>
		public static float GetEffectiveWorldLevel()
		{
			return WorldLevelManager.GetEffectiveLevel();
		}

		/// <summary>
		/// Set world level
		/// </summary>
		public static void SetWorldLevel(int level)
		{
			WorldLevelManager.SetWorldLevel(level);
		}

		/// <summary>
		/// Set world variance
		/// </summary>
		public static void SetWorldVariance(float variance)
		{
			WorldLevelManager.SetVariance(variance);
		}

		#endregion

		#region Coordination Methods

		/// <summary>
		/// Sync world level to match average player level
		/// Useful for multiplayer or when you want world difficulty to scale with players
		/// </summary>
		public static void SyncWorldLevelToPlayers()
		{
			// Get all active players
			int totalLevel = 0;
			int playerCount = 0;

			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Terraria.Player player = Main.player[i];
				if (player != null && player.active)
				{
					totalLevel += PlayerLevelManager.GetLevel(player);
					playerCount++;
				}
			}

			if (playerCount > 0)
			{
				int averageLevel = totalLevel / playerCount;
				WorldLevelManager.SetWorldLevel(averageLevel);
			}
		}

		/// <summary>
		/// Get level difference between player and world
		/// Positive = player is higher level, Negative = world is higher level
		/// </summary>
		public static float GetLevelDifference(Terraria.Player player)
		{
			int playerLevel = PlayerLevelManager.GetLevel(player);
			float worldLevel = WorldLevelManager.GetEffectiveLevel();
			return playerLevel - worldLevel;
		}

		/// <summary>
		/// Check if player is underleveled for current world
		/// </summary>
		public static bool IsPlayerUnderleveled(Terraria.Player player, int levelThreshold = 3)
		{
			return GetLevelDifference(player) < -levelThreshold;
		}

		/// <summary>
		/// Check if player is overleveled for current world
		/// </summary>
		public static bool IsPlayerOverleveled(Terraria.Player player, int levelThreshold = 3)
		{
			return GetLevelDifference(player) > levelThreshold;
		}

		#endregion

		#region Level Display Control

		/// <summary>
		/// Enable or disable player level tags
		/// </summary>
		public static void SetPlayerLevelDisplay(bool enabled)
		{
			LevelDisplay.ShowPlayerLevels = enabled;
		}

		/// <summary>
		/// Enable or disable NPC level tags
		/// </summary>
		public static void SetNPCLevelDisplay(bool enabled)
		{
			LevelDisplay.ShowNPCLevels = enabled;
		}

		/// <summary>
		/// Set whether level tags only show when hovering
		/// </summary>
		public static void SetHoverOnlyMode(bool hoverOnly)
		{
			LevelDisplay.ShowOnlyWhenHovered = hoverOnly;
		}

		/// <summary>
		/// Get current player level display state
		/// </summary>
		public static bool IsPlayerLevelDisplayEnabled()
		{
			return LevelDisplay.ShowPlayerLevels;
		}

		/// <summary>
		/// Get current NPC level display state
		/// </summary>
		public static bool IsNPCLevelDisplayEnabled()
		{
			return LevelDisplay.ShowNPCLevels;
		}

		#endregion

		#region Utility

		/// <summary>
		/// Get a summary of current level status
		/// </summary>
		public static string GetLevelSummary(Terraria.Player player)
		{
			int playerLevel = GetPlayerLevel(player);
			int playerXP = GetPlayerXP(player);
			int worldLevel = GetWorldLevel();
			float worldVariance = GetWorldVariance();

			return $"Player Level: {playerLevel} (XP: {playerXP}) | World Level: {worldLevel} (Variance: {worldVariance:F2})";
		}

		#endregion
	}
}
