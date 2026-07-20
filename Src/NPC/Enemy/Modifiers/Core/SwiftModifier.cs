using Terraria;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Core
{
	/// <summary>
	/// Swift modifier - Increased movement and attack speed
	/// </summary>
	public class SwiftModifier : IModifier
	{
		private const float SpeedBoost = 1.3f;
		private const float MaxSpeed = 20f; // cap so acceleration-based AIs can't run away

		public string GetPrefix() => "Swift";

		public void Apply(Terraria.NPC npc)
		{
			// Movement speed is applied per-frame in Update, not here: NPC AI overwrites velocity every
			// frame, so a one-time boost at spawn was a no-op (that was the bug).
		}

		public void OnHitByPlayer(Terraria.NPC npc, Player player) { }

		public void Update(Terraria.NPC npc)
		{
			// Re-apply the speed boost after the NPC's AI has set velocity for this frame. Cap the
			// resulting magnitude so an acceleration-based AI (one that reads its own velocity) can't
			// compound the boost into a runaway; velocity-clamping AIs just see a stable ~1.3x speed.
			float speed = npc.velocity.Length();
			if (speed > 0.01f && speed < MaxSpeed)
			{
				float newSpeed = System.Math.Min(speed * SpeedBoost, MaxSpeed);
				npc.velocity *= newSpeed / speed;
			}
		}

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.0f;

		public int GetSpawnWeight() => 100; // Common
	}
}
