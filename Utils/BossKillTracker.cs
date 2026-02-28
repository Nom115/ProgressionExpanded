using Terraria;

namespace ProgressionExpanded.Utils
{
	/// <summary>
	/// Tracks boss kills for progression gating
	/// </summary>
	public static class BossKillTracker
	{
		// Boss kill tracking for pre-hardmode bosses
		private static bool downedKingSlime = false;
		private static bool downedEyeOfCthulhu = false;
		private static bool downedEvilBoss = false; // Eater of Worlds or Brain of Cthulhu
		private static bool downedQueenBee = false;
		private static bool downedSkeletron = false;
		private static bool downedDeerclops = false;
		private static bool downedWallOfFlesh = false;

		/// <summary>
		/// Update boss kill tracking from world state
		/// </summary>
		public static void UpdateBossTracking()
		{
			downedKingSlime = NPC.downedSlimeKing;
			downedEyeOfCthulhu = NPC.downedBoss1;
			downedEvilBoss = NPC.downedBoss2;
			downedQueenBee = NPC.downedQueenBee;
			downedSkeletron = NPC.downedBoss3;
			downedDeerclops = NPC.downedDeerclops;
			downedWallOfFlesh = Main.hardMode;
		}

		/// <summary>
		/// Get the number of pre-hardmode bosses defeated
		/// </summary>
		public static int GetBossesDefeated()
		{
			UpdateBossTracking();
			int count = 0;
			if (downedKingSlime) count++;
			if (downedEyeOfCthulhu) count++;
			if (downedEvilBoss) count++;
			if (downedQueenBee) count++;
			if (downedSkeletron) count++;
			if (downedDeerclops) count++;
			if (downedWallOfFlesh) count++;
			return count;
		}

		// Boss kill accessors
		public static bool DownedKingSlime
		{
			get { UpdateBossTracking(); return downedKingSlime; }
		}

		public static bool DownedEyeOfCthulhu
		{
			get { UpdateBossTracking(); return downedEyeOfCthulhu; }
		}

		public static bool DownedEvilBoss
		{
			get { UpdateBossTracking(); return downedEvilBoss; }
		}

		public static bool DownedQueenBee
		{
			get { UpdateBossTracking(); return downedQueenBee; }
		}

		public static bool DownedSkeletron
		{
			get { UpdateBossTracking(); return downedSkeletron; }
		}

		public static bool DownedDeerclops
		{
			get { UpdateBossTracking(); return downedDeerclops; }
		}

		public static bool DownedWallOfFlesh
		{
			get { UpdateBossTracking(); return downedWallOfFlesh; }
		}
	}
}
