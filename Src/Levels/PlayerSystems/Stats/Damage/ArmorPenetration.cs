using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Damage
{
	/// <summary>
	/// Manages armor penetration for the player
	/// </summary>
	public class ArmorPenetration
	{
		private readonly Player player;
		private const string DATA_KEY = "BonusArmorPenetration";

		private int bonusArmorPenetration = 0;

		public ArmorPenetration(Player player)
		{
			this.player = player;
			LoadFromData();
		}

		private void LoadFromData()
		{
			bonusArmorPenetration = PlayerDataManager.GetInt(player, DATA_KEY, 0);
		}

		public void ResetEffects()
		{
			// Reset temporary bonuses (buffs/debuffs reset each frame)
			// Permanent bonuses are preserved
		}

		public void Apply()
		{
			// Apply armor penetration to all damage classes
			player.GetArmorPenetration(DamageClass.Generic) += bonusArmorPenetration;
		}

		/// <summary>
		/// Add permanent armor penetration
		/// </summary>
		public void AddArmorPenetration(int amount)
		{
			bonusArmorPenetration += amount;
			PlayerDataManager.SetInt(player, DATA_KEY, bonusArmorPenetration);
		}

		public int GetArmorPenetration() => bonusArmorPenetration;
	}
}
