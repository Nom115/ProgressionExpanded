using System.Collections.Generic;
using System.Linq;
using Terraria;
using ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Core;
using ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Corruption;
using ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Crimson;
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
				new BrutalModifier()
			};
		}

		/// <summary>
		/// Roll random modifiers from the pool, filtering by conditions
		/// </summary>
		public static List<IModifier> RollModifiers(int count, Terraria.NPC npc = null)
		{
			if (allModifiers == null || allModifiers.Count == 0)
				Initialize();

			var result = new List<IModifier>();
			var available = new List<IModifier>(allModifiers);

			// Add conditional modifiers
			var vileSpitModifier = new VileSpitModifier();
			if (npc != null && VileSpitModifier.CanApply(npc))
			{
				available.Add(vileSpitModifier);
			}
var leechModifier = new LeechModifier();
			if (npc != null && LeechModifier.CanApply(npc))
			{
				available.Add(leechModifier);
			}

			
			for (int i = 0; i < count && available.Count > 0; i++)
			{
				// Weighted selection
				int totalWeight = available.Sum(m => m.GetSpawnWeight());
				int roll = Main.rand.Next(totalWeight);
				int current = 0;

				IModifier selected = null;
				foreach (var modifier in available)
				{
					current += modifier.GetSpawnWeight();
					if (roll < current)
					{
						selected = modifier;
						break;
					}
				}

				if (selected != null)
				{
					// Create new instance of the modifier
					result.Add(CreateModifierInstance(selected));
					available.Remove(selected);
				}
			}

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
