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
		/// Called when the NPC is hit by a player
		/// Can apply effects to player or NPC
		/// </summary>
		void OnHit(Terraria.NPC npc, Player player);

		/// <summary>
		/// Called every frame for active effects (projectile firing, auras, etc.)
		/// </summary>
		void Update(Terraria.NPC npc);

		/// <summary>
		/// Called every frame for life regeneration effects (DoTs, healing, etc.)
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
