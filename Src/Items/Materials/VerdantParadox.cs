using Terraria;
using Terraria.ID;
using ProgressionExpanded.Items.Orbs;

namespace ProgressionExpanded.Items.Materials
{
	/// <summary>Verdant Paradox — reroll all of an item's current modifiers for a fresh set.</summary>
	public class VerdantParadox : OrbItem
	{
		public override string Texture => "ProgressionExpanded/Assets/Items/Materials/VerdantParadox";

		protected override int OrbRarity => ItemRarityID.LightRed;

		public override bool CanApplyTo(Item target) => OrbActions.ModCount(target) > 0;
		public override OrbResult Apply(Item target) => OrbActions.Reroll(target);
	}
}
