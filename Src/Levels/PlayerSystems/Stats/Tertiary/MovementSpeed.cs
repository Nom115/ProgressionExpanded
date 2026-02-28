using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Tertiary
{
	/// <summary>
	/// Manages movement speed for the player
	/// </summary>
	public class MovementSpeed
	{
		private readonly Player player;
		private const string DATA_KEY = "BonusMovementSpeed";

		private float bonusMovementSpeed = 0f;

		public MovementSpeed(Player player)
		{
			this.player = player;
			LoadFromData();
		}

		private void LoadFromData()
		{
			bonusMovementSpeed = PlayerDataManager.GetFloat(player, DATA_KEY, 0f);
		}

		public void ResetEffects()
		{
			// Reset temporary bonuses (buffs/debuffs reset each frame)
			// Permanent bonuses are preserved
		}

		public void Apply()
		{
			// Apply movement speed bonus
			// moveSpeed affects running speed (0.1 = 10% faster movement)
			player.moveSpeed += bonusMovementSpeed;
		}

		/// <summary>
		/// Add permanent movement speed (0.1 = 10% faster movement)
		/// </summary>
		public void AddMovementSpeed(float amount)
		{
			bonusMovementSpeed += amount;
			PlayerDataManager.SetFloat(player, DATA_KEY, bonusMovementSpeed);
		}

		public float GetMovementSpeed() => bonusMovementSpeed;
	}
}
