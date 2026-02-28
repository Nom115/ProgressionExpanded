using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Defences
{
	/// <summary>
	/// Manages bonus defense for the player
	/// </summary>
	public class Defense
	{
		private readonly Player player;
		private const string DATA_KEY = "BonusDefense";

		private int bonusDefense = 0;

		public Defense(Player player)
		{
			this.player = player;
			LoadFromData();
		}

		private void LoadFromData()
		{
			bonusDefense = PlayerDataManager.GetInt(player, DATA_KEY, 0);
		}

		public void ResetEffects()
		{
			// Reset temporary bonuses (buffs/debuffs reset each frame)
			// Permanent bonuses are preserved
		}

		public void Apply()
		{
			// Apply defense bonus
			player.statDefense += bonusDefense;
		}

		/// <summary>
		/// Add permanent defense
		/// </summary>
		public void AddDefense(int amount)
		{
			bonusDefense += amount;
			PlayerDataManager.SetInt(player, DATA_KEY, bonusDefense);
		}

		public int GetDefense() => bonusDefense;
	}
}
