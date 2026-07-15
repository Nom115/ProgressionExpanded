using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.UI;
using Terraria.GameContent.UI.Elements;
using ProgressionExpanded.Src.Levels.PlayerSystems;
using ProgressionExpanded.Src.Levels.PlayerSystems.Attributes;
using ProgressionExpanded.Src.Levels.PlayerSystems.Talents;

namespace ProgressionExpanded.Src.UI.PassiveTree
{
	/// <summary>
	/// The attributes view: spend passive points on Strength, Dexterity and Intellect.
	///
	/// These draw on the same pool as the masteries, so the available-points figure is shared and
	/// deliberately shown on both tabs — the whole tension of the system is that a point here is a
	/// mastery rank you gave up.
	/// </summary>
	public class AttributesPanel : UIPanel
	{
		private UIText infoText;
		private UIElement rowContainer;
		private string hoverTooltip;

		public AttributesPanel()
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

			rowContainer = new UIElement();
			rowContainer.Width.Set(0, 1f);
			rowContainer.Height.Set(-40, 1f);
			rowContainer.Top.Set(40, 0f);
			Append(rowContainer);

			RebuildRows();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			PassivePointManager points = Main.LocalPlayer?.GetModPlayer<PassivePointManager>();
			if (points != null)
				infoText.SetText($"Available: {points.GetAvailablePoints()} points   |   1 point per level");
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			hoverTooltip = null;
			base.Draw(spriteBatch);

			// Rows can't draw their own tooltips without being clipped, so they hand the text up here
			// and it gets drawn after everything else.
			if (!string.IsNullOrEmpty(hoverTooltip))
				Main.instance.MouseText(hoverTooltip);
		}

		public void SetHoverTooltip(string text) => hoverTooltip = text;

		private void RebuildRows()
		{
			rowContainer.RemoveAllChildren();

			AddRow(PlayerAttribute.Strength, 0);
			AddRow(PlayerAttribute.Dexterity, 1);
			AddRow(PlayerAttribute.Intellect, 2);
		}

		private void AddRow(PlayerAttribute attribute, int index)
		{
			AttributeRow row = new AttributeRow(this, attribute);
			row.Top.Set(index * 86f, 0f);
			rowContainer.Append(row);
		}

		public void Invest(PlayerAttribute attribute)
		{
			if (AttributeManager.Get(Main.LocalPlayer).TryInvest(attribute))
			{
				SoundEngine.PlaySound(SoundID.MenuTick);
				RebuildRows();
				Recalculate();
			}
			else
			{
				SoundEngine.PlaySound(SoundID.MenuClose);
			}
		}

		public void Refund(PlayerAttribute attribute)
		{
			if (AttributeManager.Get(Main.LocalPlayer).TryRefund(attribute))
			{
				SoundEngine.PlaySound(SoundID.MenuTick);
				RebuildRows();
				Recalculate();
			}
			else
			{
				SoundEngine.PlaySound(SoundID.MenuClose);
			}
		}
	}

	/// <summary>One attribute: what it is, what you have, what it's doing for you, and the buttons.</summary>
	public class AttributeRow : UIPanel
	{
		private readonly AttributesPanel owner;
		private readonly PlayerAttribute attribute;

		public AttributeRow(AttributesPanel owner, PlayerAttribute attribute)
		{
			this.owner = owner;
			this.attribute = attribute;

			Width.Set(-24, 1f);
			Height.Set(78, 0f);
			SetPadding(8f);
			BackgroundColor = TintFor(attribute) * 0.55f;
			BorderColor = TintFor(attribute) * 0.9f;

			Build();
		}

		private static Color TintFor(PlayerAttribute attribute)
		{
			switch (attribute)
			{
				case PlayerAttribute.Strength: return new Color(200, 90, 80);
				case PlayerAttribute.Dexterity: return new Color(210, 190, 90);
				default: return new Color(80, 140, 200);
			}
		}

		private static string NameOf(PlayerAttribute attribute)
		{
			switch (attribute)
			{
				case PlayerAttribute.Strength: return "Strength";
				case PlayerAttribute.Dexterity: return "Dexterity";
				default: return "Intellect";
			}
		}

		/// <summary>What one more point buys, in the units the player actually sees.</summary>
		private static string PerPointOf(PlayerAttribute attribute)
		{
			switch (attribute)
			{
				case PlayerAttribute.Strength:
					return $"+{AttributeManager.HealthPerStrength} maximum life per point";
				case PlayerAttribute.Dexterity:
					return "+1% melee attack speed, magic cast speed and ranged velocity per point";
				default:
					return $"+{AttributeManager.ManaPerIntellect} maximum mana per point";
			}
		}

		/// <summary>The running total, so the player can see what they've actually bought.</summary>
		private string TotalOf(Player player, int value)
		{
			switch (attribute)
			{
				case PlayerAttribute.Strength:
					return $"+{value * AttributeManager.HealthPerStrength} maximum life";
				case PlayerAttribute.Dexterity:
					return $"+{value}% attack speed / velocity";
				default:
					return $"+{value * AttributeManager.ManaPerIntellect} maximum mana";
			}
		}

		/// <summary>
		/// Juggernaut turns Dexterity and Intellect off entirely. Saying so on the row is the only
		/// warning a player gets before sinking points into a stat that does nothing for their build.
		/// </summary>
		private string SuppressedNote(Player player)
		{
			TalentPlayer talents = TalentPlayer.Get(player);

			if (attribute == PlayerAttribute.Dexterity
				&& talents.Suppresses(TalentSuppression.AttackSpeedIncreases))
				return "  [DISABLED by Juggernaut]";

			if (attribute == PlayerAttribute.Intellect
				&& talents.Suppresses(TalentSuppression.ManaFromIntellect))
				return "  [DISABLED by Juggernaut]";

			return "";
		}

		private void Build()
		{
			Player player = Main.LocalPlayer;
			int value = AttributeManager.Get(player).Get(attribute);

			UIText title = new UIText($"{NameOf(attribute)}  —  {value}", 1.1f);
			title.Left.Set(4, 0f);
			Append(title);

			UIText effect = new UIText(TotalOf(player, value) + SuppressedNote(player), 0.85f);
			effect.Left.Set(4, 0f);
			effect.Top.Set(26, 0f);
			effect.TextColor = new Color(200, 200, 200);
			Append(effect);

			UIText perPoint = new UIText(PerPointOf(attribute), 0.75f);
			perPoint.Left.Set(4, 0f);
			perPoint.Top.Set(46, 0f);
			perPoint.TextColor = new Color(150, 150, 150);
			Append(perPoint);

			SmallButton minus = new SmallButton("-", new Color(120, 70, 70), () => owner.Refund(attribute), 34f, 30f);
			minus.HAlign = 1f;
			minus.VAlign = 0.5f;
			minus.Left.Set(-42, 0f);
			Append(minus);

			SmallButton plus = new SmallButton("+", new Color(70, 120, 80), () => owner.Invest(attribute), 34f, 30f);
			plus.HAlign = 1f;
			plus.VAlign = 0.5f;
			Append(plus);
		}

		public override void MouseOver(UIMouseEvent evt)
		{
			base.MouseOver(evt);
			owner.SetHoverTooltip($"{NameOf(attribute)}\n{PerPointOf(attribute)}");
		}
	}
}
