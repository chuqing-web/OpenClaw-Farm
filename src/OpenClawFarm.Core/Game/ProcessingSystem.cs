using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class ProcessingSystem
{
    public const string FactoryId = "factory_01";
    public int X { get; } = ItemIds.TileToPixel(26, 13).X;
    public int Y { get; } = ItemIds.TileToPixel(26, 13).Y;

    private static readonly RecipeState[] AllRecipes =
    [
        new(ItemIds.CropJam, "草莓果酱", new() { [ItemIds.CropStrawberry] = 3 }, 45),
        new(ItemIds.CropFlour, "面粉", new() { [ItemIds.CropWheat] = 5 }, 28),
        new(ItemIds.CropCheese, "奶酪", new() { [ItemIds.CropMilk] = 2 }, 55),
        new(ItemIds.CropGiftBox, "果酱礼盒", new() { [ItemIds.CropJam] = 2, [ItemIds.CropPumpkin] = 1 }, 120),
        new(ItemIds.CropCake, "农场蛋糕", new() { [ItemIds.CropFlour] = 2, [ItemIds.CropEgg] = 2, [ItemIds.CropJam] = 1 }, 180),
        new(ItemIds.CropCloth, "羊毛布", new() { [ItemIds.CropWool] = 3 }, 90),
        new(ItemIds.CropCharcoal, "木炭", new() { [ItemIds.CropWheat] = 2, [ItemIds.CropCorn] = 1 }, 8),
        new(ItemIds.IngotIron, "铁锭", new() { [ItemIds.OreIron] = 4, [ItemIds.CropCharcoal] = 2 }, 35),
        new(ItemIds.IngotSilver, "银锭", new() { [ItemIds.OreSilver] = 3, [ItemIds.CropCharcoal] = 3 }, 70),
        new(ItemIds.FishDried, "鱼干", new() { [ItemIds.FishCommon] = 3, [ItemIds.CropCharcoal] = 1 }, 14),
        new(ItemIds.MealFishStew, "鲜鱼炖菜", new() { [ItemIds.FishMedium] = 2, [ItemIds.CropCarrot] = 1 }, 0),
        new(ItemIds.BaitBasic, "普通鱼饵", new() { [ItemIds.CropWheat] = 2 }, 0),
        new(ItemIds.BaitAdvanced, "高级鱼饵", new() { [ItemIds.CropCarrot] = 1, [ItemIds.FishCommon] = 1 }, 0),
        new(ItemIds.CropPlank, "木板", new() { [ItemIds.CropWood] = 3 }, 14),
        new(ItemIds.ToolAxe, "斧头", new() { [ItemIds.IngotIron] = 2, [ItemIds.CropWood] = 5 }, 0),
    ];

    private readonly HashSet<string> _unlocked = [
        ItemIds.CropJam, ItemIds.CropFlour, ItemIds.CropCheese,
        ItemIds.CropCharcoal, ItemIds.BaitBasic, ItemIds.FishDried,
        ItemIds.ToolAxe,
    ];

    public ProcessingState ToState() => new(
        FactoryId, X, Y,
        AllRecipes.ToList(),
        _unlocked.ToList());

    private static readonly Dictionary<string, int> OutputYield = new()
    {
        [ItemIds.CropFlour] = 3,   // 5 wheat → 3 flour (40% loss)
        [ItemIds.FishDried] = 2,   // 3 fish → 2 dried (~33% loss)
        [ItemIds.CropPlank] = 2,   // 3 wood → 2 plank (~33% loss)
        [ItemIds.IngotIron] = 1,
        [ItemIds.IngotSilver] = 1,
        [ItemIds.ToolAxe] = 1,
    };

    public (bool Ok, string Message, string? OutputId) Process(string outputId, Inventory inv)
    {
        if (!_unlocked.Contains(outputId))
            return (false, "recipe not unlocked", null);

        var recipe = AllRecipes.FirstOrDefault(r => r.OutputId == outputId);
        if (recipe == null)
            return (false, "unknown recipe", null);

        foreach (var (item, need) in recipe.Inputs)
        {
            if (inv.GetCount(item) < need)
                return (false, $"need {need}x {item}", null);
        }

        foreach (var (item, need) in recipe.Inputs)
            inv.RemoveItem(item, need);

        var yield = OutputYield.GetValueOrDefault(outputId, 1);
        inv.AddItem(outputId, yield);
        return (true, $"processed {yield}x {outputId}", outputId);
    }

    public void Unlock(string outputId) => _unlocked.Add(outputId);

    public List<string> ExportUnlocked() => _unlocked.ToList();

    public void Restore(List<string> unlocked)
    {
        _unlocked.Clear();
        foreach (var id in unlocked)
            _unlocked.Add(id);
        if (_unlocked.Count == 0)
        {
            _unlocked.Add(ItemIds.CropJam);
            _unlocked.Add(ItemIds.CropFlour);
            _unlocked.Add(ItemIds.CropCheese);
        }
    }
}
