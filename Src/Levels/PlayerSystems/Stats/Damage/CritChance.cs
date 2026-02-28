using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Damage
{
	/// <summary>
	/// Manages bonus critical strike chance for the player
	/// </summary>
	public class CritChance
	{
		private readonly Player player;
		private const string DATA_KEY = "BonusCritChance";

		private int bonusCritChance = 0;

		public CritChance(Player player)
		{
			this.player = player;
			LoadFromData();
		}

		private void LoadFromData()
		{
			bonusCritChance = PlayerDataManager.GetInt(player, DATA_KEY, 0);
		}

		public void ResetEffects()
		{
			// Reset temporary bonuses (buffs/debuffs reset each frame)
			// Permanent bonuses are preserved
		}

		public void Apply()
		{
			// Apply crit chance to all damage classes
			player.GetCritChance(DamageClass.Generic) += bonusCritChance;
		}

		/// <summary>
		/// Add permanent critical strike chance (1 = 1%)
		/// </summary>
		public void AddCritChance(int amount)
		{
			bonusCritChance += amount;
			PlayerDataManager.SetInt(player, DATA_KEY, bonusCritChance);
		}

		public int GetCritChance() => bonusCritChance;
	}
}
