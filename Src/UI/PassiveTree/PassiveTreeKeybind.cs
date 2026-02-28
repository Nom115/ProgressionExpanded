using Terraria.ModLoader;

namespace ProgressionExpanded.Src.UI.PassiveTree
{
	/// <summary>
	/// Keybind for toggling the passive tree UI
	/// </summary>
	public class PassiveTreeKeybind : ModSystem
	{
		public static ModKeybind TogglePassiveTreeKey { get; private set; }

		public override void Load()
		{
			TogglePassiveTreeKey = KeybindLoader.RegisterKeybind(Mod, "Toggle Passive Tree", "P");
		}

		public override void Unload()
		{
			TogglePassiveTreeKey = null;
		}
	}
}
