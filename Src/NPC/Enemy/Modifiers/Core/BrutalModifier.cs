using Terraria;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Core
{
	/// <summary>
	/// Brutal modifier - Increased damage output
	/// </summary>
	public class BrutalModifier : IModifier
	{
		public string GetPrefix() => "Brutal";

		public void Apply(Terraria.NPC npc)
		{
			// Increase damage by 40%
			npc.damage = (int)(npc.damage * 1.4f);
		}

		public void OnHit(Terraria.NPC npc, Player player) { }

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.0f;

		public int GetSpawnWeight() => 65; // Uncommon-Rare
	}
}
