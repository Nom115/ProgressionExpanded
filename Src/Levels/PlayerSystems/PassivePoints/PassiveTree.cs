using System.Collections.Generic;
using System.Linq;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints
{
	/// <summary>
	/// Represents a complete passive skill tree
	/// </summary>
	public class PassiveTree
	{
		public string TreeId { get; set; }
		public string TreeName { get; set; }
		public string Description { get; set; }
		public Dictionary<string, PassiveNode> Nodes { get; set; } = new Dictionary<string, PassiveNode>();

		/// <summary>
		/// Get a node by its ID
		/// </summary>
		public PassiveNode GetNode(string nodeId)
		{
			if (Nodes.ContainsKey(nodeId))
				return Nodes[nodeId];
			return null;
		}

		/// <summary>
		/// Add a node to the tree
		/// </summary>
		public void AddNode(PassiveNode node)
		{
			if (node != null && !string.IsNullOrEmpty(node.NodeId))
			{
				Nodes[node.NodeId] = node;
			}
		}

		/// <summary>
		/// Get all nodes that have no prerequisites (starting nodes)
		/// </summary>
		public List<PassiveNode> GetStartingNodes()
		{
			return Nodes.Values.Where(n => n.Prerequisites == null || n.Prerequisites.Count == 0).ToList();
		}

		/// <summary>
		/// Get all nodes that require a specific node as a prerequisite
		/// </summary>
		public List<PassiveNode> GetDependentNodes(string nodeId)
		{
			return Nodes.Values.Where(n => n.Prerequisites != null && n.Prerequisites.Contains(nodeId)).ToList();
		}

		/// <summary>
		/// Validate tree structure (check for invalid prerequisites, circular dependencies, etc.)
		/// </summary>
		public bool ValidateTree(out string errorMessage)
		{
			errorMessage = "";

			// Check for nodes with invalid prerequisites
			foreach (var node in Nodes.Values)
			{
				if (node.Prerequisites != null)
				{
					foreach (string prereqId in node.Prerequisites)
					{
						if (!Nodes.ContainsKey(prereqId))
						{
							errorMessage = $"Node '{node.NodeId}' has invalid prerequisite '{prereqId}'";
							return false;
						}
					}
				}
			}

			// Check for circular dependencies (basic check)
			foreach (var node in Nodes.Values)
			{
				if (HasCircularDependency(node.NodeId, new HashSet<string>()))
				{
					errorMessage = $"Circular dependency detected involving node '{node.NodeId}'";
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Check if a node has circular dependencies
		/// </summary>
		private bool HasCircularDependency(string nodeId, HashSet<string> visited)
		{
			if (visited.Contains(nodeId))
				return true;

			visited.Add(nodeId);

			PassiveNode node = GetNode(nodeId);
			if (node?.Prerequisites != null)
			{
				foreach (string prereqId in node.Prerequisites)
				{
					if (HasCircularDependency(prereqId, new HashSet<string>(visited)))
						return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Get total number of nodes in tree
		/// </summary>
		public int GetNodeCount()
		{
			return Nodes.Count;
		}

		/// <summary>
		/// Calculate total points required to max out entire tree
		/// </summary>
		public int GetMaxPointsRequired()
		{
			int total = 0;
			foreach (var node in Nodes.Values)
			{
				total += node.GetTotalPointCost(node.MaxTier);
			}
			return total;
		}
	}
}
