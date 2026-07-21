using Terraria;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.NPCs.Enemy.Elemental
{
	/// <summary>
	/// Holds one enemy's private randomness, from which <see cref="ElementalResistance"/> derives its
	/// per-element resistances.
	///
	/// <b>History.</b> This class used to be <c>ElementalDotNPC</c> and owned the whole damage-over-time
	/// engine (instances, the stack cap, <c>lifeRegen</c> delivery, vanilla-DoT cancellation). The mod
	/// moved from "elemental DoT added on top of the hit" to TRUE conversion (a slice of the hit is
	/// converted and dealt instantly, facing resistance instead of armour — see
	/// <c>ElementalConversionApplier</c> and CLAUDE.md §10), so there are no persistent DoTs any more.
	/// All that remains — and all that was ever per-enemy state — is the resistance seed.
	/// </summary>
	public class ElementalResistanceNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		/// <summary>
		/// This enemy's private randomness. Zero means not yet rolled.
		/// </summary>
		private int resistanceSeed;

		/// <summary>
		/// A stable per-enemy seed, rolled on first read.
		///
		/// <b>An int, not a float[5], and that is the whole point.</b> EnemyModifierSystem.Clone copies
		/// its modifier <i>list</i> but shares the IModifier instances, which is why VileSpitModifier's
		/// shootTimer cross-talks between cloned enemies today. An array of rolled resistances here would
		/// be a fresh instance of that exact bug. A value type cannot be aliased at all — even a maintainer
		/// who forgets the Clone line below gets correct behaviour, because MemberwiseClone copies it.
		///
		/// <b>Lazy on read, which deletes the ordering question.</b> The only callers are
		/// <c>ElementalResistance.Get</c> (reached from <c>ElementalConversionApplier.ModifyHitNPC</c>,
		/// necessarily long after SetDefaults / ScaleStats / every OnSpawn) and the hover panel. So there
		/// is no hook to register and nothing to initialise: the value materialises when first asked for
		/// and is fixed forever after. Most enemies are never hit by a conversion build and never roll one.
		///
		/// Note Bleed returns from <c>ElementalResistance.Get</c> before reaching this, so a pure-Rend
		/// build never allocates a seed.
		///
		/// Main.rand makes this client-authoritative and therefore MP-divergent — consistent with the rest
		/// of the mod, which is single-player by design (CLAUDE.md §7). If MP ever matters, hashing
		/// npc.type is the drop-in replacement, at the cost of every enemy of a type sharing resistances.
		/// </summary>
		public int ResistanceSeed
		{
			get
			{
				// Excludes 0 so the sentinel can never collide with a legitimately rolled seed.
				if (resistanceSeed == 0)
					resistanceSeed = Main.rand.Next(1, int.MaxValue);

				return resistanceSeed;
			}
		}

		protected override bool CloneNewInstances => false;

		/// <summary>
		/// A split enemy (a worm chunk) inherits its parent's resistances rather than rerolling: the two
		/// halves are the same creature. Safe to copy unconditionally — 0 simply means the parent never
		/// rolled, and the child rolls for itself on first read.
		/// </summary>
		public override GlobalNPC Clone(NPC from, NPC to)
		{
			var fromGlobal = from.GetGlobalNPC<ElementalResistanceNPC>();
			var toGlobal = (ElementalResistanceNPC)base.Clone(from, to);

			toGlobal.resistanceSeed = fromGlobal.resistanceSeed;

			return toGlobal;
		}
	}
}
