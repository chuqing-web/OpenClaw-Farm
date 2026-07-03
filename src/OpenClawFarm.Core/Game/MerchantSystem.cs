using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class MerchantSystem
{
    public MerchantShop Crop { get; }
    public MerchantShop Ore { get; }
    public MerchantShop Fish { get; }

    public MerchantSystem(int? daySeed = null)
    {
        var (cx, cy) = ItemIds.MerchantPos;
        var (ox, oy) = ItemIds.OreMerchantPos;
        var (fx, fy) = ItemIds.FishmongerPos;
        Crop = new MerchantShop(ItemIds.MerchantId, "作物商人", "crop", cx, cy, daySeed);
        Ore = new MerchantShop(ItemIds.OreMerchantId, "矿石商人", "ore", ox, oy, daySeed);
        Fish = new MerchantShop(ItemIds.FishmongerId, "渔贩", "fish", fx, fy, daySeed);
    }

    public MerchantShop? Get(string? id) => id switch
    {
        ItemIds.MerchantId or null or "" => Crop,
        ItemIds.OreMerchantId => Ore,
        ItemIds.FishmongerId => Fish,
        _ => null,
    };

    public IEnumerable<MerchantShop> All => [Crop, Ore, Fish];

    public List<MerchantState> ToState() => All.Select(m => m.ToState()).ToList();
}

public sealed class MerchantShop
{
    public string Id { get; }
    public string Name { get; }
    public string Category { get; }
    public int X { get; }
    public int Y { get; }
    public string SceneId { get; } = "farm_main";
    private readonly double _priceMultiplier;

    public MerchantShop(string id, string name, string category, int x, int y, int? daySeed)
    {
        Id = id;
        Name = name;
        Category = category;
        X = x;
        Y = y;
        var seed = daySeed ?? TodaySeed();
        var fluctuation = ((seed % 21) - 10) / 100.0;
        _priceMultiplier = 1 + fluctuation + category switch
        {
            "ore" => 0.05,
            "fish" => -0.03,
            _ => 0,
        };
    }

    private static int TodaySeed()
    {
        var d = DateTime.Now;
        return d.Year * 10000 + d.Month * 100 + d.Day;
    }

    public double PriceMultiplier => _priceMultiplier;

    public bool Accepts(string itemId) =>
        ItemIds.IsSellable(itemId) && ItemIds.GetMarketCategory(itemId) == Category;

    public Dictionary<string, int> GetPrices()
    {
        var prices = new Dictionary<string, int>();
        foreach (var (itemId, basePrice) in ItemIds.CropBasePrices)
        {
            if (!Accepts(itemId)) continue;
            prices[itemId] = (int)Math.Round(basePrice * _priceMultiplier);
        }
        return prices;
    }

    public MerchantState ToState() => new(Id, Name, Category, X, Y, SceneId, GetPrices());
}
