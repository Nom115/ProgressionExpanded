using System.Collections.Generic;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints
{
	/// <summary>
	/// Represents a single node in a passive tree
	/// Can have multiple tiers and grant flat or percentage-based stat bonuses
	/// </summary>
	public class PassiveNode
	{
		// Node identification
		public string NodeId { get; set; }
		public string DisplayName { get; set; }
		public string Description { get; set; }

		// Node configuration
		public int MaxTier { get; set; } = 1;
		public int PointsPerTier { get; set; } = 1;
		public List<string> Prerequisites { get; set; } = new List<string>();

		// Visual positioning (for UI)
		public int PositionX { get; set; }
		public int PositionY { get; set; }

		// Stat bonuses (applied per tier)
		public Dictionary<string, float> FlatBonuses { get; set; } = new Dictionary<string, float>();
		public Dictionary<string, float> PercentBonuses { get; set; } = new Dictionary<string, float>();

		/// <summary>
		/// Get the total points required to reach a specific tier
		/// </summary>
		public int GetTotalPointCost(int tier)
		{
			if (tier <= 0) return 0;
			if (tier > MaxTier) tier = MaxTier;
			return tier * PointsPerTier;
		}

		/// <summary>
		/// Get the points needed to upgrade from current tier to next tier
		/// </summary>
		public int GetUpgradeCost(int currentTier)
		{
			if (currentTier >= MaxTier) return 0;
			return PointsPerTier;
		}

		/// <summary>
		/// Check if all prerequisites are met
		/// </summary>
		public bool ArePrerequisitesMet(Dictionary<string, int> allocatedNodes)
		{
			if (Prerequisites == null || Prerequisites.Count == 0)
				return true;

			foreach (string prereqId in Prerequisites)
			{
				if (!allocatedNodes.ContainsKey(prereqId) || allocatedNodes[prereqId] <= 0)
					return false;
			}

			return true;
		}

		/// <summary>
		/// Get the flat bonus value for a specific stat at a given tier
		/// </summary>
		public float GetFlatBonus(string statName, int tier)
		{
			if (FlatBonuses.ContainsKey(statName))
				return FlatBonuses[statName] * tier;
			return 0f;
		}

		/// <summary>
		/// Get the percent bonus value for a specific stat at a given tier
		/// </summary>
		public float GetPercentBonus(string statName, int tier)
		{
			if (PercentBonuses.ContainsKey(statName))
				return PercentBonuses[statName] * tier;
			return 0f;
		}

		/// <summary>
		/// Get a formatted description with stat bonuses for a specific tier
		/// </summary>
		public string GetFormattedDescription(int tier)
		{
			if (tier <= 0)
				return Description;

			string result = Description + "\n";

			// Add flat bonuses
			foreach (var bonus in FlatBonuses)
			{
				float value = bonus.Value * tier;
				result += $"\n+{value} {FormatStatName(bonus.Key)}";
			}

			// Add percent bonuses
			foreach (var bonus in PercentBonuses)
			{
				float value = bonus.Value * tier * 100f; // Convert to percentage
				result += $"\n+{value}% {FormatStatName(bonus.Key)}";
			}

			if (MaxTier > 1)
				result += $"\n\nTier: {tier}/{MaxTier}";

			return result;
		}

		/// <summary>
		/// Format stat names for display (e.g., "MeleeDamage" -> "Melee Damage")
		/// </summary>
		private string FormatStatName(string statName)
		{
			// Add space before capital letters
			string formatted = "";
			for (int i = 0; i < statName.Length; i++)
			{
				if (i > 0 && char.IsUpper(statName[i]))
					formatted += " ";
				formatted += statName[i];
			}
			return formatted;
		}
	}
}
