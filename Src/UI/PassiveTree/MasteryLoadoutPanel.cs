using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.UI;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints;
using ProgressionExpanded.Src.Levels.PlayerSystems;
using PassiveTreeType = ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints.PassiveTree;
// UIGrid lives in tModLoader's own namespace, unlike UIList/UIScrollbar (Terraria.GameContent.UI.Elements).
using UIGrid = Terraria.ModLoader.UI.Elements.UIGrid;

namespace ProgressionExpanded.Src.UI.PassiveTree
{
	/// <summary>
	/// "Mastery Loadout" view: instead of a spatial tree, the player has a fixed number of mastery
	/// slots (<see cref="PassiveTreeManager.MasterySlotCount"/>). Empty slots open a picker; socketed
	/// masteries are ranked up/down or cleared. Everything is laid out with tModLoader's managed
	/// containers (<see cref="UIGrid"/> for slots, <see cref="UIList"/> for the picker) so it cannot
	/// overflow the panel and has no lines to keep aligned — the failure mode of the old tree canvas.
	/// </summary>
	public class MasteryLoadoutPanel : UIElement
	{
		private readonly string treeId;
		private readonly PassiveTreeType tree;
		private readonly PassiveTreeManager treeManager;
		private readonly PassivePointManager pointManager;

		private UIPanel background;
		private UIText titleText;
		private UIText infoText;

		private UIElement loadoutContainer;
		private UIGrid slotsGrid;
		private UIScrollbar slotsScrollbar;

		private UIElement pickerContainer;
		private UIList pickerList;
		private UIScrollbar pickerScrollbar;

		// Tooltip requested by the hovered slot card this frame; drawn in Draw() outside the grid clip.
		private string hoverTooltip;

		public MasteryLoadoutPanel(string treeId)
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
			background = new UIPanel();
			background.Width.Set(0, 1f);
			background.Height.Set(0, 1f);
			background.BackgroundColor = new Color(33, 43, 79) * 0.95f;
			background.BorderColor = new Color(89, 116, 213);
			Append(background);

			titleText = new UIText(tree != null ? $"{tree.TreeName} — Mastery Loadout" : "Mastery Loadout", 1.2f);
			titleText.HAlign = 0.5f;
			titleText.Top.Set(6, 0f);
			background.Append(titleText);

			infoText = new UIText("", 0.9f);
			infoText.HAlign = 0.5f;
			infoText.Top.Set(34, 0f);
			background.Append(infoText);

			// ---- Loadout (slots) container ----
			loadoutContainer = new UIElement();
			loadoutContainer.Width.Set(0, 1f);
			loadoutContainer.Height.Set(-64, 1f);
			loadoutContainer.Top.Set(60, 0f);

			slotsGrid = new UIGrid();
			slotsGrid.Width.Set(-24, 1f);
			slotsGrid.Height.Set(0, 1f);
			slotsGrid.ListPadding = 12f;
			loadoutContainer.Append(slotsGrid);

			slotsScrollbar = new UIScrollbar();
			slotsScrollbar.SetView(100f, 1000f);
			slotsScrollbar.Height.Set(0, 1f);
			slotsScrollbar.HAlign = 1f;
			loadoutContainer.Append(slotsScrollbar);
			slotsGrid.SetScrollbar(slotsScrollbar);

			// ---- Picker container (hidden until an empty slot is clicked) ----
			pickerContainer = new UIElement();
			pickerContainer.Width.Set(0, 1f);
			pickerContainer.Height.Set(-64, 1f);
			pickerContainer.Top.Set(60, 0f);

			UIText pickerTitle = new UIText("Choose a mastery to socket", 1f);
			pickerTitle.HAlign = 0.5f;
			pickerTitle.Top.Set(0, 0f);
			pickerContainer.Append(pickerTitle);

			SmallButton cancelButton = new SmallButton("Cancel", new Color(120, 60, 60), ShowLoadoutMode, 80f, 26f);
			cancelButton.HAlign = 1f;
			cancelButton.Top.Set(-2, 0f);
			pickerContainer.Append(cancelButton);

			pickerList = new UIList();
			pickerList.Width.Set(-24, 1f);
			pickerList.Height.Set(-34, 1f);
			pickerList.Top.Set(34, 0f);
			pickerList.ListPadding = 6f;
			pickerContainer.Append(pickerList);

