using Terraria.ID;
using ProgressionExpanded.Items.Rarity;

namespace ProgressionExpanded.Items.Orbs
{
	// NOTE: new orbs reuse existing PNGs as placeholder art — swap in dedicated sprites later.
	// (Orb of Transmutation itself lives in Items/Materials and is retrofitted there.)

	/// <summary>
	/// Uncommon → Magic — the tier-upgrade orb used right after Transmutation.
	/// Displays as "Orb of Augmentation" (see the localization file). The class keeps its old name so
	/// existing save inventories don't lose their orbs on the rename; display name ≠ class name here.
	/// </summary>
	public class OrbOfAlteration : UpgradeOrbItem
	{
		public override string Texture => "ProgressionExpanded/Assets/Items/Materials/OrbOfTransmutation";
		protected override int OrbRarity => ItemRarityID.Blue;
		protected override ItemRarity FromRarity => Rarity.ItemRarity.Uncommon;
		protected override ItemRarity ToRarity => Rarity.ItemRarity.Magic;
	}

	/// <summary>Magic → Rare.</summary>
	public class RegalOrb : UpgradeOrbItem
	{
		public override string Texture => "ProgressionExpanded/Assets/Items/Materials/OrbOfTransmutation";
		protected override int OrbRarity => ItemRarityID.Pink;
		protected override ItemRarity FromRarity => Rarity.ItemRarity.Magic;
		protected override ItemRarity ToRarity => Rarity.ItemRarity.Rare;
	}

	/// <summary>Rare → Mythic.</summary>
	public class OrbOfAscendance : UpgradeOrbItem
	{
		public override string Texture => "ProgressionExpanded/Assets/Items/Materials/OrbOfTransmutation";
		protected override int OrbRarity => ItemRarityID.Yellow;
		protected override ItemRarity FromRarity => Rarity.ItemRarity.Rare;
		protected override ItemRarity ToRarity => Rarity.ItemRarity.Mythic;
	}

	/// <summary>Mythic → Legendary (gated to mid-hardmode by its drop rules).</summary>
	public class OrbOfCataclysm : UpgradeOrbItem
	{
		public override string Texture => "ProgressionExpanded/Assets/Items/Materials/OrbOfTransmutation";
		protected override int OrbRarity => ItemRarityID.Red;
		protected override ItemRarity FromRarity => Rarity.ItemRarity.Mythic;
		protected override ItemRarity ToRarity => Rarity.ItemRarity.Legendary;
	}
}
