using Microsoft.Xna.Framework;
using Terraria;

namespace ProgressionExpanded.Src.NPCs
{
	/// <summary>
	/// Shared "where is this NPC" queries — how far below the surface line it sits, and what biome
	/// the local player is standing in.
	///
	/// Two systems key off these: <see cref="NPCLevelManager"/> biases enemy level upward with depth
	/// and danger biomes, and <see cref="ProgressionExpanded.Items.Drops.OrbDropSystem"/> keys the
	/// bulk crafting-orb drop rates off the same axes. They live here so the definition of "the
	/// surface line" cannot drift between the two.
	/// </summary>
	public static class BiomeDepth
	{
		/// <summary>Search radius (px) for the nearest player whose biome flags we read.</summary>
		public const float BiomeRange = 2000f;

		/// <summary>
		/// 0 at or above the surface line, ramping linearly to 1 near the underworld. Drives the
		/// "deeper = higher level" bias.
		/// </summary>
		public static float GetDepthFraction(Terraria.NPC npc)
		{
			float tileY = npc.Center.Y / 16f;
			float surface = (float)Main.worldSurface;
			float bottom = Main.maxTilesY - 200f; // start of the underworld; deepest meaningful layer
			if (bottom <= surface) return 0f;      // guard against tiny / degenerate worlds
			return System.Math.Clamp((tileY - surface) / (bottom - surface), 0f, 1f);
		}

		/// <summary>
		/// True anywhere below the surface line — dirt layer, caverns and the underworld all count.
		/// Defined off <see cref="GetDepthFraction"/> so there is exactly one surface line.
		/// </summary>
		public static bool IsUnderground(Terraria.NPC npc) => GetDepthFraction(npc) > 0f;

		/// <summary>
		/// True when the nearest player within <see cref="BiomeRange"/> is standing in Corruption or
		/// Crimson. Biome is read off the player because that is the only cheap Zone* source; an NPC
		/// with no player near it returns false.
		/// </summary>
		public static bool IsEvilBiome(Terraria.NPC npc)
		{
			Terraria.Player player = NearestPlayer(npc, BiomeRange);
			return player != null && (player.ZoneCorrupt || player.ZoneCrimson);
		}

		/// <summary>Nearest active, living player to the NPC within maxRange px, or null.</summary>
		public static Terraria.Player NearestPlayer(Terraria.NPC npc, float maxRange)
		{
			Terraria.Player nearest = null;
			float nearestDistSq = maxRange * maxRange;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Terraria.Player p = Main.player[i];
				if (p == null || !p.active || p.dead) continue;
				float distSq = Vector2.DistanceSquared(p.Center, npc.Center);
				if (distSq <= nearestDistSq)
				{
					nearestDistSq = distSq;
					nearest = p;
				}
			}
			return nearest;
		}
	}
}
