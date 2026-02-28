using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ProgressionExpanded.Items.Materials
{
    public class VerdantParadox : ModItem
    {
        public override string Texture => "ProgressionExpanded/Assets/Items/Materials/VerdantParadox";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 25;

        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;

            Item.maxStack = 9999;
            Item.value = 0; // No buy/sell value - drop only
            Item.rare = ItemRarityID.LightRed;

            Item.consumable = false;
            Item.material = true; // This is a crafting material
            Item.scale = 0.25f; // Scale down the sprite

        }
    }
}