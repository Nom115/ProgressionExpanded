using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems
{
	/// <summary>
	/// Manages player health beyond vanilla caps (400 base + 100 life fruit = 500 max)
	/// Allows players to gain additional max health that persists across sessions
	/// </summary>
	public class PlayerHealthManager : ModPlayer
	{
		private const int VANILLA_MAX_HEALTH = 400;
		private const string BONUS_HEALTH_KEY = "BonusMaxHealth";

		// Cached bonus health for this session
		private int cachedBonusHealth = 0;
		private bool initialized = false;

		public override void Initialize()
		{
			// Load saved bonus health when player is initialized
			cachedBonusHealth = PlayerDataManager.GetInt(Player, BONUS_HEALTH_KEY, 0);
			initialized = false; // Will be set true in first PostUpdateMiscEffects
		}

		public override void PostUpdateMiscEffects()
		{
			// Apply bonus health every frame to ensure it persists
			// This prevents vanilla caps from overriding our bonus
			if (!initialized)
			{
				// On first update, ensure health is loaded and applied
				cachedBonusHealth = PlayerDataManager.GetInt(Player, BONUS_HEALTH_KEY, 0);
				initialized = true;
			}

			// Apply bonus health to max health
			if (cachedBonusHealth > 0)
			{
				Player.statLifeMax2 += cachedBonusHealth;
			}
		}

		public override void ModifyMaxStats(out StatModifier health, out StatModifier mana)
		{
			// Allow health to exceed vanilla caps
			health = StatModifier.Default;
			mana = StatModifier.Default;

			// Add bonus health as a flat modifier
			if (cachedBonusHealth > 0)
			{
				health.Flat += cachedBonusHealth;
			}
		}

		/// <summary>
		/// Get the current bonus health (health beyond vanilla cap)
		/// </summary>
		public int GetBonusHealth()
		{
			return cachedBonusHealth;
		}

		/// <summary>
		/// Get total maximum health including bonuses
		/// </summary>
		public int GetTotalMaxHealth()
		{
			return Player.statLifeMax2;
		}

		/// <summary>
		/// Add bonus max health beyond vanilla caps
		/// </summary>
		/// <param name="amount">Amount of health to add</param>
		public void AddBonusHealth(int amount)
		{
			if (amount <= 0)
				return;

			cachedBonusHealth += amount;
			PlayerDataManager.SetInt(Player, BONUS_HEALTH_KEY, cachedBonusHealth);

			// Immediately apply the health increase
			Player.statLifeMax2 += amount;
			
			// Heal player for the added amount to prevent them from being at low % health
			Player.statLife += amount;
			if (Player.statLife > Player.statLifeMax2)
				Player.statLife = Player.statLifeMax2;
		}

		/// <summary>
		/// Set bonus health to a specific value (replaces current bonus)
		/// </summary>
		/// <param name="amount">New bonus health amount</param>
		public void SetBonusHealth(int amount)
		{
			if (amount < 0)
				amount = 0;

			int difference = amount - cachedBonusHealth;
			cachedBonusHealth = amount;
			PlayerDataManager.SetInt(Player, BONUS_HEALTH_KEY, cachedBonusHealth);

			// Adjust current health proportionally if reducing
			if (difference < 0)
			{
				// Reducing max health - keep current health if possible
				if (Player.statLife > Player.statLifeMax2 + difference)
					Player.statLife = Player.statLifeMax2 + difference;
			}
		}

		/// <summary>
		/// Remove bonus health (useful for penalties or resets)
		/// </summary>
		/// <param name="amount">Amount of bonus health to remove</param>
		public void RemoveBonusHealth(int amount)
		{
			if (amount <= 0)
				return;

			cachedBonusHealth -= amount;
			if (cachedBonusHealth < 0)
				cachedBonusHealth = 0;

			PlayerDataManager.SetInt(Player, BONUS_HEALTH_KEY, cachedBonusHealth);

			// Reduce current health if it exceeds new max
			int newMaxHealth = Player.statLifeMax2 - amount;
			if (Player.statLife > newMaxHealth)
				Player.statLife = newMaxHealth;
		}

		/// <summary>
		/// Reset bonus health to 0
		/// </summary>
		public void ResetBonusHealth()
		{
			SetBonusHealth(0);
		}

		/// <summary>
		/// Check if player has any bonus health
		/// </summary>
		public bool HasBonusHealth()
		{
			return cachedBonusHealth > 0;
		}

		/// <summary>
		/// Get the percentage of health above vanilla cap
		/// </summary>
		public float GetBonusHealthPercentage()
		{
			if (Player.statLifeMax2 <= VANILLA_MAX_HEALTH)
				return 0f;

			return (float)cachedBonusHealth / Player.statLifeMax2;
		}

		/// <summary>
		/// Static helper: Add bonus health to a player
		/// </summary>
		public static void AddPlayerBonusHealth(Player player, int amount)
		{
			player.GetModPlayer<PlayerHealthManager>().AddBonusHealth(amount);
		}

		/// <summary>
		/// Static helper: Get a player's current bonus health
		/// </summary>
		public static int GetPlayerBonusHealth(Player player)
		{
			return player.GetModPlayer<PlayerHealthManager>().GetBonusHealth();
		}

		/// <summary>
		/// Static helper: Get a player's total max health
		/// </summary>
		public static int GetPlayerTotalMaxHealth(Player player)
		{
			return player.GetModPlayer<PlayerHealthManager>().GetTotalMaxHealth();
		}
	}
}
