using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class OrderHub
{
    private readonly DailyOrder _daily;
    private readonly List<FarmOrderExtended> _contracts = [];
    private readonly List<FarmOrderExtended> _festivals = [];
    private readonly List<CrossLineOrder> _crossLine = [];
    private readonly List<CrossLineOrder> _weeklyCross = [];
    private readonly List<CrossLineOrder> _festivalDeliveries = [];
    private int _contractCounter;
    private int _weekSeed;

    public OrderHub()
    {
        _daily = new DailyOrder();
        _weekSeed = GetWeekSeed();
        GenerateContract();
        GenerateFestival();
        GenerateCrossLineOrder();
        GenerateWeeklyCrossOrder();
        GenerateFestivalDelivery();
    }

    private static int GetWeekSeed()
    {
        var now = DateTime.UtcNow;
        return now.Year * 100 + System.Globalization.ISOWeek.GetWeekOfYear(now);
    }

    private void GenerateCrossLineOrder()
    {
        _crossLine.Clear();
        _crossLine.Add(new CrossLineOrder(
            $"cross_{DateTime.UtcNow:yyyyMMdd}",
            new Dictionary<string, int>
            {
                [ItemIds.CropStrawberry] = 20,
                [ItemIds.IngotIron] = 5,
                [ItemIds.FishMedium] = 8,
            },
            new Dictionary<string, int>(),
            1200, false, false,
            DateTime.UtcNow.AddDays(1).ToString("O"),
            "daily"));
    }

    private void GenerateWeeklyCrossOrder()
    {
        var scale = 1 + (_weekSeed % 4) * 0.25;
        _weeklyCross.Clear();
        _weeklyCross.Add(new CrossLineOrder(
            $"weekly_{_weekSeed}",
            new Dictionary<string, int>
            {
                [ItemIds.CropStrawberry] = (int)(50 * scale),
                [ItemIds.IngotIron] = (int)(15 * scale),
                [ItemIds.FishMedium] = (int)(20 * scale),
            },
            new Dictionary<string, int>(),
            (int)(3500 * scale), false, false,
            DateTime.UtcNow.AddDays(7).ToString("O"),
            "weekly"));
    }

    private void GenerateFestivalDelivery()
    {
        var season = DateTime.UtcNow.Month switch
        {
            >= 3 and <= 5 => ("spring", ItemIds.CropStrawberry),
            >= 6 and <= 8 => ("summer", ItemIds.CropCorn),
            >= 9 and <= 11 => ("autumn", ItemIds.CropPumpkin),
            _ => ("winter", ItemIds.CropWheat),
        };
        _festivalDeliveries.Clear();
        _festivalDeliveries.Add(new CrossLineOrder(
            $"festival_{season.Item1}",
            new Dictionary<string, int>
            {
                [season.Item2] = 25,
                [ItemIds.IngotSilver] = 5,
                [ItemIds.FishRare] = 6,
                [ItemIds.CropJam] = 3,
            },
            new Dictionary<string, int>(),
            2500, false, false,
            DateTime.UtcNow.AddDays(3).ToString("O"),
            "festival"));
    }

    private void GenerateContract()
    {
        var crops = ItemIds.CropBasePrices.Keys.Where(k => k.StartsWith("crop_")).ToArray();
        var seed = _weekSeed;
        _contracts.Clear();
        _contracts.Add(new FarmOrderExtended(
            $"contract_{++_contractCounter}", "contract", crops[seed % crops.Length],
            20, 0, 500, null,
            DateTime.UtcNow.AddDays(7).ToString("O"), false, false));
    }

    private void GenerateFestival()
    {
        var season = DateTime.UtcNow.Month switch
        {
            >= 3 and <= 5 => "spring_festival",
            >= 6 and <= 8 => "summer_festival",
            >= 9 and <= 11 => "autumn_festival",
            _ => "winter_festival",
        };
        _festivals.Clear();
        _festivals.Add(new FarmOrderExtended(
            $"festival_{season}", "festival", ItemIds.CropPumpkin,
            15, 0, 1500, null,
            DateTime.UtcNow.AddDays(3).ToString("O"), false, false));
    }

    public void OnNewDay(int gameDay)
    {
        GenerateCrossLineOrder();
        if (gameDay % 7 == 1)
        {
            var ws = GetWeekSeed();
            if (ws != _weekSeed)
            {
                _weekSeed = ws;
                GenerateWeeklyCrossOrder();
                GenerateContract();
            }
        }
        if (gameDay % 28 == 1)
            GenerateFestivalDelivery();
    }

    public FarmOrderState GetDailyState() => _daily.GetState();

    public OrderHubState GetState()
    {
        var daily = _daily.GetState().Orders.Select(o => new FarmOrderExtended(
            $"daily_{o.CropId}", "daily", o.CropId, o.Required, o.Delivered, o.Reward,
            null, _daily.ExpiresAt, o.Delivered >= o.Required, false)).ToList();

        var allCross = _crossLine.Concat(_weeklyCross).Concat(_festivalDeliveries).ToList();
        return new OrderHubState([..daily, .._contracts, .._festivals], allCross);
    }

    public int OnSell(string cropId, int count)
    {
        var bonus = _daily.OnSell(cropId, count);
        foreach (var order in _contracts.Concat(_festivals).Where(o => !o.Completed))
        {
            if (order.ItemId != null && order.ItemId != cropId) continue;
            if (order.ItemId == null && order.CropId != cropId) continue;
            var idx = order.Type == "contract"
                ? _contracts.FindIndex(o => o.Id == order.Id)
                : _festivals.FindIndex(o => o.Id == order.Id);
            var list = order.Type == "contract" ? _contracts : _festivals;
            var cur = list[idx];
            var delivered = Math.Min(cur.Required, cur.Delivered + count);
            list[idx] = cur with { Delivered = delivered, Completed = delivered >= cur.Required };
            if (delivered >= cur.Required && !cur.Completed)
                bonus += cur.Reward;
        }
        return bonus;
    }

    public (bool Ok, string Message, int Gold) DeliverOrder(string? orderId, Inventory inv, string? typeFilter = null)
    {
        var pool = typeFilter switch
        {
            "weekly" => _weeklyCross,
            "festival" => _festivalDeliveries,
            "daily" => _crossLine,
            _ => _crossLine.Concat(_weeklyCross).Concat(_festivalDeliveries).ToList(),
        };

        var order = (orderId != null
                ? pool.FirstOrDefault(o => o.Id == orderId && !o.Completed)
                : null)
            ?? pool.FirstOrDefault(o => !o.Completed);
        if (order == null) return (false, "no active order", 0);

        foreach (var (itemId, need) in order.Required)
        {
            var have = inv.GetCount(itemId);
            if (have < need)
                return (false, $"need {need}x {itemId} (have {have})", 0);
        }

        foreach (var (itemId, need) in order.Required)
            inv.RemoveItem(itemId, need);

        UpdateOrderList(order, o => o with { Completed = true, Claimed = true });
        inv.AddGold(order.GoldReward);
        return (true, $"{order.Type} order complete +{order.GoldReward}g", order.GoldReward);
    }

    private void UpdateOrderList(CrossLineOrder order, Func<CrossLineOrder, CrossLineOrder> update)
    {
        void Upd(List<CrossLineOrder> list)
        {
            var idx = list.FindIndex(o => o.Id == order.Id);
            if (idx >= 0) list[idx] = update(list[idx]);
        }
        Upd(_crossLine);
        Upd(_weeklyCross);
        Upd(_festivalDeliveries);
    }

    public (bool Ok, string Message, int Gold) DeliverCrossLine(string? orderId, Inventory inv) =>
        DeliverOrder(orderId, inv, null);

    public (bool Ok, string Message, int Gold) DeliverWeekly(string? orderId, Inventory inv) =>
        DeliverOrder(orderId, inv, "weekly");

    public (bool Ok, string Message, int Gold) DeliverFestival(string? orderId, Inventory inv) =>
        DeliverOrder(orderId, inv, "festival");

    public bool ClaimOrder(string orderId)
    {
        foreach (var list in new[] { _contracts, _festivals })
        {
            var idx = list.FindIndex(o => o.Id == orderId && o.Completed && !o.Claimed);
            if (idx >= 0)
            {
                list[idx] = list[idx] with { Claimed = true };
                return true;
            }
        }
        return false;
    }

    public OrderHubSaveData Export() => new(
        _contracts.Select(c => c with { }).ToList(),
        _festivals.Select(f => f with { }).ToList(),
        _crossLine.Select(c => c with { }).ToList(),
        _weeklyCross.Select(c => c with { }).ToList(),
        _festivalDeliveries.Select(c => c with { }).ToList());

    public void Restore(OrderHubSaveData? data)
    {
        if (data == null) return;
        _contracts.Clear();
        _contracts.AddRange(data.Contracts);
        _festivals.Clear();
        _festivals.AddRange(data.Festivals);
        _crossLine.Clear();
        _crossLine.AddRange(data.CrossLine);
        _weeklyCross.Clear();
        _weeklyCross.AddRange(data.WeeklyCross);
        _festivalDeliveries.Clear();
        _festivalDeliveries.AddRange(data.FestivalDeliveries);
        if (_crossLine.Count == 0) GenerateCrossLineOrder();
        if (_weeklyCross.Count == 0) GenerateWeeklyCrossOrder();
        if (_festivalDeliveries.Count == 0) GenerateFestivalDelivery();
    }
}
