using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.UI;
using Terraria.GameContent.UI.Elements;
using ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints;
using ProgressionExpanded.Src.Levels.PlayerSystems;
using PassiveTreeType = ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints.PassiveTree;

namespace ProgressionExpanded.Src.UI.PassiveTree
{
	/// <summary>
	/// Panel displaying a single passive tree
	/// </summary>
	public class PassiveTreePanel : UIElement
	{
		private readonly PassiveTreeType tree;
		private readonly string treeId;
		private readonly PassiveTreeManager treeManager;
		private readonly PassivePointManager pointManager;
		
		private UIPanel background;
		private UIText titleText;
		private UIText pointsText;
		private UIElement nodeContainer;
		
		private List<PassiveNodeButton> nodeButtons = new List<PassiveNodeButton>();
		private List<NodeConnection> connections = new List<NodeConnection>();

		public PassiveTreePanel(string treeId)
		{
			this.treeId = treeId;
			this.tree = PassiveTreeLoader.GetTree(treeId);
			this.treeManager = Main.LocalPlayer.GetModPlayer<PassiveTreeManager>();
			this.pointManager = Main.LocalPlayer.GetModPlayer<PassivePointManager>();
			
			Width.Set(0, 0.8f);
			Height.Set(0, 0.8f);
			HAlign = 0.5f;
			VAlign = 0.5f;
			
			InitializeUI();
		}

		private void InitializeUI()
		{
			// Background panel
			background = new UIPanel();
			background.Width.Set(0, 1f);
			background.Height.Set(0, 1f);
			background.BackgroundColor = new Color(33, 43, 79) * 0.95f;
			background.BorderColor = new Color(89, 116, 213);
			Append(background);
			
			// Title
			titleText = new UIText(tree.TreeName, 1.2f);
			titleText.HAlign = 0.5f;
			titleText.Top.Set(10, 0f);
			background.Append(titleText);
			
			// Points display
			pointsText = new UIText("", 0.9f);
			pointsText.HAlign = 0.5f;
			pointsText.Top.Set(40, 0f);
			background.Append(pointsText);
			
			// Node container (scrollable area)
			nodeContainer = new UIElement();
			nodeContainer.Width.Set(-20, 1f);
			nodeContainer.Height.Set(-120, 1f);
			nodeContainer.Top.Set(80, 0f);
			nodeContainer.Left.Set(10, 0f);
			nodeContainer.OverflowHidden = true;
			background.Append(nodeContainer);
			
			// Create nodes and connections
			CreateNodesAndConnections();
			
			// Center the tree view
			CenterTreeView();
		}

		private void CreateNodesAndConnections()
		{
			nodeButtons.Clear();
			connections.Clear();
			
			if (tree == null)
				return;
			
			// Create node buttons
			foreach (var nodePair in tree.Nodes)
			{
				PassiveNode node = nodePair.Value;
				PassiveNodeButton button = new PassiveNodeButton(node, treeId, treeManager);
				nodeButtons.Add(button);
				nodeContainer.Append(button);
				
				// Create connections for prerequisites
				if (node.Prerequisites != null)
				{
					foreach (string prereqId in node.Prerequisites)
					{
						PassiveNode prereqNode = tree.GetNode(prereqId);
						if (prereqNode != null)
						{
							connections.Add(new NodeConnection(prereqNode, node));
						}
					}
				}
			}
		}

		private void CenterTreeView()
		{
			if (nodeButtons.Count == 0)
				return;
			
			// Calculate bounds of all nodes
			float minX = float.MaxValue;
			float maxX = float.MinValue;
			float minY = float.MaxValue;
			float maxY = float.MinValue;
			
			foreach (var button in nodeButtons)
			{
				PassiveNode node = button.GetNode();
				float x = node.PositionX * 80f;
				float y = node.PositionY * 80f;
				
				if (x < minX) minX = x;
				if (x > maxX) maxX = x;
				if (y < minY) minY = y;
				if (y > maxY) maxY = y;
			}
			
			// Calculate center offset
			float treeWidth = maxX - minX + 48f;
			float treeHeight = maxY - minY + 48f;
			float containerWidth = nodeContainer.GetDimensions().Width;
			float containerHeight = nodeContainer.GetDimensions().Height;
			
			float offsetX = (containerWidth - treeWidth) / 2f - minX;
			float offsetY = 50f - minY; // Start near top
			
			// Apply offset to all nodes
			foreach (var button in nodeButtons)
			{
				button.Left.Set(button.Left.Pixels + offsetX, 0f);
				button.Top.Set(button.Top.Pixels + offsetY, 0f);
				button.Recalculate();
			}
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			
			// Update points display
			int availablePoints = pointManager.GetAvailablePoints();
			int spentPoints = pointManager.GetSpentPoints();
			pointsText.SetText($"Available Points: {availablePoints} | Spent: {spentPoints}");
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			
			// Draw connections between nodes
			DrawConnections(spriteBatch);
		}

		private void DrawConnections(SpriteBatch spriteBatch)
		{
			Texture2D texture = Terraria.GameContent.TextureAssets.MagicPixel.Value;
			
			foreach (var connection in connections)
			{
				// Find the buttons for these nodes
				PassiveNodeButton fromButton = null;
				PassiveNodeButton toButton = null;
				
				foreach (var button in nodeButtons)
				{
					if (button.GetNode().NodeId == connection.FromNode.NodeId)
						fromButton = button;
					if (button.GetNode().NodeId == connection.ToNode.NodeId)
						toButton = button;
				}
				
				if (fromButton == null || toButton == null)
					continue;
				
				// Get center positions of nodes
				Vector2 fromPos = fromButton.GetDimensions().Center();
				Vector2 toPos = toButton.GetDimensions().Center();
				
				// Determine connection color based on if prerequisite is met
				int prereqTier = treeManager.GetNodeTier(treeId, connection.FromNode.NodeId);
				Color lineColor = prereqTier > 0 ? new Color(100, 255, 100) : new Color(80, 80, 80);
				
				// Draw line
				DrawLine(spriteBatch, fromPos, toPos, lineColor, 2f);
			}
		}

		private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness)
		{
			Vector2 edge = end - start;
			float angle = (float)System.Math.Atan2(edge.Y, edge.X);
			
			Texture2D texture = Terraria.GameContent.TextureAssets.MagicPixel.Value;
			
			spriteBatch.Draw(
				texture,
				new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), (int)thickness),
				null,
				color,
				angle,
				new Vector2(0, 0.5f),
				SpriteEffects.None,
				0
			);
		}

		public string GetTreeId() => treeId;
	}

	/// <summary>
	/// Helper class to store node connections
	/// </summary>
	public class NodeConnection
	{
		public PassiveNode FromNode { get; set; }
		public PassiveNode ToNode { get; set; }

		public NodeConnection(PassiveNode from, PassiveNode to)
		{
			FromNode = from;
			ToNode = to;
		}
	}
}
