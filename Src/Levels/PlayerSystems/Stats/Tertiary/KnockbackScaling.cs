using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Tertiary
{
	/// <summary>
	/// Manages knockback scaling for the player
	/// </summary>
	public class KnockbackScaling
	{
		private readonly Player player;
		private const string DATA_KEY = "BonusKnockbackScaling";

		private float bonusKnockback = 0f;

		public KnockbackScaling(Player player)
		{
			this.player = player;
			LoadFromData();
		}

		private void LoadFromData()
		{
			bonusKnockback = PlayerDataManager.GetFloat(player, DATA_KEY, 0f);
		}

		public void ResetEffects()
		{
			// Reset temporary bonuses (buffs/debuffs reset each frame)
			// Permanent bonuses are preserved
		}

		public void Apply()
		{
			// Apply knockback multiplier (0.1 = 10% more knockback)
			player.GetKnockback(DamageClass.Generic) += bonusKnockback;
		}

		/// <summary>
		/// Add permanent knockback scaling (0.1 = 10% more knockback)
		/// </summary>
		public void AddKnockback(float amount)
		{
			bonusKnockback += amount;
			PlayerDataManager.SetFloat(player, DATA_KEY, bonusKnockback);
		}

		public float GetKnockback() => bonusKnockback;
	}
}
