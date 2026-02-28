using Terraria;
using Terraria.ID;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Core
{
	/// <summary>
	/// Shocking modifier - Electrifies player, increasing damage taken
	/// </summary>
	public class ShockingModifier : IModifier
	{
		public string GetPrefix() => "Shocking";

		public void Apply(Terraria.NPC npc) { }

		public void OnHit(Terraria.NPC npc, Player player)
		{
			// Apply Electrified debuff for 5 seconds (prevents movement items)
			player.AddBuff(BuffID.Electrified, 300);
		}

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.0f;

		public int GetSpawnWeight() => 55; // Rare
	}
}
