using Terraria.ModLoader;
using ProgressionExpanded.Src.NPCs.Enemy.Elemental;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints.NotablePassives
{
	/// <summary>
	/// Base for the five elemental masteries. Each CONVERTS a percentage of the damage you deal into
	/// instant elemental damage of its element, by contributing to the shared pool on CombatEffectStats.
	/// <c>ElementalConversionApplier</c> is the pool's sole consumer and owns the split and the
	/// resistance lookup.
	///
	/// <b>Conversion is a trade, not a bonus.</b> Since the true-conversion rework (CLAUDE.md §10) the
	/// converted slice is taken OUT of the armour-facing hit and dealt against the target's per-element
	/// resistance instead — so a point in one of these is a matchup lever: it wins on an enemy weak to
	/// (or heavily armoured against) that element, and loses on one that resists it. Read the target with
	/// the hover panel. Contributing to a shared pool gives all five the same shape as Bloodthirst.
	/// </summary>
	public abstract class ElementalMastery : ModPlayer
	{
		protected const string TreeId = "warrior_tree";

		/// <summary>
		/// Node id in warrior_tree.json that gates this mastery. Hardcoded, and nothing validates it
		/// — a rename in the JSON silently disables the effect. See TalentBehaviour's Id for why the
		/// talent system does not have this hole.
		/// </summary>
		protected abstract string NodeId { get; }

		/// <summary>Which element this mastery converts damage into.</summary>
		protected abstract DamageElement Element { get; }

		/// <summary>
		/// Fraction of the damage you deal that is CONVERTED to this element, per tier. At 2% per tier
		/// that is 8% converted at rank 4.
		///
		/// <b>Balance note after the true-conversion rework.</b> This is no longer a flat DPS add — it is
		/// how much of your hit gets redirected from armour to this element's resistance. So 8% is a light
		/// matchup dip: near-neutral on an average enemy (RollMean +15% makes a blind convert a small
		/// loss), a gain against a weakness or heavy armour. If the masteries feel too weak to spend a
		/// point on, this is the dial (raise it), or move ElementalResistance.RollMean toward 0. Untested
		/// in play.
		/// </summary>
		protected virtual float ConversionPerTier => 0.02f;

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
			// PostUpdateMiscEffects — so contributing there would race the wipe. Same reasoning, and
			// the same phase, as Bloodthirst and StatApplier.
			//
			// The pool is whole percents; ConversionPerTier is a fraction. Hence the x100.
			CombatEffectStats.Get(Player).ElementalConversion[(int)Element] += ConversionPerTier * tier * 100f;
		}
	}

	/// <summary>Rend — your hits cause Bleeding. Physical: ignores elemental resistance.</summary>
	public class Rend : ElementalMastery
	{
		protected override string NodeId => "rend_notable";
		protected override DamageElement Element => DamageElement.Bleed;
	}

	/// <summary>Immolation — your hits set enemies On Fire.</summary>
	public class Immolation : ElementalMastery
	{
		protected override string NodeId => "immolation_notable";
		protected override DamageElement Element => DamageElement.Fire;
	}

	/// <summary>Venom — your hits inflict Venom.</summary>
	public class Venom : ElementalMastery
	{
		protected override string NodeId => "venom_notable";
		protected override DamageElement Element => DamageElement.Poison;
	}

	/// <summary>Frostbite — your hits inflict Frostburn.</summary>
	public class Frostbite : ElementalMastery
	{
		protected override string NodeId => "frostbite_notable";
		protected override DamageElement Element => DamageElement.Cold;
	}

	/// <summary>Shock — your hits inflict Electrified.</summary>
	public class Shock : ElementalMastery
	{
		protected override string NodeId => "shock_notable";
		protected override DamageElement Element => DamageElement.Lightning;
	}
}
