using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ProgressionExpanded.Items.Modifiers
{
	/// <summary>
	/// Categories an item can fall into for modifier-pool selection. Classification is by item
	/// <em>properties</em> (never hardcoded IDs) so it works for modded items too.
	/// </summary>
	public enum ItemCategory
	{
		None = 0,
		MeleeWeapon,
		RangedWeapon,
		MagicWeapon,
		SummonWeapon,
		Armor,
		Accessory
	}

	public static class ItemCategoryClassifier
	{
		/// <summary>
		/// Classify an item into a modifier category, or <see cref="ItemCategory.None"/> if it is
		/// not an eligible piece of equipment.
		/// </summary>
		public static ItemCategory Classify(Item item)
		{
			if (item == null || item.IsAir)
				return ItemCategory.None;

			// Ammo carries a damage value for the weapon that fires it, but is not itself a weapon.
			// This also covers coins: vanilla makes every coin Coin Gun ammo, so a Silver Coin has a
			// damage stat and would otherwise classify as a ranged weapon and roll modifiers — which
			// is exactly how a stack of currency ends up granting +4% damage.
			if (item.ammo != AmmoID.None)
				return ItemCategory.None;

			// Accessories (checked first: some accessories also carry defense).
			if (item.accessory)
				return ItemCategory.Accessory;

			// Armor: has defense and occupies a head/body/leg slot (excludes vanity-only pieces).
			if (item.defense > 0 && (item.headSlot >= 0 || item.bodySlot >= 0 || item.legSlot >= 0))
				return ItemCategory.Armor;

			// Weapons: deal damage, are actually usable, and are not tools. The useStyle test is what
			// separates a real weapon from anything that merely carries a damage number — plenty of
			// material and currency items do, and none of them are ever swung.
			if (item.damage > 0 && item.useStyle != ItemUseStyleID.None && !IsTool(item))
				return ClassifyWeapon(item);

			return ItemCategory.None;
		}

		public static bool IsEligible(Item item) => Classify(item) != ItemCategory.None;

		private static bool IsTool(Item item)
		{
			return item.pick > 0 || item.axe > 0 || item.hammer > 0 || item.fishingPole > 0;
		}

		private static ItemCategory ClassifyWeapon(Item item)
		{
			// CountsAsClass handles hybrid/modded damage classes robustly.
			if (item.CountsAsClass(DamageClass.Melee))
				return ItemCategory.MeleeWeapon;
			if (item.CountsAsClass(DamageClass.Ranged))
				return ItemCategory.RangedWeapon;
			if (item.CountsAsClass(DamageClass.Magic))
				return ItemCategory.MagicWeapon;
			if (item.CountsAsClass(DamageClass.Summon))
				return ItemCategory.SummonWeapon;

			// Unknown/generic/thrown damage types fall back to the melee pool.
			return ItemCategory.MeleeWeapon;
		}

		/// <summary>Maps a category to the JSON pool id it rolls from.</summary>
		public static string GetPoolId(ItemCategory category)
		{
			return category switch
			{
				ItemCategory.MeleeWeapon => "melee_weapon",
				ItemCategory.RangedWeapon => "ranged_weapon",
				ItemCategory.MagicWeapon => "magic_weapon",
				ItemCategory.SummonWeapon => "summon_weapon",
				ItemCategory.Armor => "armor",
				ItemCategory.Accessory => "accessory",
				_ => null
			};
		}

		/// <summary>Convenience: the pool id an item rolls from, or null if ineligible.</summary>
		public static string GetPoolId(Item item) => GetPoolId(Classify(item));
	}
}
