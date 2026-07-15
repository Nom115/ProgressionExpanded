using Terraria.ID;
using ProgressionExpanded.Items.Orbs;
using ProgressionExpanded.Items.Rarity;

namespace ProgressionExpanded.Items.Materials
{
	/// <summary>Normal → Uncommon (rolls the item's first modifier). The earliest crafting orb.</summary>
	public class OrbOfTransmutation : UpgradeOrbItem
	{
		public override string Texture => "ProgressionExpanded/Assets/Items/Materials/OrbOfTransmutation";

		protected override int OrbRarity => ItemRarityID.Green;
		protected override ItemRarity FromRarity => ItemRarity.Normal;
		protected override ItemRarity ToRarity => ItemRarity.Uncommon;
	}
}
