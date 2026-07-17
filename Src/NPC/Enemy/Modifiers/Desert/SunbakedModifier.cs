using Terraria;
using Terraria.GameContent.Events;
using ProgressionExpanded.Src.NPCs;
using ProgressionExpanded.Src.NPCs.Enemy.Elemental;

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
	/// <item>Put <b>any damage-over-time</b> on it — vanilla or the mod's elemental masteries.</item>
	/// </list>
	/// That last one is the useful one: it hands the elemental system (CLAUDE.md §10) a job that raw
	/// DPS cannot do, so a single point in any of the five masteries is the answer rather than a
	/// bigger weapon. Same shape as Juggernaut, where "DoTs switch the whole regen engine off" is the
	/// design and not an oversight.
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

			// ⚠️ Both guards are load-bearing, and they are two guards because they answer two
			// different orderings.
			//
			// Vanilla's DoTs each do `if (lifeRegen > 0) lifeRegen = 0;` before subtracting, and all
			// of them run before ANY tModLoader hook (NPCLoader.UpdateLifeRegen sits at the very end
			// of UpdateNPC_BuffApplyDOTs, NPC.cs:109271). So by the time we arrive, a negative
			// lifeRegen reliably means "vanilla is already burning this thing" — read it directly.
			if (npc.lifeRegen < 0)
				return;

			// Our own elemental DoTs are the other case, and lifeRegen CANNOT be used for them: they
			// land in ElementalDotNPC's UpdateLifeRegen, a sibling GlobalNPC whose hook order against
			// ours is a load-order accident. Ask its state instead — stable at any point in the frame.
			if (npc.TryGetGlobalNPC(out ElementalDotNPC dots) && dots.HasAnyInstance())
				return;

			// Half-HP-per-second units, hence the x2. See ElementalDotNPC.regenAccumulator; no
			// accumulator needed here because RegenPerSecond is a whole number.
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
