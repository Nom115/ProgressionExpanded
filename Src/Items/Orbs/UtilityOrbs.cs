using Terraria;
using Terraria.ID;

namespace ProgressionExpanded.Items.Orbs
{
	// NOTE: placeholder art — reuses existing orb PNGs until dedicated sprites exist.

	/// <summary>Divine Orb — reroll the numeric values of an item's modifiers.</summary>
	public class DivineOrb : OrbItem
	{
		public override string Texture => "ProgressionExpanded/Assets/Items/Materials/VerdantParadox";
		protected override int OrbRarity => ItemRarityID.Cyan;

		public override bool CanApplyTo(Item target) => OrbActions.ModCount(target) > 0;
		public override OrbResult Apply(Item target) => OrbActions.DivineReroll(target);
	}

	/// <summary>Orb of Annulment — remove one random modifier.</summary>
	public class OrbOfAnnulment : OrbItem
	{
		public override string Texture => "ProgressionExpanded/Assets/Items/Materials/TransmutationCore";
		protected override int OrbRarity => ItemRarityID.Orange;

		public override bool CanApplyTo(Item target) => OrbActions.ModCount(target) > 0;
		public override OrbResult Apply(Item target) => OrbActions.Annul(target);
	}

	/// <summary>
	/// Adds one modifier to an item below its rarity's mod cap.
	/// Displays as "Exalted Orb" (see the localization file). The class keeps its old name so existing
	/// save inventories don't lose their orbs on the rename; display name ≠ class name here.
	/// </summary>
	public class OrbOfAugmentation : OrbItem
	{
		public override string Texture => "ProgressionExpanded/Assets/Items/Materials/VerdantParadox";
		protected override int OrbRarity => ItemRarityID.Green;

		public override bool CanApplyTo(Item target)
			=> OrbActions.IsEligible(target)
			&& OrbActions.GetRarity(target) != Rarity.ItemRarity.Normal
			&& OrbActions.ModCount(target) < OrbActions.MaxModsFor(target);

		public override OrbResult Apply(Item target) => OrbActions.Augment(target);
	}
}
