using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;
using ProgressionExpanded.Src.Config;

namespace ProgressionExpanded.Src.UI
{
	/// <summary>
	/// Displays the player's health bar with real numeric values
	/// </summary>
	public class HealthBar : UIState
	{
		// Bar dimensions
		private const int BAR_WIDTH = 250;
		private const int BAR_HEIGHT = 24;
		private const int BAR_PADDING = 4;

		// Colors
		private static readonly Color BarBackgroundColor = new Color(20, 20, 20, 200);
		private static readonly Color BarBorderColor = new Color(255, 255, 255, 255);
		private static readonly Color HealthBarColor = new Color(220, 20, 60); // Crimson red
		private static readonly Color HealthBarGlowColor = new Color(255, 69, 96); // Light red
		private static readonly Color LowHealthColor = new Color(139, 0, 0); // Dark red (below 25%)
		private static readonly Color TextColor = new Color(255, 255, 255);

		private ProgressionConfig Config => ModContent.GetInstance<ProgressionConfig>();

		public override void Draw(SpriteBatch spriteBatch)
		{
			// Check if health bar should be shown
			if (Config == null || !Config.ShowCustomHealthBar)
				return;

			Player player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;

			// Get player health data
			int currentHP = player.statLife;
			int maxHP = player.statLifeMax2;
			float healthProgress = (float)currentHP / maxHP;

			// Calculate position (bottom-left by default, configurable)
			float scale = Config.HealthBarScale;
			Vector2 position = new Vector2(
				Config.HealthBarX,
				Main.screenHeight - Config.HealthBarY
			);

			// Draw the health bar
			DrawHealthBar(spriteBatch, position, scale, currentHP, maxHP, healthProgress);

			base.Draw(spriteBatch);
		}

		private void DrawHealthBar(SpriteBatch spriteBatch, Vector2 position, float scale, int currentHP, int maxHP, float progress)
		{
			// Scale dimensions
			int barWidth = (int)(BAR_WIDTH * scale);
			int barHeight = (int)(BAR_HEIGHT * scale);
			int padding = (int)(BAR_PADDING * scale);

			// Draw background
			Rectangle bgRect = new Rectangle((int)position.X, (int)position.Y, barWidth, barHeight);
			DrawRectangle(spriteBatch, bgRect, BarBackgroundColor);

			// Draw HP fill
			int fillWidth = (int)((barWidth - padding * 2) * progress);
			if (fillWidth > 0)
			{
				Rectangle fillRect = new Rectangle(
					(int)position.X + padding,
					(int)position.Y + padding,
					fillWidth,
					barHeight - padding * 2
				);

				// Choose color based on health percentage
				Color fillColor = progress > 0.25f ? HealthBarColor : LowHealthColor;
				Color glowColor = progress > 0.25f ? HealthBarGlowColor : LowHealthColor;

				// Draw glow effect
				DrawRectangle(spriteBatch, fillRect, glowColor);

				// Draw main fill (slightly smaller for depth)
				Rectangle innerFillRect = new Rectangle(
					fillRect.X,
					fillRect.Y + 1,
					fillRect.Width,
					fillRect.Height - 2
				);
				DrawRectangle(spriteBatch, innerFillRect, fillColor);
			}

			// Draw border
			DrawRectangleBorder(spriteBatch, bgRect, BarBorderColor, 2);

			// Draw text - HP numbers in center
			DynamicSpriteFont font = FontAssets.MouseText.Value;

			// HP text (center of bar)
			string hpText = $"{currentHP} / {maxHP} HP";
			Vector2 hpTextSize = font.MeasureString(hpText) * scale;
			Vector2 hpTextPos = new Vector2(
				position.X + (barWidth - hpTextSize.X) / 2,
				position.Y + (barHeight - hpTextSize.Y) / 2
			);

			// Draw text with shadows for readability
			Terraria.Utils.DrawBorderString(spriteBatch, hpText, hpTextPos, TextColor, scale);
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
	/// ModSystem that manages the Health bar UI layer and hides vanilla hearts
	/// </summary>
	public class HealthBarUISystem : ModSystem
	{
		private HealthBar healthBar;
		private UserInterface healthBarInterface;

		public override void Load()
		{
			if (!Main.dedServ)
			{
				healthBar = new HealthBar();
				healthBarInterface = new UserInterface();
				healthBarInterface.SetState(healthBar);
			}
		}

		public override void UpdateUI(GameTime gameTime)
		{
			healthBarInterface?.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
		{
			// Hide vanilla hearts if config enabled
			var config = ModContent.GetInstance<ProgressionConfig>();
			if (config != null && config.HideVanillaHearts)
			{
				int resourceBarsIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Resource Bars"));
				if (resourceBarsIndex != -1)
				{
					layers.RemoveAt(resourceBarsIndex);
				}
			}

			// Add custom health bar
			int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
			if (mouseTextIndex != -1)
			{
				layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
					"ProgressionExpanded: Health Bar",
					delegate
					{
						healthBarInterface?.Draw(Main.spriteBatch, Main._drawInterfaceGameTime);
						return true;
					},
					InterfaceScaleType.UI)
				);
			}
		}
	}
}
