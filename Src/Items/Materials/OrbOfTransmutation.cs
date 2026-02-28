using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ProgressionExpanded.Items.Materials
{
    public class OrbOfTransmutation : ModItem
    {
        // If you put the PNG in Assets/Items/Materials/OrbOfTransmutation.png
        // uncomment this line to explicitly point to it.
        public override string Texture => "ProgressionExpanded/Assets/Items/Materials/OrbOfTransmutation";

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
            Item.rare = ItemRarityID.Green;

            Item.consumable = false;
            Item.material = true; // This is a crafting material
            Item.scale = 0.25f; // Scale down the sprite

        }
    }
}