			pickerScrollbar = new UIScrollbar();
			pickerScrollbar.SetView(100f, 1000f);
			pickerScrollbar.Height.Set(-34, 1f);
			pickerScrollbar.Top.Set(34, 0f);
			pickerScrollbar.HAlign = 1f;
			pickerContainer.Append(pickerScrollbar);
			pickerList.SetScrollbar(pickerScrollbar);

			// Start in loadout mode.
			background.Append(loadoutContainer);
			RebuildSlots();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			if (pointManager != null && treeManager != null)
			{
				infoText.SetText(
					$"Available: {pointManager.GetAvailablePoints()} pts   |   Slots: {treeManager.GetSocketedCount(treeId)}/{PassiveTreeManager.MasterySlotCount}");
			}
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			hoverTooltip = null;
			base.Draw(spriteBatch);

			// Slot-card tooltips are drawn here — after base.Draw and outside the UIGrid's clip
			// region — so they don't get cut off at the grid edge.
			if (!string.IsNullOrEmpty(hoverTooltip))
				Main.instance.MouseText(hoverTooltip);
		}

		public void SetHoverTooltip(string text)
		{
			hoverTooltip = text;
		}

		#region Mode switching

		private void ShowLoadoutMode()
		{
			if (pickerContainer.Parent != null)
				pickerContainer.Remove();
			if (loadoutContainer.Parent == null)
				background.Append(loadoutContainer);

			RebuildSlots();
			Recalculate();
		}

		public void OpenPicker()
		{
			if (loadoutContainer.Parent != null)
				loadoutContainer.Remove();
			if (pickerContainer.Parent == null)
				background.Append(pickerContainer);

			RebuildPicker();
			Recalculate();
		}

		#endregion

		#region Rebuilds

