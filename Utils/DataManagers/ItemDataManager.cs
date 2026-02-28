using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ProgressionExpanded.Utils.DataManagers
{
	/// <summary>
	/// Simple API for saving and loading per-item instance data.
	/// Handles all serialization automatically.
	/// Data is tied to individual item instances and persists with them.
	/// </summary>
	public class ItemDataManager : GlobalItem
	{
		// Instance storage - per item instance
		private Dictionary<string, int> intData = new();
		private Dictionary<string, float> floatData = new();
		private Dictionary<string, bool> boolData = new();
		private Dictionary<string, string> stringData = new();

		// Required for per-instance data
		public override bool InstancePerEntity => true;

		#region Set Methods
		
		/// <summary>
		/// Save an integer value to item data
		/// </summary>
		public static void SetInt(Item item, string key, int value)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("Key cannot be null or empty", nameof(key));
			item.GetGlobalItem<ItemDataManager>().intData[key] = value;
		}

		/// <summary>
		/// Save a float value to item data
		/// </summary>
		public static void SetFloat(Item item, string key, float value)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("Key cannot be null or empty", nameof(key));
			item.GetGlobalItem<ItemDataManager>().floatData[key] = value;
		}

		/// <summary>
		/// Save a boolean value to item data
		/// </summary>
		public static void SetBool(Item item, string key, bool value)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("Key cannot be null or empty", nameof(key));
			item.GetGlobalItem<ItemDataManager>().boolData[key] = value;
		}

		/// <summary>
		/// Save a string value to item data
		/// </summary>
		public static void SetString(Item item, string key, string value)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("Key cannot be null or empty", nameof(key));
			item.GetGlobalItem<ItemDataManager>().stringData[key] = value ?? string.Empty;
		}

		#endregion

		#region Get Methods

		/// <summary>
		/// Get an integer value from item data
		/// </summary>
		/// <param name="item">The item to retrieve data from</param>
		/// <param name="key">The key to retrieve</param>
		/// <param name="defaultValue">Default value if key doesn't exist</param>
		public static int GetInt(Item item, string key, int defaultValue = 0)
		{
			if (string.IsNullOrEmpty(key))
				return defaultValue;
			return item.GetGlobalItem<ItemDataManager>().intData.TryGetValue(key, out int value) ? value : defaultValue;
		}

		/// <summary>
		/// Get a float value from item data
		/// </summary>
		/// <param name="item">The item to retrieve data from</param>
		/// <param name="key">The key to retrieve</param>
		/// <param name="defaultValue">Default value if key doesn't exist</param>
		public static float GetFloat(Item item, string key, float defaultValue = 0f)
		{
			if (string.IsNullOrEmpty(key))
				return defaultValue;
			return item.GetGlobalItem<ItemDataManager>().floatData.TryGetValue(key, out float value) ? value : defaultValue;
		}

		/// <summary>
		/// Get a boolean value from item data
		/// </summary>
		/// <param name="item">The item to retrieve data from</param>
		/// <param name="key">The key to retrieve</param>
		/// <param name="defaultValue">Default value if key doesn't exist</param>
		public static bool GetBool(Item item, string key, bool defaultValue = false)
		{
			if (string.IsNullOrEmpty(key))
				return defaultValue;
			return item.GetGlobalItem<ItemDataManager>().boolData.TryGetValue(key, out bool value) ? value : defaultValue;
		}

		/// <summary>
		/// Get a string value from item data
		/// </summary>
		/// <param name="item">The item to retrieve data from</param>
		/// <param name="key">The key to retrieve</param>
		/// <param name="defaultValue">Default value if key doesn't exist</param>
		public static string GetString(Item item, string key, string defaultValue = "")
		{
			if (string.IsNullOrEmpty(key))
				return defaultValue;
			return item.GetGlobalItem<ItemDataManager>().stringData.TryGetValue(key, out string value) ? value : defaultValue;
		}

		#endregion

		#region Utility Methods

		/// <summary>
		/// Check if a key exists in item data
		/// </summary>
		public static bool HasKey(Item item, string key)
		{
			if (string.IsNullOrEmpty(key))
				return false;
			var data = item.GetGlobalItem<ItemDataManager>();
			return data.intData.ContainsKey(key) || 
			       data.floatData.ContainsKey(key) || 
			       data.boolData.ContainsKey(key) || 
			       data.stringData.ContainsKey(key);
		}

		/// <summary>
		/// Remove a key from item data
		/// </summary>
		public static void RemoveKey(Item item, string key)
		{
			if (string.IsNullOrEmpty(key))
				return;
			var data = item.GetGlobalItem<ItemDataManager>();
			data.intData.Remove(key);
			data.floatData.Remove(key);
			data.boolData.Remove(key);
			data.stringData.Remove(key);
		}

		/// <summary>
		/// Clear all item data (use with caution)
		/// </summary>
		public static void ClearAll(Item item)
		{
			item.GetGlobalItem<ItemDataManager>().ClearAllData();
		}

		private void ClearAllData()
		{
			intData.Clear();
			floatData.Clear();
			boolData.Clear();
			stringData.Clear();
		}

		#endregion

		#region GlobalItem Overrides

		public override void SaveData(Item item, TagCompound tag)
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
				Mod.Logger.Error($"Failed to save item data: {ex.Message}");
				// Don't throw - let the game continue even if save fails
			}
		}

		public override void LoadData(Item item, TagCompound tag)
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
				Mod.Logger.Error($"Failed to load item data: {ex.Message}");
				// Clear potentially corrupted data
				ClearAllData();
			}
		}

		public override GlobalItem Clone(Item from, Item to)
		{
			var clone = (ItemDataManager)base.Clone(from, to);
			// Deep copy the dictionaries
			clone.intData = new Dictionary<string, int>(intData);
			clone.floatData = new Dictionary<string, float>(floatData);
			clone.boolData = new Dictionary<string, bool>(boolData);
			clone.stringData = new Dictionary<string, string>(stringData);
			return clone;
		}

		#endregion
	}
}
