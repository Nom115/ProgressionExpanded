using Terraria;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Core
{
	/// <summary>
	/// Juggernaut modifier - Massively increased health
	/// </summary>
	public class JuggernautModifier : IModifier
	{
		public string GetPrefix() => "Juggernaut";

		// Excluded from bosses: it doubles HP, and boss HP comes solely from the deterministic boss
		// curve (NPCLevelManager.ApplyLevelScaling). Trash still rolls it normally.
		public ModifierCategory Category => ModifierCategory.Excluded;

		public void Apply(Terraria.NPC npc)
		{
			// Increase health by 100%
			npc.lifeMax = (int)(npc.lifeMax * 2.0f);
			npc.life = npc.lifeMax;
		}

		public void OnHitByPlayer(Terraria.NPC npc, Player player) { }

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.0f;

		public int GetSpawnWeight() => 80; // Uncommon
	}
}
