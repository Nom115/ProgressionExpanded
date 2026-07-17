using Terraria;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers
{
	/// <summary>
	/// Interface for enemy modifiers
	/// Modifiers enhance enemies with special abilities and stat changes
	/// </summary>
	public interface IModifier
	{
		/// <summary>
		/// Get the display prefix for this modifier (e.g., "Swift", "Blazing")
		/// </summary>
		string GetPrefix();

		/// <summary>
		/// Apply the modifier's stat changes to the NPC
		/// Called once when the NPC spawns
		/// </summary>
		void Apply(Terraria.NPC npc);

		/// <summary>
		/// Called when the <b>player strikes this NPC</b> — a retaliation hook. The player has just
		/// dealt damage; nothing has happened to the player.
		///
		/// ⚠️ This was called <c>OnHit</c> until 2026-07-17, and the name was actively harmful: every
		/// ailment modifier implemented it as though it meant "this NPC hit the player" and applied a
		/// debuff. Because the dispatch is the opposite, swinging at a Venomous Zombie poisoned
		/// <i>you</i> — from any range, without the enemy ever touching you. Ailments belong in
		/// <see cref="OnHitPlayer"/>. If you reach for this hook, be sure you mean "the player
		/// attacked me".
		/// </summary>
		void OnHitByPlayer(Terraria.NPC npc, Player player);

		/// <summary>
		/// Called when this <b>NPC damages the player</b> through contact damage. This is the hook for
		/// ailments: the enemy touched you, so you catch fire.
		///
		/// Default no-op, so the majority of modifiers — which have nothing to say here — do not each
		/// carry an empty override. Only reachable through an <see cref="IModifier"/> reference, which
		/// is how <c>EnemyModifierSystem</c> holds them.
		///
		/// ⚠️ Contact damage only. tModLoader dispatches <c>GlobalNPC.OnHitPlayer</c> for the NPC's own
		/// hit, so damage from a <i>projectile the NPC fired</i> never arrives here — a "Vile Igniting"
		/// enemy will not ignite you with its spit, only with its body.
		/// </summary>
		void OnHitPlayer(Terraria.NPC npc, Player player, Player.HurtInfo hurtInfo) { }

		/// <summary>
		/// Called every frame for active effects (projectile firing, auras, etc.)
		/// </summary>
		void Update(Terraria.NPC npc);

		/// <summary>
		/// Called every frame for life regeneration effects (DoTs, healing, etc.)
		///
		/// ⚠️ tModLoader calls this at the <b>end</b> of vanilla's DoT pass (<c>NPC.cs:109271</c>) —
		/// after every <c>if (lifeRegen > 0) lifeRegen = 0;</c> cancel that each vanilla DoT performs.
		/// So a positive contribution here <b>heals straight through Poisoned/On Fire</b> unless you
		/// guard on <c>npc.lifeRegen &lt; 0</c> yourself. Mirror of the player-side trap in CLAUDE.md §8.
		/// </summary>
		void UpdateLifeRegen(Terraria.NPC npc, ref int damage);

		/// <summary>
		/// Called when the NPC is killed
		/// Can create effects, explosions, or other on-death behaviors
		/// </summary>
		void OnKill(Terraria.NPC npc);

		/// <summary>
		/// Get additional XP bonus multiplier for this modifier
		/// </summary>
		float GetXPBonus();

		/// <summary>
		/// Get the weight for spawn chance (higher = more common)
		/// </summary>
		int GetSpawnWeight();
	}
}
