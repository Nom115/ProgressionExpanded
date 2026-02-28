using Terraria;
using Terraria.ID;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Core
{
	/// <summary>
	/// Venomous modifier - Applies poison debuff on hit
	/// </summary>
	public class VenomousModifier : IModifier
	{
		public string GetPrefix() => "Venomous";

		public void Apply(Terraria.NPC npc) { }

		public void OnHit(Terraria.NPC npc, Player player)
		{
			// Apply Poisoned debuff to player for 5 seconds
			player.AddBuff(BuffID.Poisoned, 300);
		}

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.0f;

		public int GetSpawnWeight() => 60; // Rare
	}
}
