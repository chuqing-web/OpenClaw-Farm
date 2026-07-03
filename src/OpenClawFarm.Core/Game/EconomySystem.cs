namespace OpenClawFarm.Core.Game;

using OpenClawFarm.Core.Models;

/// <summary>三线通用经济：动态物价、挂机衰减、跨线消耗统计。</summary>
public sealed class EconomySystem
{
    private readonly Dictionary<string, int> _dailySold = new();
    private int _farmTicks;
    private int _mineTicks;
    private int _fishTicks;
    private int _lastGameDay;

    public void OnNewDay(int gameDay)
    {
        if (gameDay != _lastGameDay)
        {
            _dailySold.Clear();
            _lastGameDay = gameDay;
        }
    }

    public void RecordActivity(string route)
    {
        switch (route)
        {
            case "farm": _farmTicks++; _mineTicks = Math.Max(0, _mineTicks - 2); _fishTicks = Math.Max(0, _fishTicks - 2); break;
            case "mine": _mineTicks++; _farmTicks = Math.Max(0, _farmTicks - 2); _fishTicks = Math.Max(0, _fishTicks - 2); break;
            case "fish": _fishTicks++; _farmTicks = Math.Max(0, _farmTicks - 2); _mineTicks = Math.Max(0, _mineTicks - 2); break;
        }
    }

    public double GetYieldMultiplier(string route) => route switch
    {
        "farm" => DecayMultiplier(_farmTicks),
        "mine" => DecayMultiplier(_mineTicks),
        "fish" => DecayMultiplier(_fishTicks),
        _ => 1.0,
    };

    private static double DecayMultiplier(int ticks)
    {
        if (ticks <= ItemIds.ActivityDecayStartTicks) return 1.0;
        var over = ticks - ItemIds.ActivityDecayStartTicks;
        var maxOver = ItemIds.ActivityDecayStartTicks * 3;
        var t = Math.Min(1.0, over / (double)maxOver);
        return 1.0 - t * (1.0 - ItemIds.ActivityDecayFloorPct / 100.0);
    }

    public void RecordSale(string itemId, int count)
    {
        var cat = ItemIds.GetMarketCategory(itemId);
        _dailySold[cat] = _dailySold.GetValueOrDefault(cat) + count;
    }

    public double GetPriceMultiplier(string itemId)
    {
        var cat = ItemIds.GetMarketCategory(itemId);
        var sold = _dailySold.GetValueOrDefault(cat);
        if (sold <= 50) return 1.0;
        if (sold <= 100) return 0.7;
        if (sold <= 200) return 0.4;
        return 0.2;
    }

    public int GetSellPrice(string itemId, double merchantMult, double prestigeMult)
    {
        if (!ItemIds.CropBasePrices.TryGetValue(itemId, out var basePrice)) return 0;
        return Math.Max(1, (int)Math.Round(basePrice * merchantMult * prestigeMult * GetPriceMultiplier(itemId)));
    }

    public EconomyState ToState() => new(
        new(_dailySold),
        _farmTicks, _mineTicks, _fishTicks,
        GetYieldMultiplier("farm"), GetYieldMultiplier("mine"), GetYieldMultiplier("fish"));

    public EconomySaveData Export() => new(
        new(_dailySold), _farmTicks, _mineTicks, _fishTicks, _lastGameDay);

    public void Restore(EconomySaveData? data)
    {
        _dailySold.Clear();
        _farmTicks = _mineTicks = _fishTicks = 0;
        _lastGameDay = 0;
        if (data == null) return;
        foreach (var (k, v) in data.DailySold) _dailySold[k] = v;
        _farmTicks = data.FarmTicks;
        _mineTicks = data.MineTicks;
        _fishTicks = data.FishTicks;
        _lastGameDay = data.LastGameDay;
    }
}
