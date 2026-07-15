using Microsoft.Xna.Framework;

namespace ProgressionExpanded.Items.Rarity
{
	/// <summary>
	/// Rarity tiers for equippable items. Mirrors the enemy-side <c>EnemyRarity</c>, but on items
	/// the tier drives how many modifiers the item carries (the fixed ladder below).
	/// </summary>
	public enum ItemRarity
	{
		Normal = 0,     // no modifiers (white name)
		Uncommon = 1,   // 1 modifier (green)
		Magic = 2,      // 2 modifiers (blue)
		Rare = 3,       // 3 modifiers (yellow)
		Mythic = 4,     // 4 modifiers (orange)
		Legendary = 5   // 6 modifiers (red)
	}

	/// <summary>
	/// Display + gameplay configuration for each item rarity tier.
	/// </summary>
	public class ItemRarityInfo
	{
		public string Name { get; set; }
		public Color Color { get; set; }
		public int ModCount { get; set; }
	}

	/// <summary>
	/// Static config for item rarity tiers. The mod-count ladder is the design-locked
	/// 0 / 1 / 2 / 3 / 4 / 6.
	/// </summary>
	public static class ItemRarityConfig
	{
		public static ItemRarityInfo GetInfo(ItemRarity rarity)
		{
			return rarity switch
			{
				ItemRarity.Normal => new ItemRarityInfo
				{
					Name = "Normal",
					Color = new Color(200, 200, 200),
					ModCount = 0
				},
				ItemRarity.Uncommon => new ItemRarityInfo
				{
					Name = "Uncommon",
					Color = new Color(100, 255, 100),
					ModCount = 1
				},
				ItemRarity.Magic => new ItemRarityInfo
				{
					Name = "Magic",
					Color = new Color(100, 150, 255),
					ModCount = 2
				},
				ItemRarity.Rare => new ItemRarityInfo
				{
					Name = "Rare",
					Color = new Color(255, 235, 100),
					ModCount = 3
				},
				ItemRarity.Mythic => new ItemRarityInfo
				{
					Name = "Mythic",
					Color = new Color(255, 150, 50),
					ModCount = 4
				},
				ItemRarity.Legendary => new ItemRarityInfo
				{
					Name = "Legendary",
					Color = new Color(255, 80, 80),
					ModCount = 6
				},
				_ => GetInfo(ItemRarity.Normal)
			};
		}

		/// <summary>Number of modifiers an item of this rarity should carry.</summary>
		public static int GetModCount(ItemRarity rarity) => GetInfo(rarity).ModCount;

		/// <summary>Clamp an arbitrary int (e.g. loaded from a save) into a valid rarity.</summary>
		public static ItemRarity FromInt(int value)
		{
			if (value < (int)ItemRarity.Normal) return ItemRarity.Normal;
			if (value > (int)ItemRarity.Legendary) return ItemRarity.Legendary;
			return (ItemRarity)value;
		}

		/// <summary>The next rarity up, or the same tier if already Legendary.</summary>
		public static ItemRarity Next(ItemRarity rarity)
		{
			return rarity >= ItemRarity.Legendary ? ItemRarity.Legendary : (ItemRarity)((int)rarity + 1);
		}
	}
}
