using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

/// <summary>多水域钓鱼：鱼饵消耗、每日额度、产出衰减。</summary>
public sealed class FishingSystem
{
    public sealed record Pond(string Id, string Name, int X, int Y, string[] Fish, string BaitReq, int DailyCap);

    public static readonly Pond[] Ponds =
    [
        new("p01", "pond", ItemIds.TileToPixel(35, 7).X, ItemIds.TileToPixel(35, 7).Y,
            [ItemIds.FishCommon, ItemIds.FishMedium], ItemIds.BaitBasic, 40),
        new("p02", "river", ItemIds.TileToPixel(38, 12).X, ItemIds.TileToPixel(38, 12).Y,
            [ItemIds.FishMedium, ItemIds.FishRare], ItemIds.BaitBasic, 30),
        new("p03", "lake", ItemIds.TileToPixel(36, 15).X, ItemIds.TileToPixel(36, 15).Y,
            [ItemIds.FishRare, ItemIds.FishGlow], ItemIds.BaitAdvanced, 20),
        new("p04", "underground", ItemIds.TileToPixel(8, 24).X, ItemIds.TileToPixel(8, 24).Y,
            [ItemIds.FishGlow, ItemIds.FishRare], ItemIds.BaitAdvanced, 15),
    ];

    private readonly Dictionary<string, int> _dailyCaught = new();
    private readonly Dictionary<string, int> _pondFatigue = new();
    private int _lastGameDay;

    public void OnNewDay(int gameDay)
    {
        if (gameDay != _lastGameDay)
        {
            _dailyCaught.Clear();
            _pondFatigue.Clear();
            _lastGameDay = gameDay;
        }
    }

    public Pond? GetPond(string id) => Ponds.FirstOrDefault(p => p.Id == id);

    public (bool Ok, string Message, string? FishId) Fish(
        string pondId, string? baitId, EconomySystem economy, Inventory inv)
    {
        var pond = GetPond(pondId);
        if (pond == null) return (false, "unknown pond", null);

        var caught = _dailyCaught.GetValueOrDefault(pondId);
        if (caught >= pond.DailyCap)
            return (false, "daily catch limit reached for this water", null);

        var bait = baitId ?? pond.BaitReq;
        if (inv.GetCount(bait) < 1)
            return (false, $"need bait: {bait}", null);
        inv.RemoveItem(bait, 1);

        var fatigue = _pondFatigue.GetValueOrDefault(pondId);
        var fatigueMult = fatigue > 2700 ? 0.2 : fatigue > 900 ? 0.5 : 1.0;
        if (Random.Shared.NextDouble() > economy.GetYieldMultiplier("fish") * fatigueMult)
            return (true, "no bite this cast", null);

        var fish = pond.Fish[Random.Shared.Next(pond.Fish.Length)];
        if (!inv.TryAddItem(fish, 1))
            return (false, "fish storage full", null);

        _dailyCaught[pondId] = caught + 1;
        _pondFatigue[pondId] = fatigue + 1;
        economy.RecordActivity("fish");
        return (true, $"caught {fish}", fish);
    }

    public (bool Ok, string Message) CraftBait(string baitId, Inventory inv)
    {
        if (baitId == ItemIds.BaitBasic)
        {
            if (inv.GetCount(ItemIds.CropWheat) < 2)
                return (false, "need 2 crop_wheat");
            inv.RemoveItem(ItemIds.CropWheat, 2);
        }
        else if (baitId == ItemIds.BaitAdvanced)
        {
            if (inv.GetCount(ItemIds.CropCarrot) < 1 || inv.GetCount(ItemIds.FishCommon) < 1)
                return (false, "need 1 crop_carrot + 1 fish_common");
            inv.RemoveItem(ItemIds.CropCarrot, 1);
            inv.RemoveItem(ItemIds.FishCommon, 1);
        }
        else return (false, "unknown bait recipe");

        inv.AddItem(baitId, 3);
        return (true, $"crafted 3x {baitId}");
    }

    public FishPondState ToState(string? pondId = null)
    {
        var pond = pondId != null ? GetPond(pondId) : Ponds[0];
        if (pond == null) pond = Ponds[0];
        return new FishPondState(
            pond.Id, pond.Name,
            _dailyCaught.GetValueOrDefault(pond.Id),
            pond.DailyCap,
            _pondFatigue.GetValueOrDefault(pond.Id),
            pond.X, pond.Y,
            pond.Fish, pond.BaitReq);
    }

    public List<FishPondState> AllPondSummaries() =>
        Ponds.Select(p => ToState(p.Id)).ToList();

    public FishingSaveData Export() => new(
        new(_dailyCaught), new(_pondFatigue), _lastGameDay);

    public void Restore(FishingSaveData? data)
    {
        _dailyCaught.Clear();
        _pondFatigue.Clear();
        _lastGameDay = 0;
        if (data == null) return;
        foreach (var (k, v) in data.DailyCaught) _dailyCaught[k] = v;
        foreach (var (k, v) in data.PondFatigue) _pondFatigue[k] = v;
        _lastGameDay = data.LastGameDay;
    }
}
