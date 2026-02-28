using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints
{
	/// <summary>
	/// Manages player's class selection for passive trees
	/// </summary>
	public class ClassSelectionManager : ModPlayer
	{
		private const string SELECTED_CLASS_KEY = "SelectedClass";
		
		public enum PlayerClass
		{
			None,
			Melee,
			Ranged,
			Magic,
			Summoner
		}

		/// <summary>
		/// Get the player's selected class
		/// </summary>
		public static PlayerClass GetSelectedClass(Terraria.Player player)
		{
			string className = PlayerDataManager.GetString(player, SELECTED_CLASS_KEY, "None");
			if (System.Enum.TryParse<PlayerClass>(className, out PlayerClass result))
				return result;
			return PlayerClass.None;
		}

		/// <summary>
		/// Set the player's selected class
		/// </summary>
		public static void SetSelectedClass(Terraria.Player player, PlayerClass playerClass)
		{
			PlayerDataManager.SetString(player, SELECTED_CLASS_KEY, playerClass.ToString());
		}

		/// <summary>
		/// Check if player has selected a class
		/// </summary>
		public static bool HasSelectedClass(Terraria.Player player)
		{
			return GetSelectedClass(player) != PlayerClass.None;
		}

		/// <summary>
		/// Reset player's class selection
		/// </summary>
		public static void ResetClass(Terraria.Player player)
		{
			SetSelectedClass(player, PlayerClass.None);
		}

		/// <summary>
		/// Get the tree ID associated with a class
		/// </summary>
		public static string GetTreeIdForClass(PlayerClass playerClass)
		{
			return playerClass switch
			{
				PlayerClass.Melee => "warrior_tree",
				PlayerClass.Ranged => "ranger_tree",
				PlayerClass.Magic => "mage_tree",
				PlayerClass.Summoner => "summoner_tree",
				_ => null
			};
		}

		/// <summary>
		/// Get display name for a class
		/// </summary>
		public static string GetClassDisplayName(PlayerClass playerClass)
		{
			return playerClass switch
			{
				PlayerClass.Melee => "Melee",
				PlayerClass.Ranged => "Ranged",
				PlayerClass.Magic => "Magic",
				PlayerClass.Summoner => "Summoner",
				_ => "None"
			};
		}
	}
}
