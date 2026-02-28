using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints
{
	/// <summary>
	/// Loads passive trees from JSON files
	/// </summary>
	public static class PassiveTreeLoader
	{
		private static Dictionary<string, PassiveTree> loadedTrees = new Dictionary<string, PassiveTree>();

		/// <summary>
		/// Load all passive trees from the PassiveTrees directory
		/// </summary>
		public static void LoadAllTrees(Mod mod)
		{
			loadedTrees.Clear();

			// Get all JSON files from the PassiveTrees directory
			string treesPath = Path.Combine(mod.GetType().Namespace, "Src", "Levels", "PlayerSystems", "PassivePoints", "PassiveTrees");
			
			try
			{
				// Try to load from mod's file structure
				var files = mod.GetFileNames();
				foreach (var file in files)
				{
					if (file.Contains("PassiveTrees") && file.EndsWith(".json"))
					{
						LoadTreeFromMod(mod, file);
					}
				}

				mod.Logger.Info($"Loaded {loadedTrees.Count} passive tree(s)");
			}
			catch (Exception ex)
			{
				mod.Logger.Error($"Error loading passive trees: {ex.Message}");
			}
		}

		/// <summary>
		/// Load a specific tree from the mod's files
		/// </summary>
		private static void LoadTreeFromMod(Mod mod, string filePath)
		{
			try
			{
				using (Stream stream = mod.GetFileStream(filePath))
				using (StreamReader reader = new StreamReader(stream))
				{
					string jsonContent = reader.ReadToEnd();
					PassiveTree tree = JsonConvert.DeserializeObject<PassiveTree>(jsonContent);

					if (tree != null && !string.IsNullOrEmpty(tree.TreeId))
					{
						// Validate the tree
						if (tree.ValidateTree(out string errorMessage))
						{
							loadedTrees[tree.TreeId] = tree;
							mod.Logger.Info($"Loaded passive tree: {tree.TreeName} ({tree.GetNodeCount()} nodes)");
						}
						else
						{
							mod.Logger.Error($"Invalid tree structure in {filePath}: {errorMessage}");
						}
					}
				}
			}
			catch (Exception ex)
			{
				mod.Logger.Error($"Error loading tree from {filePath}: {ex.Message}");
			}
		}

		/// <summary>
		/// Load a tree from a JSON string (for testing or dynamic loading)
		/// </summary>
		public static PassiveTree LoadTreeFromJson(string jsonContent)
		{
			try
			{
				PassiveTree tree = JsonConvert.DeserializeObject<PassiveTree>(jsonContent);
				
				if (tree != null)
				{
					if (tree.ValidateTree(out string errorMessage))
					{
						if (!string.IsNullOrEmpty(tree.TreeId))
						{
							loadedTrees[tree.TreeId] = tree;
						}
						return tree;
					}
					else
					{
						throw new Exception($"Invalid tree: {errorMessage}");
					}
				}
				else
				{
					throw new Exception("Failed to deserialize tree - result was null");
				}
			}
			catch (Exception ex)
			{
				throw new Exception($"Failed to parse tree JSON: {ex.Message}");
			}
		}

		/// <summary>
		/// Get a loaded tree by ID
		/// </summary>
		public static PassiveTree GetTree(string treeId)
		{
			if (loadedTrees.ContainsKey(treeId))
				return loadedTrees[treeId];
			return null;
		}

		/// <summary>
		/// Get all loaded trees
		/// </summary>
		public static Dictionary<string, PassiveTree> GetAllTrees()
		{
			return new Dictionary<string, PassiveTree>(loadedTrees);
		}

		/// <summary>
		/// Check if a tree is loaded
		/// </summary>
		public static bool IsTreeLoaded(string treeId)
		{
			return loadedTrees.ContainsKey(treeId);
		}

		/// <summary>
		/// Get IDs of all loaded trees
		/// </summary>
		public static List<string> GetLoadedTreeIds()
		{
			return new List<string>(loadedTrees.Keys);
		}

		/// <summary>
		/// Reload all trees (useful for development)
		/// </summary>
		public static void ReloadTrees(Mod mod)
		{
			LoadAllTrees(mod);
		}

		/// <summary>
		/// Clear all loaded trees
		/// </summary>
		public static void UnloadTrees()
		{
			loadedTrees.Clear();
		}
	}
}
