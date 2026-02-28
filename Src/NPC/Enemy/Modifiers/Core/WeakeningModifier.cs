using Terraria;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Core
{
	/// <summary>
	/// Weakening modifier - Reduces player defense on hit
	/// </summary>
	public class WeakeningModifier : IModifier
	{
		public string GetPrefix() => "Weakening";

		public void Apply(Terraria.NPC npc) { }

		public void OnHit(Terraria.NPC npc, Player player)
		{
			// Reduce defense by 5 for 8 seconds
			player.AddBuff(Terraria.ID.BuffID.BrokenArmor, 480);
		}

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.0f;

		public int GetSpawnWeight() => 70; // Uncommon
	}
}
