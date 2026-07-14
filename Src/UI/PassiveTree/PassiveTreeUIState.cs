using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.UI;
using Terraria.GameContent.UI.Elements;
using ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints;
using ProgressionExpanded.Src.Levels.PlayerSystems;
using PassiveTreeType = ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints.PassiveTree;
using Terraria.ModLoader;

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
		
		private Dictionary<string, MasteryLoadoutPanel> treePanels = new Dictionary<string, MasteryLoadoutPanel>();
		private Dictionary<string, TreeTabButton> tabButtons = new Dictionary<string, TreeTabButton>();
		private string currentTreeId;
		
		private UITextPanel<string> closeButton;
		private UITextPanel<string> resetButton;
		private UITextPanel<string> respecButton;
		
		private ClassSelectionPanel classSelectionPanel;
		private bool showingClassSelection = false;
		
		// Cache mod reference to avoid namespace conflicts
		private static Mod Mod => ModContent.GetInstance<global::ProgressionExpanded.ProgressionExpanded>();

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
			
			// Reset button (resets current tree)
			resetButton = new UITextPanel<string>("Reset Tree");
			resetButton.Width.Set(100, 0f);
			resetButton.Height.Set(40, 0f);
			resetButton.HAlign = 1f;
			resetButton.VAlign = 0.5f;
			resetButton.Left.Set(-60, 0f);
			resetButton.BackgroundColor = new Color(200, 100, 50);
			resetButton.OnLeftClick += (evt, element) => ResetCurrentTree();
			headerPanel.Append(resetButton);
			
			// Respec button (resets tree and returns to class selection)
			respecButton = new UITextPanel<string>("Respec Class");
			respecButton.Width.Set(120, 0f);
			respecButton.Height.Set(40, 0f);
			respecButton.HAlign = 1f;
			respecButton.VAlign = 0.5f;
			respecButton.Left.Set(-170, 0f);
			respecButton.BackgroundColor = new Color(150, 50, 200);
			respecButton.OnLeftClick += (evt, element) => RespecClass();
			headerPanel.Append(respecButton);
			
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
			
			// Initialize class selection panel
			classSelectionPanel = new ClassSelectionPanel();
			classSelectionPanel.OnClassSelected += OnClassSelected;
		}

		public override void OnActivate()
		{
			base.OnActivate();

			// Don't check player state during mod loading.
			if (Main.LocalPlayer == null || !Main.LocalPlayer.active)
				return;

			// Ensure the class selection panel exists.
			if (classSelectionPanel == null)
			{
				classSelectionPanel = new ClassSelectionPanel();
				classSelectionPanel.OnClassSelected += OnClassSelected;
			}

			// Show class selection or the tree depending on whether a class is chosen.
			ClassSelectionManager.PlayerClass selectedClass = ClassSelectionManager.GetSelectedClass(Main.LocalPlayer);
			if (selectedClass == ClassSelectionManager.PlayerClass.None)
				ShowClassSelection();
			else
				ShowPassiveTree();
		}

		private void ShowClassSelection()
		{
			// Clear content area and reset tree ID.
			contentArea.RemoveAllChildren();
			currentTreeId = null;

			// Hide tree-only buttons and tabs.
			if (headerPanel.HasChild(resetButton))
				resetButton.Remove();
			if (headerPanel.HasChild(respecButton))
				respecButton.Remove();
			if (mainContainer.HasChild(tabContainer))
				tabContainer.Remove();

			// Fresh class selection panel.
			classSelectionPanel = new ClassSelectionPanel();
			classSelectionPanel.OnClassSelected += OnClassSelected;
			classSelectionPanel.Activate();

			contentArea.Append(classSelectionPanel);
			showingClassSelection = true;
			titleText.SetText("Choose Your Path");
		}

		private void ShowPassiveTree()
		{
			// Clear content area and reset tree ID.
			contentArea.RemoveAllChildren();
			currentTreeId = null;

			// Show tree buttons and tabs.
			if (!headerPanel.HasChild(resetButton))
				headerPanel.Append(resetButton);
			if (!headerPanel.HasChild(respecButton))
				headerPanel.Append(respecButton);
			if (!mainContainer.HasChild(tabContainer))
				mainContainer.Append(tabContainer);

			showingClassSelection = false;
			titleText.SetText("Passive Skill Tree");

			LoadSelectedClassTree();
		}

		private void OnClassSelected(ClassSelectionManager.PlayerClass playerClass)
		{
			// Save selection
			ClassSelectionManager.SetSelectedClass(Main.LocalPlayer, playerClass);
			
			// Show passive tree
			ShowPassiveTree();
		}

		private void LoadSelectedClassTree()
		{
			// Clear existing panels/tabs.
			treePanels.Clear();
			tabButtons.Clear();
			tabContainer.RemoveAllChildren();

			// Resolve the tree for the selected class.
			ClassSelectionManager.PlayerClass selectedClass = ClassSelectionManager.GetSelectedClass(Main.LocalPlayer);
			string treeId = ClassSelectionManager.GetTreeIdForClass(selectedClass);

			if (string.IsNullOrEmpty(treeId))
			{
				ShowContentError($"No tree found for class {selectedClass}.");
				return;
			}

			PassiveTreeType tree = PassiveTreeLoader.GetTree(treeId);
			if (tree == null)
			{
				Mod.Logger.Error($"[PassiveTree] Tree '{treeId}' is not loaded.");
				ShowContentError($"Tree '{treeId}' not found!");
				return;
			}

			// Tab button for this tree.
			TreeTabButton tabButton = new TreeTabButton(tree.TreeName, treeId);
			tabButton.Left.Set(10, 0f);
			tabButton.VAlign = 0.5f;
			tabButton.OnLeftClick += (evt, element) => SwitchToTree(treeId);
			tabContainer.Append(tabButton);
			tabButtons[treeId] = tabButton;

			// Mastery loadout panel for this tree.
			MasteryLoadoutPanel panel = new MasteryLoadoutPanel(treeId);
			panel.Activate();
			treePanels[treeId] = panel;

			SwitchToTree(treeId);
			Recalculate();
		}

		private void ShowContentError(string message)
		{
			UIText errorText = new UIText(message, 1.2f);
			errorText.HAlign = 0.5f;
			errorText.VAlign = 0.5f;
			errorText.TextColor = Color.Red;
			contentArea.Append(errorText);
		}

		private void ResetCurrentTree()
		{
			if (showingClassSelection || string.IsNullOrEmpty(currentTreeId))
				return;
			
			// Get tree manager and reset
			PassiveTreeManager manager = Main.LocalPlayer.GetModPlayer<PassiveTreeManager>();
			manager.ResetTree(currentTreeId);
			
			// Refresh UI
			ShowPassiveTree();
			
			Main.NewText("Tree reset! All points refunded.", Color.Yellow);
		}

		private void RespecClass()
		{
			if (showingClassSelection)
				return;
			
			// Reset all allocations
			PassiveTreeManager manager = Main.LocalPlayer.GetModPlayer<PassiveTreeManager>();
			manager.ResetAllTrees();
			
			// Reset class selection
			ClassSelectionManager.ResetClass(Main.LocalPlayer);
			
			// Show class selection
			ShowClassSelection();
			
			Main.NewText("Class reset! Choose a new path.", Color.Yellow);
		}

		private void SwitchToTree(string treeId)
		{
			if (currentTreeId == treeId)
				return;

			// Hide the current panel and deactivate its tab.
			if (!string.IsNullOrEmpty(currentTreeId) && treePanels.ContainsKey(currentTreeId))
			{
				contentArea.RemoveChild(treePanels[currentTreeId]);
				if (tabButtons.ContainsKey(currentTreeId))
					tabButtons[currentTreeId].SetActive(false);
			}

			// Show the new panel and activate its tab.
			currentTreeId = treeId;
			if (treePanels.ContainsKey(treeId))
			{
				contentArea.Append(treePanels[treeId]);
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
