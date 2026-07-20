using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;
using ProgressionExpanded.Src.Config;
using ProgressionExpanded.Src.NPCs.Enemy;
using ProgressionExpanded.Src.NPCs.Enemy.Elemental;

namespace ProgressionExpanded.Src.UI
{
	/// <summary>
	/// Hover panel that reveals an enemy's per-element resistance profile — the missing half of the
	/// elemental system's legibility. The per-enemy resistance roll (see <see cref="ElementalResistance"/>)
	/// is otherwise invisible: a +40% fire roll shows nothing on screen, and wards land in un-named
	/// modifier slots on most repeat bosses. Hovering an enemy shows its name in its rarity colour and
	/// one row per element, so the player can read the matchup and switch element before swinging.
	///
	/// The numbers are the enemy's INTRINSIC resistance (penetration 0), not what any particular build
	/// would deal — it is a "know your enemy" readout, so it must not drift with the local player's gear.
	///
	/// ⚠️ <b>Reading an elemental resistance materialises the enemy's resistance seed on first hover.</b>
	/// <c>ElementalResistance.Get</c> reads <c>ElementalDotNPC.ResistanceSeed</c>, which lazily rolls via
	/// <c>Main.rand</c> the first time it is touched. Hovering therefore advances <c>Main.rand</c> and
	/// locks the seed in a frame early. The seed is immutable once set, so the panel and later combat
	/// always agree; the roll fires at most once per enemy; and Bleed / never-killed pinnacles return
	/// before touching the seed. Harmless for this single-player-first mod — noted so it is a decision,
	/// not a surprise.
	/// </summary>
	public class EnemyResistPanel : UIState
	{
		private static readonly Color ResistColor = new Color(120, 230, 130);   // positive: resists
		private static readonly Color WeakColor = new Color(255, 120, 120);     // negative: weak to
		private static readonly Color NeutralColor = new Color(200, 200, 200);
		private static readonly Color PanelBackground = new Color(20, 20, 20, 210);
		private static readonly Color PanelBorder = new Color(90, 90, 90, 220);

		private ProgressionConfig Config => ModContent.GetInstance<ProgressionConfig>();

		public override void Draw(SpriteBatch spriteBatch)
		{
			if (Config == null || !Config.ShowEnemyResistPanel)
				return;

			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead)
				return;

			NPC npc = FindHoveredEnemy();
			if (npc == null)
				return;

			DrawPanel(spriteBatch, npc, Config.EnemyResistPanelScale);
			base.Draw(spriteBatch);
		}

