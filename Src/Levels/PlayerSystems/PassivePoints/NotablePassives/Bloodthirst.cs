using System;
using Terraria;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints.NotablePassives
{
	/// <summary>
	/// Notable Passive: Bloodthirst — the player-side mirror of the enemy "Leech" affix.
	/// Landing a hit on an enemy heals a small, tier-scaled amount of health. An internal
	/// cooldown keeps multi-hit / AoE weapons from over-healing. Gated on the node tier.
	/// </summary>
	public class Bloodthirst : ModPlayer
	{
		private const string TreeId = "warrior_tree";
		private const string NodeId = "bloodthirst_notable";
		private const int HealCooldownTicks = 18; // ~0.3s between heals

		private int tier;
		private int healCooldown;

		public override void ResetEffects()
		{
			tier = Player.GetModPlayer<PassiveTreeManager>().GetNodeTier(TreeId, NodeId);
		}

		public override void PostUpdate()
		{
			if (healCooldown > 0)
				healCooldown--;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (tier <= 0 || healCooldown > 0)
				return;

			// Ignore critters, town NPCs and dummies.
			if (target.friendly || target.lifeMax <= 5)
				return;

			if (Player.statLife >= Player.statLifeMax2)
				return;

			int heal = 2 + tier; // tier 1..4 -> 3..6 HP per proc
			int newLife = Math.Min(Player.statLifeMax2, Player.statLife + heal);
			int healed = newLife - Player.statLife;
			if (healed <= 0)
				return;

			Player.statLife = newLife;
			Player.HealEffect(healed);
			healCooldown = HealCooldownTicks;
		}
	}
}
