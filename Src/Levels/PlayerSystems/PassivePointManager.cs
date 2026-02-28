using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems
{
	/// <summary>
	/// Manages passive points that players earn on level up
	/// Passive points can be spent to permanently increase stats
	/// </summary>
	public class PassivePointManager : ModPlayer
	{
		private const string AVAILABLE_POINTS_KEY = "AvailablePassivePoints";
		private const string TOTAL_POINTS_KEY = "TotalPassivePointsEarned";
		private const string SPENT_POINTS_KEY = "SpentPassivePoints";

		private int availablePoints = 0;
		private int totalPointsEarned = 0;
		private int spentPoints = 0;

		public override void Initialize()
		{
			availablePoints = 0;
			totalPointsEarned = 0;
			spentPoints = 0;
			initialized = true;
		}

		public override void SaveData(Terraria.ModLoader.IO.TagCompound tag)
		{
			tag[AVAILABLE_POINTS_KEY] = availablePoints;
			tag[TOTAL_POINTS_KEY] = totalPointsEarned;
			tag[SPENT_POINTS_KEY] = spentPoints;
		}

		public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
		{
			availablePoints = tag.GetInt(AVAILABLE_POINTS_KEY);
			totalPointsEarned = tag.GetInt(TOTAL_POINTS_KEY);
			spentPoints = tag.GetInt(SPENT_POINTS_KEY

		/// <summary>
		/// Get the number of available (unspent) passive points
		/// </summary>
		public int GetAvailablePoints()
		{
			return availablePoints;
		}

		/// <summary>
		/// Get the total number of passive points earned (spent + available)
		/// </summary>
		public int GetTotalPointsEarned()
		{
			return totalPointsEarned;
		}

		/// <summary>
		/// Get the number of spent passive points
		/// </summary>
		public int GetSpentPoints()
		{
			return spentPoints;
		}

		/// <summary>
		/// Award passive points to the player (called on level up)
		/// </summary>
		/// <param name="amount">Number of points to award</param>
		public void AwardPoints(int amount)
		{
			if (amount <= 0)
				return;

			availablePoints += amount;
			totalPointsEarned += amount;
			SaveToData();
		}

		/// <summary>
		/// Spend passive points (returns true if successful)
		/// </summary>
		/// <returns>True if the player had enough points to spend</returns>
		public bool SpendPoints(int amount)
		{
			if (amount <= 0)
				return false;

			if (availablePoints < amount)
				return false;

			availablePoints -= amount;
			spentPoints += amount;
			SaveToData();
			

		/// <summary>
		/// Check if the player has enough points to spend
		/// </summary>
		public bool HasEnoughPoints(int amount)
		{
			return availablePoints >= amount;
		}

		/// <summary>
		/// Refund passive points (for respec functionality)
		/// </summary>
		/// <param name="amount">Number of points to refund</param>
		public void RefundPoints(int amount)
		{
			if (amount <= 0)
				return;

			int refundAmount = System.Math.Min(amount, spentPoints);
			availablePoints += refundAmount;
			spentPoints -= refundAmount;
			SaveToData();
		}
}

		/// <summary>
		/// Reset all passive points (refund all spent points)
		/// </summary>
		public void ResetAllPoints()
		{
			availablePoints = totalPointsEarned;
			spentPoints = 0;
		}

		/// <summary>
		/// Clear all passive points (for new character or reset)
		/// </summary>
		public void ClearAllPoints()
		{
			availablePoints = 0;
			totalPointsEarned = 0;
			spentPoints = 0
		/// <summary>
		/// Static helper: Award passive points to a player
		/// </summary>
		public static void AwardPlayerPoints(Player player, int amount)
		{
			player.GetModPlayer<PassivePointManager>().AwardPoints(amount);
		}

		/// <summary>
		/// Static helper: Get a player's available passive points
		/// </summary>
		public static int GetPlayerAvailablePoints(Player player)
		{
			return player.GetModPlayer<PassivePointManager>().GetAvailablePoints();
		}

		/// <summary>
		/// Static helper: Spend passive points for a player
		/// </summary>
		public static bool SpendPlayerPoints(Player player, int amount)
		{
			return player.GetModPlayer<PassivePointManager>().SpendPoints(amount);
		}

		/// <summary>
		/// Static helper: Check if player has enough points
		/// </summary>
		public static bool PlayerHasEnoughPoints(Player player, int amount)
		{
			return player.GetModPlayer<PassivePointManager>().HasEnoughPoints(amount);
		}
	}
}
