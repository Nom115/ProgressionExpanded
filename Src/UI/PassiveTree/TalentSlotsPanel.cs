using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.UI;
using Terraria.UI.Chat;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using ProgressionExpanded.Src.Levels.PlayerSystems.Talents;
using UIGrid = Terraria.ModLoader.UI.Elements.UIGrid;

namespace ProgressionExpanded.Src.UI.PassiveTree
{
	/// <summary>
	/// The six talent slots.
	///
	/// Unlike the old mastery loadout — where a "slot" was just a position in a name-sorted list and
	/// empties were padding — every slot here has a permanent identity and its own three choices.
	/// The grid is built from TalentSlots.All, never from what the player happens to have picked, so
	/// a locked slot still occupies its place and can say what unlocks it.
	/// </summary>
	public class TalentSlotsPanel : UIPanel
	{
		private UIText infoText;
		private UIElement slotsContainer;
		private UIGrid slotsGrid;
		private UIScrollbar slotsScrollbar;

		private UIElement choiceContainer;
		private UIList choiceList;
		private UIScrollbar choiceScrollbar;
		private UIText choiceTitle;

		private string hoverTooltip;

		public TalentSlotsPanel()
		{
			Width.Set(0, 1f);
			Height.Set(0, 1f);
			BackgroundColor = new Color(26, 40, 89) * 0.7f;
			BorderColor = new Color(89, 116, 213) * 0.8f;
			SetPadding(12f);
		}

		public override void OnInitialize()
		{
			infoText = new UIText("", 1.1f);
			infoText.HAlign = 0.5f;
			Append(infoText);

			slotsContainer = new UIElement();
			slotsContainer.Width.Set(0, 1f);
			slotsContainer.Height.Set(-40, 1f);
			slotsContainer.Top.Set(40, 0f);

			slotsGrid = new UIGrid();
			slotsGrid.Width.Set(-24, 1f);
			slotsGrid.Height.Set(0, 1f);
			slotsGrid.ListPadding = 12f;
			slotsContainer.Append(slotsGrid);

			slotsScrollbar = new UIScrollbar();
			slotsScrollbar.SetView(100f, 1000f);
			slotsScrollbar.Height.Set(0, 1f);
			slotsScrollbar.HAlign = 1f;
			slotsContainer.Append(slotsScrollbar);
			slotsGrid.SetScrollbar(slotsScrollbar);

			choiceContainer = new UIElement();
			choiceContainer.Width.Set(0, 1f);
			choiceContainer.Height.Set(-40, 1f);
			choiceContainer.Top.Set(40, 0f);

			choiceTitle = new UIText("", 1.0f);
			choiceTitle.HAlign = 0.5f;
			choiceContainer.Append(choiceTitle);

			choiceList = new UIList();
			choiceList.Width.Set(-24, 1f);
			choiceList.Height.Set(-70, 1f);
			choiceList.Top.Set(30, 0f);
			choiceList.ListPadding = 8f;
			choiceContainer.Append(choiceList);

			choiceScrollbar = new UIScrollbar();
			choiceScrollbar.SetView(100f, 1000f);
			choiceScrollbar.Height.Set(-70, 1f);
			choiceScrollbar.Top.Set(30, 0f);
			choiceScrollbar.HAlign = 1f;
			choiceContainer.Append(choiceScrollbar);
			choiceList.SetScrollbar(choiceScrollbar);

			SmallButton cancel = new SmallButton("Cancel", new Color(90, 90, 110), ShowSlots, 80f, 26f);
			cancel.HAlign = 0.5f;
			cancel.VAlign = 1f;
			choiceContainer.Append(cancel);

			Append(slotsContainer);
			RebuildSlots();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			int unlocked = 0;
			foreach (TalentSlot slot in TalentSlots.Unlocked(Main.LocalPlayer))
				unlocked++;

			infoText.SetText($"Talent slots unlocked: {unlocked}/{TalentSlots.Count}   |   Talents cost no points");
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			hoverTooltip = null;
			base.Draw(spriteBatch);

			if (!string.IsNullOrEmpty(hoverTooltip))
				Main.instance.MouseText(hoverTooltip);
		}

		public void SetHoverTooltip(string text) => hoverTooltip = text;

		private void ShowSlots()
		{
			if (HasChild(choiceContainer))
				choiceContainer.Remove();
			if (!HasChild(slotsContainer))
				Append(slotsContainer);

			RebuildSlots();
			Recalculate();
		}

