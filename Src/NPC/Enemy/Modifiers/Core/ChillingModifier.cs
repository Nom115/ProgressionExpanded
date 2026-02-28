using Terraria;
using Terraria.ID;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Core
{
	/// <summary>
	/// Chilling modifier - Slows and chills the player
	/// </summary>
	public class ChillingModifier : IModifier
	{
		public string GetPrefix() => "Chilling";

		public void Apply(Terraria.NPC npc) { }

		public void OnHit(Terraria.NPC npc, Player player)
		{
			// Apply Chilled debuff for 6 seconds (reduces movement speed)
			player.AddBuff(BuffID.Chilled, 360);
		}

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.0f;

		public int GetSpawnWeight() => 60; // Rare
	}
}
