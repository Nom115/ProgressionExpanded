using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.UI;
using Terraria.GameContent.UI.Elements;
using Terraria.Audio;
using Terraria.ID;
using ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints;
using System;

namespace ProgressionExpanded.Src.UI.PassiveTree
{
	/// <summary>
	/// Panel for selecting player class
	/// </summary>
	public class ClassSelectionPanel : UIElement
	{
		private UIPanel background;
		private UIText titleText;
		private UIText descriptionText;
		
		public event Action<ClassSelectionManager.PlayerClass> OnClassSelected;

		public override void OnInitialize()
		{
			Width.Set(0, 0.6f);
			Height.Set(0, 0.6f);
			HAlign = 0.5f;
			VAlign = 0.5f;
			
			// Background
			background = new UIPanel();
			background.Width.Set(0, 1f);
			background.Height.Set(0, 1f);
			background.BackgroundColor = new Color(33, 43, 79) * 0.95f;
			background.BorderColor = new Color(89, 116, 213);
			Append(background);
			
			// Title
			titleText = new UIText("Choose Your Path", 1.8f);
			titleText.HAlign = 0.5f;
			titleText.Top.Set(40, 0f);
			background.Append(titleText);
			
			// Description
			descriptionText = new UIText("Select your class specialization. This choice is permanent unless reset.", 0.9f);
			descriptionText.HAlign = 0.5f;
			descriptionText.Top.Set(90, 0f);
			background.Append(descriptionText);
			
			// Create class buttons
			CreateClassButtons();
		}

		private void CreateClassButtons()
		{
			float buttonWidth = 180f;
			float buttonHeight = 120f;
			float spacing = 20f;
			
			// Use HAlign to center buttons properly
			// Calculate positions relative to center
			float totalWidth = (buttonWidth * 4) + (spacing * 3);
			float halfWidth = totalWidth / 2f;
			
			// Melee button - leftmost
			ClassButton meleeButton = new ClassButton(
				ClassSelectionManager.PlayerClass.Melee,
				"Melee",
				"Masters of close combat\nBonuses to melee damage\nand defense",
				true // Available
			);
			meleeButton.Width.Set(buttonWidth, 0f);
			meleeButton.Height.Set(buttonHeight, 0f);
			meleeButton.HAlign = 0.5f;
			meleeButton.Left.Set(-halfWidth + (buttonWidth / 2), 0f);
			meleeButton.Top.Set(160, 0f);
			meleeButton.OnClassSelected += SelectClass;
			background.Append(meleeButton);
			
			// Ranged button
			ClassButton rangedButton = new ClassButton(
				ClassSelectionManager.PlayerClass.Ranged,
				"Ranged",
				"Experts in ranged combat\nBonuses to ranged damage\nand critical strikes",
				false // Locked
			);
			rangedButton.Width.Set(buttonWidth, 0f);
			rangedButton.Height.Set(buttonHeight, 0f);
			rangedButton.HAlign = 0.5f;
			rangedButton.Left.Set(-halfWidth + (buttonWidth / 2) + buttonWidth + spacing, 0f);
			rangedButton.Top.Set(160, 0f);
			background.Append(rangedButton);
			
			// Magic button
			ClassButton magicButton = new ClassButton(
				ClassSelectionManager.PlayerClass.Magic,
				"Magic",
				"Wielders of arcane power\nBonuses to magic damage\nand mana efficiency",
				false // Locked
			);
			magicButton.Width.Set(buttonWidth, 0f);
			magicButton.Height.Set(buttonHeight, 0f);
			magicButton.HAlign = 0.5f;
			magicButton.Left.Set(-halfWidth + (buttonWidth / 2) + (buttonWidth + spacing) * 2, 0f);
			magicButton.Top.Set(160, 0f);
			background.Append(magicButton);
			
			// Summoner button
			ClassButton summonerButton = new ClassButton(
				ClassSelectionManager.PlayerClass.Summoner,
				"Summoner",
				"Masters of minions\nBonuses to minion damage\nand max minions",
				false // Locked
			);
			summonerButton.Width.Set(buttonWidth, 0f);
			summonerButton.Height.Set(buttonHeight, 0f);
			summonerButton.HAlign = 0.5f;
			summonerButton.Left.Set(-halfWidth + (buttonWidth / 2) + (buttonWidth + spacing) * 3, 0f);
			summonerButton.Top.Set(160, 0f);
			background.Append(summonerButton);
			
			// Warning text
			UIText warningText = new UIText("Note: Only Melee is currently available", 0.8f);
			warningText.HAlign = 0.5f;
			warningText.Top.Set(300, 0f);
			warningText.TextColor = Color.Yellow;
			background.Append(warningText);
		}

		private void SelectClass(ClassSelectionManager.PlayerClass playerClass)
		{
			OnClassSelected?.Invoke(playerClass);
		}

		/// <summary>
		/// Individual class selection button
		/// </summary>
		private class ClassButton : UIPanel
		{
			private readonly ClassSelectionManager.PlayerClass playerClass;
			private readonly string className;
			private readonly string description;
			private readonly bool isAvailable;
			
			private UIText classNameText;
			private UIText descriptionText;
			private UIText lockedText;
			
			public event Action<ClassSelectionManager.PlayerClass> OnClassSelected;

			public ClassButton(ClassSelectionManager.PlayerClass playerClass, string className, string description, bool isAvailable)
			{
				this.playerClass = playerClass;
				this.className = className;
				this.description = description;
				this.isAvailable = isAvailable;
				
				BackgroundColor = isAvailable ? new Color(60, 80, 120) * 0.9f : new Color(40, 40, 40) * 0.9f;
				BorderColor = isAvailable ? new Color(89, 116, 213) : new Color(80, 80, 80);
				
				// Class name
				classNameText = new UIText(className, 1.2f);
				classNameText.HAlign = 0.5f;
				classNameText.Top.Set(15, 0f);
				classNameText.TextColor = isAvailable ? Color.White : Color.Gray;
				Append(classNameText);
				
				// Description
				descriptionText = new UIText(description, 0.7f);
				descriptionText.HAlign = 0.5f;
				descriptionText.Top.Set(50, 0f);
				descriptionText.TextColor = isAvailable ? Color.LightGray : Color.DarkGray;
				Append(descriptionText);
				
				// Locked indicator
				if (!isAvailable)
				{
					lockedText = new UIText("Coming Soon", 0.8f);
					lockedText.HAlign = 0.5f;
					lockedText.VAlign = 1f;
					lockedText.Top.Set(-15, 0f);
					lockedText.TextColor = Color.Red;
					Append(lockedText);
				}
			}

			public override void LeftMouseDown(UIMouseEvent evt)
			{
				base.LeftMouseDown(evt);
				
				if (!isAvailable)
				{
					SoundEngine.PlaySound(SoundID.MenuClose);
					return;
				}
				
				SoundEngine.PlaySound(SoundID.MenuTick);
				OnClassSelected?.Invoke(playerClass);
			}

			public override void MouseOver(UIMouseEvent evt)
			{
				base.MouseOver(evt);
				
				if (isAvailable)
				{
					BackgroundColor = new Color(80, 100, 140) * 0.9f;
					SoundEngine.PlaySound(SoundID.MenuTick);
				}
			}

			public override void MouseOut(UIMouseEvent evt)
			{
				base.MouseOut(evt);
				
				if (isAvailable)
				{
					BackgroundColor = new Color(60, 80, 120) * 0.9f;
				}
			}
		}
	}
}
