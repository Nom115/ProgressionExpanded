using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.NPCs.Enemy.Elemental
{
	/// <summary>
	/// Every elemental damage-over-time instance currently burning on one enemy, and the code that
	/// ticks them and turns them into actual damage.
	///
	/// <b>Why this owns the state instead of the buff.</b> Vanilla's NPC DoT is a flat constant per
	/// buff (4 HP/s fire, 8 cold, 30 poison) and there is no NPC DoT at all for Bleeding or
	/// Electrified. A DoT whose size comes from the hit that applied it cannot be expressed as a
	/// vanilla buff, so the buff is reduced to an icon and the numbers live here.
	///
	/// <b>Why lifeRegen and not SimpleStrikeNPC.</b> Striking re-enters the hit pipeline, which
	/// re-enters ModPlayer.OnHitNPC, which is what applies DoT — a DoT that applies DoT. Both
	/// Stagger (Player.Hurt) and Hellfire (SimpleStrikeNPC) needed a re-entrancy flag to survive
	/// exactly that, and Stagger's cost the mod an infinite loop before it was found. lifeRegen
	/// cannot re-enter anything, so that entire bug class is unrepresentable here rather than
	/// merely guarded against. Vanilla also does the death check, OnKill and therefore the XP award
	/// for free.
	/// </summary>
	public class ElementalDotNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		/// <summary>
		/// Instances per element, per enemy.
		///
		/// <b>The cap is the weapon-speed dial, and 20 is chosen to keep it fair.</b> Below the cap
		/// a DoT's share of your damage is <c>conversion x duration</c> — independent of attack
		/// speed, because a weapon that hits twice as often stacks twice as many instances of half
		/// the size. The cap only bites once a weapon exceeds <c>cap / duration</c> hits per second
		/// (20 / 4s = 5 hits/sec), above which fast weapons start losing DoT that slow ones keep. A
		/// cap of 5 would put that threshold at 1.25 hits/sec and hand slow weapons roughly 8x the
		/// DoT of fast ones for no stated reason. Twenty floats per element per enemy is nothing.
		/// </summary>
		public const int MaxInstancesPerElement = 20;

		/// <summary>
		/// One burning instance: a rate and a countdown, not a pool of damage.
		///
		/// Note the difference from StaggerInstance, which carries RemainingDamage and dumps the
		/// remainder when it expires. This is a per-second rate for a duration, so expiry just
		/// stops it — there is no remainder, and a longer duration is straightforwardly more total
		/// damage rather than the same damage spread thinner.
		/// </summary>
		private class DotInstance
		{
			public float DamagePerSecond;
			public float TimeRemaining;
		}

		/// <summary>
		/// Lazy — null until this enemy actually takes a DoT, which most never do. Indexed by
		/// (int)DamageElement; the inner lists are lazy for the same reason.
		/// </summary>
		private List<DotInstance>[] instances;

		/// <summary>
		/// Carries the sub-unit remainder of our lifeRegen contribution between frames.
		///
		/// lifeRegen is an int, so a 2.4 HP/s DoT wants to subtract 4.8 units and would truncate to
		/// 4 — quietly losing 17% of the damage, every frame, forever. Vanilla accumulates lifeRegen
		/// into lifeRegenCount rather than applying it directly, so spending the fraction only once
		/// it reaches a whole unit reproduces the exact rate on average.
		/// </summary>
		private float regenAccumulator;

		/// <summary>
		/// This enemy's private randomness, from which ElementalResistance derives its per-element
		/// resistances. Zero means not yet rolled.
		/// </summary>
		private int resistanceSeed;

		/// <summary>
		/// A stable per-enemy seed, rolled on first read.
		///
		/// <b>An int, not a float[5], and that is the whole point.</b> EnemyModifierSystem.Clone copies
		/// its modifier <i>list</i> but shares the IModifier instances, which is why VileSpitModifier's
		/// shootTimer cross-talks between cloned enemies today. An array of rolled resistances here
		/// would be a fresh instance of that exact bug. A value type cannot be aliased at all — even a
		/// maintainer who forgets the Clone line below gets correct behaviour, because MemberwiseClone
		/// copies it. Same instinct as the lifeRegen-over-SimpleStrikeNPC choice above: make the bug
		/// class unrepresentable rather than guard against it.
		///
		/// <b>Lazy on read, which deletes the ordering question.</b> The only caller is
		/// ElementalResistance.Get, reached from ElementalDotApplier.OnHitNPC — necessarily long after
		/// SetDefaults, ScaleStats and every GlobalNPC's OnSpawn. So there is no hook to register, no
		/// dependence on GlobalNPC ordering, and nothing to initialise: the value materialises when
		/// first asked for and is fixed forever after. Most enemies never take elemental damage and so
		/// never roll one at all.
		///
		/// Note Bleed returns before reaching this, so a pure-Rend build never allocates a seed.
		///
		/// Main.rand makes this client-authoritative and therefore MP-divergent — consistent with the
		/// rest of the mod, which is single-player by design (see the net-sync limitation in CLAUDE.md
		/// §7). If MP ever matters, hashing npc.type is the drop-in replacement, at the cost of every
		/// enemy of a type sharing one set of resistances.
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

		#region Mutation

		/// <summary>
		/// Start a new DoT of <paramref name="element"/> on this enemy.
		///
		/// At the cap, a new instance replaces the <b>weakest</b> one, and only if it beats it. Not
		/// the oldest: a stream of chip damage must never push a big hit's DoT out early, which is
		/// the failure mode a plain FIFO cap has. Refusing a weaker instance outright is the other
		/// half of that — otherwise the last hit always wins regardless of size.
		/// </summary>
		public void AddInstance(DamageElement element, float damagePerSecond, float duration)
		{
			if (damagePerSecond <= 0f || duration <= 0f)
				return;

			if (instances == null)
				instances = new List<DotInstance>[DamageElementInfo.Count];

			int index = (int)element;
			if (instances[index] == null)
				instances[index] = new List<DotInstance>();

			List<DotInstance> list = instances[index];

			if (list.Count < MaxInstancesPerElement)
			{
				list.Add(new DotInstance { DamagePerSecond = damagePerSecond, TimeRemaining = duration });
				return;
			}

			int weakest = 0;
			for (int i = 1; i < list.Count; i++)
			{
				if (list[i].DamagePerSecond < list[weakest].DamagePerSecond)
					weakest = i;
			}

			if (list[weakest].DamagePerSecond >= damagePerSecond)
				return;

			list[weakest].DamagePerSecond = damagePerSecond;
			list[weakest].TimeRemaining = duration;
		}

		/// <summary>
		/// Whether we currently own a DoT of this element on this enemy. This is what makes the
		/// vanilla cancel below safe to scope.
		/// </summary>
		public bool HasInstance(DamageElement element)
		{
			if (instances == null)
				return false;

			List<DotInstance> list = instances[(int)element];
			return list != null && list.Count > 0;
		}

		/// <summary>
		/// Whether any element of ours is currently ticking on this enemy.
		///
		/// <b>Ask this rather than reading npc.lifeRegen.</b> Vanilla's DoTs are all summed before any
		/// tModLoader hook runs, so a reader in UpdateLifeRegen sees them reliably — but ours land in
		/// <i>this</i> GlobalNPC's UpdateLifeRegen, and the order two GlobalNPCs' hooks run in is a
		/// load-order detail, not a guarantee. A caller reading lifeRegen for our DoTs would therefore
		/// work or not depending on registration order. State is stable at any point in the frame;
		/// same "pull, not push" reasoning as TalentSuppression in CLAUDE.md §6.
		/// </summary>
		public bool HasAnyInstance()
		{
			if (instances == null)
				return false;

			for (int e = 0; e < instances.Length; e++)
			{
				if (instances[e] != null && instances[e].Count > 0)
					return true;
			}

			return false;
		}

		#endregion

		#region Tick and delivery

		/// <summary>
		/// Both the tick and the payout. tModLoader calls this once per frame per NPC, at the end of
		/// vanilla's own DoT pass — so <c>npc.lifeRegen</c> already holds vanilla's total when we
		/// arrive, which is what lets us cancel it selectively below.
		/// </summary>
		public override void UpdateLifeRegen(NPC npc, ref int damage)
		{
			if (instances == null)
				return;

			float totalDamagePerSecond = TickAndSum();
			if (totalDamagePerSecond <= 0f)
			{
				// Nothing of ours is burning, so we own no vanilla flag and cancel nothing. Drop the
				// carried fraction rather than letting it leak into the next DoT.
				regenAccumulator = 0f;
				return;
			}

			CancelVanillaDot(npc);

			// Note we do NOT zero positive lifeRegen first, though every vanilla DoT case does
			// (`if (lifeRegen > 0) lifeRegen = 0;` precedes each subtraction). Two reasons. It would
			// make us order-dependent against RegeneratingModifier, which adds +2 from the very same
			// hook via EnemyModifierSystem — whoever ran last would win, and GlobalNPC hook order is
			// not ours to rely on. And netting the two is a defensible rule in its own right: a
			// Regenerating enemy shrugs off a little DoT, which is what that affix is for. Follow
			// this rule if you add another DoT: subtract, don't clobber.
			//
			// lifeRegen is in HALF-HP-per-second units, hence the x2. See regenAccumulator.
			regenAccumulator += totalDamagePerSecond * 2f;
			int units = (int)regenAccumulator;
			regenAccumulator -= units;
			if (units > 0)
				npc.lifeRegen -= units;

			// Chunk size ONLY. Vanilla deals `damage` life per 120*damage of accumulated lifeRegen,
			// so the total is identical whatever this is set to — it controls the combat-text number
			// and how often it pops, and nothing else. Raising it does not raise the damage; only
			// lifeRegen above moves real health. Aim for roughly four popups a second, and never
			// lower another contributor's chunk.
			int chunk = (int)(totalDamagePerSecond / 4f);
			if (chunk > damage)
				damage = chunk;
		}

		/// <summary>
		/// Age every instance by one frame, drop the expired, and return the surviving total rate.
		/// </summary>
		private float TickAndSum()
		{
			const float deltaTime = 1f / 60f;
			float total = 0f;

			for (int e = 0; e < DamageElementInfo.Count; e++)
			{
				List<DotInstance> list = instances[e];
				if (list == null || list.Count == 0)
					continue;

				for (int i = list.Count - 1; i >= 0; i--)
				{
					DotInstance instance = list[i];
					instance.TimeRemaining -= deltaTime;

					if (instance.TimeRemaining <= 0f)
					{
						list.RemoveAt(i);
						continue;
					}

					total += instance.DamagePerSecond;
				}
			}

			return total;
		}

		/// <summary>
		/// Undo vanilla's flat DoT for the elements we are burning, so our number is the only
		/// number and all five elements behave identically.
		///
		/// <b>Scoped to instances we own.</b> A vanilla flaming sword sets the same npc.onFire flag
		/// we do, and cancelling unconditionally would silently strip the burn off every fire weapon
		/// in the game — including for players who never touched the elemental masteries. Asking
		/// whether we put it there keeps that damage intact.
		///
		/// Known edge: if a torch's burn and our Immolation are up at once they share one flag, so
		/// the torch's 8 is eaten along with ours. Accepted — vanilla exposes no per-source DoT.
		/// </summary>
		private void CancelVanillaDot(NPC npc)
		{
			for (int e = 0; e < DamageElementInfo.Count; e++)
			{
				var element = (DamageElement)e;

				if (!DamageElementInfo.VanillaFlagIsSet(npc, element))
					continue;
				if (!HasInstance(element))
					continue;

				npc.lifeRegen += DamageElementInfo.VanillaLifeRegenFor(element);
			}
		}

		#endregion

		#region Cloning

		protected override bool CloneNewInstances => false;

		/// <summary>
		/// Deep copy, deliberately.
		///
		/// EnemyModifierSystem.Clone allocates a fresh List but copies the element references, and
		/// that is a live bug today — VileSpitModifier's shootTimer cross-talks between cloned
		/// enemies because of it. A shallow copy here would have two enemies sharing one DoT's
		/// countdown, so each would age it and both would expire at double speed.
		/// </summary>
		public override GlobalNPC Clone(NPC from, NPC to)
		{
			var fromGlobal = from.GetGlobalNPC<ElementalDotNPC>();
			var toGlobal = (ElementalDotNPC)base.Clone(from, to);

			toGlobal.regenAccumulator = fromGlobal.regenAccumulator;

			// A split enemy inherits its parent's resistances rather than rerolling: the two halves of
			// a worm are the same creature. Safe to copy unconditionally — 0 simply means the parent
			// never rolled, and the child will roll for itself on first read.
			toGlobal.resistanceSeed = fromGlobal.resistanceSeed;

			toGlobal.instances = null;

			if (fromGlobal.instances == null)
				return toGlobal;

			toGlobal.instances = new List<DotInstance>[DamageElementInfo.Count];

			for (int e = 0; e < DamageElementInfo.Count; e++)
			{
				List<DotInstance> source = fromGlobal.instances[e];
				if (source == null)
					continue;

				var copy = new List<DotInstance>(source.Count);
				for (int i = 0; i < source.Count; i++)
				{
					copy.Add(new DotInstance
					{
						DamagePerSecond = source[i].DamagePerSecond,
						TimeRemaining = source[i].TimeRemaining,
					});
				}

				toGlobal.instances[e] = copy;
			}

			return toGlobal;
		}

		#endregion
	}
}
