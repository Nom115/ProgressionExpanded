using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;
using ProgressionExpanded.Src.Levels.PlayerSystems;
using ProgressionExpanded.Src.Config;

namespace ProgressionExpanded.Src.UI
{
	/// <summary>
	/// Displays the player's XP bar and level information
	/// </summary>
	public class XpBar : UIState
	{
		// Bar dimensions
		private const int BAR_WIDTH = 200;
		private const int BAR_HEIGHT = 20;
		private const int BAR_PADDING = 4;

		// Colors
		private static readonly Color BarBackgroundColor = new Color(20, 20, 20, 200);
		private static readonly Color BarBorderColor = new Color(255, 255, 255, 255);
		private static readonly Color XPBarColor = new Color(138, 43, 226); // Purple
		private static readonly Color XPBarGlowColor = new Color(186, 85, 211); // Light purple
		private static readonly Color TextColor = new Color(255, 255, 255);

		private ProgressionConfig Config => ModContent.GetInstance<ProgressionConfig>();

		public override void Draw(SpriteBatch spriteBatch)
		{
			// Check if XP bar should be shown
			if (Config == null || !Config.ShowXPBar)
				return;

			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead)
				return;

			// Get player level and XP data
			int currentLevel = PlayerLevelManager.GetLevel(player);
			int currentXP = PlayerLevelManager.GetXP(player);
			int requiredXP = PlayerLevelManager.GetXPRequiredForLevel(currentLevel);
			float xpProgress = PlayerLevelManager.GetXPProgress(player);

			// Calculate position (bottom-left by default, configurable)
			float scale = Config.XPBarScale;
			Vector2 position = new Vector2(
				Config.XPBarX,
				Main.screenHeight - Config.XPBarY
			);

			// Draw the XP bar
			DrawXPBar(spriteBatch, position, scale, currentLevel, currentXP, requiredXP, xpProgress);

			base.Draw(spriteBatch);
		}

		private void DrawXPBar(SpriteBatch spriteBatch, Vector2 position, float scale, int level, int currentXP, int requiredXP, float progress)
		{
			// Scale dimensions
			int barWidth = (int)(BAR_WIDTH * scale);
			int barHeight = (int)(BAR_HEIGHT * scale);
			int padding = (int)(BAR_PADDING * scale);

			// Draw background
			Rectangle bgRect = new Rectangle((int)position.X, (int)position.Y, barWidth, barHeight);
			DrawRectangle(spriteBatch, bgRect, BarBackgroundColor);

			// Draw XP fill
			int fillWidth = (int)((barWidth - padding * 2) * progress);
			if (fillWidth > 0)
			{
				Rectangle fillRect = new Rectangle(
					(int)position.X + padding,
					(int)position.Y + padding,
					fillWidth,
					barHeight - padding * 2
				);
				
				// Draw glow effect
				DrawRectangle(spriteBatch, fillRect, XPBarGlowColor);
				
				// Draw main fill (slightly smaller for depth)
				Rectangle innerFillRect = new Rectangle(
					fillRect.X,
					fillRect.Y + 1,
					fillRect.Width,
					fillRect.Height - 2
				);
				DrawRectangle(spriteBatch, innerFillRect, XPBarColor);
			}

			// Draw border
			DrawRectangleBorder(spriteBatch, bgRect, BarBorderColor, 2);

			// Draw text - Level on left, XP on right
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			
			// Level text (left side)
			string levelText = $"Lv {level}";
			Vector2 levelTextSize = font.MeasureString(levelText) * scale;
			Vector2 levelTextPos = new Vector2(
				position.X - levelTextSize.X - 10,
				position.Y + (barHeight - levelTextSize.Y) / 2
			);
			
			// XP text (center of bar)
			string xpText = $"{currentXP} / {requiredXP}";
			Vector2 xpTextSize = font.MeasureString(xpText) * scale;
			Vector2 xpTextPos = new Vector2(
				position.X + (barWidth - xpTextSize.X) / 2,
				position.Y + (barHeight - xpTextSize.Y) / 2
			);

			// Draw text with shadows
			Terraria.Utils.DrawBorderString(spriteBatch, levelText, levelTextPos, TextColor, scale);
			Terraria.Utils.DrawBorderString(spriteBatch, xpText, xpTextPos, TextColor, scale);
		}

		/// <summary>
		/// Draw a filled rectangle
		/// </summary>
		private void DrawRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), color);
		}

		/// <summary>
		/// Draw a rectangle border
		/// </summary>
		private void DrawRectangleBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			
			// Top
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
			// Bottom
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
			// Left
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
			// Right
			spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
		}
	}

	/// <summary>
	/// ModSystem that manages the XP bar UI layer
	/// </summary>
	public class XpBarUISystem : ModSystem
	{
		private XpBar xpBar;
		private UserInterface xpBarInterface;

		public override void Load()
		{
			if (!Main.dedServ)
			{
				xpBar = new XpBar();
				xpBarInterface = new UserInterface();
				xpBarInterface.SetState(xpBar);
			}
		}

		public override void UpdateUI(GameTime gameTime)
		{
			xpBarInterface?.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
		{
			int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
			if (mouseTextIndex != -1)
			{
				layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
					"ProgressionExpanded: XP Bar",
					delegate
					{
						xpBarInterface?.Draw(Main.spriteBatch, Main._drawInterfaceGameTime);
						return true;
					},
					InterfaceScaleType.UI)
				);
			}
		}
	}
}
