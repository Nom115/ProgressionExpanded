using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using ProgressionExpanded.Items.Orbs;

namespace ProgressionExpanded.Src.UI.Transmutation
{
	/// <summary>
	/// The Transmutation Bench: one item socket plus a button per orb the mod defines. Clicking an
	/// orb applies it to the socketed item (consuming one from the player's inventory on success).
	/// Modeled on <c>PassiveTreeUIState</c> / <c>PassiveTreeUISystem</c>.
	/// </summary>
	public class TransmutationUIState : UIState
	{
		// Display order of the orbs (by internal/class name).
		private static readonly string[] OrbOrder =
		{
			"OrbOfTransmutation", "OrbOfAlteration", "RegalOrb", "OrbOfAscendance", "OrbOfCataclysm",
			"VerdantParadox", "DivineOrb", "OrbOfAnnulment", "OrbOfAugmentation"
		};

		private UIPanel panel;
		private TransmutationSlot itemSlot;
		private UIText feedbackText;
		private readonly List<OrbButton> orbButtons = new List<OrbButton>();

		public override void OnInitialize()
		{
			panel = new UIPanel();
			panel.Width.Set(360f, 0f);
			panel.Height.Set(150f + OrbOrder.Length * 30f, 0f);
			panel.HAlign = 0.5f;
			panel.VAlign = 0.5f;
			panel.SetPadding(12f);
			panel.BackgroundColor = new Color(33, 43, 79) * 0.95f;
			Append(panel);

			var title = new UIText("Transmutation Bench", 0.5f, true);
			title.HAlign = 0.5f;
			panel.Append(title);

			var close = new UIText("X", 0.85f);
			close.HAlign = 1f;
			close.OnLeftClick += (evt, el) => ModContent.GetInstance<TransmutationUISystem>().Close();
			panel.Append(close);

			itemSlot = new TransmutationSlot(0.95f);
			itemSlot.HAlign = 0.5f;
			itemSlot.Top.Set(34f, 0f);
			panel.Append(itemSlot);

			var hint = new UIText("Place an item, then click an orb.", 0.72f);
			hint.HAlign = 0.5f;
			hint.Top.Set(96f, 0f);
			panel.Append(hint);

			float y = 122f;
			foreach (var orb in GetOrbsInOrder())
			{
				var btn = new OrbButton(orb, this);
				btn.Top.Set(y, 0f);
				btn.Left.Set(0f, 0f);
				btn.Width.Set(-8f, 1f);
				panel.Append(btn);
				orbButtons.Add(btn);
				y += 30f;
			}

			feedbackText = new UIText("", 0.78f);
			feedbackText.HAlign = 0.5f;
			feedbackText.VAlign = 1f;
			panel.Append(feedbackText);
		}

		public bool IsMouseOverPanel() => panel != null && panel.ContainsPoint(Main.MouseScreen);

		public void SetFeedback(string message, bool success)
		{
			if (feedbackText == null) return;
			feedbackText.SetText(message ?? string.Empty);
			feedbackText.TextColor = success ? new Color(120, 255, 120) : new Color(255, 150, 150);
		}

		public void ApplyOrb(OrbItem orb)
		{
			Item target = itemSlot.Item;
			if (target == null || target.IsAir)
			{
				SetFeedback("Place an item in the slot first.", false);
				return;
			}

			if (CountOwned(orb.Type) <= 0)
			{
				SetFeedback($"You have no {Lang.GetItemNameValue(orb.Type)}.", false);
				return;
			}

			OrbResult result = orb.Apply(target);
			SetFeedback(result.Message, result.Success);

			if (result.Success)
			{
				ConsumeOne(orb.Type);
				SoundEngine.PlaySound(SoundID.Item37); // reforge chime
			}

			RefreshButtons();
		}

		public void RefreshButtons()
		{
			foreach (var button in orbButtons)
				button.Refresh();
		}

		public int CountOwned(int type)
		{
			int count = 0;
			foreach (var item in Main.LocalPlayer.inventory)
			{
				if (item != null && !item.IsAir && item.type == type)
					count += item.stack;
			}
			return count;
		}

		private void ConsumeOne(int type)
		{
			foreach (var item in Main.LocalPlayer.inventory)
			{
				if (item != null && !item.IsAir && item.type == type)
				{
					item.stack--;
					if (item.stack <= 0)
						item.TurnToAir();
					return;
				}
			}
		}

		/// <summary>Return the socketed item to the player (called on close) so it is never lost.</summary>
		public void ReturnItemToPlayer()
		{
			if (itemSlot?.Item == null || itemSlot.Item.IsAir)
				return;

			Player player = Main.LocalPlayer;
			Item leftover = player.GetItem(Main.myPlayer, itemSlot.Item, GetItemSettings.InventoryEntityToPlayerInventorySettings);
			if (leftover != null && !leftover.IsAir)
				player.QuickSpawnItem(player.GetSource_Misc("TransmutationReturn"), leftover, leftover.stack);

			itemSlot.Item = new Item();
			itemSlot.Item.SetDefaults(ItemID.None);
		}

		private IEnumerable<OrbItem> GetOrbsInOrder()
		{
			var orbs = ModContent.GetInstance<ProgressionExpanded>()
				.GetContent<ModItem>()
				.OfType<OrbItem>()
				.ToList();
			orbs.Sort((a, b) => OrbIndex(a).CompareTo(OrbIndex(b)));
			return orbs;
		}

		private static int OrbIndex(OrbItem orb)
		{
			int i = Array.IndexOf(OrbOrder, orb.Name);
			return i < 0 ? int.MaxValue : i;
		}
	}

	/// <summary>A clickable row for one orb type: shows its name + owned count, applies on click.</summary>
	public class OrbButton : UIPanel
	{
		private static readonly Color Idle = new Color(63, 82, 151) * 0.85f;
		private static readonly Color Hover = new Color(96, 120, 205) * 0.95f;

		private readonly OrbItem orb;
		private readonly TransmutationUIState state;
		private readonly UIText label;

		public OrbButton(OrbItem orb, TransmutationUIState state)
		{
			this.orb = orb;
			this.state = state;

			Height.Set(26f, 0f);
			SetPadding(2f);
			BackgroundColor = Idle;

			label = new UIText(string.Empty, 0.8f);
			label.VAlign = 0.5f;
			label.Left.Set(8f, 0f);
			Append(label);

			OnLeftClick += (evt, el) => state.ApplyOrb(orb);
			OnMouseOver += (evt, el) => BackgroundColor = Hover;
			OnMouseOut += (evt, el) => BackgroundColor = Idle;

			Refresh();
		}

		public void Refresh()
		{
			int owned = state.CountOwned(orb.Type);
			label.SetText($"{Lang.GetItemNameValue(orb.Type)}  (x{owned})");
			label.TextColor = owned > 0 ? Color.White : new Color(150, 150, 150);
		}
	}
}
