using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

/// <summary>奢侈装饰：永久消耗物资，无产出。</summary>
public sealed class DecorationSystem
{
    private static readonly Dictionary<string, (string Name, Dictionary<string, int> Cost)> Catalog = new()
    {
        [ItemIds.DecorFlowerBed] = ("花坛", new()
        {
            [ItemIds.CropStrawberry] = 30,
            [ItemIds.CropHybridStar] = 5,
        }),
        [ItemIds.DecorAquarium] = ("水族馆", new()
        {
            [ItemIds.FishGlow] = 3,
            [ItemIds.FishRare] = 8,
            [ItemIds.IngotSilver] = 2,
        }),
        [ItemIds.DecorStatue] = ("金属雕塑", new()
        {
            [ItemIds.OreGold] = 8,
            [ItemIds.OreCrystal] = 4,
        }),
    };

    private readonly List<string> _placed = [];

    public (bool Ok, string Message) Place(string decorId, Inventory inv)
    {
        if (!Catalog.TryGetValue(decorId, out var def))
            return (false, "unknown decoration");
        if (_placed.Contains(decorId))
            return (false, "already placed");

        foreach (var (item, need) in def.Cost)
        {
            if (inv.GetCount(item) < need)
                return (false, $"need {need}x {item}");
        }
        foreach (var (item, need) in def.Cost)
            inv.RemoveItem(item, need);

        _placed.Add(decorId);
        return (true, $"placed {def.Name} ({decorId})");
    }

    public DecorationState ToState() => new(
        Catalog.Select(kv => new DecorationEntry(kv.Key, kv.Value.Name, _placed.Contains(kv.Key), kv.Value.Cost)).ToList());

    public DecorationSaveData Export() => new(_placed.ToList());

    public void Restore(DecorationSaveData? data)
    {
        _placed.Clear();
        if (data == null) return;
        _placed.AddRange(data.Placed);
    }
}
