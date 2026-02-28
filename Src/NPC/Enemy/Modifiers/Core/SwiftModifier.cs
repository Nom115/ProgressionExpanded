using Terraria;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Core
{
	/// <summary>
	/// Swift modifier - Increased movement and attack speed
	/// </summary>
	public class SwiftModifier : IModifier
	{
		public string GetPrefix() => "Swift";

		public void Apply(Terraria.NPC npc)
		{
			// Increase movement speed by 30%
			npc.velocity *= 1.3f;
			
			// Reduce attack cooldown (if applicable)
			// This is a general modifier that affects most NPCs
		}

		public void OnHit(Terraria.NPC npc, Player player) { }

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.0f;

		public int GetSpawnWeight() => 100; // Common
	}
}
