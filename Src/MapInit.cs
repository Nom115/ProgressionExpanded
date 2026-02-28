using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;
using ProgressionExpanded.Utils;

namespace ProgressionExpanded.Src
{
/// <summary>
/// Handles initialization and cleanup when worlds (maps) are loaded or unloaded.
/// Central point for managing data managers and world state.
/// </summary>
public class MapInit : ModSystem
{
// Track if we've initialized the current world
private static bool isWorldInitialized = false;
private static bool showWelcomeMessage = false;
private static int frameCounter = 0;

/// <summary>
/// Called when entering a world or creating a new one
/// Use this to set up default values or validate world data
/// </summary>
public override void OnWorldLoad()
{
// Initialize default world data if needed
InitializeDefaultWorldData();

// Validate existing world data
ValidateWorldData();

isWorldInitialized = true;
showWelcomeMessage = true;
frameCounter = 0;
}

/// <summary>
/// Called every frame - use to show messages after world is fully loaded
/// </summary>
public override void PostUpdateWorld()
{
if (showWelcomeMessage)
{
frameCounter++;
// Wait a few frames for the game to be fully ready
if (frameCounter > 10 && !Main.gameMenu)
{
showWelcomeMessage = false;
InChatAlerts.Info("World loaded successfully!");

// Testing: Give player bonus health if below 2000
var player = Main.LocalPlayer;
if (player != null && player.active)
{
var healthManager = player.GetModPlayer<Src.Levels.PlayerSystems.PlayerHealthManager>();
int currentMaxHP = healthManager.GetTotalMaxHealth();

if (currentMaxHP < 2000)
{
int healthToAdd = 2000 - currentMaxHP;
healthManager.AddBonusHealth(healthToAdd);
InChatAlerts.Info($"Added {healthToAdd} bonus health! Total HP: {healthManager.GetTotalMaxHealth()}");
}
else
{
InChatAlerts.Info($"Current max HP: {currentMaxHP}");
}
}
}
}
}

/// <summary>
/// Called when exiting a world
/// Use this for cleanup and ensuring data is properly saved
/// </summary>
public override void OnWorldUnload()
{
// Perform any cleanup tasks before world data is saved
CleanupWorldData();

isWorldInitialized = false;
showWelcomeMessage = false;
frameCounter = 0;
}

/// <summary>
/// Called after world generation completes (for new worlds)
/// Use this to set initial values for a fresh world
/// </summary>
public override void PostWorldGen()
{
// Set initial world data for new worlds
SetInitialWorldData();

// Message will be shown via PostUpdateWorld
showWelcomeMessage = true;
frameCounter = 0;
}

/// <summary>
/// Initialize default world data if not present
/// This runs every time a world is loaded
/// </summary>
private void InitializeDefaultWorldData()
{
// Example: Set default values if they don't exist
if (!WorldDataManager.HasKey("initialized"))
{
WorldDataManager.SetBool("initialized", true);
WorldDataManager.SetInt("worldVersion", 1);
}

// Check for mod version updates
int savedVersion = WorldDataManager.GetInt("worldVersion", 0);
if (savedVersion < 1)
{
// Handle migration from older versions
MigrateWorldData(savedVersion, 1);
}
}

/// <summary>
/// Set initial data for newly generated worlds
/// </summary>
private void SetInitialWorldData()
{
WorldDataManager.SetBool("initialized", true);
WorldDataManager.SetInt("worldVersion", 1);
WorldDataManager.SetInt("worldCreationTime", (int)System.DateTime.UtcNow.Ticks);

// Add any other initial world state here
WorldDataManager.SetInt("bossesDefeated", 0);
WorldDataManager.SetBool("hardmodeEnabled", false);
}

/// <summary>
/// Validate world data integrity
/// </summary>
private void ValidateWorldData()
{
// Example: Ensure critical values are within valid ranges
int worldVersion = WorldDataManager.GetInt("worldVersion", -1);
if (worldVersion < 0 || worldVersion > 1000)
{
InChatAlerts.Warning("Invalid world version detected, resetting to current");
WorldDataManager.SetInt("worldVersion", 1);
}

// Add more validation as needed
}

/// <summary>
/// Clean up world data before unload
/// </summary>
private void CleanupWorldData()
{
// Remove any temporary data that shouldn't persist
// Example: Clear cache keys
if (WorldDataManager.HasKey("tempSessionData"))
{
WorldDataManager.RemoveKey("tempSessionData");
}

// Add any other cleanup logic here
}

/// <summary>
/// Migrate world data from old version to new version
/// </summary>
/// <param name="fromVersion">Old version number</param>
/// <param name="toVersion">New version number</param>
private void MigrateWorldData(int fromVersion, int toVersion)
{
// Example migration logic
if (fromVersion == 0 && toVersion == 1)
{
// Add new keys introduced in version 1
if (!WorldDataManager.HasKey("bossesDefeated"))
{
WorldDataManager.SetInt("bossesDefeated", 0);
}
}

// Update version
WorldDataManager.SetInt("worldVersion", toVersion);
}

/// <summary>
/// Check if the world has been initialized
/// </summary>
public static bool IsWorldInitialized()
{
return isWorldInitialized;
}

/// <summary>
/// Get world age in seconds since creation
/// Returns -1 if world creation time is not set
/// </summary>
public static long GetWorldAge()
{
if (!WorldDataManager.HasKey("worldCreationTime"))
{
return -1;
}

long creationTime = WorldDataManager.GetInt("worldCreationTime", 0);
long currentTime = (int)System.DateTime.UtcNow.Ticks;
return (currentTime - creationTime) / 10000000; // Convert to seconds
}
}
}
