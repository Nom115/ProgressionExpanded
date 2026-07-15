using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;
using ProgressionExpanded.Src.Config;
using ProgressionExpanded.Src.Levels.PlayerSystems.Talents;
using ProgressionExpanded.Src.Levels.PlayerSystems.Talents.Behaviours;

namespace ProgressionExpanded.Src.UI
{
	/// <summary>
	/// Live readout of the Vengeance ramp: how much damage you have taken in the last 4 seconds, and
	/// the bonus it is currently buying you.
	///
	/// Vengeance is the only talent whose payoff moves several times a second and decays on its own,
	/// so without a number on screen the player is guessing at when to commit. Stagger surfaces its
	/// live state through a ModBuff tooltip, but only because its DoT needed a buff icon anyway — a
	/// buff introduced purely to carry text would be a worse fit than a HUD element, and a buff tip
	/// is not visible at a glance mid-fight, which is exactly when this number matters.
	///
	/// Draws nothing unless Vengeance is actually picked AND the ramp is live, so it costs a picked
	/// talent nothing while idle and never appears for the other two pinnacle choices.
	/// </summary>
	public class VengeanceReadout : UIState
	{
		private static readonly Color LabelColor = new Color(255, 190, 190);
		private static readonly Color RampColor = new Color(255, 80, 80);
		private static readonly Color MaxedColor = new Color(255, 215, 0);

		private ProgressionConfig Config => ModContent.GetInstance<ProgressionConfig>();

		public override void Draw(SpriteBatch spriteBatch)
		{
			if (Config == null || !Config.ShowVengeanceReadout)
				return;

			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead)
				return;

			VengeanceTalent vengeance = FindVengeance(player);
			if (vengeance == null)
				return;

			float damage = vengeance.GetRecentDamage();
			if (damage <= 0f)
				return;

			float bonus = vengeance.GetCurrentBonus(player);

			DrawReadout(
				spriteBatch,
				new Vector2(Config.VengeanceReadoutX, Main.screenHeight - Config.VengeanceReadoutY),
				Config.VengeanceReadoutScale,
				damage,
				bonus);

			base.Draw(spriteBatch);
		}

		/// <summary>
		/// Never cache the instance across frames: TalentPlayer.RebuildActive discards and recreates
		/// behaviours whenever the set of picks changes, so a held reference would go stale and keep
		/// reporting a talent the player no longer has.
		/// </summary>
		private static VengeanceTalent FindVengeance(Player player)
		{
			foreach (TalentBehaviour behaviour in TalentPlayer.Get(player).Active)
			{
				if (behaviour is VengeanceTalent vengeance)
					return vengeance;
			}
			return null;
		}

		private void DrawReadout(SpriteBatch spriteBatch, Vector2 position, float scale, float damage, float bonus)
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;

			// Gold once the bonus is pinned at its ceiling — that is the moment the player is paying
			// for the ramp (still taking damage) without getting any more out of it.
			bool maxed = bonus >= VengeanceTalent.MaxRampBonus;

			string damageText = $"{(int)damage} damage taken (4s)";
			string bonusText = $"Vengeance +{(int)(bonus * 100f)}% damage & healing";

			Terraria.Utils.DrawBorderString(spriteBatch, bonusText, position, maxed ? MaxedColor : RampColor, scale);

			Vector2 lineOffset = new Vector2(0f, font.MeasureString(bonusText).Y * scale);
			Terraria.Utils.DrawBorderString(spriteBatch, damageText, position + lineOffset, LabelColor, scale);
		}
	}

	/// <summary>ModSystem that manages the Vengeance readout UI layer.</summary>
	public class VengeanceReadoutUISystem : ModSystem
	{
		private VengeanceReadout readout;
		private UserInterface readoutInterface;

		public override void Load()
		{
			if (!Main.dedServ)
			{
				readout = new VengeanceReadout();
				readoutInterface = new UserInterface();
				readoutInterface.SetState(readout);
			}
		}

		public override void UpdateUI(GameTime gameTime)
		{
			readoutInterface?.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
		{
			int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
			if (mouseTextIndex != -1)
			{
				layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
					"ProgressionExpanded: Vengeance Readout",
					delegate
					{
						readoutInterface?.Draw(Main.spriteBatch, Main._drawInterfaceGameTime);
						return true;
					},
					InterfaceScaleType.UI)
				);
			}
		}
	}
}
