using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;
using ProgressionExpanded.Src.Levels.PlayerLevel;
using ProgressionExpanded.Src.NPCs;

namespace ProgressionExpanded.Src.Levels
{
	/// <summary>
	/// Handles visual display of level tags above players and NPCs
	/// </summary>
	public class LevelDisplay
	{
		// Configuration
		public static bool ShowPlayerLevels { get; set; } = true;
		public static bool ShowNPCLevels { get; set; } = true;
		public static bool ShowOnlyWhenHovered { get; set; } = false;
		
		// Colors
		private static readonly Color PlayerLevelColor = new Color(100, 200, 255); // Light blue
		private static readonly Color NPCLevelColor = new Color(255, 200, 100); // Orange
		private static readonly Color BossLevelColor = new Color(255, 50, 50); // Red

		/// <summary>
		/// Draw level tag above a player
		public static void DrawPlayerLevelTag(Player player)
		{
			// Skip if hover-only mode and player is not the local player or not hovered
			if (ShowOnlyWhenHovered && player.whoAmI != Main.myPlayer)
			{
				Rectangle mouseRect = new Rectangle(Main.mouseX, Main.mouseY, 1, 1);
				Rectangle playerRect = new Rectangle(
					(int)(player.position.X - Main.screenPosition.X),
					(int)(player.position.Y - Main.screenPosition.Y),
					player.width,
					player.height
				);
				
				if (!mouseRect.Intersects(playerRect))
					return;
			}

			int level = PlayerLevelManager.GetLevel(player);
			string levelText = $"Lv {level}";

			// Calculate position below player (above their feet)
			Vector2 position = new Vector2(
				player.Center.X - Main.screenPosition.X,
				player.Bottom.Y - Main.screenPosition.Y + 5 // Below the player
			);

			DrawLevelText(levelText, position, PlayerLevelColor);
		}

		/// <summary>
		/// Draw level tag above an NPC (called from PostDraw in NPCLevelManager)
		/// </summary>
		public static void DrawNPCLevelTag(Terraria.NPC npc, int level, string displayName = null, Color? rarityColor = null)
		{
			if (!ShowNPCLevels) return;

			// Skip friendly NPCs and town NPCs
			if (npc.friendly || npc.townNPC) return;

			// Skip if hover-only mode and NPC is not hovered
			if (ShowOnlyWhenHovered)
			{
				Rectangle mouseRect = new Rectangle(Main.mouseX, Main.mouseY, 1, 1);
				Rectangle npcRect = new Rectangle(
					(int)(npc.position.X - Main.screenPosition.X),
					(int)(npc.position.Y - Main.screenPosition.Y),
					npc.width,
					npc.height
				);
				
				if (!mouseRect.Intersects(npcRect))
					return;
			}

			// Use modified display name if provided, otherwise show level
			string levelText;
			if (!string.IsNullOrEmpty(displayName))
			{
				levelText = $"Lv {level} - {displayName}";
			}
			else
			{
				levelText = $"Lv {level}";
			}

			// Calculate position above NPC
			Vector2 position = new Vector2(
				npc.Center.X - Main.screenPosition.X,
				npc.position.Y - Main.screenPosition.Y - 30 // Above the NPC
			);

			// Use rarity color if provided, otherwise default colors
			Color color;
			if (rarityColor.HasValue)
			{
				color = rarityColor.Value;
			}
			else
			{
				color = npc.boss ? BossLevelColor : NPCLevelColor;
			}

			DrawLevelText(levelText, position, color);
		}

		/// <summary>
		/// Draw centered level text at the specified position
		/// </summary>
		private static void DrawLevelText(string text, Vector2 position, Color color)
		{
			// Get the font
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			
			// Measure text to center it
			Vector2 textSize = font.MeasureString(text);
			Vector2 centeredPosition = position - new Vector2(textSize.X / 2f, 0);

			// Draw shadow first for better visibility
			Terraria.Utils.DrawBorderString(Main.spriteBatch, text, centeredPosition, color, 1f);
		}
	}

	/// <summary>
	/// ModPlayer that handles player-specific level display hooks
	/// </summary>
	public class PlayerLevelDisplay : ModPlayer
	{
		public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright)
		{
			// Draw level tag for all players during draw phase
			if (LevelDisplay.ShowPlayerLevels && Player.active && !Player.dead)
			{
				LevelDisplay.DrawPlayerLevelTag(Player);
			}
		}
	}
}
