using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Newtonsoft.Json;
using ProgressionExpanded.Src.Levels.PlayerSystems.Stats;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints
{
	/// <summary>
	/// Manages a player's passive tree allocations and applies bonuses
	/// </summary>
	public class PassiveTreeManager : ModPlayer
	{
		private const string ALLOCATIONS_KEY = "PassiveTreeAllocations";

		// Stores node allocations: Dictionary<TreeId, Dictionary<NodeId, CurrentTier>>
		private Dictionary<string, Dictionary<string, int>> treeAllocations = new Dictionary<string, Dictionary<string, int>>();

		// Cached bonuses for performance
		private Dictionary<string, float> cachedFlatBonuses = new Dictionary<string, float>();
		private Dictionary<string, float> cachedPercentBonuses = new Dictionary<string, float>();

		public override void Initialize()
		{
			// Deliberately does NOT read saved data. Allocations live in this ModPlayer's own
			// TagCompound, so LoadData is the authoritative load and tModLoader guarantees it runs
			// before any update tick. This used to read PlayerDataManager here and re-read on the
			// first PostUpdateMiscEffects to work around Initialize running before LoadData — that
			// whole dance is gone with the storage move, and with it the risk of a save-wipe.
			treeAllocations.Clear();
			RecalculateBonuses();
		}

		public override void SaveData(Terraria.ModLoader.IO.TagCompound tag)
		{
			tag[ALLOCATIONS_KEY] = JsonConvert.SerializeObject(treeAllocations);
		}

		public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
		{
			string json = tag.GetString(ALLOCATIONS_KEY);
			if (string.IsNullOrEmpty(json))
				json = "{}";

			try
			{
				treeAllocations = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json)
					?? new Dictionary<string, Dictionary<string, int>>();
			}
			catch
			{
				treeAllocations = new Dictionary<string, Dictionary<string, int>>();
			}

			RecalculateBonuses();
		}

		/// <summary>
		/// Max life/mana are applied here rather than through the per-frame stat switches, because
		/// StatModifier is the only channel where flat and percentage contributions compose
		/// correctly — and it combines cleanly across ModPlayers, so AttributeManager can add
		/// Strength's health independently without either of us knowing about the other.
		/// </summary>
		public override void ModifyMaxStats(out StatModifier health, out StatModifier mana)
		{
			health = StatModifier.Default;
			mana = StatModifier.Default;

			// Base sits inside the multipliers, Flat sits outside them. Flat max-life bonuses go in
			// Base so that percentage bonuses (Fortitude) scale them, matching how Strength is added.
			health.Base += GetFlatBonus("MaxHealth");
			mana.Base += GetFlatBonus("MaxMana");

			// += on a StatModifier is the additive-percentage operator: += 0.20f is "+20% maximum
			// life", stacking additively with every other source rather than compounding.
			health += GetPercentBonus("MaxLifePercent");
			mana += GetPercentBonus("MaxManaPercent");
		}

		public override void PostUpdateMiscEffects()
		{
			// Reconciling the point economy is deliberately NOT done here. The tree is no longer the
			// only sink — attributes draw on the same pool — so reconciling against this tree's spend
			// alone would set spent to the tree total and refund every attribute point while the
			// attributes stayed allocated, compounding on every reload. PassivePointManager owns that
			// invariant now and sums both sinks; see its PostUpdateMiscEffects.
			ApplyBonusesToStats();
		}

		#region Allocation Management

		/// <summary>
		/// Allocate a point to a specific node in a tree
		/// </summary>
		public bool AllocateNode(string treeId, string nodeId)
		{
			PassiveTree tree = PassiveTreeLoader.GetTree(treeId);
			if (tree == null)
				return false;

			PassiveNode node = tree.GetNode(nodeId);
			if (node == null)
				return false;

			// Get current tier
			int currentTier = GetNodeTier(treeId, nodeId);

			// Check if node is maxed
			if (currentTier >= node.MaxTier)
				return false;

			// Check prerequisites
			if (!node.ArePrerequisitesMet(GetTreeAllocations(treeId)))
				return false;

			// Check if player has enough points
			PassivePointManager pointManager = Player.GetModPlayer<PassivePointManager>();
			int cost = node.GetUpgradeCost(currentTier);
			if (!pointManager.SpendPoints(cost))
				return false;

			// Allocate the node
			if (!treeAllocations.ContainsKey(treeId))
				treeAllocations[treeId] = new Dictionary<string, int>();

			treeAllocations[treeId][nodeId] = currentTier + 1;

			// Save and recalculate
			RecalculateBonuses();

			return true;
		}

		/// <summary>
		/// Deallocate a node (refund points)
		/// </summary>
		public bool DeallocateNode(string treeId, string nodeId)
		{
			PassiveTree tree = PassiveTreeLoader.GetTree(treeId);
			if (tree == null)
				return false;

			PassiveNode node = tree.GetNode(nodeId);
			if (node == null)
				return false;

			int currentTier = GetNodeTier(treeId, nodeId);
			if (currentTier <= 0)
				return false;

			// Check if any dependent nodes are allocated
			var dependentNodes = tree.GetDependentNodes(nodeId);
			foreach (var depNode in dependentNodes)
			{
				if (GetNodeTier(treeId, depNode.NodeId) > 0)
					return false; // Can't deallocate if dependencies exist
			}

			// Refund points
			PassivePointManager pointManager = Player.GetModPlayer<PassivePointManager>();
			int refundAmount = node.PointsPerTier;
			pointManager.RefundPoints(refundAmount);

			// Deallocate
			treeAllocations[treeId][nodeId] = currentTier - 1;
			if (treeAllocations[treeId][nodeId] <= 0)
				treeAllocations[treeId].Remove(nodeId);

			// Save and recalculate
			RecalculateBonuses();

			return true;
		}

		/// <summary>
		/// Reset entire tree (refund all points)
		/// </summary>
		public void ResetTree(string treeId)
		{
			if (!treeAllocations.ContainsKey(treeId))
				return;

			PassiveTree tree = PassiveTreeLoader.GetTree(treeId);
			if (tree == null)
				return;

			// Calculate total points to refund
			int totalRefund = 0;
			foreach (var allocation in treeAllocations[treeId])
			{
				PassiveNode node = tree.GetNode(allocation.Key);
				if (node != null)
				{
					totalRefund += node.GetTotalPointCost(allocation.Value);
				}
			}

			// Refund points
			if (totalRefund > 0)
			{
				PassivePointManager pointManager = Player.GetModPlayer<PassivePointManager>();
				pointManager.RefundPoints(totalRefund);
			}

			// Clear allocations
			treeAllocations[treeId].Clear();
			RecalculateBonuses();
		}

		/// <summary>
		/// Reset all trees (refund all points)
		/// </summary>
		public void ResetAllTrees()
		{
			// Calculate total points to refund from all trees
			int totalRefund = 0;
			PassivePointManager pointManager = Player.GetModPlayer<PassivePointManager>();

			foreach (var treeEntry in treeAllocations)
			{
				PassiveTree tree = PassiveTreeLoader.GetTree(treeEntry.Key);
				if (tree == null)
					continue;

				foreach (var allocation in treeEntry.Value)
				{
					PassiveNode node = tree.GetNode(allocation.Key);
					if (node != null)
						totalRefund += node.GetTotalPointCost(allocation.Value);
				}
			}

			// Refund points
			if (totalRefund > 0)
				pointManager.RefundPoints(totalRefund);

			// Clear all allocations
			treeAllocations.Clear();
			RecalculateBonuses();
		}

		/// <summary>
		/// Drop every allocation without refunding anything. Only for the schema migration, which
		/// rebuilds the point economy from the player's level immediately afterwards — any other
		/// caller wants ResetAllTrees, which refunds.
		/// </summary>
		public void WipeAllocations()
		{
			treeAllocations.Clear();
			RecalculateBonuses();
		}

		/// <summary>
		/// Get the current tier of a node
		/// </summary>
		public int GetNodeTier(string treeId, string nodeId)
		{
			if (!treeAllocations.ContainsKey(treeId))
				return 0;

			if (!treeAllocations[treeId].ContainsKey(nodeId))
				return 0;

			return treeAllocations[treeId][nodeId];
		}

		/// <summary>
		/// Sum the point cost of every allocated node across all trees. Used on load to reconcile
		/// PassivePointManager's spent/available totals against the true allocations. Nodes or trees
		/// missing from the loaded defs are skipped (their cost is unknowable).
		/// </summary>
		public int GetTotalSpentAcrossTrees()
		{
			int total = 0;
			foreach (var treeEntry in treeAllocations)
			{
				PassiveTree tree = PassiveTreeLoader.GetTree(treeEntry.Key);
				if (tree == null)
					continue;

				foreach (var allocation in treeEntry.Value)
				{
					PassiveNode node = tree.GetNode(allocation.Key);
					if (node != null)
						total += node.GetTotalPointCost(allocation.Value);
				}
			}
			return total;
		}

		/// <summary>
		/// Get all allocations for a specific tree
		/// </summary>
		public Dictionary<string, int> GetTreeAllocations(string treeId)
		{
			if (!treeAllocations.ContainsKey(treeId))
				return new Dictionary<string, int>();

			return new Dictionary<string, int>(treeAllocations[treeId]);
		}

		/// <summary>
		/// Check if a node can be allocated
		/// </summary>
		public bool CanAllocateNode(string treeId, string nodeId)
		{
			PassiveTree tree = PassiveTreeLoader.GetTree(treeId);
			if (tree == null) return false;

			PassiveNode node = tree.GetNode(nodeId);
			if (node == null) return false;

			int currentTier = GetNodeTier(treeId, nodeId);
			if (currentTier >= node.MaxTier) return false;

			if (!node.ArePrerequisitesMet(GetTreeAllocations(treeId))) return false;

			PassivePointManager pointManager = Player.GetModPlayer<PassivePointManager>();
			return pointManager.HasEnoughPoints(node.GetUpgradeCost(currentTier));
		}

		#endregion

		#region Mastery Loadout

		// The mastery view uses these instead of the tree-style AllocateNode/DeallocateNode above:
		// masteries are gated ONLY by the shared point pool, not by a prerequisite web and no longer
		// by a slot count either. Prerequisites in the JSON are ignored here.
		//
		// The old 6-slot cap is gone. It used to be the only limit that mattered, and it capped
		// effective spend at roughly 25-30 of the 99 points a character earns — the rest were
		// unspendable. Masteries now compete with attributes for the same pool, which is a real
		// constraint on its own: maxing every mastery costs more points than a character ever earns.
		// The six fixed slots that remain are the boss-gated talent slots, which are a separate
		// system and cost no points.

		/// <summary>
		/// Number of masteries currently ranked (allocated at tier &gt; 0) in a tree.
		/// </summary>
		public int GetSocketedCount(string treeId)
		{
			if (!treeAllocations.ContainsKey(treeId))
				return 0;

			int count = 0;
			foreach (var entry in treeAllocations[treeId])
			{
				if (entry.Value > 0)
					count++;
			}
			return count;
		}

		/// <summary>
		/// Socket a mastery (if not already socketed) or rank it up one tier. Ignores prerequisites.
		/// Socketing a new mastery requires a free slot; ranking an existing one does not.
		/// </summary>
		public bool AllocateMastery(string treeId, string nodeId)
		{
			PassiveTree tree = PassiveTreeLoader.GetTree(treeId);
			if (tree == null)
				return false;

			PassiveNode node = tree.GetNode(nodeId);
			if (node == null)
				return false;

			int currentTier = GetNodeTier(treeId, nodeId);

			// Already maxed.
			if (currentTier >= node.MaxTier)
				return false;

			// Spend the points.
			PassivePointManager pointManager = Player.GetModPlayer<PassivePointManager>();
			int cost = node.GetUpgradeCost(currentTier);
			if (!pointManager.SpendPoints(cost))
				return false;

			if (!treeAllocations.ContainsKey(treeId))
				treeAllocations[treeId] = new Dictionary<string, int>();

			treeAllocations[treeId][nodeId] = currentTier + 1;

			RecalculateBonuses();
			return true;
		}

		/// <summary>
		/// Rank a socketed mastery down one tier (refunding a point); unsockets it at tier 0.
		/// Ignores dependent-node checks — the loadout has no prerequisite web.
		/// </summary>
		public bool DeallocateMastery(string treeId, string nodeId)
		{
			PassiveTree tree = PassiveTreeLoader.GetTree(treeId);
			if (tree == null)
				return false;

			PassiveNode node = tree.GetNode(nodeId);
			if (node == null)
				return false;

			int currentTier = GetNodeTier(treeId, nodeId);
			if (currentTier <= 0)
				return false;

			PassivePointManager pointManager = Player.GetModPlayer<PassivePointManager>();
			pointManager.RefundPoints(node.PointsPerTier);

			treeAllocations[treeId][nodeId] = currentTier - 1;
			if (treeAllocations[treeId][nodeId] <= 0)
				treeAllocations[treeId].Remove(nodeId);

			RecalculateBonuses();
			return true;
		}

		/// <summary>
		/// Fully unsocket a mastery, refunding every point invested in it and freeing the slot.
		/// </summary>
		public void ClearMastery(string treeId, string nodeId)
		{
			int currentTier = GetNodeTier(treeId, nodeId);
			if (currentTier <= 0)
				return;

			PassiveTree tree = PassiveTreeLoader.GetTree(treeId);
			PassiveNode node = tree?.GetNode(nodeId);
			if (node != null)
			{
				PassivePointManager pointManager = Player.GetModPlayer<PassivePointManager>();
				pointManager.RefundPoints(node.GetTotalPointCost(currentTier));
			}

			if (treeAllocations.ContainsKey(treeId))
				treeAllocations[treeId].Remove(nodeId);

			RecalculateBonuses();
		}

		#endregion

		#region Bonus Calculation

		/// <summary>
		/// Recalculate all bonuses from allocated nodes
		/// </summary>
		private void RecalculateBonuses()
		{
			cachedFlatBonuses.Clear();
			cachedPercentBonuses.Clear();

			foreach (var treeEntry in treeAllocations)
			{
				string treeId = treeEntry.Key;
				PassiveTree tree = PassiveTreeLoader.GetTree(treeId);
				if (tree == null) continue;

				foreach (var nodeEntry in treeEntry.Value)
				{
					string nodeId = nodeEntry.Key;
					int tier = nodeEntry.Value;

					PassiveNode node = tree.GetNode(nodeId);
					if (node == null) continue;

					// Add flat bonuses
					foreach (var bonus in node.FlatBonuses)
					{
						string statName = bonus.Key;
						float value = node.GetFlatBonus(statName, tier);

						if (!cachedFlatBonuses.ContainsKey(statName))
							cachedFlatBonuses[statName] = 0f;
						cachedFlatBonuses[statName] += value;
					}

					// Add percent bonuses
					foreach (var bonus in node.PercentBonuses)
					{
						string statName = bonus.Key;
						float value = node.GetPercentBonus(statName, tier);

						if (!cachedPercentBonuses.ContainsKey(statName))
							cachedPercentBonuses[statName] = 0f;
						cachedPercentBonuses[statName] += value;
					}
				}
			}
		}

		/// <summary>
		/// Apply cached bonuses to player stats. Flats run before percents, so a percent bonus scales
		/// the flat total as well as vanilla's — deliberate, and the reason Fortitude is worth having
		/// alongside Strength rather than instead of it.
		/// </summary>
		private void ApplyBonusesToStats()
		{
			foreach (var flatBonus in cachedFlatBonuses)
				StatApplier.ApplyFlat(Player, flatBonus.Key, flatBonus.Value);

			foreach (var percentBonus in cachedPercentBonuses)
				StatApplier.ApplyPercent(Player, percentBonus.Key, percentBonus.Value);
		}



		/// <summary>
		/// Get total flat bonus for a specific stat
		/// </summary>
		public float GetFlatBonus(string statName)
		{
			return cachedFlatBonuses.ContainsKey(statName) ? cachedFlatBonuses[statName] : 0f;
		}

		/// <summary>
		/// Get total percent bonus for a specific stat
		/// </summary>
		public float GetPercentBonus(string statName)
		{
			return cachedPercentBonuses.ContainsKey(statName) ? cachedPercentBonuses[statName] : 0f;
		}

		#endregion


		#region Static Helpers

		/// <summary>
		/// Get a player's tree manager
		/// </summary>
		public static PassiveTreeManager GetTreeManager(Player player)
		{
			return player.GetModPlayer<PassiveTreeManager>();
		}

		#endregion
	}
}
