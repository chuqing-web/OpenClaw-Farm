namespace OpenClawFarm.Core.Models;

public static class ItemIds
{
    public const string SeedStrawberry = "seed_strawberry";
    public const string SeedWheat = "seed_wheat";
    public const string SeedCarrot = "seed_carrot";
    public const string SeedCorn = "seed_corn";
    public const string SeedPumpkin = "seed_pumpkin";
    public const string CropStrawberry = "crop_strawberry";
    public const string CropWheat = "crop_wheat";
    public const string CropCarrot = "crop_carrot";
    public const string CropCorn = "crop_corn";
    public const string CropPumpkin = "crop_pumpkin";
    public const string ToolWateringCan = "tool_watering_can";
    public const string ToolHoe = "tool_hoe";
    public const string ToolSickle = "tool_sickle";
    public const string MerchantId = "merchant_01";
    public const string WellId = "well_01";

    public const int MapWidth = 40;
    public const int MapHeight = 30;
    public const int TileSize = 32;
    public const int MerchantInteractRange = TileSize * 3;

    public static readonly IReadOnlyDictionary<string, string> SeedToCrop = new Dictionary<string, string>
    {
        [SeedStrawberry] = CropStrawberry,
        [SeedWheat] = CropWheat,
        [SeedCarrot] = CropCarrot,
        [SeedCorn] = CropCorn,
        [SeedPumpkin] = CropPumpkin,
    };

    public static readonly IReadOnlyDictionary<string, int> CropBasePrices = new Dictionary<string, int>
    {
        [CropStrawberry] = 18,
        [CropWheat] = 10,
        [CropCarrot] = 14,
        [CropCorn] = 16,
        [CropPumpkin] = 22,
    };

    public static readonly string[] AllSeeds =
        [SeedStrawberry, SeedWheat, SeedCarrot, SeedCorn, SeedPumpkin];

    public static (int X, int Y) MerchantPos => WorldMapData.MerchantPixel;
    public static (int X, int Y) WellPos => WorldMapData.WellPixel;
    public static (int X, int Y) PlayerSpawn => WorldMapData.PlayerSpawnPixel;

    public static (int X, int Y) TileToPixel(int tileX, int tileY) =>
        (tileX * TileSize + TileSize / 2, tileY * TileSize + TileSize / 2);

    public static readonly (string Id, int TileX, int TileY)[] LandLayout = BuildLandLayout();

    private static (string Id, int TileX, int TileY)[] BuildLandLayout()
    {
        var list = new List<(string, int, int)>();
        var n = 1;
        for (var ty = WorldMapData.FarmOriginY; ty < WorldMapData.FarmOriginY + WorldMapData.FarmRows; ty++)
        {
            for (var tx = WorldMapData.FarmOriginX; tx < WorldMapData.FarmOriginX + WorldMapData.FarmCols; tx++)
            {
                list.Add(($"land_{n:D2}", tx, ty));
                n++;
            }
        }
        return list.ToArray();
    }
}
