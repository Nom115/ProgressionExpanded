using Terraria;
using Terraria.ID;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Core
{
	/// <summary>
	/// Igniting modifier - Sets player on fire
	/// </summary>
	public class IgnitingModifier : IModifier
	{
		public string GetPrefix() => "Igniting";

		public void Apply(Terraria.NPC npc) { }

		public void OnHit(Terraria.NPC npc, Player player)
		{
			// Apply OnFire debuff for 7 seconds
			player.AddBuff(BuffID.OnFire, 420);
		}

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.0f;

		public int GetSpawnWeight() => 60; // Rare
	}
}
