using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Damage
{
	/// <summary>
	/// Manages bonus attack speed for the player
	/// </summary>
	public class AttackSpeed
	{
		private readonly Player player;
		private const string DATA_KEY = "BonusAttackSpeed";

		private float bonusAttackSpeed = 0f;

		public AttackSpeed(Player player)
		{
			this.player = player;
			LoadFromData();
		}

		private void LoadFromData()
		{
			bonusAttackSpeed = PlayerDataManager.GetFloat(player, DATA_KEY, 0f);
		}

		public void ResetEffects()
		{
			// Reset temporary bonuses (buffs/debuffs reset each frame)
			// Permanent bonuses are preserved
		}

		public void Apply()
		{
			// Apply attack speed to all damage classes (0.1 = 10% faster attacks)
			player.GetAttackSpeed(DamageClass.Generic) += bonusAttackSpeed;
		}

		/// <summary>
		/// Add permanent attack speed (0.1 = 10% faster attacks)
		/// </summary>
		public void AddAttackSpeed(float amount)
		{
			bonusAttackSpeed += amount;
			PlayerDataManager.SetFloat(player, DATA_KEY, bonusAttackSpeed);
		}

		public float GetAttackSpeed() => bonusAttackSpeed;
	}
}
