using Microsoft.Xna.Framework;
using Terraria.GameContent.UI.ResourceSets;
using Terraria.ModLoader;
using ProgressionExpanded.Src.Config;

namespace ProgressionExpanded.Src.UI
{
	/// <summary>
	/// Suppresses only the vanilla life and/or mana displays. The breath bar, buff/debuff icons and
	/// golf power share the same "Vanilla: Resource Bars" interface layer, so they keep drawing in
	/// their default vanilla positions.
	/// </summary>
	public class VanillaResourceOverlay : ModResourceOverlay
	{
		/// <param name="drawingLife">true for the life display, false for the mana display.</param>
		private static bool ShouldHide(bool drawingLife)
		{
			var config = ModContent.GetInstance<ProgressionConfig>();
			if (config == null)
				return false;

			// Each vanilla display only hides while the custom bar is actually there to replace it, so
			// the two toggles can't combine into no readout at all.
			return drawingLife
				? config.HideVanillaHearts && config.ShowCustomHealthBar
				: config.HideVanillaMana && config.ShowCustomManaBar;
		}

		public override bool PreDrawResourceDisplay(PlayerStatsSnapshot snapshot, IPlayerResourcesDisplaySet displaySet, bool drawingLife, ref Color textColor, out bool drawText)
		{
			if (ShouldHide(drawingLife))
			{
				drawText = false;
				return false;
			}

			drawText = true;
			return true;
		}

		// Without this, a hidden display still shows its "Life: 400/400" tooltip on hover.
		public override bool DisplayHoverText(PlayerStatsSnapshot snapshot, IPlayerResourcesDisplaySet displaySet, bool drawingLife)
		{
			return !ShouldHide(drawingLife);
		}
	}
}
