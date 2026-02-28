using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
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
			if (passiveTreeInterface?.CurrentState != null)
			{
				passiveTreeInterface.Update(gameTime);
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
		}
	}
}
