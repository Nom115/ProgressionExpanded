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
	/// Displays the player's mana with real numeric values.
	///
	/// Mana is not a caster-only readout in this mod: Stagger raises max mana by 50% of max life and
	/// pays its bleed out of the pool before touching life, so for that pinnacle the mana bar is the
	/// health bar. The stars are a poor fit for that — they read as a count, not a quantity, and the
	/// exact number is what tells the player whether the next hit gets split or lands whole.
	/// </summary>
	public class ManaBar : UIState
	{
		// Bar dimensions
		private const int BAR_WIDTH = 250;
		private const int BAR_HEIGHT = 24;
		private const int BAR_PADDING = 4;

		// Below this fraction the pool is close to the cliff where Stagger stops splitting hits.
		private const float LowManaFraction = 0.25f;

		// Colors
		private static readonly Color BarBackgroundColor = new Color(20, 20, 20, 200);
		private static readonly Color BarBorderColor = new Color(255, 255, 255, 255);
		private static readonly Color ManaBarColor = new Color(70, 100, 220); // Deep blue
		private static readonly Color ManaBarGlowColor = new Color(120, 160, 255); // Light blue
		private static readonly Color LowManaColor = new Color(40, 55, 130); // Dark blue (below 25%)
		private static readonly Color TextColor = new Color(255, 255, 255);

		private ProgressionConfig Config => ModContent.GetInstance<ProgressionConfig>();

		public override void Draw(SpriteBatch spriteBatch)
		{
			if (Config == null || !Config.ShowCustomManaBar)
				return;

			Player player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;

			int currentMana = player.statMana;
			int maxMana = player.statManaMax2;

			// Juggernaut sets statManaMax2 = 0 every frame, so a zero pool is a normal state, not an
			// edge case: dividing by it would put NaN through the fill width and print "0 / 0".
			// Drawing nothing is also the right answer for that build — it has no mana to report.
			if (maxMana <= 0)
				return;

			float manaProgress = (float)currentMana / maxMana;

			float scale = Config.ManaBarScale;
			Vector2 position = new Vector2(
				Config.ManaBarX,
				Main.screenHeight - Config.ManaBarY
			);

			DrawManaBar(spriteBatch, position, scale, currentMana, maxMana, manaProgress);

			base.Draw(spriteBatch);
		}

		private void DrawManaBar(SpriteBatch spriteBatch, Vector2 position, float scale, int currentMana, int maxMana, float progress)
		{
			// Scale dimensions
			int barWidth = (int)(BAR_WIDTH * scale);
			int barHeight = (int)(BAR_HEIGHT * scale);
			int padding = (int)(BAR_PADDING * scale);

			// Draw background
			Rectangle bgRect = new Rectangle((int)position.X, (int)position.Y, barWidth, barHeight);
			DrawRectangle(spriteBatch, bgRect, BarBackgroundColor);

			// Draw mana fill
			int fillWidth = (int)((barWidth - padding * 2) * progress);
			if (fillWidth > 0)
			{
				Rectangle fillRect = new Rectangle(
					(int)position.X + padding,
					(int)position.Y + padding,
					fillWidth,
					barHeight - padding * 2
				);

				Color fillColor = progress > LowManaFraction ? ManaBarColor : LowManaColor;
				Color glowColor = progress > LowManaFraction ? ManaBarGlowColor : LowManaColor;

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

			// Draw text - mana numbers in center
			DynamicSpriteFont font = FontAssets.MouseText.Value;

			string manaText = $"{currentMana} / {maxMana} MP";
			Vector2 manaTextSize = font.MeasureString(manaText) * scale;
			Vector2 manaTextPos = new Vector2(
				position.X + (barWidth - manaTextSize.X) / 2,
				position.Y + (barHeight - manaTextSize.Y) / 2
			);

			// Draw text with shadows for readability
			Terraria.Utils.DrawBorderString(spriteBatch, manaText, manaTextPos, TextColor, scale);
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
	/// ModSystem that manages the Mana bar UI layer
	/// </summary>
	public class ManaBarUISystem : ModSystem
	{
		private ManaBar manaBar;
		private UserInterface manaBarInterface;

		public override void Load()
		{
			if (!Main.dedServ)
			{
				manaBar = new ManaBar();
				manaBarInterface = new UserInterface();
				manaBarInterface.SetState(manaBar);
			}
		}

		public override void UpdateUI(GameTime gameTime)
		{
			manaBarInterface?.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
		{
			// Hiding the vanilla stars is NOT done here — see HealthBarUISystem: "Vanilla: Resource
			// Bars" is one layer covering life, mana, breath, buffs and golf power. VanillaResourceOverlay
			// suppresses just the mana display.
			int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
			if (mouseTextIndex != -1)
			{
				layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
					"ProgressionExpanded: Mana Bar",
					delegate
					{
						manaBarInterface?.Draw(Main.spriteBatch, Main._drawInterfaceGameTime);
						return true;
					},
					InterfaceScaleType.UI)
				);
			}
		}
	}
}
