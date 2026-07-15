using Terraria;

namespace ProgressionExpanded.Src.NPCs.Enemy.Elemental
{
	/// <summary>
	/// How much an enemy resists each element. This is the single place a resistance number is
	/// decided, so every consumer agrees on both the sources and the clamp.
	///
	/// Resistance is a fraction: 0 is neutral, positive reduces the damage, and <b>negative
	/// amplifies it</b> — an enemy at -50% takes 150% of the element's damage. Callers multiply by
	/// <c>1 - Get(...)</c>.
	///
	/// <b>Phase 1 returns 0 for everything.</b> The engine is wired through this method so that
	/// filling the sources in is a change to this file alone. Phase 2 adds: rarity tier, rolled
	/// resistance affixes, type resistance derived from <c>npc.buffImmune</c> (vanilla already
	/// curates which enemies shrug off which element, so no hardcoded id table is needed and modded
	/// NPCs work for free), a per-enemy random roll, and Bleed's armour-derived physical
	/// resistance.
	/// </summary>
	public static class ElementalResistance
	{
		/// <summary>
		/// The most an enemy can resist. Deliberately below 1.0: nothing is ever immune, so an
		/// elemental build always does *something* everywhere rather than reading 0 on a bad
		/// matchup. This matters more than usual here because elemental DoT is added on top of the
		/// hit rather than converted out of it — a hard immunity would delete the investment
		/// outright instead of merely blunting it.
		/// </summary>
		public const float MaxResistance = 0.90f;

		/// <summary>
		/// The most an enemy can be amplified by. At -1.0 the element deals double damage; the
		/// floor stops a stack of negative-resistance sources compounding into a one-shot.
		/// </summary>
		public const float MinResistance = -1.00f;

		/// <summary>
		/// This enemy's resistance to <paramref name="element"/>, after
		/// <paramref name="penetration"/> and after clamping.
		///
		/// Penetration is subtracted from the total before the clamp, so it can push a resistant
		/// enemy down toward neutral and past it — that is the whole point of the stat. It is
		/// applied here rather than by the caller so that the clamp always gets the last word.
		/// </summary>
		public static float Get(NPC npc, DamageElement element, float penetration)
		{
			// Phase 2 sums the sources into this. Bleed will branch here to its armour-derived
			// physical resistance rather than reading an elemental one.
			float resistance = 0f;

			return Clamp(resistance - penetration);
		}

		/// <summary>Clamp a raw resistance total into the legal range.</summary>
		public static float Clamp(float resistance)
		{
			if (resistance > MaxResistance)
				return MaxResistance;
			if (resistance < MinResistance)
				return MinResistance;
			return resistance;
		}
	}
}
