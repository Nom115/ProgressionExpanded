using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems
{
	/// <summary>
	/// Handles first-time player notifications
	/// </summary>
	public class FirstTimeNotifications : ModPlayer
	{
		private const string PASSIVE_TREE_NOTIF_KEY = "PassiveTreeNotificationShown";
		private bool hasShownThisSession = false;

		public override void OnEnterWorld()
		{
			// Only show once per character
			bool hasShownBefore = PlayerDataManager.GetBool(Player, PASSIVE_TREE_NOTIF_KEY, false);
			
			if (!hasShownBefore && !hasShownThisSession && Main.netMode != NetmodeID.Server)
			{
				// Delay the message slightly so it's not lost in world load messages
				hasShownThisSession = true;
			}
		}

		public override void PostUpdate()
		{
			// Show message 2 seconds after entering world
			if (hasShownThisSession && !PlayerDataManager.GetBool(Player, PASSIVE_TREE_NOTIF_KEY, false))
			{
				if (Player.whoAmI == Main.myPlayer && Main.netMode != NetmodeID.Server)
				{
					// Check if enough time has passed (roughly 2 seconds at 60 FPS)
					if (Player.miscCounter > 120)
					{
						Main.NewText("Welcome! Press P to open your Passive Skill Tree and choose your class.", new Color(255, 215, 0));
						PlayerDataManager.SetBool(Player, PASSIVE_TREE_NOTIF_KEY, true);
					}
				}
			}
		}
	}
}
