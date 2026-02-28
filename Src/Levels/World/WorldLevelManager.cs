using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.WorldLevel
{
	/// <summary>
	/// Manages world level and difficulty variance.
	/// World level affects enemy scaling and loot quality.
	/// Variance adds randomization to keep gameplay interesting.
	/// </summary>
	public static class WorldLevelManager
	{
		// Constants
		private const int BASE_WORLD_LEVEL = 1;
		private const int MAX_WORLD_LEVEL = 100;
		private const float DEFAULT_VARIANCE = 0.0f;
		private const float MAX_VARIANCE = 2.0f;
		private const float MIN_VARIANCE = -2.0f;

		#region World Level Management

		/// <summary>
		/// Get the current world level
		/// </summary>
		public static int GetWorldLevel()
		{
			return WorldDataManager.GetInt("worldLevel", BASE_WORLD_LEVEL);
		}

		/// <summary>
		/// Set the world level
		/// </summary>
		public static void SetWorldLevel(int level)
		{
			int clampedLevel = System.Math.Clamp(level, BASE_WORLD_LEVEL, MAX_WORLD_LEVEL);
			WorldDataManager.SetInt("worldLevel", clampedLevel);
		}

		/// <summary>
		/// Increase world level by a specified amount
		/// </summary>
		public static void IncreaseWorldLevel(int amount)
		{
			if (amount <= 0) return;
			
			int currentLevel = GetWorldLevel();
			SetWorldLevel(currentLevel + amount);
		}

		#endregion

		#region Variance Management

		/// <summary>
		/// Get the current world difficulty variance
		/// Variance modifies effective world level for calculations
		/// </summary>
		public static float GetVariance()
		{
			return WorldDataManager.GetFloat("worldVariance", DEFAULT_VARIANCE);
		}

		/// <summary>
		/// Set the world difficulty variance
		/// </summary>
		public static void SetVariance(float variance)
		{
			float clampedVariance = System.Math.Clamp(variance, MIN_VARIANCE, MAX_VARIANCE);
			WorldDataManager.SetFloat("worldVariance", clampedVariance);
		}

		/// <summary>
		/// Apply random variance within a range
		/// </summary>
		/// <param name="minVariance">Minimum variance to apply</param>
		/// <param name="maxVariance">Maximum variance to apply</param>
		public static void ApplyRandomVariance(float minVariance = -1.0f, float maxVariance = 1.0f)
		{
			float randomVariance = (float)(Terraria.Main.rand.NextDouble() * (maxVariance - minVariance) + minVariance);
			SetVariance(randomVariance);
		}

		/// <summary>
		/// Get effective world level including variance
		/// </summary>
		public static float GetEffectiveLevel()
		{
			int baseLevel = GetWorldLevel();
			float variance = GetVariance();
			return baseLevel + variance;
		}

		#endregion

		#region Scaling Calculations

		/// <summary>
		/// Get a scaling multiplier based on world level
		/// </summary>
		/// <param name="baseValue">The base value to scale</param>
		/// <param name="scalingFactor">How much each level increases the value (default 0.05 = 5% per level)</param>
		public static float GetScaledValue(float baseValue, float scalingFactor = 0.05f)
		{
			float effectiveLevel = GetEffectiveLevel();
			return baseValue * (1f + (effectiveLevel - 1) * scalingFactor);
		}

		/// <summary>
		/// Get enemy health multiplier based on world level
		/// </summary>
		public static float GetEnemyHealthMultiplier()
		{
			return GetScaledValue(1.0f, 0.15f); // 15% per level
		}

		/// <summary>
		/// Get enemy damage multiplier based on world level
		/// </summary>
		public static float GetEnemyDamageMultiplier()
		{
			return GetScaledValue(1.0f, 0.14f); // 14% per level
		}

		/// <summary>
		/// Get loot quality multiplier based on world level
		/// </summary>
		public static float GetLootQualityMultiplier()
		{
			return GetScaledValue(1.0f, 0.01f); // 1% per level
		}

		#endregion

		#region Utility

		/// <summary>
		/// Reset world level and variance to defaults
		/// </summary>
		public static void Reset()
		{
			SetWorldLevel(BASE_WORLD_LEVEL);
			SetVariance(DEFAULT_VARIANCE);
		}

		/// <summary>
		/// Check if world is at max level
		/// </summary>
		public static bool IsMaxLevel()
		{
			return GetWorldLevel() >= MAX_WORLD_LEVEL;
		}

		/// <summary>
		/// Get max world level cap
		/// </summary>
		public static int GetMaxLevel()
		{
			return MAX_WORLD_LEVEL;
		}

		/// <summary>
		/// Get current variance range
		/// </summary>
		public static (float min, float max) GetVarianceRange()
		{
			return (MIN_VARIANCE, MAX_VARIANCE);
		}

		#endregion
	}
}
