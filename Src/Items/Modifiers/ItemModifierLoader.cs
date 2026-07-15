using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Terraria.ModLoader;

namespace ProgressionExpanded.Items.Modifiers
{
	/// <summary>
	/// Loads item-modifier pools from the bundled "ItemModifiers/*.json" files. Mirrors
	/// <c>PassiveTreeLoader</c>: filter the mod's embedded file list, deserialize with Newtonsoft,
	/// validate, and cache in a static dictionary keyed by PoolId.
	/// </summary>
	public static class ItemModifierLoader
	{
		private static Dictionary<string, ItemModifierPool> loadedPools = new Dictionary<string, ItemModifierPool>();

		/// <summary>Load every pool from the mod's embedded ItemModifiers directory.</summary>
		public static void LoadAll(Mod mod)
		{
			loadedPools.Clear();

			try
			{
				var files = mod.GetFileNames();
				foreach (var file in files)
				{
					if (file.Contains("ItemModifiers") && file.EndsWith(".json"))
						LoadPoolFromMod(mod, file);
				}

				mod.Logger.Info($"Loaded {loadedPools.Count} item modifier pool(s)");
			}
			catch (Exception ex)
			{
				mod.Logger.Error($"Error loading item modifier pools: {ex.Message}");
			}
		}

		private static void LoadPoolFromMod(Mod mod, string filePath)
		{
			try
			{
				using Stream stream = mod.GetFileStream(filePath);
				using StreamReader reader = new StreamReader(stream);

				string jsonContent = reader.ReadToEnd();
				ItemModifierPool pool = JsonConvert.DeserializeObject<ItemModifierPool>(jsonContent);

				if (pool != null && !string.IsNullOrEmpty(pool.PoolId))
				{
					if (pool.Validate(out string errorMessage))
					{
						loadedPools[pool.PoolId] = pool;
						mod.Logger.Info($"Loaded item modifier pool: {pool.PoolId} ({pool.Modifiers.Count} mods)");
					}
					else
					{
						mod.Logger.Error($"Invalid item modifier pool in {filePath}: {errorMessage}");
					}
				}
			}
			catch (Exception ex)
			{
				mod.Logger.Error($"Error loading item modifier pool from {filePath}: {ex.Message}");
			}
		}

		/// <summary>Get a loaded pool by id, or null.</summary>
		public static ItemModifierPool GetPool(string poolId)
		{
			if (!string.IsNullOrEmpty(poolId) && loadedPools.TryGetValue(poolId, out var pool))
				return pool;
			return null;
		}

		/// <summary>Resolve a modifier definition from its pool + id, or null.</summary>
		public static ItemModifierDefinition GetDefinition(string poolId, string modId)
		{
			return GetPool(poolId)?.GetModifier(modId);
		}

		public static bool IsPoolLoaded(string poolId) => loadedPools.ContainsKey(poolId);

		public static IReadOnlyDictionary<string, ItemModifierPool> GetAllPools() => loadedPools;

		public static void Unload() => loadedPools.Clear();
	}
}