		public void OpenChoices(TalentSlot slot)
		{
			if (!TalentPlayer.Get(Main.LocalPlayer).IsSlotUnlocked(slot))
			{
				SoundEngine.PlaySound(SoundID.MenuClose);
				return;
			}

			if (HasChild(slotsContainer))
				slotsContainer.Remove();
			if (!HasChild(choiceContainer))
				Append(choiceContainer);

			choiceTitle.SetText($"{slot.DisplayName} — choose one");

			choiceList.Clear();
			foreach (string id in TalentRegistry.GetChoices(slot.Key))
			{
				TalentBehaviour prototype = TalentRegistry.GetPrototype(id);
				if (prototype != null)
					choiceList.Add(new TalentChoiceRow(this, slot, prototype));
			}

			SoundEngine.PlaySound(SoundID.MenuOpen);
			Recalculate();
		}

		private void RebuildSlots()
		{
			slotsGrid.Clear();

			// Always iterate the slot table, never the picks. A locked slot has to keep its place in
			// the grid so it can tell the player what to go and kill.
			foreach (TalentSlot slot in TalentSlots.All)
				slotsGrid.Add(new TalentSlotCard(this, slot));
		}

		public void Pick(TalentSlot slot, string talentId)
		{
			if (TalentPlayer.Get(Main.LocalPlayer).TryPick(slot.Key, talentId))
			{
				SoundEngine.PlaySound(SoundID.MenuTick);
				ShowSlots();
			}
			else
			{
				SoundEngine.PlaySound(SoundID.MenuClose);
			}
		}

		public void ClearSlot(TalentSlot slot)
		{
			if (TalentPlayer.Get(Main.LocalPlayer).ClearSlot(slot.Key))
			{
				SoundEngine.PlaySound(SoundID.MenuTick);
				RebuildSlots();
				Recalculate();
			}
		}
	}

	/// <summary>One of the six slots: locked, empty, or filled.</summary>
	public class TalentSlotCard : UIPanel
	{
		private readonly TalentSlotsPanel owner;
		private readonly TalentSlot slot;
		private readonly bool unlocked;
		private readonly TalentBehaviour picked;

		public TalentSlotCard(TalentSlotsPanel owner, TalentSlot slot)
		{
			this.owner = owner;
			this.slot = slot;

			Player player = Main.LocalPlayer;
			unlocked = slot.IsUnlocked(player);
			picked = TalentRegistry.GetPrototype(TalentPlayer.Get(player).GetPick(slot.Key));

			Width.Set(240f, 0f);
			Height.Set(132f, 0f);
			SetPadding(8f);

			if (!unlocked)
			{
				BackgroundColor = new Color(40, 40, 50) * 0.8f;
				BorderColor = new Color(70, 70, 80);
				BuildLocked();
			}
			else if (picked == null)
			{
				BackgroundColor = new Color(45, 60, 100) * 0.8f;
				BorderColor = new Color(110, 140, 220);
				BuildEmpty();
			}
			else
			{
				BackgroundColor = new Color(60, 45, 100) * 0.85f;
				BorderColor = new Color(170, 120, 230);
				BuildFilled();
			}
		}

		private void BuildLocked()
		{
			UIText name = new UIText(slot.DisplayName, 0.9f);
			name.HAlign = 0.5f;
			name.TextColor = new Color(130, 130, 140);
			Append(name);

			UIText locked = new UIText("LOCKED", 1.0f);
			locked.HAlign = 0.5f;
			locked.VAlign = 0.5f;
			locked.TextColor = new Color(170, 90, 90);
			Append(locked);

			UIText hint = new UIText(slot.UnlockHint, 0.7f);
			hint.HAlign = 0.5f;
			hint.VAlign = 1f;
			hint.TextColor = new Color(150, 150, 150);
			Append(hint);
		}

		private void BuildEmpty()
		{
			UIText name = new UIText(slot.DisplayName, 0.9f);
			name.HAlign = 0.5f;
			Append(name);

			UIText plus = new UIText("+", 1.8f);
			plus.HAlign = 0.5f;
			plus.VAlign = 0.5f;
			Append(plus);

			UIText hint = new UIText("click to choose", 0.7f);
			hint.HAlign = 0.5f;
			hint.VAlign = 1f;
			hint.TextColor = new Color(160, 160, 160);
			Append(hint);
		}

		private void BuildFilled()
		{
			UIText slotName = new UIText(slot.DisplayName, 0.7f);
			slotName.HAlign = 0.5f;
			slotName.TextColor = new Color(150, 150, 170);
			Append(slotName);

			UIText name = new UIText(picked.DisplayName, 1.0f);
			name.HAlign = 0.5f;
			name.Top.Set(22, 0f);
			name.TextColor = new Color(255, 215, 0);
			Append(name);

			UIText change = new UIText("click to change", 0.7f);
			change.HAlign = 0.5f;
			change.VAlign = 1f;
			change.TextColor = new Color(160, 160, 160);
			Append(change);

			SmallButton clear = new SmallButton("x", new Color(110, 60, 60), () => owner.ClearSlot(slot), 20f, 20f);
			clear.HAlign = 1f;
			clear.Top.Set(-2, 0f);
			Append(clear);
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			if (unlocked)
				owner.OpenChoices(slot);
		}

