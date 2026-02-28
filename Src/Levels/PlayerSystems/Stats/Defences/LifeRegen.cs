using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Defences
{
	/// <summary>
	/// Manages bonus life regeneration for the player
	/// </summary>
	public class LifeRegen
	{
		private readonly Player player;
		private const string DATA_KEY = "BonusLifeRegen";

		private int bonusLifeRegen = 0;

		public LifeRegen(Player player)
		{
			this.player = player;
			LoadFromData();
		}

		private void LoadFromData()
		{
			bonusLifeRegen = PlayerDataManager.GetInt(player, DATA_KEY, 0);
		}

		public void ResetEffects()
		{
			// Reset temporary bonuses (buffs/debuffs reset each frame)
			// Permanent bonuses are preserved
		}

		public void Apply()
		{
			// Apply life regeneration bonus
			// In Terraria, lifeRegen is measured in 1/2 health per second when positive
			// Each point of lifeRegen = 0.5 health/second
			player.lifeRegen += bonusLifeRegen;
		}

		/// <summary>
		/// Add permanent life regeneration (2 = 1 health/second)
		/// </summary>
		public void AddLifeRegen(int amount)
		{
			bonusLifeRegen += amount;
			PlayerDataManager.SetInt(player, DATA_KEY, bonusLifeRegen);
		}

		public int GetLifeRegen() => bonusLifeRegen;

		/// <summary>
		/// Get life regeneration in health per second
		/// </summary>
		public float GetLifeRegenPerSecond()
		{
			return bonusLifeRegen / 2f;
		}
	}
}
