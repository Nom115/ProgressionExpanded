using Terraria;

namespace ProgressionExpanded.Src.NPCs.Enemy
{
	/// <summary>
	/// Defines rarity tiers for enemies, affecting their stats, drops, and appearance
	/// </summary>
	public enum EnemyRarity
	{
		Common = 0,      // Normal enemies (white name)
		Uncommon = 1,    // Slightly stronger (green name)
		Rare = 2,        // Notable challenge (blue name)
		Epic = 3,        // Significant threat (purple name)
		Legendary = 4,   // Extremely dangerous (orange name)
		Mythic = 5       // Ultimate challenge (red name)
	}

	/// <summary>
	/// Configuration for each rarity tier
	/// </summary>
	public static class EnemyRarityConfig
	{
		public static RarityInfo GetRarityInfo(EnemyRarity rarity)
		{
			return rarity switch
			{
				EnemyRarity.Common => new RarityInfo
				{
					Name = "",
					Color = new Microsoft.Xna.Framework.Color(255, 255, 255),
					StatMultiplier = 1.0f,
					DropChanceMultiplier = 1.0f,
					DropQuantityMultiplier = 1.0f,
					XPMultiplier = 1.0f,
					SpawnChance = 0.70f, // 70% spawn rate
					MaxModifiers = 0
				},
				EnemyRarity.Uncommon => new RarityInfo
				{
					Name = "Uncommon",
					Color = new Microsoft.Xna.Framework.Color(100, 255, 100),
					StatMultiplier = 1.3f,
					DropChanceMultiplier = 1.2f,
					DropQuantityMultiplier = 1.2f,
					XPMultiplier = 1.3f,
					SpawnChance = 0.20f, // 20% spawn rate
					MaxModifiers = 1
				},
				EnemyRarity.Rare => new RarityInfo
				{
					Name = "Rare",
					Color = new Microsoft.Xna.Framework.Color(100, 150, 255),
					StatMultiplier = 1.6f,
					DropChanceMultiplier = 1.5f,
					DropQuantityMultiplier = 1.5f,
					XPMultiplier = 1.6f,
					SpawnChance = 0.07f, // 7% spawn rate
					MaxModifiers = 2
				},
				EnemyRarity.Epic => new RarityInfo
				{
					Name = "Epic",
					Color = new Microsoft.Xna.Framework.Color(200, 100, 255),
					StatMultiplier = 2.0f,
					DropChanceMultiplier = 2.0f,
					DropQuantityMultiplier = 2.0f,
					XPMultiplier = 2.0f,
					SpawnChance = 0.02f, // 2% spawn rate
					MaxModifiers = 3
				},
				EnemyRarity.Legendary => new RarityInfo
				{
					Name = "Legendary",
					Color = new Microsoft.Xna.Framework.Color(255, 165, 0),
					StatMultiplier = 2.5f,
					DropChanceMultiplier = 2.5f,
					DropQuantityMultiplier = 2.5f,
					XPMultiplier = 2.5f,
					SpawnChance = 0.008f, // 0.8% spawn rate
					MaxModifiers = 4
				},
				EnemyRarity.Mythic => new RarityInfo
				{
					Name = "Mythic",
					Color = new Microsoft.Xna.Framework.Color(255, 50, 50),
					StatMultiplier = 3.0f,
					DropChanceMultiplier = 3.0f,
					DropQuantityMultiplier = 3.0f,
					XPMultiplier = 3.0f,
					SpawnChance = 0.002f, // 0.2% spawn rate
					MaxModifiers = 5
				},
				_ => GetRarityInfo(EnemyRarity.Common)
			};
		}

		/// <summary>
		/// Roll random rarity based on spawn chances
		/// </summary>
		public static EnemyRarity RollRarity()
		{
			float roll = Main.rand.NextFloat(1f);
			float cumulative = 0f;

			// Check from highest to lowest rarity
			for (int i = (int)EnemyRarity.Mythic; i >= 0; i--)
			{
				var rarity = (EnemyRarity)i;
				var info = GetRarityInfo(rarity);
				cumulative += info.SpawnChance;

				if (roll < cumulative)
					return rarity;
			}

			return EnemyRarity.Common;
		}
	}

	/// <summary>
	/// Information about a rarity tier
	/// </summary>
	public class RarityInfo
	{
		public string Name { get; set; }
		public Microsoft.Xna.Framework.Color Color { get; set; }
		public float StatMultiplier { get; set; }
		public float DropChanceMultiplier { get; set; }
		public float DropQuantityMultiplier { get; set; }
		public float XPMultiplier { get; set; }
		public float SpawnChance { get; set; }
		public int MaxModifiers { get; set; }
	}
}
