using Terraria;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Core
{
	/// <summary>
	/// Tough modifier - Increased defense and knockback resistance
	/// </summary>
	public class ToughModifier : IModifier
	{
		public string GetPrefix() => "Tough";

		public void Apply(Terraria.NPC npc)
		{
			// Increase defense by 50%
			npc.defense = (int)(npc.defense * 1.5f);
			
			// Increase knockback resistance
			npc.knockBackResist *= 0.5f;
		}

		public void OnHit(Terraria.NPC npc, Player player) { }

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.0f;

		public int GetSpawnWeight() => 100; // Common
	}
}
