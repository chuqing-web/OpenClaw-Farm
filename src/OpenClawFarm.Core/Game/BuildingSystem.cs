using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class BuildingSystem
{
    private static readonly Dictionary<string, Dictionary<string, int>> UnlockMaterials = new()
    {
        ["greenhouse"] = new() { [ItemIds.CropWheat] = 40, [ItemIds.CropCorn] = 20, [ItemIds.CropWood] = 20, [ItemIds.IngotIron] = 3 },
        ["auto_sprinkler"] = new() { [ItemIds.CropCarrot] = 30, [ItemIds.IngotIron] = 8, [ItemIds.CropCharcoal] = 5, [ItemIds.CropPlank] = 10 },
        ["auto_harvester"] = new() { [ItemIds.CropCorn] = 50, [ItemIds.IngotIron] = 12, [ItemIds.IngotSilver] = 2, [ItemIds.CropPlank] = 15 },
        ["warehouse"] = new() { [ItemIds.CropWheat] = 25, [ItemIds.OreIron] = 10, [ItemIds.CropWood] = 15 },
        ["lumber_camp"] = new() { [ItemIds.CropWood] = 30, [ItemIds.CropPlank] = 5 },
        ["sawmill"] = new() { [ItemIds.CropWood] = 20, [ItemIds.CropPlank] = 8, [ItemIds.IngotIron] = 3 },
    };

    private static readonly Dictionary<string, Dictionary<string, int>> UpgradeBaseMaterials = new()
    {
        ["greenhouse"] = new() { [ItemIds.CropStrawberry] = 15, [ItemIds.IngotIron] = 2, [ItemIds.CropPlank] = 5 },
        ["auto_sprinkler"] = new() { [ItemIds.CropCarrot] = 10, [ItemIds.IngotIron] = 3, [ItemIds.CropWood] = 8 },
        ["auto_harvester"] = new() { [ItemIds.CropCorn] = 15, [ItemIds.IngotSilver] = 1, [ItemIds.CropPlank] = 8 },
        ["barn"] = new() { [ItemIds.CropWheat] = 20, [ItemIds.IngotIron] = 2, [ItemIds.CropWood] = 10 },
        ["factory"] = new() { [ItemIds.IngotIron] = 3, [ItemIds.FishCommon] = 5, [ItemIds.CropPlank] = 5 },
        ["lumber_camp"] = new() { [ItemIds.CropWood] = 15, [ItemIds.CropPlank] = 3 },
        ["sawmill"] = new() { [ItemIds.CropWood] = 10, [ItemIds.CropPlank] = 5, [ItemIds.IngotIron] = 2 },
    };

    private readonly Dictionary<string, BuildingState> _buildings = new()
    {
        ["greenhouse"] = new("greenhouse", "温室大棚", false, 0, 2000),
        ["auto_sprinkler"] = new("auto_sprinkler", "自动洒水器", false, 0, 3500),
        ["auto_harvester"] = new("auto_harvester", "自动收割机", false, 0, 5000),
        ["factory"] = new("factory", "加工工坊", true, 1, 0),
        ["barn"] = new("barn", "畜棚", true, 1, 1500),
        ["warehouse"] = new("warehouse", "种子仓库", false, 0, 1200),
        ["lumber_camp"] = new("lumber_camp", "伐木营地", false, 0, 800),
        ["sawmill"] = new("sawmill", "锯木厂", false, 0, 1500),
    };

    public IReadOnlyDictionary<string, BuildingState> All => _buildings;

    public (bool Ok, string Message) TryUnlock(string id, Inventory inv)
    {
        if (!_buildings.TryGetValue(id, out var b))
            return (false, "unknown building");
        if (b.Unlocked)
            return (false, "already unlocked");

        if (inv.Gold < b.UpgradeCost)
            return (false, $"need {b.UpgradeCost} gold");

        if (UnlockMaterials.TryGetValue(id, out var mats))
        {
            foreach (var (item, need) in mats)
            {
                if (inv.GetCount(item) < need)
                    return (false, $"need {need}x {item}");
            }
            foreach (var (item, need) in mats)
                inv.RemoveItem(item, need);
        }

        inv.AddGold(-b.UpgradeCost);
        _buildings[id] = b with { Unlocked = true, Level = 1 };
        return (true, $"unlocked {b.Name}");
    }

    public (bool Ok, string Message) TryUpgrade(string id, Inventory inv)
    {
        if (!_buildings.TryGetValue(id, out var b))
            return (false, "unknown building");
        if (!b.Unlocked)
            return (false, "building not unlocked");
        if (b.Level >= 5)
            return (false, "max level reached");

        if (!UpgradeBaseMaterials.TryGetValue(id, out var baseMats))
            return (false, "building cannot upgrade");

        var scale = Math.Pow(1.8, b.Level);
        var goldCost = (int)(b.UpgradeCost * scale);
        if (goldCost > 0 && inv.Gold < goldCost)
            return (false, $"need {goldCost} gold");

        foreach (var (item, baseNeed) in baseMats)
        {
            var need = Math.Max(1, (int)Math.Ceiling(baseNeed * scale));
            if (inv.GetCount(item) < need)
                return (false, $"need {need}x {item}");
        }

        if (goldCost > 0) inv.AddGold(-goldCost);
        foreach (var (item, baseNeed) in baseMats)
        {
            var need = Math.Max(1, (int)Math.Ceiling(baseNeed * scale));
            inv.RemoveItem(item, need);
        }

        var nextLevel = b.Level + 1;
        var nextCost = (int)(b.UpgradeCost * Math.Pow(1.8, nextLevel));
        _buildings[id] = b with { Level = nextLevel, UpgradeCost = nextCost > 0 ? nextCost : b.UpgradeCost };
        return (true, $"{b.Name} upgraded to level {nextLevel}");
    }

    public List<BuildingState> ToList() => _buildings.Values.ToList();

    public static bool TryGetUnlockWoodNeed(string id, out int wood)
    {
        wood = 0;
        if (!UnlockMaterials.TryGetValue(id, out var mats)) return false;
        if (!mats.TryGetValue(ItemIds.CropWood, out wood)) return false;
        return wood > 0;
    }

    public List<string> ExportUnlocked() =>
        _buildings.Where(kv => kv.Value.Unlocked).Select(kv => kv.Key).ToList();

    public void Restore(List<string> unlocked)
    {
        foreach (var key in _buildings.Keys.ToList())
        {
            var b = _buildings[key];
            var isUnlocked = unlocked.Contains(key) || (key is "factory" or "barn");
            _buildings[key] = b with { Unlocked = isUnlocked, Level = isUnlocked ? 1 : 0 };
        }
    }
}
