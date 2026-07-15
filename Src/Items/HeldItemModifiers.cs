using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Items.Modifiers;

namespace ProgressionExpanded.Items
{
	/// <summary>
	/// Applies the handful of weapon modifiers that vanilla only exposes player-side (attack speed,
	/// mana cost, armour penetration) for the currently held weapon. Runs in
	/// <see cref="PostUpdateEquips"/>, which is before item-use processing, so the values are set in
	/// time for the swing. Damage / crit / knockback are handled per-hit by
	/// <c>ItemModifierSystem.ModifyWeapon*</c> instead.
	/// </summary>
	public class HeldItemModifiers : ModPlayer
	{
		public override void PostUpdateEquips()
		{
			Item item = Player.HeldItem;
			if (item == null || item.IsAir)
				return;

			var state = ItemModifierSystem.GetState(item);
			if (state == null || !state.HasRolled)
				return;

			ItemModifierApplier.ApplyHeldWeaponStats(Player, item, state.GetMods());
		}
	}
}
