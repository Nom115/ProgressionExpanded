using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Damage
{
	/// <summary>
	/// Manages critical damage multiplier for the player
	/// Vanilla crits deal 2x damage; this allows increasing that multiplier
	/// </summary>
	public class CriticalDamageMultiplier
	{
		private readonly Player player;
		private const string DATA_KEY = "BonusCritDamageMultiplier";

		private float bonusCritMultiplier = 0f;

		public CriticalDamageMultiplier(Player player)
		{
			this.player = player;
			LoadFromData();
		}

		private void LoadFromData()
		{
			bonusCritMultiplier = PlayerDataManager.GetFloat(player, DATA_KEY, 0f);
		}

		public void ResetEffects()
		{
			// Reset temporary bonuses (buffs/debuffs reset each frame)
			// Permanent bonuses are preserved
		}

		public void Apply()
		{
			// Critical damage multiplier is typically handled in ModifyHitNPC hooks
			// This stores the value for retrieval by damage calculation systems
		}

		/// <summary>
		/// Add permanent critical damage multiplier (0.5 = crits deal 2.5x instead of 2x)
		/// </summary>
		public void AddCritMultiplier(float amount)
		{
			bonusCritMultiplier += amount;
			PlayerDataManager.SetFloat(player, DATA_KEY, bonusCritMultiplier);
		}

		/// <summary>
		/// Get the total critical damage multiplier (base 2.0 + bonuses)
		/// </summary>
		public float GetTotalCritMultiplier()
		{
			return 2.0f + bonusCritMultiplier;
		}

		public float GetBonusCritMultiplier() => bonusCritMultiplier;
	}
}
