using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Damage
{
	/// <summary>
	/// Manages generic damage bonus that applies to all damage types
	/// </summary>
	public class GenericDamage
	{
		private readonly Player player;
		private const string DATA_KEY = "BonusGenericDamage";

		private float bonusDamageFlat = 0f;
		private float bonusDamageMultiplier = 0f;

		public GenericDamage(Player player)
		{
			this.player = player;
			LoadFromData();
		}

		private void LoadFromData()
		{
			bonusDamageFlat = PlayerDataManager.GetFloat(player, DATA_KEY + "_Flat", 0f);
			bonusDamageMultiplier = PlayerDataManager.GetFloat(player, DATA_KEY + "_Mult", 0f);
		}

		public void ResetEffects()
		{
			// Reset temporary bonuses (buffs/debuffs reset each frame)
			// Permanent bonuses are preserved
		}

		public void Apply()
		{
			// Apply flat damage bonus to all damage types
			player.GetDamage(DamageClass.Generic).Flat += bonusDamageFlat;
			
			// Apply multiplicative damage bonus to all damage types
			player.GetDamage(DamageClass.Generic) *= 1f + bonusDamageMultiplier;
		}

		/// <summary>
		/// Add permanent flat generic damage
		/// </summary>
		public void AddFlatDamage(float amount)
		{
			bonusDamageFlat += amount;
			PlayerDataManager.SetFloat(player, DATA_KEY + "_Flat", bonusDamageFlat);
		}

		/// <summary>
		/// Add permanent generic damage multiplier (0.1 = 10% more damage)
		/// </summary>
		public void AddMultiplier(float amount)
		{
			bonusDamageMultiplier += amount;
			PlayerDataManager.SetFloat(player, DATA_KEY + "_Mult", bonusDamageMultiplier);
		}

		public float GetFlatDamage() => bonusDamageFlat;
		public float GetMultiplier() => bonusDamageMultiplier;
	}
}
