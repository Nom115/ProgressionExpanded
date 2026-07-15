using Terraria.ModLoader;
using ProgressionExpanded.Src.NPCs.Enemy.Elemental;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints.NotablePassives
{
	/// <summary>
	/// Base for the five elemental masteries. Each converts a percentage of the damage you deal into
	/// damage-over-time of its element, by contributing to the shared pool on CombatEffectStats.
	/// <c>ElementalDotApplier</c> is the pool's sole consumer and owns the duration, the resistance
	/// lookup, the stack cap and the buff icon.
	///
	/// <b>What these used to be, and why that had to change.</b> They called
	/// <c>target.AddBuff(BuffID.OnFire, ticks)</c> and let vanilla deal the damage. That was broken
	/// in two ways at once. Vanilla's NPC DoT is a flat constant — 4 HP/s for OnFire, 8 for
	/// Frostburn, 30 for Venom — identical at level 1 and level 100, against enemy health that the
	/// world-level system scales without limit; and Venom was therefore 7.5x Immolation for the same
	/// point cost, for no design reason. Worse, vanilla has <b>no NPC DoT at all</b> for Bleeding or
	/// Electrified: <c>NPC.UpdateNPC_BuffSetFlags</c> never handles buff ids 30 or 144, and no
	/// <c>npc.bleeding</c>/<c>npc.electrified</c> field exists — they are player-only. Rend and Shock
	/// applied an icon, ticked a timer, and dealt literally nothing. Both faults come from the same
	/// root: AddBuff's only lever is duration, so a DoT sized by the hit that applied it simply
	/// cannot be expressed that way.
	///
	/// Contributing to a pool that a mod-owned ticker reads fixes both, and gives all five the same
	/// shape as Bloodthirst.
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
		/// Fraction of the damage you deal that becomes this element's DoT, per second, per tier.
		///
		/// <b>This is the primary balance dial for the whole elemental system.</b> The tuning
		/// identity: for any weapon under the stack cap, DoT DPS divided by direct DPS equals
		/// <c>conversion x duration</c> — and that is independent of attack speed, because a weapon
		/// hitting twice as often stacks twice as many instances of half the size. At 2% per tier
		/// that is 8% at rank 4, which against the 4s base duration is 32% of your direct DPS per
		/// element. Untested in play; expect to move it.
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
