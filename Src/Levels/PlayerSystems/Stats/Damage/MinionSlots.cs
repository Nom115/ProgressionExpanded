using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Damage
{
	/// <summary>
	/// Manages bonus minion slots for the player
	/// </summary>
	public class MinionSlots
	{
		private readonly Player player;
		private const string DATA_KEY = "BonusMinionSlots";

		private int bonusMinionSlots = 0;

		public MinionSlots(Player player)
		{
			this.player = player;
			LoadFromData();
		}

		private void LoadFromData()
		{
			bonusMinionSlots = PlayerDataManager.GetInt(player, DATA_KEY, 0);
		}

		public void ResetEffects()
		{
			// Reset temporary bonuses (buffs/debuffs reset each frame)
			// Permanent bonuses are preserved
		}

		public void Apply()
		{
			// Apply additional minion slots
			player.maxMinions += bonusMinionSlots;
		}

		/// <summary>
		/// Add permanent minion slots
		/// </summary>
		public void AddMinionSlots(int amount)
		{
			bonusMinionSlots += amount;
			PlayerDataManager.SetInt(player, DATA_KEY, bonusMinionSlots);
		}

		public int GetMinionSlots() => bonusMinionSlots;
	}
}
