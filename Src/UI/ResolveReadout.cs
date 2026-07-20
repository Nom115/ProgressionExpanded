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
	/// Live readout of Juggernaut's Resolve ramp: how hardened you are (0..100%) and the damage reduction
	/// it is currently buying. A ramping, otherwise-invisible mitigation "feels bad" without a number on
	/// screen — the player cannot tell why their toughness climbs while under fire and fades in a lull,
	/// which is exactly the tension the talent is built on.
	///
	/// Same shape as VengeanceReadout: draws nothing unless Juggernaut is actually picked AND the ramp
	/// is above zero, so it costs a picked talent nothing while idle and never appears for the other two
	/// pinnacle choices.
	/// </summary>
	public class ResolveReadout : UIState
	{
		private static readonly Color LabelColor = new Color(190, 205, 220);
		private static readonly Color RampColor = new Color(150, 180, 210);
		private static readonly Color MaxedColor = new Color(255, 215, 0);

		private ProgressionConfig Config => ModContent.GetInstance<ProgressionConfig>();

		public override void Draw(SpriteBatch spriteBatch)
		{
			if (Config == null || !Config.ShowResolveReadout)
				return;

			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead)
				return;

			JuggernautTalent juggernaut = FindJuggernaut(player);
			if (juggernaut == null)
				return;

			float ramp = juggernaut.Resolve;
			if (ramp <= 0f)
				return;

			DrawReadout(
				spriteBatch,
				new Vector2(Config.ResolveReadoutX, Main.screenHeight - Config.ResolveReadoutY),
				Config.ResolveReadoutScale,
				ramp,
				juggernaut.DamageReduction);

			base.Draw(spriteBatch);
		}

		/// <summary>
		/// Never cache the instance across frames: TalentPlayer.RebuildActive discards and recreates
		/// behaviours whenever the set of picks changes, so a held reference would go stale and keep
		/// reporting a talent the player no longer has.
		/// </summary>
		private static JuggernautTalent FindJuggernaut(Player player)
		{
			foreach (TalentBehaviour behaviour in TalentPlayer.Get(player).Active)
			{
				if (behaviour is JuggernautTalent juggernaut)
					return juggernaut;
			}
			return null;
		}

		private void DrawReadout(SpriteBatch spriteBatch, Vector2 position, float scale, float ramp, float damageReduction)
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;

			// Gold once fully hardened — the wall is at its toughest, and any lull from here only softens it.
			bool maxed = ramp >= 1f;

			string rampText = $"Resolve {(int)(ramp * 100f)}%";
			string drText = $"+{(int)(damageReduction * 100f)}% damage reduction";

			Terraria.Utils.DrawBorderString(spriteBatch, rampText, position, maxed ? MaxedColor : RampColor, scale);

			Vector2 lineOffset = new Vector2(0f, font.MeasureString(rampText).Y * scale);
			Terraria.Utils.DrawBorderString(spriteBatch, drText, position + lineOffset, LabelColor, scale);
		}
	}

	/// <summary>ModSystem that manages the Resolve readout UI layer.</summary>
	public class ResolveReadoutUISystem : ModSystem
	{
		private ResolveReadout readout;
		private UserInterface readoutInterface;

		public override void Load()
		{
			if (!Main.dedServ)
			{
				readout = new ResolveReadout();
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
					"ProgressionExpanded: Resolve Readout",
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
