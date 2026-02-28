using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.UI;
using Terraria.GameContent.UI.Elements;
using ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints;
using PassiveTreeType = ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints.PassiveTree;

namespace ProgressionExpanded.Src.UI.PassiveTree
{
	/// <summary>
	/// Main UI state for the passive tree interface
	/// </summary>
	public class PassiveTreeUIState : UIState
	{
		private UIElement mainContainer;
		private UIPanel headerPanel;
		private UIText titleText;
		private UIPanel tabContainer;
		private UIElement contentArea;
		
		private Dictionary<string, PassiveTreePanel> treePanels = new Dictionary<string, PassiveTreePanel>();
		private Dictionary<string, TreeTabButton> tabButtons = new Dictionary<string, TreeTabButton>();
		private string currentTreeId;
		
		private UITextPanel<string> closeButton;

		public override void OnInitialize()
		{
			// Main container
			mainContainer = new UIElement();
			mainContainer.Width.Set(0, 0.9f);
			mainContainer.Height.Set(0, 0.9f);
			mainContainer.HAlign = 0.5f;
			mainContainer.VAlign = 0.5f;
			Append(mainContainer);
			
			// Header panel
			headerPanel = new UIPanel();
			headerPanel.Width.Set(0, 1f);
			headerPanel.Height.Set(60, 0f);
			headerPanel.BackgroundColor = new Color(26, 40, 89) * 0.95f;
			headerPanel.BorderColor = new Color(89, 116, 213);
			mainContainer.Append(headerPanel);
			
			// Title
			titleText = new UIText("Passive Skill Tree", 1.5f);
			titleText.HAlign = 0.5f;
			titleText.VAlign = 0.5f;
			headerPanel.Append(titleText);
			
			// Close button
			closeButton = new UITextPanel<string>("X");
			closeButton.Width.Set(40, 0f);
			closeButton.Height.Set(40, 0f);
			closeButton.HAlign = 1f;
			closeButton.VAlign = 0.5f;
			closeButton.Left.Set(-10, 0f);
			closeButton.BackgroundColor = new Color(200, 50, 50);
			closeButton.OnLeftClick += (evt, element) => Close();
			headerPanel.Append(closeButton);
			
			// Tab container
			tabContainer = new UIPanel();
			tabContainer.Width.Set(0, 1f);
			tabContainer.Height.Set(50, 0f);
			tabContainer.Top.Set(60, 0f);
			tabContainer.BackgroundColor = new Color(30, 40, 70) * 0.95f;
			tabContainer.BorderColor = new Color(89, 116, 213);
			mainContainer.Append(tabContainer);
			
			// Content area
			contentArea = new UIElement();
			contentArea.Width.Set(0, 1f);
			contentArea.Height.Set(-110, 1f);
			contentArea.Top.Set(110, 0f);
			mainContainer.Append(contentArea);
			
			// Load all trees
			LoadTrees();
		}

		private void LoadTrees()
		{
			var treeIds = PassiveTreeLoader.GetLoadedTreeIds();
			
			if (treeIds.Count == 0)
			{
				// No trees loaded
				UIText noTreesText = new UIText("No passive trees loaded!", 1.2f);
				noTreesText.HAlign = 0.5f;
				noTreesText.VAlign = 0.5f;
				noTreesText.TextColor = Color.Red;
				contentArea.Append(noTreesText);
				return;
			}
			
			// Create tabs and panels for each tree
			float tabX = 10f;
			foreach (string treeId in treeIds)
			{
				PassiveTreeType tree = PassiveTreeLoader.GetTree(treeId);
				if (tree == null) continue;
				
				// Create tab button
				TreeTabButton tabButton = new TreeTabButton(tree.TreeName, treeId);
				tabButton.Left.Set(tabX, 0f);
				tabButton.VAlign = 0.5f;
				tabButton.OnLeftClick += (evt, element) => SwitchToTree(treeId);
				tabContainer.Append(tabButton);
				tabButtons[treeId] = tabButton;
				
				tabX += tabButton.Width.Pixels + 10f;
				
				// Create tree panel
				PassiveTreePanel panel = new PassiveTreePanel(treeId);
				treePanels[treeId] = panel;
			}
			
			// Show first tree by default
			if (treeIds.Count > 0)
			{
				SwitchToTree(treeIds[0]);
			}
		}

		private void SwitchToTree(string treeId)
		{
			if (currentTreeId == treeId)
				return;
			
			// Hide current panel
			if (!string.IsNullOrEmpty(currentTreeId) && treePanels.ContainsKey(currentTreeId))
			{
				contentArea.RemoveChild(treePanels[currentTreeId]);
				
				// Deactivate tab
				if (tabButtons.ContainsKey(currentTreeId))
					tabButtons[currentTreeId].SetActive(false);
			}
			
			// Show new panel
			currentTreeId = treeId;
			if (treePanels.ContainsKey(treeId))
			{
				contentArea.Append(treePanels[treeId]);
				
				// Activate tab
				if (tabButtons.ContainsKey(treeId))
					tabButtons[treeId].SetActive(true);
			}
		}

		public void Close()
		{
			PassiveTreeUISystem.Instance?.CloseUI();
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			// Draw dark overlay
			spriteBatch.Draw(
				Terraria.GameContent.TextureAssets.MagicPixel.Value,
				new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
				Color.Black * 0.5f
			);
			
			base.Draw(spriteBatch);
		}
	}

	/// <summary>
	/// Tab button for switching between trees
	/// </summary>
	public class TreeTabButton : UIPanel
	{
		private UIText text;
		private string treeId;
		private bool isActive = false;

		public TreeTabButton(string label, string treeId)
		{
			this.treeId = treeId;
			
			Width.Set(150, 0f);
			Height.Set(40, 0f);
			
			text = new UIText(label, 0.9f);
			text.HAlign = 0.5f;
			text.VAlign = 0.5f;
			Append(text);
			
			SetActive(false);
		}

		public void SetActive(bool active)
		{
			isActive = active;
			
			if (active)
			{
				BackgroundColor = new Color(89, 116, 213) * 0.8f;
				BorderColor = Color.White;
			}
			else
			{
				BackgroundColor = new Color(50, 60, 90) * 0.7f;
				BorderColor = new Color(89, 116, 213);
			}
		}

		public override void MouseOver(UIMouseEvent evt)
		{
			base.MouseOver(evt);
			if (!isActive)
			{
				BackgroundColor = new Color(70, 80, 110) * 0.8f;
			}
		}

		public override void MouseOut(UIMouseEvent evt)
		{
			base.MouseOut(evt);
			if (!isActive)
			{
				BackgroundColor = new Color(50, 60, 90) * 0.7f;
			}
		}

		public string GetTreeId() => treeId;
	}
}
