using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace ProgressionExpanded.Src.Config
{
	public class ProgressionConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		[Header("XPBarSettings")]
		[DefaultValue(true)]
		public bool ShowXPBar { get; set; }

		[Range(0, 1920)]
		[DefaultValue(20)]
		public int XPBarX { get; set; }

		[Range(0, 1080)]
		[DefaultValue(80)]
		public int XPBarY { get; set; }

		[Range(0.5f, 2.0f)]
		[Increment(0.1f)]
		[DefaultValue(1.0f)]
		public float XPBarScale { get; set; }
	[Header("HealthBarSettings")]
	[DefaultValue(true)]
	public bool ShowCustomHealthBar { get; set; }

	[DefaultValue(false)]
	public bool HideVanillaHearts { get; set; }

	[Range(0, 1920)]
	[DefaultValue(20)]
	public int HealthBarX { get; set; }

	[Range(0, 1080)]
	[DefaultValue(120)]
	public int HealthBarY { get; set; }

	[Range(0.5f, 2.0f)]
	[Increment(0.1f)]
	[DefaultValue(1.0f)]
	public float HealthBarScale { get; set; }
		[Header("LevelDisplaySettings")]
		[DefaultValue(true)]
		public bool ShowPlayerLevels { get; set; }

		[DefaultValue(true)]
		public bool ShowNPCLevels { get; set; }

		[DefaultValue(false)]
		public bool ShowOnlyWhenHovered { get; set; }

		public override void OnChanged()
		{
			// Apply settings when config changes
			Src.Levels.LevelDisplay.ShowPlayerLevels = ShowPlayerLevels;
			Src.Levels.LevelDisplay.ShowNPCLevels = ShowNPCLevels;
			Src.Levels.LevelDisplay.ShowOnlyWhenHovered = ShowOnlyWhenHovered;
		}
	}
}
