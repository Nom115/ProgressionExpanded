using System;
using Terraria;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints.NotablePassives
{
	/// <summary>
	/// Notable Passive: Bloodthirst — the player-side mirror of the enemy "Leech" affix.
	/// Landing a hit leeches a percentage of the damage dealt as life. An internal cooldown keeps
	/// multi-hit / AoE weapons from over-healing. Gated on the node tier.
	/// </summary>
	public class Bloodthirst : ModPlayer
	{
		private const string TreeId = "warrior_tree";
		private const string NodeId = "bloodthirst_notable";
		private const int HealCooldownTicks = 18; // ~0.3s between heals

		/// <summary>Fraction of damage dealt leeched per tier — 2% per tier, 8% at max rank.</summary>
		private const float LeechPerTier = 0.02f;

		/// <summary>
		/// Per-hit ceiling as a fraction of max life. Leech scales with the weapon, which is the
		/// point, but without a cap a single big hit on a boss would full-heal on its own and the
		/// cooldown wouldn't matter.
		/// </summary>
		private const float MaxLeechPerHit = 0.15f;

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

			var effects = Player.GetModPlayer<CombatEffectStats>();

			// Leech a share of the damage dealt, amplified by Healing, plus any leech granted by
			// gear/passives via CombatEffectStats. This used to be a flat (2 + tier) HP, which meant
			// 6 HP a hit at max rank — meaningful at level 10 and a rounding error against a
			// Hardmode weapon. As a percentage of damage it keeps pace with the whole game.
			float leechFraction = LeechPerTier * tier + effects.LifeLeechPercent / 100f;
			int heal = (int)Math.Round(damageDone * leechFraction * (1f + effects.HealingPercent / 100f));

			int perHitCap = Math.Max(1, (int)(Player.statLifeMax2 * MaxLeechPerHit));
			if (heal > perHitCap)
				heal = perHitCap;

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
