using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ProgressionExpanded.Utils.DataManagers
{
	/// <summary>
	/// Simple API for saving and loading per-player data.
	/// Handles all serialization automatically.
	/// Data is tied to individual players and persists with their character file.
	/// </summary>
	public class PlayerDataManager : ModPlayer
	{
		// Instance storage - per player
		private Dictionary<string, int> intData = new();
		private Dictionary<string, float> floatData = new();
		private Dictionary<string, bool> boolData = new();
		private Dictionary<string, string> stringData = new();

		#region Set Methods
		
		/// <summary>
		/// Save an integer value to player data
		/// </summary>
		public static void SetInt(Player player, string key, int value)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("Key cannot be null or empty", nameof(key));
			player.GetModPlayer<PlayerDataManager>().intData[key] = value;
		}

		/// <summary>
		/// Save a float value to player data
		/// </summary>
		public static void SetFloat(Player player, string key, float value)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("Key cannot be null or empty", nameof(key));
			player.GetModPlayer<PlayerDataManager>().floatData[key] = value;
		}

		/// <summary>
		/// Save a boolean value to player data
		/// </summary>
		public static void SetBool(Player player, string key, bool value)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("Key cannot be null or empty", nameof(key));
			player.GetModPlayer<PlayerDataManager>().boolData[key] = value;
		}

		/// <summary>
		/// Save a string value to player data
		/// </summary>
		public static void SetString(Player player, string key, string value)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("Key cannot be null or empty", nameof(key));
			player.GetModPlayer<PlayerDataManager>().stringData[key] = value ?? string.Empty;
		}

		#endregion

		#region Get Methods

		/// <summary>
		/// Get an integer value from player data
		/// </summary>
		/// <param name="player">The player to retrieve data from</param>
		/// <param name="key">The key to retrieve</param>
		/// <param name="defaultValue">Default value if key doesn't exist</param>
		public static int GetInt(Player player, string key, int defaultValue = 0)
		{
			if (string.IsNullOrEmpty(key))
				return defaultValue;
			return player.GetModPlayer<PlayerDataManager>().intData.TryGetValue(key, out int value) ? value : defaultValue;
		}

		/// <summary>
		/// Get a float value from player data
		/// </summary>
		/// <param name="player">The player to retrieve data from</param>
		/// <param name="key">The key to retrieve</param>
		/// <param name="defaultValue">Default value if key doesn't exist</param>
		public static float GetFloat(Player player, string key, float defaultValue = 0f)
		{
			if (string.IsNullOrEmpty(key))
				return defaultValue;
			return player.GetModPlayer<PlayerDataManager>().floatData.TryGetValue(key, out float value) ? value : defaultValue;
		}

		/// <summary>
		/// Get a boolean value from player data
		/// </summary>
		/// <param name="player">The player to retrieve data from</param>
		/// <param name="key">The key to retrieve</param>
		/// <param name="defaultValue">Default value if key doesn't exist</param>
		public static bool GetBool(Player player, string key, bool defaultValue = false)
		{
			if (string.IsNullOrEmpty(key))
				return defaultValue;
			return player.GetModPlayer<PlayerDataManager>().boolData.TryGetValue(key, out bool value) ? value : defaultValue;
		}

		/// <summary>
		/// Get a string value from player data
		/// </summary>
		/// <param name="player">The player to retrieve data from</param>
		/// <param name="key">The key to retrieve</param>
		/// <param name="defaultValue">Default value if key doesn't exist</param>
		public static string GetString(Player player, string key, string defaultValue = "")
		{
			if (string.IsNullOrEmpty(key))
				return defaultValue;
			return player.GetModPlayer<PlayerDataManager>().stringData.TryGetValue(key, out string value) ? value : defaultValue;
		}

		#endregion

		#region Utility Methods

		/// <summary>
		/// Check if a key exists in player data
		/// </summary>
		public static bool HasKey(Player player, string key)
		{
			if (string.IsNullOrEmpty(key))
				return false;
			var data = player.GetModPlayer<PlayerDataManager>();
			return data.intData.ContainsKey(key) || 
			       data.floatData.ContainsKey(key) || 
			       data.boolData.ContainsKey(key) || 
			       data.stringData.ContainsKey(key);
		}

		/// <summary>
		/// Remove a key from player data
		/// </summary>
		public static void RemoveKey(Player player, string key)
		{
			if (string.IsNullOrEmpty(key))
				return;
			var data = player.GetModPlayer<PlayerDataManager>();
			data.intData.Remove(key);
			data.floatData.Remove(key);
			data.boolData.Remove(key);
			data.stringData.Remove(key);
		}

		/// <summary>
		/// Clear all player data (use with caution)
		/// </summary>
		public static void ClearAll(Player player)
		{
			player.GetModPlayer<PlayerDataManager>().ClearAllData();
		}

		private void ClearAllData()
		{
			intData.Clear();
			floatData.Clear();
			boolData.Clear();
			stringData.Clear();
		}

		#endregion

		#region ModPlayer Overrides

		public override void SaveData(TagCompound tag)
		{
			try
			{
				// Save each dictionary as a nested TagCompound to prevent key/value desync
				
				// Save integer data
				if (intData.Count > 0)
				{
					var intTag = new TagCompound();
					foreach (var kvp in intData)
					{
						intTag[kvp.Key] = kvp.Value;
					}
					tag["intData"] = intTag;
				}

				// Save float data
				if (floatData.Count > 0)
				{
					var floatTag = new TagCompound();
					foreach (var kvp in floatData)
					{
						floatTag[kvp.Key] = kvp.Value;
					}
					tag["floatData"] = floatTag;
				}

				// Save boolean data
				if (boolData.Count > 0)
				{
					var boolTag = new TagCompound();
					foreach (var kvp in boolData)
					{
						boolTag[kvp.Key] = kvp.Value;
					}
					tag["boolData"] = boolTag;
				}

				// Save string data
				if (stringData.Count > 0)
				{
					var stringTag = new TagCompound();
					foreach (var kvp in stringData)
					{
						stringTag[kvp.Key] = kvp.Value;
					}
					tag["stringData"] = stringTag;
				}
			}
			catch (Exception ex)
			{
				Mod.Logger.Error($"Failed to save player data: {ex.Message}");
				// Don't throw - let the game continue even if save fails
			}
		}

		public override void LoadData(TagCompound tag)
		{
			// Clear existing data first
			ClearAllData();

			try
			{
				// Load integer data
				if (tag.ContainsKey("intData"))
				{
					var intTag = tag.Get<TagCompound>("intData");
					foreach (var kvp in intTag)
					{
						if (kvp.Value is int intValue)
						{
							intData[kvp.Key] = intValue;
						}
					}
				}

				// Load float data
				if (tag.ContainsKey("floatData"))
				{
					var floatTag = tag.Get<TagCompound>("floatData");
					foreach (var kvp in floatTag)
					{
						if (kvp.Value is float floatValue)
						{
							floatData[kvp.Key] = floatValue;
						}
					}
				}

				// Load boolean data
				if (tag.ContainsKey("boolData"))
				{
					var boolTag = tag.Get<TagCompound>("boolData");
					foreach (var kvp in boolTag)
					{
						if (kvp.Value is bool boolValue)
						{
							boolData[kvp.Key] = boolValue;
						}
					}
				}

				// Load string data
				if (tag.ContainsKey("stringData"))
				{
					var stringTag = tag.Get<TagCompound>("stringData");
					foreach (var kvp in stringTag)
					{
						if (kvp.Value is string stringValue)
						{
							stringData[kvp.Key] = stringValue;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Mod.Logger.Error($"Failed to load player data: {ex.Message}");
				// Clear potentially corrupted data
				ClearAllData();
			}
		}

		public override void Initialize()
		{
			// Clear all data when player is initialized
			ClearAllData();
			
			// Initialize default player data
			InitializePlayerDefaults();
		}

		/// <summary>
		/// Initialize default player data values
		/// </summary>
		private void InitializePlayerDefaults()
		{
			// Set default player data if not present
			if (!intData.ContainsKey("initialized"))
			{
				intData["initialized"] = 1; // Use int as bool substitute
				intData["playerVersion"] = 1;
			}

			// Check player data version for migration
			int playerVersion = intData.TryGetValue("playerVersion", out int version) ? version : 0;
			if (playerVersion < 1)
			{
				MigratePlayerData(playerVersion, 1);
			}
		}

		/// <summary>
		/// Migrate player data from old version to new version
		/// </summary>
		private void MigratePlayerData(int fromVersion, int toVersion)
		{
			// Add migration logic here as needed
			
			intData["playerVersion"] = toVersion;
		}

		#endregion
	}
}
