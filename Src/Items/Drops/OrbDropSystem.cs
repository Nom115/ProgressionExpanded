using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ProgressionExpanded.Items.Materials;
using ProgressionExpanded.Items.Orbs;
using ProgressionExpanded.Utils;

namespace ProgressionExpanded.Items.Drops
{
	/// <summary>
	/// Drops crafting orbs from slain enemies, progression-gated so the rarity ceiling opens up over
	/// a playthrough: the Legendary-tier orb (Cataclysm) only drops from mid-hardmode onward.
	/// Mirrors <c>EnemyXPRewards.OnKill</c>.
	/// </summary>
	public class OrbDropSystem : GlobalNPC
	{
		public override void OnKill(NPC npc)
		{
			// Drops are authoritative on the server / single-player (MP client sync is out of scope).
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;

			if (npc.friendly || npc.townNPC || npc.SpawnedFromStatue || npc.lifeMax <= 5 || npc.CountsAsACritter)
				return;

			// Bosses are rare, so they pour out far more orbs per kill.
			float bossMult = npc.boss ? 25f : 1f;

			int bosses = BossKillTracker.GetBossesDefeated();

			// (orb, gate, base per-kill chance)
			TryDrop<OrbOfTransmutation>(npc, true, 0.020f, bossMult);
			TryDrop<OrbOfAlteration>(npc, true, 0.012f, bossMult);
			TryDrop<VerdantParadox>(npc, true, 0.015f, bossMult);
			TryDrop<OrbOfAugmentation>(npc, bosses >= 1, 0.008f, bossMult);
			TryDrop<RegalOrb>(npc, bosses >= 3, 0.006f, bossMult);
			TryDrop<OrbOfAnnulment>(npc, Main.hardMode, 0.008f, bossMult);
			TryDrop<OrbOfAscendance>(npc, Main.hardMode, 0.005f, bossMult);
			TryDrop<DivineOrb>(npc, NPC.downedMechBossAny, 0.004f, bossMult);
			TryDrop<OrbOfCataclysm>(npc, NPC.downedMechBossAny, 0.0025f, bossMult);
		}

		private static void TryDrop<T>(NPC npc, bool gate, float chance, float mult) where T : ModItem
		{
			if (!gate)
				return;

			if (Main.rand.NextFloat() < chance * mult)
				Item.NewItem(npc.GetSource_Loot(), npc.getRect(), ModContent.ItemType<T>());
		}
	}
}
