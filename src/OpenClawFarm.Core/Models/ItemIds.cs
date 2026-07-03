namespace OpenClawFarm.Core.Models;

public static class ItemIds
{
    // --- 种植 ---
    public const string SeedStrawberry = "seed_strawberry";
    public const string SeedWheat = "seed_wheat";
    public const string SeedCarrot = "seed_carrot";
    public const string SeedCorn = "seed_corn";
    public const string SeedPumpkin = "seed_pumpkin";
    public const string SeedHybridStar = "seed_hybrid_star";
    public const string SeedHybridGold = "seed_hybrid_gold";
    public const string CropStrawberry = "crop_strawberry";
    public const string CropWheat = "crop_wheat";
    public const string CropCarrot = "crop_carrot";
    public const string CropCorn = "crop_corn";
    public const string CropPumpkin = "crop_pumpkin";
    public const string CropHybridStar = "crop_hybrid_star";
    public const string CropHybridGold = "crop_hybrid_gold";
    public const string ToolWateringCan = "tool_watering_can";
    public const string ToolHoe = "tool_hoe";
    public const string ToolSickle = "tool_sickle";
    public const string ToolPesticide = "tool_pesticide";
    public const string ToolFertilizer = "tool_fertilizer";
    public const string ToolAxe = "tool_axe";
    public const string CropWood = "crop_wood";
    public const string CropPlank = "crop_plank";
    public const string CropCharcoal = "crop_charcoal";

    // --- 挖矿 ---
    public const string OreStone = "ore_stone";
    public const string OreIron = "ore_iron";
    public const string OreSilver = "ore_silver";
    public const string OreGold = "ore_gold";
    public const string OreCrystal = "ore_crystal";
    public const string IngotIron = "ingot_iron";
    public const string IngotSilver = "ingot_silver";
    public const string ToolPickaxe = "tool_pickaxe";
    public const string ToolPickaxeAdv = "tool_pickaxe_adv";
    public const string ToolMineLantern = "tool_mine_lantern";
    public const string CropMushroom = "crop_mushroom";

    // --- 钓鱼 ---
    public const string FishCommon = "fish_common";
    public const string FishMedium = "fish_medium";
    public const string FishRare = "fish_rare";
    public const string FishGlow = "fish_glow";
    public const string FishDried = "fish_dried";
    public const string ToolRod = "tool_rod";
    public const string ToolRodAdv = "tool_rod_adv";
    public const string BaitBasic = "bait_basic";
    public const string BaitAdvanced = "bait_advanced";
    public const string MealFishStew = "meal_fish_stew";

    // --- 通用 ---
    public const string MerchantId = "merchant_01";
    public const string BlacksmithId = "blacksmith_01";
    public const string FishmongerId = "fishmonger_01";
    public const string OreMerchantId = "ore_merchant_01";
    public const string DecorFlowerBed = "decor_flower_bed";
    public const string DecorAquarium = "decor_aquarium";
    public const string DecorStatue = "decor_statue";
    public const string WellId = "well_01";
    public const string MineEntranceId = "mine_entrance";
    public const string CropEgg = "crop_egg";
    public const string CropMilk = "crop_milk";
    public const string CropWool = "crop_wool";
    public const string CropJam = "crop_jam";
    public const string CropFlour = "crop_flour";
    public const string CropCheese = "crop_cheese";
    public const string CropGiftBox = "crop_gift_box";
    public const string CropCake = "crop_cake";
    public const string CropCloth = "crop_cloth";
    public const string FactoryId = "factory_01";

    public const int MapWidth = 40;
    public const int MapHeight = 30;
    public const int TileSize = 32;
    public const int MerchantInteractRange = TileSize * 3;

    public const int ActivityDecayStartTicks = 3600; // ~4h @4s/tick
    public const int ActivityDecayFloorPct = 20;

