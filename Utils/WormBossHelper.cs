using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace ProgressionExpanded.Utils
{
	/// <summary>
	/// Single source of truth for multi-segment worm-boss identity. A worm boss (Eater of Worlds,
	/// The Destroyer, most modded worms) is many separate NPCs — a Head plus a chain of Body/Tail
	/// segments. The mod's per-NPC systems (rarity/modifiers, level) would otherwise roll each
	/// segment independently, producing a chaotic body where every piece differs. This helper lets
	/// them treat the whole worm as one entity: the <b>Head is authoritative and rolls; segments
	/// copy it</b>.
	///
	/// <para>Linkage primitives:</para>
	/// <list type="bullet">
	/// <item>Standard (non-splitting) worms stamp <see cref="NPC.realLife"/> on every segment with the
	/// head's <c>whoAmI</c>, and the head self-references (<c>realLife == whoAmI</c>). A standalone
	/// NPC leaves it at <c>-1</c>.</item>
	/// <item>The Eater of Worlds <i>splits</i>, so its segments keep <c>realLife == -1</c> forever and
	/// must be recognised by NPC id instead (<see cref="ResolveHead"/> finds the nearest head).</item>
	/// </list>
	///
	/// <para>⚠️ <c>realLife</c> is stamped by the head's AI, <b>not</b> at <c>OnSpawn</c> — so callers
	/// must resolve worm identity from <c>PostAI</c> (after vanilla AI) or later, never from
	/// <c>OnSpawn</c>.</para>
	/// </summary>
	public static class WormBossHelper
	{
		/// <summary>
		/// True if this NPC is a worm <b>head</b> — the piece that rolls its own rarity/modifiers/level
		/// and that segments copy. Standalone enemies and segments return false.
		/// </summary>
		public static bool IsHead(NPC npc)
		{
			if (npc.type == NPCID.EaterofWorldsHead)
				return true;

			// Standard worm heads set realLife to their own whoAmI. A standalone NPC has realLife == -1.
			return npc.realLife != -1 && npc.realLife == npc.whoAmI;
		}

		/// <summary>
		/// True if this NPC is a worm <b>segment</b> (Body/Tail) — the pieces that copy their head.
		/// </summary>
		public static bool IsSegment(NPC npc)
		{
			if (npc.type == NPCID.EaterofWorldsBody || npc.type == NPCID.EaterofWorldsTail)
				return true;

			return npc.realLife != -1 && npc.realLife != npc.whoAmI;
		}

		/// <summary>
		/// The head NPC that this NPC's roll should be sourced from, or <c>null</c> if this NPC is a
		/// head or a standalone enemy (i.e. it rolls for itself). Used both for copying the head's roll
		/// and, via <see cref="BossProgressionTracker"/>, for routing a segment's boss identity to its
		/// head so the whole worm shares one pinnacle/defeat state.
		/// </summary>
		public static NPC ResolveHead(NPC npc)
		{
			// Standard worm segment → its stamped head.
			if (npc.realLife != -1 && npc.realLife != npc.whoAmI)
			{
				NPC head = Main.npc[npc.realLife];
				return head != null && head.active ? head : null;
			}

			// Eater of Worlds splits, so its segments carry realLife == -1. Fall back to the nearest
			// active head — unambiguous at initial summon (one head, adjacent), which is when the
			// one-time roll/harmonisation actually happens.
			if (npc.type == NPCID.EaterofWorldsBody || npc.type == NPCID.EaterofWorldsTail)
				return NearestEaterOfWorldsHead(npc);

			return null;
		}

		private static NPC NearestEaterOfWorldsHead(NPC npc)
		{
			NPC nearest = null;
			float bestDistSq = float.MaxValue;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC other = Main.npc[i];
				if (other == null || !other.active || other.type != NPCID.EaterofWorldsHead)
					continue;

				float distSq = Vector2.DistanceSquared(npc.Center, other.Center);
				if (distSq < bestDistSq)
				{
					bestDistSq = distSq;
					nearest = other;
				}
			}

			return nearest;
		}
	}
}