		public override void MouseOver(UIMouseEvent evt)
		{
			base.MouseOver(evt);

			if (!unlocked)
				owner.SetHoverTooltip($"{slot.DisplayName}\nLocked — {slot.UnlockHint}");
			else if (picked == null)
				owner.SetHoverTooltip($"{slot.DisplayName}\nClick to choose one of three talents.");
			else
				owner.SetHoverTooltip($"{picked.DisplayName}\n{TalentChoiceRow.Wrap(picked.Description)}");
		}
	}

	/// <summary>One of the three choices for a slot.</summary>
	public class TalentChoiceRow : UIPanel
	{
		private const float DescriptionScale = 0.75f;
		private const float DescriptionTop = 26f;
		private const float Padding = 8f;

		private readonly TalentSlotsPanel owner;
		private readonly TalentSlot slot;
		private readonly TalentBehaviour talent;
		private readonly bool isCurrent;

		public TalentChoiceRow(TalentSlotsPanel owner, TalentSlot slot, TalentBehaviour talent)
		{
			this.owner = owner;
			this.slot = slot;
			this.talent = talent;

			isCurrent = TalentPlayer.Get(Main.LocalPlayer).GetPick(slot.Key) == talent.Id;

			string wrapped = Wrap(talent.Description);

			Width.Set(0, 1f);
			// Sized to the text rather than a constant. The talents do not have comparable
			// description lengths — a one-liner sits next to Stagger's seven — so any fixed height
			// is either clipping the longest or padding out the rest.
			Height.Set(DescriptionTop + MeasureHeight(wrapped) + Padding, 0f);
			SetPadding(Padding);
			BackgroundColor = isCurrent ? new Color(60, 100, 60) * 0.9f : new Color(40, 50, 90) * 0.85f;
			BorderColor = isCurrent ? new Color(120, 200, 120) : new Color(90, 116, 200);

			UIText name = new UIText(talent.DisplayName + (isCurrent ? "   (current)" : ""), 1.0f);
			name.Left.Set(4, 0f);
			name.TextColor = new Color(255, 215, 0);
			Append(name);

			UIText description = new UIText(wrapped, DescriptionScale);
			description.Left.Set(4, 0f);
			description.Top.Set(DescriptionTop, 0f);
			description.TextColor = new Color(210, 210, 210);
			Append(description);
		}

		/// <summary>
		/// Pixel height of already-wrapped text, so the card can size itself to it.
		///
		/// This has to be measured by hand because <b>UIText cannot report the height of multi-line
		/// text unless IsWrapped is set</b>: InternalSetText measures the string properly only on
		/// the IsWrapped branch, and otherwise hardcodes MinHeight to <c>16f * textScale</c> no
		/// matter how many newlines the string holds. We wrap by hand (see Wrap), so a card that
		/// trusted UIText would be told its seven-line description was 12px tall.
		///
		/// Measuring at scale 1 and multiplying afterwards mirrors UIText's own IsWrapped branch,
		/// and is not a stylistic choice: ChatManager.GetStringSize scales the per-newline advance
		/// by baseScale.Y but NOT the final line's height, so passing the scale in directly
		/// over-measures the last line.
		/// </summary>
		private static float MeasureHeight(string wrapped)
		{
			return ChatManager.GetStringSize(FontAssets.MouseText.Value, wrapped, Vector2.One).Y
				* DescriptionScale;
		}

		/// <summary>
		/// UIText doesn't wrap on its own and these descriptions are long, so break them by hand.
		///
		/// Counts characters, not pixels, against a proportional font — so the right margin is
		/// ragged and the width is a conservative guess rather than a fit. It holds up because the
		/// panel is 90% of the screen and this wraps at roughly a third of that at every supported
		/// resolution. The alternative is UIText.IsWrapped, which wraps against real inner width,
		/// but only once the element has been laid out — which is not true in this constructor.
		/// </summary>
		internal static string Wrap(string text)
		{
			const int lineLength = 78;
			System.Text.StringBuilder builder = new System.Text.StringBuilder();
			int lineStart = 0;

			string[] words = text.Split(' ');
			foreach (string word in words)
			{
				if (builder.Length - lineStart + word.Length > lineLength)
				{
					builder.Append('\n');
					lineStart = builder.Length;
				}
				else if (builder.Length > 0)
				{
					builder.Append(' ');
				}

				builder.Append(word);
			}

			return builder.ToString();
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			owner.Pick(slot, talent.Id);
		}

		public override void MouseOver(UIMouseEvent evt)
		{
			base.MouseOver(evt);
			// Wrapped: vanilla's MouseText does not break long lines, and these run to 500+ chars.
			owner.SetHoverTooltip($"{talent.DisplayName}\n{Wrap(talent.Description)}");
		}
	}
}
