using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Tertiary
{
	/// <summary>
	/// Manages projectile speed multiplier for the player
	/// </summary>
	public class ProjectileSpeed
	{
		private readonly Player player;
		private const string DATA_KEY = "BonusProjectileSpeed";

		private float bonusProjectileSpeed = 0f;

		public ProjectileSpeed(Player player)
		{
			this.player = player;
			LoadFromData();
		}

		private void LoadFromData()
		{
			bonusProjectileSpeed = PlayerDataManager.GetFloat(player, DATA_KEY, 0f);
		}

		public void ResetEffects()
		{
			// Reset temporary bonuses (buffs/debuffs reset each frame)
			// Permanent bonuses are preserved
		}

		public void Apply()
		{
			// Projectile speed is typically handled in Shoot hooks
			// This stores the value for retrieval by projectile systems
		}

		/// <summary>
		/// Add permanent projectile speed (0.1 = 10% faster projectiles)
		/// </summary>
		public void AddProjectileSpeed(float amount)
		{
			bonusProjectileSpeed += amount;
			PlayerDataManager.SetFloat(player, DATA_KEY, bonusProjectileSpeed);
		}

		/// <summary>
		/// Get the projectile speed multiplier (1.0 = normal, 1.1 = 10% faster)
		/// </summary>
		public float GetProjectileSpeedMultiplier()
		{
			return 1f + bonusProjectileSpeed;
		}

		public float GetBonusProjectileSpeed() => bonusProjectileSpeed;
	}
}
