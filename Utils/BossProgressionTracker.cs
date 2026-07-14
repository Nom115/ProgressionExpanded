using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Utils
{
	/// <summary>
	/// Tracks which bosses have been defeated in the current world, keyed by a stable
	/// identifier that works for BOTH vanilla and modded bosses (no hardcoded NPC ids).
	///
	/// This drives the "pinnacle" rule: the very first time a boss is killed it is left
	/// completely untouched (vanilla health, no rarity, no affix modifiers, and its level
	/// has no bearing on the damage it deals or takes). Every kill after the first gets the
	/// full level-scaling + modifier treatment.
	///
	/// State is persisted per-world via <see cref="WorldDataManager"/> (a bool flag per key),
	/// so it survives save/reload automatically.
	/// </summary>
	public static class BossProgressionTracker
	{
		private const string KeyPrefix = "boss_defeated:";

		/// <summary>
		/// Stable identity for a boss NPC. Modded NPCs expose a persistent
		/// "ModName/InternalName" (<see cref="ModNPC.FullName"/>); vanilla NPCs use their
		/// (stable) net id. This is what makes the whole system mod-agnostic.
		/// </summary>
		public static string GetBossKey(NPC npc)
		{
			if (npc.ModNPC != null)
				return npc.ModNPC.FullName;

			return "Terraria/" + npc.netID;
		}

		/// <summary>
		/// Whether this NPC should be treated as a boss for progression purposes.
		/// Covers vanilla/modded bosses (<see cref="NPC.boss"/>) plus anything a mod flags
		/// via <see cref="NPCID.Sets.ShouldBeCountedAsBoss"/> (e.g. event minibosses).
		/// </summary>
		public static bool IsTrackedBoss(NPC npc)
		{
			return npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type];
		}

		/// <summary>
		/// True if this boss has already been defeated at least once in this world.
		/// </summary>
		public static bool HasDefeated(NPC npc)
		{
			return WorldDataManager.GetBool(KeyPrefix + GetBossKey(npc), false);
		}

		/// <summary>
		/// True if this is a boss we have NOT yet defeated in this world — the untouched
		/// "pinnacle" first encounter. Non-bosses always return false.
		/// </summary>
		public static bool IsPinnacleEncounter(NPC npc)
		{
			return IsTrackedBoss(npc) && !HasDefeated(npc);
		}

		/// <summary>
		/// Record that a boss has been defeated. Safe to call for any NPC — non-bosses
		/// are ignored. Idempotent.
		/// </summary>
		public static void RecordDefeat(NPC npc)
		{
			if (!IsTrackedBoss(npc))
				return;

			WorldDataManager.SetBool(KeyPrefix + GetBossKey(npc), true);
		}
	}

	/// <summary>
	/// Records boss defeats on kill. Being a <see cref="GlobalNPC"/> with no filtering, this
	/// fires for every NPC in the game — including those added by other mods — so the pinnacle
	/// system is fully modular with no per-boss registration.
	/// </summary>
	public class BossDefeatRecorder : GlobalNPC
	{
		public override void OnKill(NPC npc)
		{
			BossProgressionTracker.RecordDefeat(npc);
		}
	}
}
