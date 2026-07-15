using System.Collections.Generic;

namespace ProgressionExpanded.Items.Modifiers
{
	/// <summary>
	/// A JSON-defined pool of modifiers for one item category (e.g. "armor", "melee_weapon").
	/// The item-side analogue of a <c>PassiveTree</c>.
	/// </summary>
	public class ItemModifierPool
	{
		/// <summary>Category id — must match <c>ItemCategoryClassifier.GetPoolId</c>.</summary>
		public string PoolId { get; set; }

		/// <summary>Optional human-readable name.</summary>
		public string PoolName { get; set; }

		public List<ItemModifierDefinition> Modifiers { get; set; } = new List<ItemModifierDefinition>();

		/// <summary>Look up a modifier definition by id.</summary>
		public ItemModifierDefinition GetModifier(string modId)
		{
			if (string.IsNullOrEmpty(modId) || Modifiers == null) return null;
			foreach (var mod in Modifiers)
			{
				if (mod.ModId == modId) return mod;
			}
			return null;
		}

		/// <summary>
		/// Validate structural integrity, mirroring <c>PassiveTree.ValidateTree</c>: non-empty
		/// PoolId, unique non-empty ModIds, a valid keyword, and at least one tier per modifier.
		/// </summary>
		public bool Validate(out string error)
		{
			error = null;

			if (string.IsNullOrEmpty(PoolId))
			{
				error = "Pool is missing a PoolId";
				return false;
			}

			if (Modifiers == null || Modifiers.Count == 0)
			{
				error = $"Pool '{PoolId}' has no modifiers";
				return false;
			}

			var seen = new HashSet<string>();
			foreach (var mod in Modifiers)
			{
				if (string.IsNullOrEmpty(mod.ModId))
				{
					error = $"Pool '{PoolId}' has a modifier with no ModId";
					return false;
				}

				if (!seen.Add(mod.ModId))
				{
					error = $"Pool '{PoolId}' has duplicate ModId '{mod.ModId}'";
					return false;
				}

				string keyword = mod.Keyword?.ToLowerInvariant();
				if (keyword != "increased" && keyword != "more" && keyword != "flat")
				{
					error = $"Modifier '{mod.ModId}' has invalid Keyword '{mod.Keyword}' (expected increased/more/flat)";
					return false;
				}

				if (mod.Tiers == null || mod.Tiers.Count == 0)
				{
					error = $"Modifier '{mod.ModId}' has no tiers";
					return false;
				}
			}

			return true;
		}
	}
}