		private void RebuildSlots()
		{
			slotsGrid.Clear();
			if (tree == null)
				return;

			// Socketed masteries first (stable order by display name), then empty slots.
			List<PassiveNode> socketed = new List<PassiveNode>();
			foreach (var pair in tree.Nodes)
			{
				if (treeManager.GetNodeTier(treeId, pair.Value.NodeId) > 0)
					socketed.Add(pair.Value);
			}
			socketed.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal));

			foreach (PassiveNode node in socketed)
				slotsGrid.Add(new MasterySlotCard(this, treeManager, treeId, node));

			int emptySlots = PassiveTreeManager.MasterySlotCount - socketed.Count;
			for (int i = 0; i < emptySlots; i++)
				slotsGrid.Add(new MasterySlotCard(this, treeManager, treeId, null));
		}

		private void RebuildPicker()
		{
			pickerList.Clear();
			if (tree == null)
				return;

			List<PassiveNode> available = new List<PassiveNode>();
			foreach (var pair in tree.Nodes)
			{
				if (treeManager.GetNodeTier(treeId, pair.Value.NodeId) <= 0)
					available.Add(pair.Value);
			}
			available.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal));

			foreach (PassiveNode node in available)
				pickerList.Add(new MasteryPickerRow(this, node, pointManager));
		}

		#endregion

		#region Actions (called by slot cards / picker rows)

		public void SocketMastery(PassiveNode node)
		{
			if (treeManager.AllocateMastery(treeId, node.NodeId))
			{
				SoundEngine.PlaySound(SoundID.MenuTick);
				ShowLoadoutMode();
			}
			else
			{
				SoundEngine.PlaySound(SoundID.MenuClose);
			}
		}

		public void RankUp(PassiveNode node)
		{
			bool ok = treeManager.AllocateMastery(treeId, node.NodeId);
			SoundEngine.PlaySound(ok ? SoundID.MenuTick : SoundID.MenuClose);
			RebuildSlots();
			Recalculate();
		}

		public void RankDown(PassiveNode node)
		{
			bool ok = treeManager.DeallocateMastery(treeId, node.NodeId);
			SoundEngine.PlaySound(ok ? SoundID.MenuTick : SoundID.MenuClose);
			RebuildSlots();
			Recalculate();
		}

		public void ClearSlot(PassiveNode node)
		{
			treeManager.ClearMastery(treeId, node.NodeId);
			SoundEngine.PlaySound(SoundID.MenuClose);
			RebuildSlots();
			Recalculate();
		}

		#endregion

		/// <summary>Category tint derived from a node's dominant stat (offense / defense / utility / notable).</summary>
		public static Color CategoryColor(PassiveNode node)
		{
			foreach (string key in node.FlatBonuses.Keys)
			{
				Color? c = CategoryFor(key);
				if (c.HasValue) return c.Value;
			}
			foreach (string key in node.PercentBonuses.Keys)
			{
				Color? c = CategoryFor(key);
				if (c.HasValue) return c.Value;
			}
			return new Color(150, 110, 200); // notable / effect-only (e.g. Stagger)
		}

		private static Color? CategoryFor(string statKey)
		{
			switch (statKey)
			{
				case "MeleeDamage":
				case "MagicDamage":
				case "RangedDamage":
				case "SummonDamage":
				case "GenericDamage":
				case "CritChance":
				case "ArmorPenetration":
				case "AttackSpeed":
				case "Knockback":
				case "MinionSlots":
				case "ManaEfficiency":
					return new Color(200, 90, 80);    // offense — red
				case "Defense":
				case "LifeRegen":
				case "Endurance":
				case "MaxHealth":
					return new Color(80, 140, 200);   // defense — blue
				case "MovementSpeed":
					return new Color(210, 190, 90);   // utility — gold
				default:
					return null;
			}
		}
	}

	/// <summary>One mastery slot: either empty (click to open the picker) or holding a ranked mastery.</summary>
	public class MasterySlotCard : UIPanel
	{
		private const float CARD_W = 156f;
		private const float CARD_H = 118f;

		private readonly MasteryLoadoutPanel owner;
		private readonly PassiveTreeManager treeManager;
		private readonly string treeId;
		private readonly PassiveNode node; // null = empty
		private readonly int tier;
		private readonly int maxTier;

		public MasterySlotCard(MasteryLoadoutPanel owner, PassiveTreeManager treeManager, string treeId, PassiveNode node)
		{
			this.owner = owner;
			this.treeManager = treeManager;
			this.treeId = treeId;
			this.node = node;
			this.tier = node != null ? treeManager.GetNodeTier(treeId, node.NodeId) : 0;
			this.maxTier = node != null ? node.MaxTier : 0;

			Width.Set(CARD_W, 0f);
			Height.Set(CARD_H, 0f);
			SetPadding(6f);

			if (node == null)
				BuildEmpty();
			else
				BuildFilled();
		}

		private void BuildEmpty()
		{
			BackgroundColor = new Color(20, 26, 46) * 0.9f;
			BorderColor = new Color(60, 70, 100);

			UIText plus = new UIText("+", 1.6f);
			plus.HAlign = 0.5f;
			plus.Top.Set(20, 0f);
			Append(plus);

			UIText label = new UIText("Empty", 0.85f);
			label.HAlign = 0.5f;
			label.Top.Set(58, 0f);
			Append(label);

			UIText hint = new UIText("click to socket", 0.7f);
			hint.HAlign = 0.5f;
			hint.Top.Set(80, 0f);
			hint.TextColor = new Color(150, 160, 190);
			Append(hint);
		}

		private void BuildFilled()
		{
			BackgroundColor = new Color(38, 50, 84) * 0.97f;
			BorderColor = MasteryLoadoutPanel.CategoryColor(node);

			UIText name = new UIText(node.DisplayName, 0.85f);
			name.HAlign = 0.5f;
			name.Top.Set(6, 0f);
			Append(name);

			// [-] and [+] at the bottom centre.
			SmallButton minus = new SmallButton("-", new Color(120, 70, 70), () => owner.RankDown(node), 30f, 24f);
			minus.HAlign = 0.5f;
			minus.VAlign = 1f;
			minus.Left.Set(-20, 0f);
			Append(minus);

			SmallButton plus = new SmallButton("+", new Color(70, 120, 80), () => owner.RankUp(node), 30f, 24f);
			plus.HAlign = 0.5f;
			plus.VAlign = 1f;
			plus.Left.Set(20, 0f);
			Append(plus);

			// Clear (x) at the top-right corner.
			SmallButton clear = new SmallButton("x", new Color(110, 60, 60), () => owner.ClearSlot(node), 20f, 20f);
			clear.HAlign = 1f;
			clear.Top.Set(-2, 0f);
			Append(clear);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);

			if (node == null)
				return;

			DrawPips(spriteBatch);

			// Report the tooltip to the owner, which draws it outside the grid's clip region.
			if (IsMouseHovering)
				owner.SetHoverTooltip(BuildTooltip());
		}

		private void DrawPips(SpriteBatch spriteBatch)
		{
			CalculatedStyle dims = GetInnerDimensions();
			const float pip = 11f;
			const float gap = 4f;
			float totalW = maxTier * pip + (maxTier - 1) * gap;
			float startX = dims.X + (dims.Width - totalW) * 0.5f;
			float y = dims.Y + dims.Height - 52f;

			Texture2D px = TextureAssets.MagicPixel.Value;
			for (int i = 0; i < maxTier; i++)
			{
				var r = new Rectangle((int)(startX + i * (pip + gap)), (int)y, (int)pip, (int)pip);
				Color c = i < tier ? new Color(255, 215, 0) : new Color(60, 66, 90);
				spriteBatch.Draw(px, r, c);
			}
		}

		private string BuildTooltip()
		{
			string text = node.DisplayName;
			if (maxTier > 1)
				text += $" ({tier}/{maxTier})";
			text += "\n" + node.GetFormattedDescription(tier);

			if (tier < maxTier)
				text += $"\n\n[+] Rank up: {node.GetUpgradeCost(tier)} point(s)";
			text += "\n[-] Rank down   [x] Unsocket";
			return text;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			// Empty cards open the picker on any body click (clicks on child text bubble up here);
			// filled cards act only via their [-]/[+]/x buttons (node != null → no-op here).
			if (node == null)
				owner.OpenPicker();
		}
	}

	/// <summary>One selectable mastery in the socket picker.</summary>
	public class MasteryPickerRow : UIPanel
	{
		private readonly MasteryLoadoutPanel owner;
		private readonly PassiveNode node;
		private readonly bool affordable;

		public MasteryPickerRow(MasteryLoadoutPanel owner, PassiveNode node, PassivePointManager pointManager)
		{
			this.owner = owner;
			this.node = node;

			int cost = node.GetUpgradeCost(0);
			this.affordable = pointManager != null && pointManager.HasEnoughPoints(cost);

			Width.Set(0, 1f);
			Height.Set(64f, 0f);
			SetPadding(8f);
			BackgroundColor = (affordable ? new Color(40, 52, 88) : new Color(30, 34, 50)) * 0.95f;
			BorderColor = affordable ? MasteryLoadoutPanel.CategoryColor(node) : new Color(60, 60, 70);

			UIText name = new UIText(node.DisplayName, 0.95f);
			name.Left.Set(2, 0f);
			name.Top.Set(2, 0f);
			if (!affordable)
				name.TextColor = new Color(150, 150, 160);
			Append(name);

			UIText effect = new UIText(FirstEffectLine(node), 0.75f);
			effect.Left.Set(2, 0f);
			effect.Top.Set(28, 0f);
			effect.TextColor = new Color(170, 180, 205);
			Append(effect);

			UIText costText = new UIText($"{cost} pt", 0.85f);
			costText.HAlign = 1f;
			costText.VAlign = 0.5f;
			costText.TextColor = affordable ? new Color(120, 230, 120) : new Color(220, 120, 120);
			Append(costText);
		}

		private static string FirstEffectLine(PassiveNode node)
		{
			foreach (var b in node.FlatBonuses)
				return $"+{b.Value} {b.Key}";
			foreach (var b in node.PercentBonuses)
				return $"+{b.Value * 100f}% {b.Key}";
			return node.Description;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			if (affordable)
				owner.SocketMastery(node);
			else
				SoundEngine.PlaySound(SoundID.MenuClose);
		}

		public override void MouseOver(UIMouseEvent evt)
		{
			base.MouseOver(evt);
			if (affordable)
				BackgroundColor = new Color(56, 72, 116) * 0.98f;
		}

		public override void MouseOut(UIMouseEvent evt)
		{
			base.MouseOut(evt);
			if (affordable)
				BackgroundColor = new Color(40, 52, 88) * 0.95f;
		}
	}

	/// <summary>A compact clickable button (a small labelled panel) used for [-]/[+]/x and Cancel.</summary>
	public class SmallButton : UIPanel
	{
		private readonly Action onClick;
		private readonly Color baseColor;

		public SmallButton(string label, Color color, Action onClick, float width, float height)
		{
			this.onClick = onClick;
			this.baseColor = color;

			Width.Set(width, 0f);
			Height.Set(height, 0f);
			SetPadding(0f);
			BackgroundColor = color;
			BorderColor = new Color(0, 0, 0) * 0.4f;

			UIText text = new UIText(label, 0.9f);
			text.HAlign = 0.5f;
			text.VAlign = 0.5f;
			Append(text);
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			onClick?.Invoke();
		}

		public override void MouseOver(UIMouseEvent evt)
		{
			base.MouseOver(evt);
			BackgroundColor = baseColor * 1.35f;
		}

		public override void MouseOut(UIMouseEvent evt)
		{
			base.MouseOut(evt);
			BackgroundColor = baseColor;
		}
	}
}
