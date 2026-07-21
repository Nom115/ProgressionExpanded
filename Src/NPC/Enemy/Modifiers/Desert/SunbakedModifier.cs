using Terraria;
using Terraria.GameContent.Events;
using ProgressionExpanded.Src.NPCs;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Desert
{
	/// <summary>
	/// Sunbaked — drinks the sun. Regenerates hard while it is stood in open daylight, and not at all
	/// otherwise.
	///
	/// <b>This is a race against a heal, and it has four separate off-switches.</b> Unlike Regenerating
	/// (a flat trickle you ignore), the number here is meant to actually out-pace chip damage, so a
	/// Sunbaked enemy is either killed decisively or not at all. Every counter is something the player
	/// already has:
	/// <list type="bullet">
	/// <item>Fight it at <b>night</b>.</item>
	/// <item>Fight it <b>underground</b> — which is where half the desert is anyway.</item>
	/// <item>Fight it during a <b>sandstorm</b>, when the sun is blotted out. The desert's own weather
	/// is the counter to the desert's own affix.</item>
	/// <item>Put a <b>vanilla damage-over-time</b> on it (a flaming weapon, cursed inferno, …).</item>
	/// </list>
	/// Same shape as Juggernaut, where "DoTs switch the whole regen engine off" is the design and not an
	/// oversight. Note the mod's own elemental damage no longer qualifies: since the true-conversion
	/// rework (CLAUDE.md §10) it is instant, not a DoT, so it does not suppress regen — the counters are
	/// the three environmental switches plus a genuine vanilla burn.
	/// </summary>
	public class SunbakedModifier : IModifier
	{
		/// <summary>
		/// HP/s healed in the sun. Chosen to beat casual chip damage and lose to committed damage;
		/// flat and therefore fading across progression, exactly as CLAUDE.md §3 says flat things do.
		/// Untested first guess.
		/// </summary>
		private const int RegenPerSecond = 12;

		public string GetPrefix() => "Sunbaked";

		public void Apply(Terraria.NPC npc) { }

		public void OnHitByPlayer(Terraria.NPC npc, Player player) { }

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage)
		{
			if (!InSunlight(npc))
				return;

			// Vanilla's DoTs each do `if (lifeRegen > 0) lifeRegen = 0;` before subtracting, and all of
			// them run before ANY tModLoader hook (NPCLoader.UpdateLifeRegen sits at the very end of
			// UpdateNPC_BuffApplyDOTs). So by the time we arrive, a negative lifeRegen reliably means
			// "vanilla is already burning this thing" — read it directly and let the burn suppress regen.
			//
			// Note: the mod's own elemental damage no longer counters this. Since the true-conversion
			// rework (CLAUDE.md §10) it is instant, not a damage-over-time, so it leaves no lifeRegen
			// signature to detect. Only a vanilla DoT (a flaming weapon, cursed inferno, …) plus the
			// night / underground / sandstorm switches turn Sunbaked's regen off now.
			if (npc.lifeRegen < 0)
				return;

			// Half-HP-per-second units, hence the x2 (RegenPerSecond is whole, so no accumulator needed).
			npc.lifeRegen += 2 * RegenPerSecond;
		}

		/// <summary>
		/// Open sky, sun up, no sandstorm. IsUnderground is the shared surface line (BiomeDepth), so
		/// this cannot drift from the one the level and orb curves use.
		///
		/// Note an eclipse still counts as daylight: the sun is up, it is merely being eaten. That is
		/// a coin-flip call, kept because an Eclipse is already a hard event and this is a small part
		/// of it.
		/// </summary>
		private static bool InSunlight(Terraria.NPC npc)
		{
			return Main.dayTime
				&& !BiomeDepth.IsUnderground(npc)
				&& !Sandstorm.Happening;
		}

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.10f;

		public int GetSpawnWeight() => 45;

		/// <summary>Desert only, forever — unlike VileSpit/Leech there is no boss that unlocks it.</summary>
		public static bool CanApply(Terraria.NPC npc) => BiomeDepth.IsDesert(npc);
	}
}
