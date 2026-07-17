using Terraria;
using Terraria.ID;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Core
{
	/// <summary>
	/// Bleeding modifier - Causes player to bleed on hit
	/// </summary>
	public class BleedingModifier : IModifier
	{
		public string GetPrefix() => "Bleeding";

		public void Apply(Terraria.NPC npc) { }

		public void OnHitByPlayer(Terraria.NPC npc, Player player) { }

		public void OnHitPlayer(Terraria.NPC npc, Player player, Player.HurtInfo hurtInfo)
		{
			// Apply Bleeding debuff for 15 seconds
			player.AddBuff(BuffID.Bleeding, 900);
		}

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.0f;

		public int GetSpawnWeight() => 65; // Uncommon-Rare
	}
}
