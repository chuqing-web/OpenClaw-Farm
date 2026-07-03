using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

/// <summary>相邻同作物羁绊：每有一个相邻同种作物 +8% 生长，最多 +32%。</summary>
public sealed class LandBondSystem
{
    private readonly Dictionary<string, (int Tx, int Ty)> _tiles = new();
    private readonly Dictionary<string, List<string>> _neighbors = new();

    public LandBondSystem()
    {
        foreach (var (id, tx, ty) in ItemIds.LandLayout)
            _tiles[id] = (tx, ty);

        foreach (var (id, (tx, ty)) in _tiles)
        {
            var list = new List<string>();
            foreach (var (oid, (ox, oy)) in _tiles)
            {
                if (oid == id) continue;
                if (Math.Abs(tx - ox) + Math.Abs(ty - oy) == 1)
                    list.Add(oid);
            }
            _neighbors[id] = list;
        }
    }

    public double GetGrowthBonus(string landId, IReadOnlyDictionary<string, FarmLand> lands)
    {
        if (!_neighbors.TryGetValue(landId, out var neighbors)) return 1.0;
        if (!lands.TryGetValue(landId, out var self) || self.CropId == null) return 1.0;

        var matches = neighbors.Count(n =>
            lands.TryGetValue(n, out var l) && l.CropId == self.CropId && l.State is "planted" or "growing" or "mature");
        return 1.0 + Math.Min(0.32, matches * 0.08);
    }

    public LandBondState ToState(IReadOnlyDictionary<string, FarmLand> lands)
    {
        var bonds = lands.Values
            .Where(l => l.CropId != null && l.State != "empty")
            .Select(l => new LandBondEntry(l.Id, GetGrowthBonus(l.Id, lands)))
            .Where(b => b.Bonus > 1.01)
            .ToList();
        return new LandBondState(bonds);
    }
}
