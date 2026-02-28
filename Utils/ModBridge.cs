using Terraria.ModLoader;

namespace ProgressionExpanded.Utils
{
	public static class ModBridge
	{
		// Mod references
		public static Mod CalamityMod { get; private set; }
		public static Mod FargosSoulsMod { get; private set; }
		public static Mod FargosMutantMod { get; private set; }

		// Flags for quick checking
		public static bool CalamityLoaded { get; private set; }
		public static bool FargosSoulsLoaded { get; private set; }
		public static bool FargosMutantLoaded { get; private set; }

		/// <summary>
		/// Initialize mod bridge - checks which mods are loaded
		/// Call this in your Mod.Load() or Mod.PostSetupContent()
		/// </summary>
		public static void Initialize()
		{
			// Check for Calamity Mod
			CalamityLoaded = ModLoader.TryGetMod("CalamityMod", out Mod calamity);
			if (CalamityLoaded)
			{
				CalamityMod = calamity;
			}

			// Check for Fargo's Souls Mod
			FargosSoulsLoaded = ModLoader.TryGetMod("FargowiltasSouls", out Mod fargoSouls);
			if (FargosSoulsLoaded)
			{
				FargosSoulsMod = fargoSouls;
			}

			// Check for Fargo's Mutant Mod
			FargosMutantLoaded = ModLoader.TryGetMod("Fargowiltas", out Mod fargoMutant);
			if (FargosMutantLoaded)
			{
				FargosMutantMod = fargoMutant;
			}
		}

		/// <summary>
		/// Unload mod references to prevent memory leaks
		/// Call this in your Mod.Unload()
		/// </summary>
		public static void Unload()
		{
			CalamityMod = null;
			FargosSoulsMod = null;
			FargosMutantMod = null;
			CalamityLoaded = false;
			FargosSoulsLoaded = false;
			FargosMutantLoaded = false;
		}
	}
}
