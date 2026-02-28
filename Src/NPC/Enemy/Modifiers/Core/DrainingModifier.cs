using Terraria;
using Terraria.ID;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Core
{
	/// <summary>
	/// Draining modifier - Reduces player life regeneration
	/// </summary>
	public class DrainingModifier : IModifier
	{
		public string GetPrefix() => "Draining";

		public void Apply(Terraria.NPC npc) { }

		public void OnHit(Terraria.NPC npc, Player player)
		{
			// Apply Bleeding debuff for 10 seconds (stops natural regen)
			player.AddBuff(BuffID.Bleeding, 600);
		}

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.0f;

		public int GetSpawnWeight() => 70; // Uncommon
	}
}
