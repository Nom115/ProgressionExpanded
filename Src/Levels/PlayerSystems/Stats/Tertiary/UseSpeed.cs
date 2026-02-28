using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Tertiary
{
	/// <summary>
	/// Manages use speed (item use time reduction) for the player
	/// </summary>
	public class UseSpeed
	{
		private readonly Player player;
		private const string DATA_KEY = "BonusUseSpeed";

		private float bonusUseSpeed = 0f;

		public UseSpeed(Player player)
		{
			this.player = player;
			LoadFromData();
		}

		private void LoadFromData()
		{
			bonusUseSpeed = PlayerDataManager.GetFloat(player, DATA_KEY, 0f);
		}

		public void ResetEffects()
		{
			// Reset temporary bonuses (buffs/debuffs reset each frame)
			// Permanent bonuses are preserved
		}

		public void Apply()
		{
			// Apply use speed bonus (reduces item use time)
			// This works similarly to attack speed but applies to all items
			// Note: Terraria doesn't have a direct "useSpeed" stat like attack speed
			// This can be applied in item use hooks or through itemAnimation/itemTime modifications
			
			// For general use speed, we can use attack speed as a proxy
			player.GetAttackSpeed(DamageClass.Generic) += bonusUseSpeed;
		}

		/// <summary>
		/// Add permanent use speed (0.1 = 10% faster item use)
		/// </summary>
		public void AddUseSpeed(float amount)
		{
			bonusUseSpeed += amount;
			PlayerDataManager.SetFloat(player, DATA_KEY, bonusUseSpeed);
		}

		public float GetUseSpeed() => bonusUseSpeed;
	}
}
