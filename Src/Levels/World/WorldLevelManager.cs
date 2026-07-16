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

		// Enemy damage scaling. DELIBERATELY far gentler than the health curve, and capped.
		// See the comment on GetEnemyDamageMultiplier — this asymmetry is the whole point.
		private const float ENEMY_DAMAGE_PER_LEVEL = 0.03f;
		private const float ENEMY_DAMAGE_MAX_MULTIPLIER = 2.5f;

		/// <summary>
		/// Get enemy health multiplier based on world level.
		///
		/// Health scales fast (15%/level, uncapped) because health is <b>friction</b>: it is answered
		/// by player DPS, which grows by orders of magnitude across a playthrough (a copper shortsword
		/// to an endgame weapon). There is enormous headroom here. Damage is not the same kind of
		/// thing — see <see cref="GetEnemyDamageMultiplier"/>.
		/// </summary>
		public static float GetEnemyHealthMultiplier()
		{
			return GetScaledValue(1.0f, 0.15f); // 15% per level
		}

		/// <summary>
		/// Get enemy damage multiplier based on world level.
		///
		/// ⚠️ <b>This is 3%/level and capped at ×2.5. It used to be 14%/level uncapped (×14.86 at
		/// WL100), and that was the single biggest balance bug in the mod. Do not raise it without
		/// reading this.</b> Model: <c>.scripts/enemy_damage_scaling.py</c>.
		///
		/// Three reasons it has to be this much gentler than the health curve:
		/// <list type="number">
		/// <item><b>Damage is lethality, not friction.</b> Health is answered by DPS, which grows
		/// ~1000× across a playthrough. Damage is answered by EHP, which grows ~10× at best and
		/// almost 0× early. The two curves have completely different headroom, so sharing a rate
		/// (15% vs 14%) was the design error.</item>
		/// <item><b>Vanilla already escalates damage with progression.</b> A Zombie does 15; a Solar
		/// Crawltipede does ~180 — ×12 across a playthrough, already tuned against the gear you would
		/// have. World level correlates with progression, so a big multiplier here stacks a *second*
		/// progression curve on top of vanilla's. This multiplier's only legitimate job is "stop a
		/// WL50 Zombie from being free", which needs ~×2–3, not ×15.</item>
		/// <item><b>The player gains ~1%/level, so 14%/level diverged forever.</b> A level grants one
		/// passive point (+2 max life ≈ +1% EHP) while the world gained 14% enemy damage. Compounding,
		/// that is unwinnable at any armour value. The XP curve front-loads it too: level 8 lands at
		/// ~374 kills, before any boss gear exists.</item>
		/// </list>
		///
		/// The cap matters as much as the rate: it is what stops a level-100 world from one-shotting
		/// through endgame armour, and it is why this is a ratchet rather than a wall.
		///
		/// ⚠️ Our multiplier lands <b>on top of</b> whatever the difficulty mode already did.
		/// <c>NPC.NewNPC:91956</c> runs <c>SetDefaults</c> (→ <c>ScaleStats_ApplyGameMode</c>) before
		/// <c>NPCLoader.OnSpawn:91980</c>, so <c>npc.damage</c> already contains Master's ×3 — ×4 with
		/// For the Worthy, which adds +1 to the multiplier rather than being a separate stage — plus
		/// anything Calamity/Fargo's baked in. Everything here multiplies that.
		/// </summary>
		public static float GetEnemyDamageMultiplier()
		{
			return System.Math.Min(
				GetScaledValue(1.0f, ENEMY_DAMAGE_PER_LEVEL),
				ENEMY_DAMAGE_MAX_MULTIPLIER);
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
