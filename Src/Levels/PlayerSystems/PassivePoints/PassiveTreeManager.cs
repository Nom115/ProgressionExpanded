using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Utils.DataManagers;
using Newtonsoft.Json;

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
		private bool initialized = false;

		// Cached bonuses for performance
		private Dictionary<string, float> cachedFlatBonuses = new Dictionary<string, float>();
		private Dictionary<string, float> cachedPercentBonuses = new Dictionary<string, float>();

		public override void Initialize()
		{
			LoadAllocations();
			RecalculateBonuses();
			// tModLoader runs Initialize() BEFORE LoadData(), so the load above reads an empty
			// PlayerDataManager (allocations come back as "{}"). Re-read authoritatively on the
			// first PostUpdateMiscEffects tick (after LoadData has populated PlayerDataManager);
			// otherwise the next SaveAllocations() would overwrite the real save with the empty
			// set. Same deferred-read pattern as PlayerHealthManager.
			initialized = false;
		}

		public override void PostUpdateMiscEffects()
		{
			if (!initialized)
			{
				LoadAllocations();
				RecalculateBonuses();

				// By the first update tick, PassivePointManager.LoadData and PlayerDataManager.LoadData
				// have both run, so reconcile the point economy against the real allocations. This
				// self-heals any spent/available drift (e.g. a corrupted allocation save that reset the
				// tree but not the spent counter).
				Player.GetModPlayer<PassivePointManager>().ReconcileSpentPoints(GetTotalSpentAcrossTrees());

				initialized = true;
			}

			// Apply cached bonuses to stats
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
			SaveAllocations();
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
			SaveAllocations();
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
			SaveAllocations();
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
			SaveAllocations();
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

		// The mastery-loadout view (MasteryLoadoutPanel) uses these instead of the tree-style
		// AllocateNode/DeallocateNode above: masteries are gated by a fixed number of slots and the
		// shared point pool, NOT by a prerequisite web. Prerequisites in the JSON are ignored here.

		/// <summary>
		/// Number of masteries a player can have socketed at once.
		/// </summary>
		public const int MasterySlotCount = 6;

		/// <summary>
		/// Number of masteries currently socketed (allocated at tier &gt; 0) in a tree.
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
		/// Whether there is a free mastery slot to socket a new mastery into.
		/// </summary>
		public bool CanSocketNew(string treeId)
		{
			return GetSocketedCount(treeId) < MasterySlotCount;
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

			// Socketing a brand-new mastery requires a free slot.
			if (currentTier == 0 && !CanSocketNew(treeId))
				return false;

			// Spend the points.
			PassivePointManager pointManager = Player.GetModPlayer<PassivePointManager>();
			int cost = node.GetUpgradeCost(currentTier);
			if (!pointManager.SpendPoints(cost))
				return false;

			if (!treeAllocations.ContainsKey(treeId))
				treeAllocations[treeId] = new Dictionary<string, int>();

			treeAllocations[treeId][nodeId] = currentTier + 1;

			SaveAllocations();
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

			SaveAllocations();
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

			SaveAllocations();
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
		/// Apply bonuses to player stats
		/// </summary>
		private void ApplyBonusesToStats()
		{
			StatsManager statsManager = Player.GetModPlayer<StatsManager>();

			// Apply bonuses to each stat
			foreach (var flatBonus in cachedFlatBonuses)
			{
				ApplyFlatBonusToStat(statsManager, flatBonus.Key, flatBonus.Value);
			}

			foreach (var percentBonus in cachedPercentBonuses)
			{
				ApplyPercentBonusToStat(statsManager, percentBonus.Key, percentBonus.Value);
			}
		}

		/// <summary>
		/// Apply flat bonus to a specific stat
		/// </summary>
		private void ApplyFlatBonusToStat(StatsManager stats, string statName, float value)
		{
			// Note: This applies temporary bonuses each frame
			// The bonuses are not permanently added via the AddX methods to avoid saving issues

			switch (statName)
			{
				// Damage stats
				case "MeleeDamage":
					Player.GetDamage(DamageClass.Melee).Flat += value;
					break;
				case "MagicDamage":
					Player.GetDamage(DamageClass.Magic).Flat += value;
					break;
				case "RangedDamage":
					Player.GetDamage(DamageClass.Ranged).Flat += value;
					break;
				case "SummonDamage":
					Player.GetDamage(DamageClass.Summon).Flat += value;
					break;
				case "GenericDamage":
					Player.GetDamage(DamageClass.Generic).Flat += value;
					break;
				case "CritChance":
					Player.GetCritChance(DamageClass.Generic) += (int)value;
					break;
				case "ArmorPenetration":
					Player.GetArmorPenetration(DamageClass.Generic) += (int)value;
					break;
				case "MinionSlots":
					Player.maxMinions += (int)value;
					break;

				// Defence stats
				case "Defense":
					Player.statDefense += (int)value;
					break;
				case "MaxHealth":
					// Applied every PostUpdateMiscEffects, mirroring PlayerHealthManager's bonus HP.
					Player.statLifeMax2 += (int)value;
					break;
				case "LifeRegen":
					Player.lifeRegen += (int)value;
					break;

				// Tertiary stats
				case "MovementSpeed":
					Player.moveSpeed += value;
					break;
			}
		}

		/// <summary>
		/// Apply percent bonus to a specific stat
		/// </summary>
		private void ApplyPercentBonusToStat(StatsManager stats, string statName, float value)
		{
			switch (statName)
			{
				// Damage stats
				case "MeleeDamage":
					Player.GetDamage(DamageClass.Melee) *= 1f + value;
					break;
				case "MagicDamage":
					Player.GetDamage(DamageClass.Magic) *= 1f + value;
					break;
				case "RangedDamage":
					Player.GetDamage(DamageClass.Ranged) *= 1f + value;
					break;
				case "SummonDamage":
					Player.GetDamage(DamageClass.Summon) *= 1f + value;
					break;
				case "GenericDamage":
					Player.GetDamage(DamageClass.Generic) *= 1f + value;
					break;
				case "AttackSpeed":
					Player.GetAttackSpeed(DamageClass.Generic) += value;
					break;
				case "ManaEfficiency":
					Player.manaCost -= value;
					if (Player.manaCost < 0f) Player.manaCost = 0f;
					break;

				// Defence stats
				case "Endurance":
					Player.endurance += value;
					if (Player.endurance > 1f) Player.endurance = 1f;
					break;

				// Tertiary stats
				case "Knockback":
					Player.GetKnockback(DamageClass.Generic) += value;
					break;
				case "MovementSpeed":
					Player.moveSpeed *= 1f + value;
					break;
			}
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

		#region Data Persistence

		/// <summary>
		/// Save allocations to player data
		/// </summary>
		private void SaveAllocations()
		{
			string json = JsonConvert.SerializeObject(treeAllocations);
			PlayerDataManager.SetString(Player, ALLOCATIONS_KEY, json);
		}

		/// <summary>
		/// Load allocations from player data
		/// </summary>
		private void LoadAllocations()
		{
			string json = PlayerDataManager.GetString(Player, ALLOCATIONS_KEY, "{}");
			try
			{
				treeAllocations = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json);
				if (treeAllocations == null)
					treeAllocations = new Dictionary<string, Dictionary<string, int>>();
			}
			catch
			{
				treeAllocations = new Dictionary<string, Dictionary<string, int>>();
			}
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
