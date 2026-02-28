using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Damage
{
	/// <summary>
	/// Manages mana cost reduction for the player
	/// </summary>
	public class ManaEfficiency
	{
		private readonly Player player;
		private const string DATA_KEY = "BonusManaEfficiency";

		private float manaReduction = 0f;

		public ManaEfficiency(Player player)
		{
			this.player = player;
			LoadFromData();
		}

		private void LoadFromData()
		{
			manaReduction = PlayerDataManager.GetFloat(player, DATA_KEY, 0f);
		}

		public void ResetEffects()
		{
			// Reset temporary bonuses (buffs/debuffs reset each frame)
			// Permanent bonuses are preserved
		}

		public void Apply()
		{
			// Apply mana cost reduction (0.1 = 10% less mana cost)
			player.manaCost -= manaReduction;
			
			// Ensure mana cost doesn't go below 0
			if (player.manaCost < 0f)
				player.manaCost = 0f;
		}

		/// <summary>
		/// Add permanent mana efficiency (0.1 = 10% less mana cost)
		/// </summary>
		public void AddManaEfficiency(float amount)
		{
			manaReduction += amount;
			PlayerDataManager.SetFloat(player, DATA_KEY, manaReduction);
		}

		public float GetManaEfficiency() => manaReduction;
	}
}
