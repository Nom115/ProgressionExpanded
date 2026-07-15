using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ProgressionExpanded.Items.Materials;
using ProgressionExpanded.Items.Orbs;
using ProgressionExpanded.Src.Levels.WorldLevel;
using ProgressionExpanded.Src.NPCs;
using ProgressionExpanded.Utils;

namespace ProgressionExpanded.Items.Drops
{
	/// <summary>
	/// Drops crafting orbs from slain enemies, progression-gated so the rarity ceiling opens up over
	/// a playthrough: the Legendary-tier orb (Cataclysm) only drops from mid-hardmode onward.
	/// Mirrors <c>EnemyXPRewards.OnKill</c>.
	///
	/// The two bulk orbs — Transmutation (Normal→Uncommon) and Alteration (Uncommon→Magic) — are
	/// spent on *every* item, so their supply is tied to a farming hotspot rather than left flat:
	/// Transmutation pours in the Corruption/Crimson, Alteration pours underground, and both ramp
	/// with world level. See <see cref="HotspotMultiplier"/> for why that ramp has to exist.
	/// </summary>
	public class OrbDropSystem : GlobalNPC
	{
		// --- Farming-hotspot curve (see HotspotMultiplier). Untested first guesses. ---
		private const int HOTSPOT_MIN_WORLD_LEVEL = 5;    // below this the world has no hotspots at all
		private const float HOTSPOT_AT_THRESHOLD = 3.0f;  // multiplier the moment it switches on
		private const float HOTSPOT_PER_LEVEL = 0.15f;    // growth per world level past the threshold
		private const float HOTSPOT_MAX = 12.0f;          // cap — reached at world level 65

		// Bosses are rare, so they pour out far more orbs per kill.
		private const float BOSS_MULTIPLIER = 25f;

		public override void OnKill(NPC npc)
		{
			// Drops are authoritative on the server / single-player (MP client sync is out of scope).
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;

			if (npc.friendly || npc.townNPC || npc.SpawnedFromStatue || npc.lifeMax <= 5 || npc.CountsAsACritter)
				return;

			float bossMult = npc.boss ? BOSS_MULTIPLIER : 1f;

			int bosses = BossKillTracker.GetBossesDefeated();

			// One curve, two axes. An enemy killed underground *in* the Corruption satisfies both, so
			// that spot rains Transmutation and Alteration at once — the intended jackpot.
			float hotspot = HotspotMultiplier();
			float evilMult = BiomeDepth.IsEvilBiome(npc) ? hotspot : 1f;
			float depthMult = BiomeDepth.IsUnderground(npc) ? hotspot : 1f;

			// (orb, gate, base per-kill chance, multiplier)
			TryDrop<OrbOfTransmutation>(npc, true, 0.020f, bossMult * evilMult);
			TryDrop<OrbOfAlteration>(npc, true, 0.012f, bossMult * depthMult);
			TryDrop<VerdantParadox>(npc, true, 0.015f, bossMult);
			TryDrop<OrbOfAugmentation>(npc, bosses >= 1, 0.008f, bossMult);
			TryDrop<RegalOrb>(npc, bosses >= 3, 0.006f, bossMult);
			TryDrop<OrbOfAnnulment>(npc, Main.hardMode, 0.008f, bossMult);
			TryDrop<OrbOfAscendance>(npc, Main.hardMode, 0.005f, bossMult);
			TryDrop<DivineOrb>(npc, NPC.downedMechBossAny, 0.004f, bossMult);
			TryDrop<OrbOfCataclysm>(npc, NPC.downedMechBossAny, 0.0025f, bossMult);
		}

		/// <summary>
		/// Flat 1x until world level <see cref="HOTSPOT_MIN_WORLD_LEVEL"/>, then jumps straight to
		/// <see cref="HOTSPOT_AT_THRESHOLD"/> and grows linearly to <see cref="HOTSPOT_MAX"/>.
		///
		/// The ramp is not generosity — it tracks demand. <c>OrbActions</c> pins an item's level to the
		/// world level *at craft time*, and world level ratchets up with every player level. So the gear
		/// you crafted is left behind by the world it was crafted in, and staying current means
		/// re-rolling bases you already rolled. Orb demand therefore grows with world level, and a flat
		/// drop rate silently becomes a shrinking one.
		///
		/// The threshold exists so the curve cannot be farmed before the world has any depth to it;
		/// the cap exists so a level-100 world does not trivialise the currency entirely.
		/// </summary>
		private static float HotspotMultiplier()
		{
			int worldLevel = WorldLevelManager.GetWorldLevel();
			if (worldLevel < HOTSPOT_MIN_WORLD_LEVEL)
				return 1f;

			float ramped = HOTSPOT_AT_THRESHOLD + (worldLevel - HOTSPOT_MIN_WORLD_LEVEL) * HOTSPOT_PER_LEVEL;
			return Math.Min(ramped, HOTSPOT_MAX);
		}

		/// <summary>
		/// Rolls a drop as an EXPECTED COUNT, not a chance.
		///
		/// <c>chance * mult</c> routinely exceeds 1 now — a boss in deep Corruption at high world level
		/// reaches ~4.9 — and the old single <c>NextFloat() &lt; chance</c> test saturated there: every
		/// multiple above 1.0 was silently thrown away and paid out as one orb. The whole part now drops
		/// outright and only the fraction is rolled, so the multipliers above mean what they say.
		/// </summary>
		private static void TryDrop<T>(NPC npc, bool gate, float chance, float mult) where T : ModItem
		{
			if (!gate)
				return;

			float expected = chance * mult;
			int count = (int)expected;
			if (Main.rand.NextFloat() < expected - count)
				count++;

			if (count > 0)
				Item.NewItem(npc.GetSource_Loot(), npc.getRect(), ModContent.ItemType<T>(), count);
		}
	}
}
