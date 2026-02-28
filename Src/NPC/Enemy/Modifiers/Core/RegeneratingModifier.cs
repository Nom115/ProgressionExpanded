using Terraria;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Core
{
	/// <summary>
	/// Regenerating modifier - Slowly regenerates health
	/// </summary>
	public class RegeneratingModifier : IModifier
	{
		public string GetPrefix() => "Regenerating";

		public void Apply(Terraria.NPC npc)
		{
			// Stat boost applied, regen handled in UpdateLifeRegen
		}

		public void OnHit(Terraria.NPC npc, Player player) { }

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage)
		{
			// Regenerate 1 HP per second (60 ticks)
			npc.lifeRegen += 2; // 2 life regen = 1 HP/sec
		}

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.0f;

		public int GetSpawnWeight() => 75; // Uncommon
	}
}
