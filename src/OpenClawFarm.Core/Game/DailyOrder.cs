using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class DailyOrder
{
    private readonly List<FarmOrder> _orders;
    public string ExpiresAt { get; }

    public DailyOrder()
    {
        var crops = ItemIds.CropBasePrices.Keys.ToArray();
        var today = DateTime.Now;
        var end = new DateTime(today.Year, today.Month, today.Day, 23, 59, 59, DateTimeKind.Local);
        ExpiresAt = end.ToUniversalTime().ToString("O");

        var seed = today.Year * 10000 + today.Month * 100 + today.Day;
        _orders =
        [
            MakeOrder(crops[seed % crops.Length], 3 + seed % 3, 50 + (seed % 5) * 20),
            MakeOrder(crops[(seed + 1) % crops.Length], 2 + seed % 4, 40 + (seed % 4) * 15),
        ];
        if (seed % 2 == 0)
            _orders.Add(MakeOrder(crops[(seed + 2) % crops.Length], 4, 80));
    }

    private static FarmOrder MakeOrder(string cropId, int required, int reward) =>
        new(cropId, required, 0, reward);

    public FarmOrderState GetState() =>
        new(_orders.Select(o => o with { }).ToList(), ExpiresAt);

    public int OnSell(string cropId, int count)
    {
        var bonus = 0;
        foreach (var order in _orders)
        {
            if (order.CropId != cropId) continue;
            var remaining = order.Required - order.Delivered;
            if (remaining <= 0) continue;
            var applied = Math.Min(remaining, count);
            var idx = _orders.IndexOf(order);
            _orders[idx] = order with { Delivered = order.Delivered + applied };
            if (_orders[idx].Delivered >= order.Required)
                bonus += order.Reward;
        }
        return bonus;
    }
}
