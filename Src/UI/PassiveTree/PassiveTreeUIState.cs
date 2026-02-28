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
		
		private Dictionary<string, PassiveTreePanel> treePanels = new Dictionary<string, PassiveTreePanel>();
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
			Mod.Logger.Info("[PassiveTreeUI] OnActivate called");
			Main.NewText("[DEBUG] OnActivate called", Color.Cyan);
			
			// Don't check player state during mod loading
			if (Main.LocalPlayer == null || !Main.LocalPlayer.active)
			{
				Mod.Logger.Info("[PassiveTreeUI] Player null or inactive, returning");
				Main.NewText("[DEBUG] Player null/inactive", Color.Red);
				return;
			}
			
			// Ensure class selection panel is initialized
			if (classSelectionPanel == null)
			{
				classSelectionPanel = new ClassSelectionPanel();
				classSelectionPanel.OnClassSelected += OnClassSelected;
				Mod.Logger.Info("[PassiveTreeUI] Created new classSelectionPanel");
			}
			
			// Check if player has selected a class
			ClassSelectionManager.PlayerClass selectedClass = ClassSelectionManager.GetSelectedClass(Main.LocalPlayer);
			Mod.Logger.Info($"[PassiveTreeUI] Selected class: {selectedClass}");
			Main.NewText($"[DEBUG] Selected class: {selectedClass}", Color.Yellow);
			
			if (selectedClass == ClassSelectionManager.PlayerClass.None)
			{
				Mod.Logger.Info("[PassiveTreeUI] No class selected, showing class selection");
				Main.NewText("[DEBUG] Showing class selection", Color.Orange);
				ShowClassSelection();
			}
			else
			{
				// Always load the tree when we have a class
				Mod.Logger.Info($"[PassiveTreeUI] Class selected: {selectedClass}, showing tree");
				Main.NewText($"[DEBUG] Showing tree for {selectedClass}", Color.Lime);
				ShowPassiveTree();
			}
		}

		private void ShowClassSelection()
		{
			Mod.Logger.Info("[PassiveTreeUI] ShowClassSelection called");
			Main.NewText("[DEBUG] ShowClassSelection called", Color.Magenta);
			
			// Clear content area and reset tree ID
			contentArea.RemoveAllChildren();
			currentTreeId = null;
			Mod.Logger.Info("[PassiveTreeUI] Cleared content area and reset currentTreeId");
			
			// Hide buttons
			if (headerPanel.HasChild(resetButton))
				resetButton.Remove();
			if (headerPanel.HasChild(respecButton))
				respecButton.Remove();
			if (mainContainer.HasChild(tabContainer))
				tabContainer.Remove();
			
			// Reinitialize class selection panel to ensure it's fresh
			classSelectionPanel = new ClassSelectionPanel();
			classSelectionPanel.OnClassSelected += OnClassSelected;
			classSelectionPanel.Activate();
			Mod.Logger.Info("[PassiveTreeUI] Created and activated classSelectionPanel");
			
			// Show class selection
			contentArea.Append(classSelectionPanel);
			showingClassSelection = true;
			titleText.SetText("Choose Your Path");
			Mod.Logger.Info("[PassiveTreeUI] Appended classSelectionPanel to contentArea");
			Main.NewText("[DEBUG] Class selection panel added", Color.Magenta);
		}

		private void ShowPassiveTree()
		{
			Mod.Logger.Info("[PassiveTreeUI] ShowPassiveTree called");
			Main.NewText("[DEBUG] ShowPassiveTree called", Color.Lime);
			
			// Clear content area and reset tree ID
			contentArea.RemoveAllChildren();
			currentTreeId = null;
			Mod.Logger.Info("[PassiveTreeUI] Cleared content area and reset currentTreeId");
			
			// Show buttons and tabs
			if (!headerPanel.HasChild(resetButton))
			{
				headerPanel.Append(resetButton);
				Mod.Logger.Info("[PassiveTreeUI] Appended resetButton");
			}
			if (!headerPanel.HasChild(respecButton))
			{
				headerPanel.Append(respecButton);
				Mod.Logger.Info("[PassiveTreeUI] Appended respecButton");
			}
			if (!mainContainer.HasChild(tabContainer))
			{
				mainContainer.Append(tabContainer);
				Mod.Logger.Info("[PassiveTreeUI] Appended tabContainer");
			}
			
			showingClassSelection = false;
			titleText.SetText("Passive Skill Tree");
			Mod.Logger.Info("[PassiveTreeUI] Set title and flags");
			
			// Load the tree for selected class
			Main.NewText("[DEBUG] About to call LoadSelectedClassTree", Color.Yellow);
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
			Mod.Logger.Info("[PassiveTreeUI] LoadSelectedClassTree called");
			Main.NewText("[DEBUG] LoadSelectedClassTree called", Color.Cyan);
			
			// Clear existing
			treePanels.Clear();
			tabButtons.Clear();
			tabContainer.RemoveAllChildren();
			Mod.Logger.Info("[PassiveTreeUI] Cleared existing panels and tabs");
			
			// Get tree for selected class
			ClassSelectionManager.PlayerClass selectedClass = ClassSelectionManager.GetSelectedClass(Main.LocalPlayer);
			Mod.Logger.Info($"[PassiveTreeUI] Selected class from manager: {selectedClass}");
			Main.NewText($"[DEBUG] Got class: {selectedClass}", Color.Yellow);
			
			string treeId = ClassSelectionManager.GetTreeIdForClass(selectedClass);
			Mod.Logger.Info($"[PassiveTreeUI] Tree ID for class: {treeId}");
			Main.NewText($"[DEBUG] Tree ID: {treeId}", Color.Yellow);
			
			if (string.IsNullOrEmpty(treeId))
			{
				Mod.Logger.Error("[PassiveTreeUI] Tree ID is null or empty!");
				UIText errorText = new UIText("No tree found for selected class!", 1.2f);
				errorText.HAlign = 0.5f;
				errorText.VAlign = 0.5f;
				errorText.TextColor = Color.Red;
				contentArea.Append(errorText);
				Main.NewText("Error: No tree ID for class " + selectedClass, Color.Red);
				return;
			}
			
			PassiveTreeType tree = PassiveTreeLoader.GetTree(treeId);
			Mod.Logger.Info($"[PassiveTreeUI] Loaded tree: {(tree != null ? tree.TreeName : "NULL")}");
			Main.NewText($"[DEBUG] Tree loaded: {(tree != null ? tree.TreeName : "NULL")}", Color.Yellow);
			
			if (tree == null)
			{
				Mod.Logger.Error($"[PassiveTreeUI] Failed to load tree '{treeId}'");
				UIText errorText = new UIText($"Tree '{treeId}' not found!", 1.2f);
				errorText.HAlign = 0.5f;
				errorText.VAlign = 0.5f;
				errorText.TextColor = Color.Red;
				contentArea.Append(errorText);
				Main.NewText($"Error: Tree '{treeId}' not loaded", Color.Red);
				return;
			}
			
			// Create tab button
			Mod.Logger.Info($"[PassiveTreeUI] Creating tab button for '{tree.TreeName}'");
			TreeTabButton tabButton = new TreeTabButton(tree.TreeName, treeId);
			tabButton.Left.Set(10, 0f);
			tabButton.VAlign = 0.5f;
			tabButton.OnLeftClick += (evt, element) => SwitchToTree(treeId);
			tabContainer.Append(tabButton);
			tabButtons[treeId] = tabButton;
			Mod.Logger.Info($"[PassiveTreeUI] Tab button created and appended");
			Main.NewText("[DEBUG] Tab button created", Color.Lime);
			
			// Create tree panel
			Mod.Logger.Info($"[PassiveTreeUI] Creating PassiveTreePanel for '{treeId}'");
			PassiveTreePanel panel = new PassiveTreePanel(treeId);
			panel.Activate(); // Ensure panel is activated
			treePanels[treeId] = panel;
			Mod.Logger.Info($"[PassiveTreeUI] Panel created and stored");
			Main.NewText("[DEBUG] Panel created", Color.Lime);
			
			// Show the tree
			Mod.Logger.Info($"[PassiveTreeUI] About to call SwitchToTree");
			Main.NewText("[DEBUG] Calling SwitchToTree", Color.Cyan);
			SwitchToTree(treeId);
			
			// Force recalculation
			Mod.Logger.Info($"[PassiveTreeUI] Calling Recalculate");
			Recalculate();
			Mod.Logger.Info($"[PassiveTreeUI] LoadSelectedClassTree completed");
			Main.NewText("[DEBUG] LoadSelectedClassTree complete", Color.Lime);
		}

		private void ResetCurrentTree()
		{
			if (showingClassSelection || string.IsNullOrEmpty(currentTreeId))
				return;
			
			// Get tree manager and reset
			PassiveTreeManager manager = Main.LocalPlayer.GetModPlayer<PassiveTreeManager>();
			PassivePointManager pointManager = Main.LocalPlayer.GetModPlayer<PassivePointManager>();
			
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
			Mod.Logger.Info($"[PassiveTreeUI] SwitchToTree called with treeId: {treeId}, currentTreeId: {currentTreeId}");
			Main.NewText($"[DEBUG] SwitchToTree: {treeId}", Color.Magenta);
			
			if (currentTreeId == treeId)
			{
				Mod.Logger.Info($"[PassiveTreeUI] Already showing this tree, returning");
				return;
			}
			
			// Hide current panel
			if (!string.IsNullOrEmpty(currentTreeId) && treePanels.ContainsKey(currentTreeId))
			{
				contentArea.RemoveChild(treePanels[currentTreeId]);
				Mod.Logger.Info($"[PassiveTreeUI] Removed previous panel: {currentTreeId}");
				
				// Deactivate tab
				if (tabButtons.ContainsKey(currentTreeId))
					tabButtons[currentTreeId].SetActive(false);
			}
			
			// Show new panel
			currentTreeId = treeId;
			Mod.Logger.Info($"[PassiveTreeUI] treePanels contains '{treeId}': {treePanels.ContainsKey(treeId)}");
			Main.NewText($"[DEBUG] Panel exists: {treePanels.ContainsKey(treeId)}", Color.Yellow);
			
			if (treePanels.ContainsKey(treeId))
			{
				Mod.Logger.Info($"[PassiveTreeUI] Appending panel to contentArea");
				contentArea.Append(treePanels[treeId]);
				Mod.Logger.Info($"[PassiveTreeUI] Panel appended successfully");
				Main.NewText($"[DEBUG] Panel appended!", Color.Lime);
				
				// Activate tab
				if (tabButtons.ContainsKey(treeId))
				{
					tabButtons[treeId].SetActive(true);
					Mod.Logger.Info($"[PassiveTreeUI] Tab activated");
				}
			}
			else
			{
				Mod.Logger.Error($"[PassiveTreeUI] Panel not found in treePanels dictionary!");
				Main.NewText($"[DEBUG ERROR] Panel not in dictionary!", Color.Red);
			}
			Mod.Logger.Info($"[PassiveTreeUI] SwitchToTree completed");
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
