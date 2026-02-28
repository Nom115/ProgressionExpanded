using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Defences
{
	/// <summary>
	/// Manages vitality stat which increases max health through PlayerHealthManager
	/// </summary>
	public class Vitality
	{
		private readonly Player player;
		private const string DATA_KEY = "BonusVitality";

		private int bonusVitality = 0;
		private const int HEALTH_PER_VITALITY = 5; // Each point of vitality = 5 max health

		public Vitality(Player player)
		{
			this.player = player;
			LoadFromData();
		}

		private void LoadFromData()
		{
			bonusVitality = PlayerDataManager.GetInt(player, DATA_KEY, 0);
		}

		public void ResetEffects()
		{
			// Reset temporary bonuses (buffs/debuffs reset each frame)
			// Permanent bonuses are preserved
		}

		public void Apply()
		{
			// Vitality increases max health through PlayerHealthManager
			// The health is added permanently when vitality is increased
			// This method exists for consistency with other stats
		}

		/// <summary>
		/// Add permanent vitality points (each point = 5 max health)
		/// </summary>
		public void AddVitality(int amount)
		{
			if (amount <= 0)
				return;

			bonusVitality += amount;
			PlayerDataManager.SetInt(player, DATA_KEY, bonusVitality);

			// Add health through PlayerHealthManager
			int healthToAdd = amount * HEALTH_PER_VITALITY;
			PlayerHealthManager healthManager = player.GetModPlayer<PlayerHealthManager>();
			healthManager.AddBonusHealth(healthToAdd);
		}

		/// <summary>
		/// Remove vitality points and corresponding health
		/// </summary>
		public void RemoveVitality(int amount)
		{
			if (amount <= 0)
				return;

			bonusVitality -= amount;
			if (bonusVitality < 0)
				bonusVitality = 0;

			PlayerDataManager.SetInt(player, DATA_KEY, bonusVitality);

			// Remove health through PlayerHealthManager
			int healthToRemove = amount * HEALTH_PER_VITALITY;
			PlayerHealthManager healthManager = player.GetModPlayer<PlayerHealthManager>();
			healthManager.RemoveBonusHealth(healthToRemove);
		}

		/// <summary>
		/// Get current vitality points
		/// </summary>
		public int GetVitality() => bonusVitality;

		/// <summary>
		/// Get total health granted by vitality
		/// </summary>
		public int GetHealthFromVitality() => bonusVitality * HEALTH_PER_VITALITY;

		/// <summary>
		/// Get health per vitality point
		/// </summary>
		public static int GetHealthPerVitality() => HEALTH_PER_VITALITY;
	}
}
