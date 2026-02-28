using Terraria;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.UI.PassiveTree
{
	/// <summary>
	/// ModPlayer that handles keybind input for passive tree
	/// </summary>
	public class PassiveTreeInputHandler : ModPlayer
	{
		public override void PostUpdate()
		{
			if (PassiveTreeKeybind.TogglePassiveTreeKey.JustPressed)
			{
				PassiveTreeUISystem.Instance?.ToggleUI();
			}
		}
	}
}
