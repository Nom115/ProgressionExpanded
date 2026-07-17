using Microsoft.Xna.Framework;
using Terraria;
using ProgressionExpanded.Src.NPCs;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Desert
{
	/// <summary>
	/// Parching — the desert takes your water. Each contact hit burns mana and pushes back the moment
	/// your mana starts coming back.
	///
	/// <b>It attacks a resource, not your life, and that is the point.</b> Every other on-hit affix in
	/// the pool answers to defense and life; this one routes around both, so it is a threat to builds
	/// that have solved raw damage. Its opposite number is Stagger, whose whole thesis (CLAUDE.md §6)
	/// is that mana *is* its health bar: 55% of every hit is paid out of the pool, and at
	/// <c>statMana == 0</c> the split disarms entirely and hits land whole. So a Parching enemy is
	/// specifically the thing that turns Stagger's mitigation off — a real counter to a pinnacle pick,
	/// contained inside one biome and legible in the enemy's name.
	///
	/// ⚠️ Because of that, this is the desert affix most likely to feel unfair, and the number to move
	/// first if it does. <see cref="ManaBurnPerHit"/> is a first guess.
	///
	/// Deliberately no effect at zero mana: a warrior who spends nothing on Intellect and takes no
	/// mana-based talent should read "Parching" and correctly conclude it is a free prefix. An affix
	/// that punished everyone equally would just be another damage stat.
	/// </summary>
	public class ParchingModifier : IModifier
	{
		/// <summary>Mana destroyed per contact hit. Flat, so it fades late — see the class note.</summary>
		private const int ManaBurnPerHit = 40;

		/// <summary>
		/// Ticks of mana-regen delay stacked on the hit. Vanilla's own post-hit delay is not applied to
		/// NPC contact damage at all, so without this the pool simply refills; ~1.5s is long enough to
		/// matter without pinning regen off under sustained contact.
		/// </summary>
		private const int RegenDelayTicks = 90;

		public string GetPrefix() => "Parching";

		public void Apply(Terraria.NPC npc) { }

		public void OnHitByPlayer(Terraria.NPC npc, Player player) { }

		public void OnHitPlayer(Terraria.NPC npc, Player player, Player.HurtInfo hurtInfo)
		{
			if (player.statMana <= 0)
				return;

			int burned = System.Math.Min(ManaBurnPerHit, player.statMana);
			player.statMana -= burned;

			// Math.Max, never assignment: a delay already owed from casting must not be shortened by
			// being hit. Same rule as StaggerTalent.PostHurt (CLAUDE.md §6).
			player.manaRegenDelay = System.Math.Max(player.manaRegenDelay, RegenDelayTicks);

			// The burn is invisible otherwise — the mana bar just moves. Say it out loud, because a
			// player whose Stagger split silently stopped working deserves to know why.
			CombatText.NewText(
				new Rectangle((int)player.position.X, (int)player.position.Y, player.width, player.height),
				CombatText.HealMana, burned, false, true);
		}

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.05f;

		public int GetSpawnWeight() => 50;

		/// <summary>Desert only, forever — unlike VileSpit/Leech there is no boss that unlocks it.</summary>
		public static bool CanApply(Terraria.NPC npc) => BiomeDepth.IsDesert(npc);
	}
}
