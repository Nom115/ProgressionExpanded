using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ProgressionExpanded.Items.Materials;

namespace ProgressionExpanded.RPGModule.Items.Materials
{
    public class TransmutationCore : ModItem
    {
        public override string Texture => "ProgressionExpanded/Assets/Items/Materials/TransmutationCore";
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.rare = ItemRarityID.Blue;
            Item.maxStack = 1;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.consumable = false;
            Item.autoReuse = false;
            Item.value = Item.buyPrice(gold: 1);
        }

        public override bool ConsumeItem(Player player)
        {
            // Never consume the item
            return false;
        }

        public override bool CanRightClick() => true;

        public override void RightClick(Player player)
        {
            // Only open UI on client side for the local player
            if (Main.myPlayer == player.whoAmI && !Main.dedServ)
            {
                // ModContent.GetInstance<TransmutationUISystem>().Open();
            }
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient<OrbOfTransmutation>(5);
            // recipe.AddIngredient(ItemID.Hellstone, 10);
            recipe.AddTile(TileID.DemonAltar); // Works for both Demon and Crimson Altars (same tile ID)
            recipe.Register();
        }
    }
}
