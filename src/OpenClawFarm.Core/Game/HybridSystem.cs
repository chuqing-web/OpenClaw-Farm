using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class HybridSystem
{
    private static readonly Dictionary<string, (string A, string B, string Crop)> Recipes = new()
    {
        [ItemIds.SeedHybridStar] = (ItemIds.SeedStrawberry, ItemIds.SeedPumpkin, ItemIds.CropHybridStar),
        [ItemIds.SeedHybridGold] = (ItemIds.SeedCorn, ItemIds.SeedWheat, ItemIds.CropHybridGold),
    };

    private readonly HashSet<string> _unlocked = [];

    public (bool Ok, string Message, string? SeedId) Hybrid(string seedA, string seedB, Inventory inv)
    {
        foreach (var (outSeed, (a, b, _)) in Recipes)
        {
            if ((seedA == a && seedB == b) || (seedA == b && seedB == a))
            {
                if (inv.GetCount(a) < 2 || inv.GetCount(b) < 2)
                    return (false, $"need 2x {a} and 2x {b}", null);
                inv.RemoveItem(a, 2);
                inv.RemoveItem(b, 2);
                inv.AddItem(outSeed, 1);
                _unlocked.Add(outSeed);
                return (true, $"hybridized {outSeed}", outSeed);
            }
        }
        return (false, "unknown seed pair", null);
    }

    public HybridState ToState() => new(
        Recipes.Keys.Select(k => new HybridRecipe(k, Recipes[k].A, Recipes[k].B, _unlocked.Contains(k))).ToList());

    public HybridSaveData Export() => new(_unlocked.ToList());

    public void Restore(HybridSaveData? data)
    {
        _unlocked.Clear();
        if (data == null) return;
        foreach (var id in data.Unlocked) _unlocked.Add(id);
    }
}
