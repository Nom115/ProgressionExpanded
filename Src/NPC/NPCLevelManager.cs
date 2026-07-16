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

		// Per-NPC stat scaling, applied to the LEVEL OFFSET (deviation from the world baseline),
		// never to the absolute level — see LevelOffset() for why that distinction is load-bearing.
		private const float HEALTH_PER_LEVEL_OFFSET = 0.05f;   // +5% health per level above the baseline
		private const float DEFENSE_PER_LEVEL_OFFSET = 0.02f;  // +2% defense per level above the baseline

		// Level-difference COMBAT scaling.
		// Player -> enemy: you deal extra damage to enemies below your level (gentle ramp).
		private const float PLAYER_DAMAGE_PER_LEVEL_BELOW = 0.10f; // +10% per level the enemy is below you
		private const float PLAYER_DAMAGE_MAX_MULTIPLIER = 3.0f;   // cap (reached ~20 levels below)
		// Enemy -> player: see GetEnemyDamageMultiplier. This is in practice a DEPTH tax, not a
		// level-difference mechanic, so it is a flavour signal rather than a wall. Was 0.20f/5.0f.
		private const float ENEMY_DAMAGE_PER_LEVEL_ABOVE = 0.05f;  // +5% per level the enemy is above you
		private const float ENEMY_DAMAGE_MAX_MULTIPLIER = 1.5f;    // cap; see below for when it binds

		// --- Level-assignment range (squished to world level ±MAX_LEVEL_OFFSET, biased upward by
		// depth & danger biome; overworld hugs the world level). All tunable. ---
		private const int MAX_LEVEL_OFFSET = 6;        // hard clamp: enemies stay within ±6 of world level
		private const int SURFACE_SPREAD = 2;          // random ± spread on top of the depth/biome bias
		private const float DEPTH_WEIGHT = 1.0f;       // how strongly depth pushes level up (share of +6)
		private const float BIOME_BONUS_RANGE = 2000f; // search radius for the nearest player's biome
		private const float DUNGEON_BONUS = 0.6f;      // upward bias in the Dungeon
		private const float EVIL_BONUS = 0.35f;        // Corruption / Crimson
		private const float JUNGLE_BONUS = 0.35f;      // Jungle

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
		/// Squished to world level ±MAX_LEVEL_OFFSET; overworld hugs world level, depth & danger biomes bias upward.
		/// </summary>
		private void InitializeLevel(Terraria.NPC npc)
		{
			if (levelInitialized) return;

			int worldLevel = WorldLevelManager.GetWorldLevel();

			// Overworld hugs the world level; depth and danger biomes bias the offset upward so deep
			// caverns / the Dungeon / evil / Jungle approach +MAX_LEVEL_OFFSET. Clamped to ±6.
			float depthFrac = BiomeDepth.GetDepthFraction(npc);
			float biomeBonus = GetBiomeBonus(npc);
			float upwardBias = System.Math.Clamp(depthFrac * DEPTH_WEIGHT + biomeBonus, 0f, 1f);

			int center = (int)System.Math.Round(upwardBias * MAX_LEVEL_OFFSET);
			int spread = Main.rand.Next(-SURFACE_SPREAD, SURFACE_SPREAD + 1);
			int offset = System.Math.Clamp(center + spread, -MAX_LEVEL_OFFSET, MAX_LEVEL_OFFSET);

			int finalLevel = System.Math.Max(1, worldLevel + offset);
			SetLevel(npc, finalLevel);
		}

		/// <summary>
		/// Upward level bias from the local biome, read from the nearest active player (reusing the
		/// mod's existing "nearest player within range → read Zone*" idiom). Returns the strongest
		/// applicable bonus, or 0 for open ground / no player near.
		/// </summary>
		private static float GetBiomeBonus(Terraria.NPC npc)
		{
			Terraria.Player player = BiomeDepth.NearestPlayer(npc, BIOME_BONUS_RANGE);
			if (player == null) return 0f;

			float bonus = 0f;
			if (player.ZoneDungeon) bonus = System.Math.Max(bonus, DUNGEON_BONUS);
			if (player.ZoneCorrupt || player.ZoneCrimson) bonus = System.Math.Max(bonus, EVIL_BONUS);
			if (player.ZoneJungle) bonus = System.Math.Max(bonus, JUNGLE_BONUS);
			return bonus;
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
		/// Apply stat scaling based on NPC level.
		///
		/// ⚠️ <b>The world level must be charged exactly ONCE here.</b> The World*Multiplier values
		/// below are already functions of world level; the per-NPC term must therefore describe only
		/// how far <i>this</i> enemy deviates from the world baseline, never its absolute level.
		/// See <see cref="LevelOffset"/>.
		/// </summary>
		private void ApplyLevelScaling(Terraria.NPC npc)
		{
			if (npc.lifeMax <= 5 || npc.friendly) return; // Skip townspeople and critters

			// Pinnacle boss (first-ever kill): leave its health completely untouched.
			if (BossProgressionTracker.IsPinnacleEncounter(npc)) return;

			// Get world level scaling multipliers
			float healthMultiplier = WorldLevelManager.GetEnemyHealthMultiplier();
			float damageMultiplier = WorldLevelManager.GetEnemyDamageMultiplier();

			int levelOffset = LevelOffset();

			// Health scales with the offset (5% per level of deviation from the world baseline).
			float levelHealthMultiplier = 1.0f + (levelOffset * HEALTH_PER_LEVEL_OFFSET);
			npc.lifeMax = (int)(npc.lifeMax * healthMultiplier * levelHealthMultiplier);
			npc.life = npc.lifeMax;

			// Damage takes the world multiplier and NOTHING else.
			//
			// ⚠️ There is deliberately no per-NPC damage term here, and adding one back is a bug.
			// It would double-count the offset: GetEnemyDamageMultiplier(npcLevel, playerLevel) in
			// ModifyHitPlayer already scales damage by exactly that offset (playerLevel tracks
			// worldLevel — see the comment there), so a term here would charge it a second time.
			// Health and defense have no such counterpart, which is why they keep theirs.
			npc.damage = (int)(npc.damage * damageMultiplier);

			// Defense scales with the offset (2% per level of deviation).
			float levelDefenseMultiplier = 1.0f + (levelOffset * DEFENSE_PER_LEVEL_OFFSET);
			npc.defense = (int)(npc.defense * levelDefenseMultiplier);
		}

		/// <summary>
		/// How far this enemy deviates from the world baseline — i.e. the depth/biome offset that
		/// <see cref="InitializeLevel"/> rolled, in <c>[-MAX_LEVEL_OFFSET, +MAX_LEVEL_OFFSET]</c>.
		///
		/// ⚠️ <b>This used to be <c>npcLevel - 1</c>, and that was a live balance bug.</b>
		/// <c>npcLevel = worldLevel + offset</c>, so <c>npcLevel - 1</c> is dominated by the world
		/// level — which <see cref="ApplyLevelScaling"/> has *already* applied via the
		/// World*Multiplier values. The world level was therefore charged twice, as a product:
		/// ×1.5 inflation at WL8, ×2.0 at WL20, ×5.2 at WL100, uncapped and compounding. Reading the
		/// offset instead charges it once and gives the term the meaning it was always supposed to
		/// have. Model: <c>.scripts/enemy_damage_scaling.py</c>; write-up: <c>todo.md</c> §3a.
		///
		/// Negative for enemies below the world baseline (surface spread), which is intended — a
		/// level-<c>WL-2</c> enemy should be slightly weaker than the baseline, not merely less strong
		/// than a deep one.
		/// </summary>
		private int LevelOffset()
		{
			return npcLevel - WorldLevelManager.GetWorldLevel();
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
		///
		/// ⚠️ <b>Despite the name, this is a DEPTH tax, not a level-difference mechanic — know that
		/// before tuning it.</b> <c>PlayerLevelManager.OnLevelUp</c> calls
		/// <c>WorldLevelManager.IncreaseWorldLevel(1)</c> on every level-up, so <c>playerLevel</c>
		/// and <c>worldLevel</c> track each other by construction. <c>InitializeLevel</c> then sets
		/// <c>enemyLevel = worldLevel + offset</c>. The subtraction below therefore cancels the world
		/// level out entirely and reduces to <b>the depth/biome offset alone</b> — which is biased
		/// upward and never downward on average. So this is "you are in a cavern", not "you punched
		/// above your weight", and the player's counterpart
		/// (<see cref="GetPlayerDamageMultiplier"/>) can essentially never fire underground because
		/// enemies are essentially never below the baseline down there.
		///
		/// That is why the rate is 5%/level and not the 20% it was: at the ±6 clamp it is a ×1.30
		/// flavour signal rather than the ×2.20 wall it used to be. Fixing the *asymmetry* properly
		/// means deciding what this multiplier is for — see <c>todo.md</c> §3a.
		///
		/// ⚠️ <b>The cap is not dead code, even though the ±6 clamp puts the natural maximum at
		/// ×1.30.</b> World level is per-world and append-only, so a second character levelling in
		/// the same world pushes <c>worldLevel</c> above that character's <c>playerLevel</c> and the
		/// difference stops being bounded by the clamp. The cap is the backstop for that.
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
