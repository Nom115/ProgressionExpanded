using Terraria;
using Terraria.ID;
using ProgressionExpanded.Src.NPCs;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Desert
{
	/// <summary>
	/// Infested — something was living in it. Bursts into antlion larvae on death.
	/// </summary>
	public class InfestedModifier : IModifier
	{
		private const int MinLarvae = 2;
		private const int MaxLarvae = 3; // inclusive

		public string GetPrefix() => "Infested";

		public void Apply(Terraria.NPC npc) { }

		public void OnHitByPlayer(Terraria.NPC npc, Player player) { }

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc)
		{
			// Spawning is server-authoritative; NewNPC on a client desyncs.
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;

			// ⚠️ Worm guard, and it is not optional. Desert worms (Dune Splicer, Tomb Crawler) are made
			// of ~20 NPCs that die together, and every one of them reaches NPCLoader.OnKill through its
			// own checkDead -> NPCLoot (NPC.cs:84811). Unguarded, one Infested Dune Splicer would burst
			// for 3 larvae PER SEGMENT — sixty of them, from one kill.
			//
			// A worm head sets realLife to its own whoAmI and stamps the same value on every segment
			// (NPC.cs:52875/52888); a standalone NPC leaves it at -1 (NPC.cs:629). So this reads as
			// "am I the real entity, or just a piece of one".
			if (npc.realLife != -1 && npc.realLife != npc.whoAmI)
				return;

			int count = Main.rand.Next(MinLarvae, MaxLarvae + 1);
			for (int i = 0; i < count; i++)
			{
				NPC.NewNPC(npc.GetSource_Death(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.LarvaeAntlion);
			}
		}

		public float GetXPBonus() => 0.0f;

		public int GetSpawnWeight() => 55;

		/// <summary>
		/// Desert only, forever — unlike VileSpit/Leech there is no boss that unlocks it.
		///
		/// ⚠️ <b>The larva exclusion is what stops this being a chain reaction.</b> The larvae we spawn
		/// are ordinary NPCs: they run OnSpawn, roll their own rarity, and roll from this same pool
		/// while still stood in the desert. An Infested larva bursting into more Infested larvae is an
		/// exponential spawn loop off a single kill, and nothing downstream would stop it. Excluding
		/// the type we spawn makes the loop unrepresentable rather than merely unlikely — same instinct
		/// as the int seed in CLAUDE.md §10.
		///
		/// This does mean adding a second larva type here needs the same exclusion. If a third ever
		/// appears, this wants to become a set rather than a comparison.
		/// </summary>
		public static bool CanApply(Terraria.NPC npc)
		{
			return npc.type != NPCID.LarvaeAntlion && BiomeDepth.IsDesert(npc);
		}
	}
}
