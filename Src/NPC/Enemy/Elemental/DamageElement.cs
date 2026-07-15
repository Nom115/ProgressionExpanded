using Terraria;
using Terraria.ID;

namespace ProgressionExpanded.Src.NPCs.Enemy.Elemental
{
	/// <summary>
	/// The five damage elements a hit can convert into damage-over-time.
	///
	/// Fire, Cold, Lightning and Poison are elemental and face elemental resistance. Bleed is
	/// physical: it ignores elemental resistance entirely and is limited by the enemy's armour
	/// instead, which makes it the reliable answer to a heavily-resistant target. See
	/// <see cref="ElementalResistance"/>.
	///
	/// The numbering is load-bearing. Each value doubles as an index into CombatEffectStats'
	/// conversion pool and into ElementalDotNPC's per-element instance lists, so these arrays are
	/// sized by <see cref="DamageElementInfo.Count"/>. Append only — inserting a value in the
	/// middle silently reindexes every one of them.
	/// </summary>
	public enum DamageElement
	{
		Fire = 0,
		Cold = 1,
		Lightning = 2,
		Poison = 3,
		Bleed = 4,
	}

	/// <summary>
	/// Per-element facts about how each element rides on top of vanilla: which buff carries its
	/// icon, and what vanilla's own DoT does for that buff.
	/// </summary>
	public static class DamageElementInfo
	{
		/// <summary>Number of elements. Sizes every per-element array in the mod.</summary>
		public const int Count = 5;

		/// <summary>
		/// The vanilla buff applied purely as this element's icon. We deal the damage ourselves;
		/// the buff is decoration plus a timer the player can read.
		///
		/// These match the five masteries one-to-one, which is why they are vanilla ids and not
		/// custom ModBuffs — the existing enemy affixes (BleedingModifier, IgnitingModifier, …)
		/// already use the same ids on the player side, and PlaguebearerTalent counts debuffs by
		/// walking the NPC's vanilla buff array.
		/// </summary>
		public static int BuffIdFor(DamageElement element)
		{
			switch (element)
			{
				case DamageElement.Fire: return BuffID.OnFire;
				case DamageElement.Cold: return BuffID.Frostburn;
				case DamageElement.Lightning: return BuffID.Electrified;
				case DamageElement.Poison: return BuffID.Venom;
				default: return BuffID.Bleeding;
			}
		}

		/// <summary>
		/// How much vanilla itself subtracts from <c>npc.lifeRegen</c> while this element's carrier
		/// buff is active, in lifeRegen units (half-HP-per-second). Read straight off
		/// <c>NPC.UpdateNPC_BuffApplyDOTs</c>: <c>onFire -= 8</c>, <c>onFrostBurn -= 16</c>,
		/// <c>venom -= 60</c>.
		///
		/// Bleeding and Electrified are 0, and that is not an omission.
		/// <c>NPC.UpdateNPC_BuffSetFlags</c> never handles buff ids 30 or 144 — there is no
		/// <c>npc.bleeding</c> or <c>npc.electrified</c> field, both are player-only. Applying them
		/// to an NPC shows an icon, ticks a timer down, and does nothing whatsoever. That is
		/// precisely why Rend and Shock were dead before this system existed, and why the mod now
		/// deals the damage itself rather than trusting the buff.
		///
		/// Paired with <see cref="VanillaFlagIsSet"/> to cancel the double-dip — see
		/// ElementalDotNPC.UpdateLifeRegen.
		/// </summary>
		public static int VanillaLifeRegenFor(DamageElement element)
		{
			switch (element)
			{
				case DamageElement.Fire: return 8;
				case DamageElement.Cold: return 16;
				case DamageElement.Poison: return 60;
				default: return 0; // Lightning and Bleed set no NPC flag at all.
			}
		}

		/// <summary>
		/// Whether vanilla's own DoT flag for this element is currently set on the NPC.
		///
		/// Note this is <c>onFire</c> specifically, not <c>onFire2</c> (Cursed Inferno) or
		/// <c>onFire3</c> — those are different buffs with their own rates, and nothing here
		/// applies or cancels them.
		/// </summary>
		public static bool VanillaFlagIsSet(NPC npc, DamageElement element)
		{
			switch (element)
			{
				case DamageElement.Fire: return npc.onFire;
				case DamageElement.Cold: return npc.onFrostBurn;
				case DamageElement.Poison: return npc.venom;
				default: return false;
			}
		}
	}
}
