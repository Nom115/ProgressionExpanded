using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader;
using ProgressionExpanded.Utils;
using ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints;

namespace ProgressionExpanded
{
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class ProgressionExpanded : Mod
	{
		public override void Load()
		{
			// Initialize mod bridge to check for other mods
			ModBridge.Initialize();
			
			// Load passive trees from JSON
			PassiveTreeLoader.LoadAllTrees(this);
		}

		public override void Unload()
		{
			// Clean up mod references
			ModBridge.Unload();
			
			// Unload passive trees
			PassiveTreeLoader.UnloadTrees();
		}
	}
}
