using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ProgressionExpanded.Items.Rarity;

namespace ProgressionExpanded.Items.Orbs
{
	/// <summary>
	/// Base class for all crafting orbs. An orb is a stackable material that is consumed by the
	/// Transmutation UI when applied to an item (not "used" in the vanilla sense). Concrete orbs
	/// implement <see cref="Apply"/> (and optionally <see cref="CanApplyTo"/>) by delegating to
	/// <see cref="OrbActions"/>.
	/// </summary>
	public abstract class OrbItem : ModItem
	{
		/// <summary>Vanilla rarity colour of the orb tooltip/name. Override per orb.</summary>
		protected virtual int OrbRarity => ItemRarityID.Blue;

		/// <summary>In-world sprite scale (matches the existing orb art).</summary>
		protected virtual float OrbScale => 0.25f;

		public override void SetStaticDefaults()
		{
			Item.ResearchUnlockCount = 25;
		}

		public override void SetDefaults()
		{
			Item.width = 28;
			Item.height = 28;
			Item.maxStack = 9999;
			Item.value = 0;
			Item.rare = OrbRarity;
			Item.consumable = false;
			Item.material = true;
			Item.scale = OrbScale;
		}

		/// <summary>Apply this orb to <paramref name="target"/>. Only mutates the item on success.</summary>
		public abstract OrbResult Apply(Item target);

		/// <summary>Cheap, non-mutating precondition used to grey out invalid orbs in the UI.</summary>
		public virtual bool CanApplyTo(Item target) => OrbActions.IsEligible(target);
	}

	/// <summary>
	/// Base for the five rarity-upgrade orbs: raise an item from <see cref="FromRarity"/> to
	/// <see cref="ToRarity"/>, keeping existing modifiers and adding new distinct ones to reach the
	/// new tier's mod count.
	/// </summary>
	public abstract class UpgradeOrbItem : OrbItem
	{
		protected abstract ItemRarity FromRarity { get; }
		protected abstract ItemRarity ToRarity { get; }

		public override bool CanApplyTo(Item target)
			=> OrbActions.IsEligible(target) && OrbActions.GetRarity(target) == FromRarity;

		public override OrbResult Apply(Item target)
			=> OrbActions.UpgradeTo(target, FromRarity, ToRarity);
	}
}
