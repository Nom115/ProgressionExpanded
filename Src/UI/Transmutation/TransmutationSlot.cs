using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI;

namespace ProgressionExpanded.Src.UI.Transmutation
{
	/// <summary>
	/// A single item socket built on vanilla <see cref="ItemSlot"/>. The player drops the item to be
	/// crafted here; orbs operate on <see cref="Item"/>.
	/// </summary>
	public class TransmutationSlot : UIElement
	{
		public Item Item = new Item();

		private readonly float scale;
		private const int SlotContext = ItemSlot.Context.BankItem;

		public TransmutationSlot(float scale = 0.9f)
		{
			this.scale = scale;
			Item.SetDefaults(ItemID.None);

			float size = TextureAssets.InventoryBack.Value.Width * scale;
			Width.Set(size, 0f);
			Height.Set(size, 0f);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			float oldScale = Main.inventoryScale;
			Main.inventoryScale = scale;

			CalculatedStyle dims = GetDimensions();
			Vector2 position = new Vector2(dims.X, dims.Y);

			if (ContainsPoint(Main.MouseScreen))
			{
				Main.LocalPlayer.mouseInterface = true;
				ItemSlot.Handle(ref Item, SlotContext);
			}

			ItemSlot.Draw(spriteBatch, ref Item, SlotContext, position);
			Main.inventoryScale = oldScale;
		}
	}
}
