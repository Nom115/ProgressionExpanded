using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Damage
{
	/// <summary>
	/// Manages bonus melee damage for the player
	/// </summary>
	public class MeleeDamage
	{
		private readonly Player player;
		private const string DATA_KEY = "BonusMeleeDamage";

		private float bonusDamageFlat = 0f;
		private float bonusDamageMultiplier = 0f;

		public MeleeDamage(Player player)
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
			// Apply flat damage bonus
			player.GetDamage(DamageClass.Melee).Flat += bonusDamageFlat;
			
			// Apply multiplicative damage bonus
			player.GetDamage(DamageClass.Melee) *= 1f + bonusDamageMultiplier;
		}

		/// <summary>
		/// Add permanent flat melee damage
		/// </summary>
		public void AddFlatDamage(float amount)
		{
			bonusDamageFlat += amount;
			PlayerDataManager.SetFloat(player, DATA_KEY + "_Flat", bonusDamageFlat);
		}

		/// <summary>
		/// Add permanent melee damage multiplier (0.1 = 10% more damage)
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