		/// <summary>
		/// The first active hostile NPC whose on-screen rectangle contains the cursor. Screen-space,
		/// matching the hover idiom already used by <c>LevelDisplay.DrawNPCLevelTag</c> — the layer
		/// draws in <c>InterfaceScaleType.UI</c>, in which <c>Main.mouseX/mouseY</c> live.
		/// </summary>
		private static NPC FindHoveredEnemy()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.townNPC || npc.lifeMax <= 5)
					continue;

				Rectangle rect = new Rectangle(
					(int)(npc.position.X - Main.screenPosition.X),
					(int)(npc.position.Y - Main.screenPosition.Y),
					npc.width,
					npc.height);

				if (rect.Contains(Main.mouseX, Main.mouseY))
					return npc;
			}
			return null;
		}

		private void DrawPanel(SpriteBatch spriteBatch, NPC npc, float scale)
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			float lineHeight = font.MeasureString("Ay").Y * scale;

			// Header: the modified enemy name in its rarity colour, falling back to the vanilla name for
			// an enemy that rolled nothing (GetDisplayName is empty until a modifier lands).
			EnemyModifierSystem modifiers = npc.GetGlobalNPC<EnemyModifierSystem>();
			string header = modifiers.GetDisplayName();
			if (string.IsNullOrEmpty(header))
				header = npc.FullName;
			Color headerColor = modifiers.GetRarityInfo().Color;

			// Build each element row (element-coloured label + resist/weak-coloured value) and track the
			// widest line so the background box fits the content.
			int count = DamageElementInfo.Count;
			var elementColors = new Color[count];
			var labels = new string[count];
			var values = new string[count];
			var valueColors = new Color[count];
			var labelWidths = new float[count];

			float maxWidth = font.MeasureString(header).X * scale;

			for (int e = 0; e < count; e++)
			{
				var element = (DamageElement)e;
				// Penetration 0: intrinsic resistance, gear-independent — see the class summary.
				float resistance = ElementalResistance.Get(npc, element, 0f);
				int pct = (int)Math.Round(resistance * 100f);

				string label = element + ": ";
				string value;
				Color valueColor;
				if (pct > 0) { value = $"+{pct}% resist"; valueColor = ResistColor; }
				else if (pct < 0) { value = $"{pct}% (weak)"; valueColor = WeakColor; }
				else { value = "0%"; valueColor = NeutralColor; }

				elementColors[e] = DamageElementInfo.ColorFor(element);
				labels[e] = label;
				values[e] = value;
				valueColors[e] = valueColor;
				labelWidths[e] = font.MeasureString(label).X * scale;

				float rowWidth = labelWidths[e] + font.MeasureString(value).X * scale;
				if (rowWidth > maxWidth)
					maxWidth = rowWidth;
			}

			// Box geometry, then clamp to the screen so it never spills off an edge near a corner.
			float padX = 10f * scale;
			float padY = 8f * scale;
			float boxW = maxWidth + padX * 2f;
			float boxH = lineHeight * (count + 1) + padY * 2f;

			// Coordinate spaces differ here and it is the whole bug. This layer draws in
			// InterfaceScaleType.UI, whose canvas is UI space: Main.screenWidth/Height are already in it
			// (they are raw ÷ Main.UIScale) and boxW/boxH are too (measured from the UI-space font). But
			// Main.mouseX/Y are RAW screen pixels — proven by FindHoveredEnemy comparing them against a
			// world-derived rect. Using the raw cursor as a UI coordinate multiplied it by UIScale, which
			// shoved the panel far to the lower-right and off-screen at any UIScale != 1 (the reported
			// invisibility). Divide the cursor into UI space; leave the already-UI-space bounds alone.
			float uiScale = Main.UIScale <= 0f ? 1f : Main.UIScale;

			var origin = new Vector2(Main.mouseX / uiScale + 20f, Main.mouseY / uiScale + 20f);
			if (origin.X + boxW > Main.screenWidth) origin.X = Main.screenWidth - boxW - 4f;
			if (origin.Y + boxH > Main.screenHeight) origin.Y = Main.screenHeight - boxH - 4f;
			if (origin.X < 0f) origin.X = 0f;
			if (origin.Y < 0f) origin.Y = 0f;

			var box = new Rectangle((int)origin.X, (int)origin.Y, (int)boxW, (int)boxH);
			DrawRectangle(spriteBatch, box, PanelBackground);
			DrawRectangleBorder(spriteBatch, box, PanelBorder, 2);

			var content = new Vector2(origin.X + padX, origin.Y + padY);
			Terraria.Utils.DrawBorderString(spriteBatch, header, content, headerColor, scale);

			for (int e = 0; e < count; e++)
			{
				var rowPos = content + new Vector2(0f, lineHeight * (e + 1));
				Terraria.Utils.DrawBorderString(spriteBatch, labels[e], rowPos, elementColors[e], scale);
				Terraria.Utils.DrawBorderString(spriteBatch, values[e], rowPos + new Vector2(labelWidths[e], 0f), valueColors[e], scale);
			}
		}

		private void DrawRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), color);
		}

		private void DrawRectangleBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
		}
	}

	/// <summary>ModSystem that manages the enemy resistance hover-panel UI layer.</summary>
	public class EnemyResistPanelUISystem : ModSystem
	{
		private EnemyResistPanel panel;
		private UserInterface panelInterface;

		public override void Load()
		{
			if (!Main.dedServ)
			{
				panel = new EnemyResistPanel();
				panelInterface = new UserInterface();
				panelInterface.SetState(panel);
			}
		}

		public override void UpdateUI(GameTime gameTime)
		{
			panelInterface?.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
		{
			int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
			if (mouseTextIndex != -1)
			{
				layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
					"ProgressionExpanded: Enemy Resist Panel",
					delegate
					{
						panelInterface?.Draw(Main.spriteBatch, Main._drawInterfaceGameTime);
						return true;
					},
					InterfaceScaleType.UI)
				);
			}
		}
	}
}
