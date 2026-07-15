using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace ProgressionExpanded.Src.UI.Transmutation
{
	/// <summary>
	/// Open/close + draw host for the Transmutation Bench UI. Opened by right-clicking the
	/// Transmutation Core. Modeled on <c>PassiveTreeUISystem</c>, but it keeps the player's
	/// inventory open (so items can be dragged into the socket) and does not pause the game.
	/// </summary>
	public class TransmutationUISystem : ModSystem
	{
		public static TransmutationUISystem Instance { get; private set; }

		private UserInterface ui;
		private TransmutationUIState state;

		public bool IsOpen => ui?.CurrentState != null;

		public override void Load()
		{
			Instance = this;
			if (!Main.dedServ)
				ui = new UserInterface();
		}

		public override void Unload()
		{
			// A mod reload (Build + Reload) with the bench open must not eat a socketed item.
			ReturnSocketedItem();
			Instance = null;
			state = null;
			ui = null;
		}

		public override void OnWorldUnload()
		{
			// Leaving the world (Save & Exit) with the bench open — return the socketed item so it is
			// never stranded in the UI. The item lives only on the UI state, so this is its safety net.
			ReturnSocketedItem();
		}

		private void ReturnSocketedItem()
		{
			if (Main.dedServ || state == null || Main.LocalPlayer == null || !Main.LocalPlayer.active)
				return;
			state.ReturnItemToPlayer();
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (ui?.CurrentState == null)
				return;

			ui.Update(gameTime);

			// Block attacking/placing while hovering the bench.
			if (state != null && state.IsMouseOverPanel())
				Main.LocalPlayer.mouseInterface = true;

			// Close when the player closes their inventory (e.g. Esc).
			if (!Main.playerInventory)
				Close();
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
			if (mouseTextIndex == -1)
				return;

			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"ProgressionExpanded: Transmutation UI",
				delegate
				{
					if (IsOpen)
						ui.Draw(Main.spriteBatch, new GameTime());
					return true;
				},
				InterfaceScaleType.UI));
		}

		public void Open()
		{
			if (Main.dedServ || IsOpen || Main.LocalPlayer == null || !Main.LocalPlayer.active)
				return;

			state = new TransmutationUIState();
			state.Activate();
			ui?.SetState(state);
			Main.playerInventory = true;
		}

		public void Close()
		{
			state?.ReturnItemToPlayer();
			ui?.SetState(null);
			state = null;
		}

		public void Toggle()
		{
			if (IsOpen)
				Close();
			else
				Open();
		}
	}
}