    public static readonly IReadOnlyDictionary<string, string> SeedToCrop = new Dictionary<string, string>
    {
        [SeedStrawberry] = CropStrawberry,
        [SeedWheat] = CropWheat,
        [SeedCarrot] = CropCarrot,
        [SeedCorn] = CropCorn,
        [SeedPumpkin] = CropPumpkin,
        [SeedHybridStar] = CropHybridStar,
        [SeedHybridGold] = CropHybridGold,
    };

    public static readonly IReadOnlyDictionary<string, int> CropBasePrices = new Dictionary<string, int>
    {
        [CropStrawberry] = 18, [CropWheat] = 10, [CropCarrot] = 14, [CropCorn] = 16, [CropPumpkin] = 22,
        [CropHybridStar] = 35, [CropHybridGold] = 32,
        [CropJam] = 45, [CropFlour] = 28, [CropCheese] = 55, [CropGiftBox] = 120, [CropCake] = 180,
        [CropCloth] = 90, [CropEgg] = 12, [CropMilk] = 20, [CropWool] = 25, [CropCharcoal] = 8,
        [CropWood] = 6, [CropPlank] = 14,
        [CropMushroom] = 15,
        [OreStone] = 5, [OreIron] = 22, [OreSilver] = 45, [OreGold] = 80, [OreCrystal] = 120,
        [IngotIron] = 35, [IngotSilver] = 70,
        [FishCommon] = 8, [FishMedium] = 18, [FishRare] = 40, [FishGlow] = 65, [FishDried] = 14,
        [MealFishStew] = 0,
    };

    public static readonly string[] AllSeeds =
        [SeedStrawberry, SeedWheat, SeedCarrot, SeedCorn, SeedPumpkin];

    public static string GetMarketCategory(string itemId) => itemId switch
    {
        _ when itemId.StartsWith("crop_", StringComparison.Ordinal) && itemId != CropCharcoal => "crop",
        _ when itemId.StartsWith("ore_", StringComparison.Ordinal) || itemId.StartsWith("ingot_", StringComparison.Ordinal) => "ore",
        _ when itemId.StartsWith("fish_", StringComparison.Ordinal) => "fish",
        _ when itemId == CropCharcoal => "crop",
        _ => "misc",
    };

    public static bool IsSellable(string itemId) =>
        CropBasePrices.ContainsKey(itemId) && itemId != MealFishStew;

    public static (int X, int Y) MerchantPos => WorldMapData.MerchantPixel;
    public static (int X, int Y) WellPos => WorldMapData.WellPixel;
    public static (int X, int Y) PlayerSpawn => WorldMapData.PlayerSpawnPixel;
    public static (int X, int Y) MineEntrancePos => TileToPixel(5, 22);
    public static (int X, int Y) BlacksmithPos => TileToPixel(28, 8);
    public static (int X, int Y) FishmongerPos => TileToPixel(34, 10);
    public static (int X, int Y) OreMerchantPos => TileToPixel(7, 22);
    public static (int X, int Y) ForestEntrancePos => TileToPixel(2, 12);
    public static (int X, int Y) LumberCampPos => TileToPixel(4, 12);

    public static (int X, int Y) TileToPixel(int tileX, int tileY) =>
        (tileX * TileSize + TileSize / 2, tileY * TileSize + TileSize / 2);

    public static readonly (string Id, int TileX, int TileY)[] LandLayout = BuildLandLayout();

    private static (string Id, int TileX, int TileY)[] BuildLandLayout()
    {
        var list = new List<(string, int, int)>();
        var n = 1;
        for (var ty = WorldMapData.FarmOriginY; ty < WorldMapData.FarmOriginY + WorldMapData.FarmRows; ty++)
        for (var tx = WorldMapData.FarmOriginX; tx < WorldMapData.FarmOriginX + WorldMapData.FarmCols; tx++)
        {
            list.Add(($"land_{n:D2}", tx, ty));
            n++;
        }
        return list.ToArray();
    }
}
