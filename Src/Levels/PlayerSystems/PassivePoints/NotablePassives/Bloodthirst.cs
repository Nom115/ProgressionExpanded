using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints.NotablePassives
{
	/// <summary>
	/// Notable Passive: Bloodthirst — the player-side mirror of the enemy "Leech" affix.
	/// Landing a hit leeches a percentage of the damage dealt as life. Gated on the node tier.
	///
	/// This used to pay out the heal itself, reading CombatEffectStats.LifeLeechPercent and adding
	/// its own tier on top. That made this class the ONLY reader of that field, so leech rolled onto
	/// an item did nothing whatsoever unless the player also happened to have this mastery — the stat
	/// was real, displayed, and inert. Bloodthirst now just contributes its tier to the shared pool
	/// and LifeLeechApplier pays every source out together, under one cooldown and one cap.
	/// </summary>
	public class Bloodthirst : ModPlayer
	{
		private const string TreeId = "warrior_tree";
		private const string NodeId = "bloodthirst_notable";

		/// <summary>Fraction of damage dealt leeched per tier — 2% per tier, 8% at max rank.</summary>
		private const float LeechPerTier = 0.02f;

		private int tier;

		public override void ResetEffects()
		{
			tier = Player.GetModPlayer<PassiveTreeManager>().GetNodeTier(TreeId, NodeId);
		}

		public override void PostUpdateMiscEffects()
		{
			if (tier <= 0)
				return;

			// Contributed here, not in ResetEffects: CombatEffectStats zeroes the pool during the
			// ResetEffects pass, and tModLoader runs that pass for every ModPlayer before any
			// PostUpdateMiscEffects — so contributing there would race the reset. This is also the
			// same phase StatApplier feeds the pool from, so all leech sources land together.
			//
			// LifeLeechPercent is whole percents; LeechPerTier is a fraction. Hence the x100.
			CombatEffectStats.Get(Player).LifeLeechPercent += LeechPerTier * tier * 100f;
		}
	}
}
