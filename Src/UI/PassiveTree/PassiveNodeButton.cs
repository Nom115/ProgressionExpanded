using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.UI;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.Audio;
using ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints;

namespace ProgressionExpanded.Src.UI.PassiveTree
{
	/// <summary>
	/// Individual node button in the passive tree
	/// </summary>
	public class PassiveNodeButton : UIElement
	{
		private readonly PassiveNode node;
		private readonly string treeId;
		private readonly PassiveTreeManager treeManager;
		
		private const float NODE_SIZE = 48f;
		private const float NODE_SPACING = 80f;
		
		private bool isHovered = false;
		private int currentTier = 0;
		private bool canAllocate = false;

		public PassiveNodeButton(PassiveNode node, string treeId, PassiveTreeManager treeManager)
		{
			this.node = node;
			this.treeId = treeId;
			this.treeManager = treeManager;
			
			Width.Set(NODE_SIZE, 0f);
			Height.Set(NODE_SIZE, 0f);
			
			// Position based on node data
			Left.Set(node.PositionX * NODE_SPACING, 0f);
			Top.Set(node.PositionY * NODE_SPACING, 0f);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			
			// Update current tier
			currentTier = treeManager.GetNodeTier(treeId, node.NodeId);
			
			// Check if can allocate
			canAllocate = treeManager.CanAllocateNode(treeId, node.NodeId);
			
			// Check hover
			Rectangle hitbox = GetDimensions().ToRectangle();
			isHovered = hitbox.Contains(Main.MouseScreen.ToPoint());
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			
			// Left click to allocate
			if (evt.Target == this)
			{
				if (treeManager.AllocateNode(treeId, node.NodeId))
				{
					SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick); // Success sound
				}
				else
				{
					SoundEngine.PlaySound(Terraria.ID.SoundID.MenuClose); // Fail sound
				}
			}
		}

		public override void RightClick(UIMouseEvent evt)
		{
			base.RightClick(evt);
			
			// Right click to deallocate
			if (evt.Target == this)
			{
				if (treeManager.DeallocateNode(treeId, node.NodeId))
				{
					SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick); // Success sound
				}
				else
				{
					SoundEngine.PlaySound(Terraria.ID.SoundID.MenuClose); // Fail sound
				}
			}
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			CalculatedStyle dimensions = GetDimensions();
			Vector2 position = dimensions.Position();
			
			// Determine node color based on state
			Color nodeColor = GetNodeColor();
			
			// Draw node circle
			DrawNodeCircle(spriteBatch, position, nodeColor);
			
			// Draw tier indicator if multi-tier
			if (node.MaxTier > 1)
			{
				DrawTierIndicator(spriteBatch, position);
			}
			
			// Draw tooltip on hover
			if (isHovered)
			{
				DrawTooltip();
			}
		}

		private Color GetNodeColor()
		{
			if (currentTier >= node.MaxTier)
			{
				// Maxed - Gold
				return new Color(255, 215, 0);
			}
			else if (currentTier > 0)
			{
				// Partially allocated - Green
				return new Color(100, 255, 100);
			}
			else if (canAllocate)
			{
				// Can allocate - Yellow
				return new Color(255, 255, 100);
			}
			else
			{
				// Locked - Gray
				return new Color(128, 128, 128);
			}
		}

		private void DrawNodeCircle(SpriteBatch spriteBatch, Vector2 position, Color color)
		{
			// Get a simple texture (we'll use a white pixel and scale it)
			Texture2D texture = Terraria.GameContent.TextureAssets.MagicPixel.Value;
			
			// Draw filled circle (simple square for now, can be improved)
			Rectangle rect = new Rectangle((int)position.X, (int)position.Y, (int)NODE_SIZE, (int)NODE_SIZE);
			
			// Draw border
			Color borderColor = isHovered ? Color.White : new Color(60, 60, 60);
			spriteBatch.Draw(texture, rect, borderColor);
			
			// Draw inner filled area
			Rectangle innerRect = new Rectangle((int)position.X + 2, (int)position.Y + 2, (int)NODE_SIZE - 4, (int)NODE_SIZE - 4);
			spriteBatch.Draw(texture, innerRect, color * 0.7f);
		}

		private void DrawTierIndicator(SpriteBatch spriteBatch, Vector2 position)
		{
			// Draw tier text in bottom right of node
			string tierText = $"{currentTier}/{node.MaxTier}";
			Vector2 textSize = FontAssets.MouseText.Value.MeasureString(tierText) * 0.6f;
			Vector2 textPos = position + new Vector2(NODE_SIZE - textSize.X - 2, NODE_SIZE - textSize.Y - 2);
			
			Terraria.Utils.DrawBorderStringFourWay(
				spriteBatch,
				FontAssets.MouseText.Value,
				tierText,
				textPos.X,
				textPos.Y,
				Color.White,
				Color.Black,
				Vector2.Zero,
				0.6f
			);
		}

		private void DrawTooltip()
		{
			// Build tooltip text
			string tooltipText = node.DisplayName;
			if (node.MaxTier > 1)
			{
				tooltipText += $" ({currentTier}/{node.MaxTier})";
			}
			tooltipText += "\n" + node.GetFormattedDescription(currentTier);
			
			if (currentTier < node.MaxTier)
			{
				int cost = node.GetUpgradeCost(currentTier);
				tooltipText += $"\n\nCost: {cost} point(s)";
				
				if (!canAllocate)
				{
					tooltipText += "\n[Locked]";
				}
				else
				{
					tooltipText += "\n[Left Click to Allocate]";
				}
			}
			
			if (currentTier > 0)
			{
				tooltipText += "\n[Right Click to Deallocate]";
			}
			
			// Draw tooltip near mouse
			Main.instance.MouseText(tooltipText);
		}

		public PassiveNode GetNode() => node;
	}
}
