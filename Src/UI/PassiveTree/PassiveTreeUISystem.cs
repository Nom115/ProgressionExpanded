using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace ProgressionExpanded.Src.UI.PassiveTree
{
	/// <summary>
	/// UI System for managing the passive tree interface
	/// </summary>
	public class PassiveTreeUISystem : ModSystem
	{
		public static PassiveTreeUISystem Instance { get; private set; }
		
		private UserInterface passiveTreeInterface;
		private PassiveTreeUIState passiveTreeUI;
		
		public bool IsUIOpen => passiveTreeInterface?.CurrentState != null;

		public override void Load()
		{
			Instance = this;
			
			if (!Main.dedServ)
			{
				passiveTreeInterface = new UserInterface();
				passiveTreeUI = new PassiveTreeUIState();
				passiveTreeUI.Activate();
			}
		}

		public override void Unload()
		{
			Instance = null;
			passiveTreeUI = null;
			passiveTreeInterface = null;
		}

		public override void UpdateUI(GameTime gameTime)
		{
			// Poll the toggle key here (not in a ModPlayer hook) so it still works while the
			// game is paused: ModPlayer.PostUpdate does not run during pause, but UpdateUI does.
			if (PassiveTreeKeybind.TogglePassiveTreeKey?.JustPressed == true)
				ToggleUI();

			if (passiveTreeInterface?.CurrentState != null)
			{
				passiveTreeInterface.Update(gameTime);

				// Pause the game while the passives page is open so the player can browse at
				// their own pace. Reasserted each tick; single-player only (Main.gamePaused has
				// no effect in multiplayer).
				if (Main.netMode == NetmodeID.SinglePlayer)
					Main.gamePaused = true;
			}
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
			if (mouseTextIndex != -1)
			{
				layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
					"ProgressionExpanded: Passive Tree UI",
					delegate
					{
						if (IsUIOpen)
						{
							passiveTreeInterface.Draw(Main.spriteBatch, new GameTime());
						}
						return true;
					},
					InterfaceScaleType.UI)
				);
			}
		}

		public void ToggleUI()
		{
			if (IsUIOpen)
			{
				CloseUI();
			}
			else
			{
				OpenUI();
			}
		}

		public void OpenUI()
		{
			if (Main.LocalPlayer == null || !Main.LocalPlayer.active)
				return;
			
			// Reinitialize to refresh data
			passiveTreeUI = new PassiveTreeUIState();
			passiveTreeUI.Activate();
			
			passiveTreeInterface?.SetState(passiveTreeUI);
			Main.playerInventory = false;
		}

		public void CloseUI()
		{
			passiveTreeInterface?.SetState(null);

			// Release the pause we asserted while the page was open.
			if (Main.netMode == NetmodeID.SinglePlayer)
				Main.gamePaused = false;
		}
	}
}
