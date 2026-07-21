using System.Collections.Generic;
using System.Linq;
using Terraria;
using ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Core;
using ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Corruption;
using ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Crimson;
using ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Desert;
using ProgressionExpanded.Utils;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers
{
	/// <summary>
	/// Manages the pool of available modifiers and selection logic
	/// </summary>
	public static class ModifierPool
	{
		private static List<IModifier> allModifiers;

		/// <summary>
		/// Initialize the modifier pool with all available modifiers
		/// </summary>
		public static void Initialize()
		{
			// Update boss tracking before initializing pool
			BossKillTracker.UpdateBossTracking();

			allModifiers = new List<IModifier>
			{
				new SwiftModifier(),
				new ToughModifier(),
				new RegeneratingModifier(),
				new VenomousModifier(),
				new ExplosiveModifier(),
				new JuggernautModifier(),
				new WeakeningModifier(),
				new DrainingModifier(),
				new BleedingModifier(),
				new IgnitingModifier(),
				new ChillingModifier(),
				new ShockingModifier(),
				new BrutalModifier(),
				new FlamewardedModifier(),
				new FrostwardedModifier(),
				new StormwardedModifier(),
				new VenomwardedModifier()
			};
		}

		/// <summary>
		/// Build the pool of modifiers eligible for this NPC: the always-available set plus any
		/// conditional affixes whose CanApply passes. Returns fresh instances for the conditional ones
		/// and the shared templates for the rest; callers always run the pick through
		/// <see cref="CreateModifierInstance"/> before use, so the templates are never handed out.
		/// </summary>
		private static List<IModifier> BuildAvailablePool(Terraria.NPC npc)
		{
			if (allModifiers == null || allModifiers.Count == 0)
				Initialize();

			var available = new List<IModifier>(allModifiers);

			// Add conditional modifiers
			if (npc != null && VileSpitModifier.CanApply(npc))
			{
				available.Add(new VileSpitModifier());
			}

			if (npc != null && LeechModifier.CanApply(npc))
			{
				available.Add(new LeechModifier());
			}

			// Desert affixes. Unlike VileSpit/Leech — which unlock globally once the evil boss is down —
			// these stay desert-locked forever, so the biome keeps an identity in the late game.
			if (npc != null && ParchingModifier.CanApply(npc))
			{
				available.Add(new ParchingModifier());
			}

			if (npc != null && SunbakedModifier.CanApply(npc))
			{
				available.Add(new SunbakedModifier());
			}

			if (npc != null && InfestedModifier.CanApply(npc))
			{
				available.Add(new InfestedModifier());
			}

			return available;
		}

		/// <summary>
		/// Weighted single pick from a pool (by <see cref="IModifier.GetSpawnWeight"/>). Returns the
		/// selected template/instance (NOT a fresh copy — the caller clones it), or null for an empty or
		/// zero-weight pool.
		/// </summary>
		private static IModifier WeightedPick(List<IModifier> pool)
		{
			if (pool.Count == 0)
				return null;

			int totalWeight = pool.Sum(m => m.GetSpawnWeight());
			if (totalWeight <= 0)
				return null;

			int roll = Main.rand.Next(totalWeight);
			int current = 0;
			foreach (var modifier in pool)
			{
				current += modifier.GetSpawnWeight();
				if (roll < current)
					return modifier;
			}

			return null;
		}

		/// <summary>
		/// Roll random modifiers from the pool, filtering by conditions
		/// </summary>
		public static List<IModifier> RollModifiers(int count, Terraria.NPC npc = null)
		{
			var available = BuildAvailablePool(npc);
			var result = new List<IModifier>();

			for (int i = 0; i < count && available.Count > 0; i++)
			{
				var selected = WeightedPick(available);
				if (selected == null)
					break;

				// Create new instance of the modifier
				result.Add(CreateModifierInstance(selected));
				available.Remove(selected);
			}

			return result;
		}

		/// <summary>
		/// Roll the CURATED affix set for a boss: at most one Offensive + one Defensive affix, and never
		/// an <see cref="ModifierCategory.Excluded"/> one (Juggernaut — HP-inflating; boss HP comes from
		/// the deterministic boss curve, see NPCLevelManager.ApplyLevelScaling). This is what keeps a
		/// repeat boss varied without the old 2–5-affix HP blow-up. Because it yields ≤2 modifiers, both
		/// prefixes always show in the name (GenerateDisplayName renders the first two), so a rolled ward
		/// is never hidden on a boss.
		/// </summary>
		public static List<IModifier> RollBossModifiers(Terraria.NPC npc)
		{
			var available = BuildAvailablePool(npc);
			var result = new List<IModifier>(2);

			var offensive = WeightedPick(available.Where(m => m.Category == ModifierCategory.Offensive).ToList());
			if (offensive != null)
				result.Add(CreateModifierInstance(offensive));

			var defensive = WeightedPick(available.Where(m => m.Category == ModifierCategory.Defensive).ToList());
			if (defensive != null)
				result.Add(CreateModifierInstance(defensive));

			return result;
		}

		/// <summary>
		/// Create a new instance of the modifier type
		/// </summary>
		private static IModifier CreateModifierInstance(IModifier template)
		{
			// Create new instance of same type
			return (IModifier)System.Activator.CreateInstance(template.GetType());
		}
	}
}
