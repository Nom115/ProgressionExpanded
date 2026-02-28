using System;
using System.Collections.Generic;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ProgressionExpanded.Utils.DataManagers
{
	/// <summary>
	/// Simple API for saving and loading world data.
	/// Handles all serialization automatically.
	/// Uses instance-based storage with static accessors for proper multiplayer handling.
	/// </summary>
	public class WorldDataManager : ModSystem
	{
		// Instance storage - the single source of truth
		private Dictionary<string, int> intData = new();
		private Dictionary<string, float> floatData = new();
		private Dictionary<string, bool> boolData = new();
		private Dictionary<string, string> stringData = new();

		// Static instance accessor
		private static WorldDataManager Instance => ModContent.GetInstance<WorldDataManager>();

		#region Set Methods
		
		/// <summary>
		/// Save an integer value to world data
		/// </summary>
		public static void SetInt(string key, int value)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("Key cannot be null or empty", nameof(key));
			Instance.intData[key] = value;
		}

		/// <summary>
		/// Save a float value to world data
		/// </summary>
		public static void SetFloat(string key, float value)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("Key cannot be null or empty", nameof(key));
			Instance.floatData[key] = value;
		}

		/// <summary>
		/// Save a boolean value to world data
		/// </summary>
		public static void SetBool(string key, bool value)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("Key cannot be null or empty", nameof(key));
			Instance.boolData[key] = value;
		}

		/// <summary>
		/// Save a string value to world data
		/// </summary>
		public static void SetString(string key, string value)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("Key cannot be null or empty", nameof(key));
			Instance.stringData[key] = value ?? string.Empty;
		}

		#endregion

		#region Get Methods

		/// <summary>
		/// Get an integer value from world data
		/// </summary>
		/// <param name="key">The key to retrieve</param>
		/// <param name="defaultValue">Default value if key doesn't exist</param>
		public static int GetInt(string key, int defaultValue = 0)
		{
			if (string.IsNullOrEmpty(key))
				return defaultValue;
			return Instance.intData.TryGetValue(key, out int value) ? value : defaultValue;
		}

		/// <summary>
		/// Get a float value from world data
		/// </summary>
		/// <param name="key">The key to retrieve</param>
		/// <param name="defaultValue">Default value if key doesn't exist</param>
		public static float GetFloat(string key, float defaultValue = 0f)
		{
			if (string.IsNullOrEmpty(key))
				return defaultValue;
			return Instance.floatData.TryGetValue(key, out float value) ? value : defaultValue;
		}

		/// <summary>
		/// Get a boolean value from world data
		/// </summary>
		/// <param name="key">The key to retrieve</param>
		/// <param name="defaultValue">Default value if key doesn't exist</param>
		public static bool GetBool(string key, bool defaultValue = false)
		{
			if (string.IsNullOrEmpty(key))
				return defaultValue;
			return Instance.boolData.TryGetValue(key, out bool value) ? value : defaultValue;
		}

		/// <summary>
		/// Get a string value from world data
		/// </summary>
		/// <param name="key">The key to retrieve</param>
		/// <param name="defaultValue">Default value if key doesn't exist</param>
		public static string GetString(string key, string defaultValue = "")
		{
			if (string.IsNullOrEmpty(key))
				return defaultValue;
			return Instance.stringData.TryGetValue(key, out string value) ? value : defaultValue;
		}

		#endregion

		#region Utility Methods

		/// <summary>
		/// Check if a key exists in any data storage
		/// </summary>
		public static bool HasKey(string key)
		{
			if (string.IsNullOrEmpty(key))
				return false;
			return Instance.intData.ContainsKey(key) || 
			       Instance.floatData.ContainsKey(key) || 
			       Instance.boolData.ContainsKey(key) || 
			       Instance.stringData.ContainsKey(key);
		}

		/// <summary>
		/// Remove a key from all data storages
		/// </summary>
		public static void RemoveKey(string key)
		{
			if (string.IsNullOrEmpty(key))
				return;
			Instance.intData.Remove(key);
			Instance.floatData.Remove(key);
			Instance.boolData.Remove(key);
			Instance.stringData.Remove(key);
		}

		/// <summary>
		/// Clear all world data (use with caution)
		/// </summary>
		public static void ClearAll()
		{
			Instance.ClearAllData();
		}

		private void ClearAllData()
		{
			intData.Clear();
			floatData.Clear();
			boolData.Clear();
			stringData.Clear();
		}

		#endregion

		#region ModSystem Overrides

		public override void SaveWorldData(TagCompound tag)
		{
			try
			{
				// Save each dictionary as a nested TagCompound to prevent key/value desync
				// This is much more robust than parallel lists
				
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
				Mod.Logger.Error($"Failed to save world data: {ex.Message}");
				// Don't throw - let the game continue even if save fails
			}
		}

		public override void LoadWorldData(TagCompound tag)
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
				Mod.Logger.Error($"Failed to load world data: {ex.Message}");
				// Clear potentially corrupted data
				ClearAllData();
			}
		}

		public override void ClearWorld()
		{
			// Clear all data when world is unloaded
			ClearAllData();
		}

		#endregion
	}
}
