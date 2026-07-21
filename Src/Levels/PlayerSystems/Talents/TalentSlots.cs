using System;
using System.Collections.Generic;
using Terraria;
using ProgressionExpanded.Utils;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Talents
{
	/// <summary>
	/// One of the six fixed talent slots. A slot is a permanent identity — slot 2 is always the
	/// world-evil slot and always offers the same three choices — which is what separates this from
	/// the old mastery loadout, where a "slot" was just a position in a sorted list.
	/// </summary>
	public sealed class TalentSlot
	{
		public string Key { get; init; }
		public int Index { get; init; }
		public string DisplayName { get; init; }

		/// <summary>Shown on the card while the slot is locked, so the player knows what to go kill.</summary>
		public string UnlockHint { get; init; }

		public Func<Player, bool> IsUnlocked { get; init; }
	}

	/// <summary>
	/// The six slots, in progression order.
	///
	/// Unlocks are per-WORLD, not per-character: they read vanilla's own downed-boss flags through
	/// BossKillTracker. That means an existing world needs no migration — a world that has already
	/// downed Skeletron simply has those slots open the moment you load in — and it matches how
	/// Terraria gates everything else (ore, NPCs, recipes). The trade-off is that taking a character
	/// to a fresh world drops them back to the pinnacle slot until the bosses are re-killed.
	///
	/// King Slime and Deerclops are deliberately not gates: both are fully optional and easy to
	/// miss, and a slot the player can permanently skip past is worse than no slot.
	/// </summary>
	public static class TalentSlots
	{
		public const int Count = 6;
		public const int PinnacleLevelRequirement = 5;

		public static readonly TalentSlot[] All =
		{
			new TalentSlot
			{
				Key = "pinnacle",
				Index = 0,
				DisplayName = "Pinnacle",
				UnlockHint = $"Reach level {PinnacleLevelRequirement}",
				IsUnlocked = p => PlayerLevelManager.GetLevel(p) >= PinnacleLevelRequirement,
			},
			new TalentSlot
			{
				Key = "eye",
				Index = 1,
				DisplayName = "Precision & Terror",
				UnlockHint = "Defeat the Eye of Cthulhu",
				IsUnlocked = p => BossKillTracker.DownedEyeOfCthulhu,
			},
			new TalentSlot
			{
				Key = "evil",
				Index = 2,
				DisplayName = "Corruption & Consumption",
				UnlockHint = "Defeat the Eater of Worlds or the Brain of Cthulhu",
				// Vanilla tracks both world evils under one flag, so either boss opens this slot —
				// which is what we want, since a world only ever has one of them.
				IsUnlocked = p => BossKillTracker.DownedEvilBoss,
			},
			new TalentSlot
			{
				Key = "bee",
				Index = 3,
				DisplayName = "Swarm & Venom",
				UnlockHint = "Defeat the Queen Bee",
				IsUnlocked = p => BossKillTracker.DownedQueenBee,
			},
			new TalentSlot
			{
				Key = "skeletron",
				Index = 4,
				DisplayName = "Bone & Curse",
				UnlockHint = "Defeat Skeletron",
				IsUnlocked = p => BossKillTracker.DownedSkeletron,
			},
			new TalentSlot
			{
				Key = "wof",
				Index = 5,
				DisplayName = "Capstone",
				UnlockHint = "Defeat the Wall of Flesh",
				IsUnlocked = p => BossKillTracker.DownedWallOfFlesh,
			},
		};

		public static TalentSlot Get(string key)
		{
			foreach (TalentSlot slot in All)
			{
				if (slot.Key == key)
					return slot;
			}
			return null;
		}

		public static TalentSlot Get(int index)
		{
			if (index < 0 || index >= All.Length)
				return null;
			return All[index];
		}

		public static IEnumerable<TalentSlot> Unlocked(Player player)
		{
			foreach (TalentSlot slot in All)
			{
				if (slot.IsUnlocked(player))
					yield return slot;
			}
		}

		/// <summary>
		/// The number of unlocked <b>boss-gated</b> talent slots (0–5), read from pure world state via
		/// <see cref="BossKillTracker"/> — no <see cref="Player"/> needed. This is the boss difficulty
		/// tier: each of these five slots (Eye of Cthulhu → world evil → Queen Bee → Skeletron → Wall of
		/// Flesh) is a build-defining power spike, so the count tracks how much talent power the world's
		/// characters have. Deliberately excludes the level-5 pinnacle slot (a constant) and the
		/// non-gating King Slime / Deerclops. Consumed by the boss HP/damage curves in
		/// <c>WorldLevelManager</c>. All five gates are pre-hardmode, so this pins at 5 from Wall of
		/// Flesh onward and world level carries the ramp past it.
		/// </summary>
		public static int WorldProgressionTier()
		{
			int tier = 0;
			if (BossKillTracker.DownedEyeOfCthulhu) tier++;
			if (BossKillTracker.DownedEvilBoss) tier++;
			if (BossKillTracker.DownedQueenBee) tier++;
			if (BossKillTracker.DownedSkeletron) tier++;
			if (BossKillTracker.DownedWallOfFlesh) tier++;
			return tier;
		}
	}
}
