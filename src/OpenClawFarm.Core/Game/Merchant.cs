using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class Merchant
{
    public string Id { get; }
    public int X { get; }
    public int Y { get; }
    public string SceneId { get; }
    private readonly double _priceMultiplier;

    public Merchant(string id, int x, int y, string sceneId, int? daySeed = null)
    {
        Id = id;
        X = x;
        Y = y;
        SceneId = sceneId;
        var seed = daySeed ?? TodaySeed();
        var fluctuation = ((seed % 21) - 10) / 100.0;
        _priceMultiplier = 1 + fluctuation;
    }

    private static int TodaySeed()
    {
        var d = DateTime.Now;
        return d.Year * 10000 + d.Month * 100 + d.Day;
    }

    public Dictionary<string, int> GetPrices()
    {
        var prices = new Dictionary<string, int>();
        foreach (var (crop, basePrice) in ItemIds.CropBasePrices)
            prices[crop] = (int)Math.Round(basePrice * _priceMultiplier);
        return prices;
    }

    public int GetPrice(string itemId) => GetPrices().GetValueOrDefault(itemId);

    public static bool IsCrop(string itemId) => itemId.StartsWith("crop_", StringComparison.Ordinal);
}
