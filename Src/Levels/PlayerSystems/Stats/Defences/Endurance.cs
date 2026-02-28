using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Defences
{
	/// <summary>
	/// Manages endurance (damage reduction) for the player
	/// </summary>
	public class Endurance
	{
		private readonly Player player;
		private const string DATA_KEY = "BonusEndurance";

		private float bonusEndurance = 0f;

		public Endurance(Player player)
		{
			this.player = player;
			LoadFromData();
		}

		private void LoadFromData()
		{
			bonusEndurance = PlayerDataManager.GetFloat(player, DATA_KEY, 0f);
		}

		public void ResetEffects()
		{
			// Reset temporary bonuses (buffs/debuffs reset each frame)
			// Permanent bonuses are preserved
		}

		public void Apply()
		{
			// Apply endurance (damage reduction)
			// 0.1 = 10% damage reduction
			player.endurance += bonusEndurance;
			
			// Cap endurance at 1.0 (100% reduction) to prevent negative damage
			if (player.endurance > 1f)
				player.endurance = 1f;
		}

		/// <summary>
		/// Add permanent endurance (0.1 = 10% damage reduction)
		/// </summary>
		public void AddEndurance(float amount)
		{
			bonusEndurance += amount;
			PlayerDataManager.SetFloat(player, DATA_KEY, bonusEndurance);
		}

		public float GetEndurance() => bonusEndurance;
	}
}
