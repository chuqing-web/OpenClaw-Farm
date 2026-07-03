using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

/// <summary>根据当前状态建议 Agent 下一步动作。</summary>
public static class ActionAdvisor
{
    public record NextSuggestion(
        string Hint,
        string? ActionId = null,
        Dictionary<string, object?>? Params = null);

    public static NextSuggestion Suggest(GameWorld world)
    {
        var up = world.Upkeep.ToState();
        if (up.MineIntegrity < 40)
            return new("矿道耐久偏低，建议加固", "reinforce_mine", new());
        if (up.BuildingDurability < 50)
            return new("建筑需维修", "repair_buildings", new());
        if (up.PondEcology < 55)
            return new("鱼塘生态下降，建议投喂", "feed_pond", new());

        foreach (var order in world.Orders.GetState().CrossLineOrders.Where(o => !o.Completed))
        {
            if (order.Required.All(kv => world.Inventory.GetCount(kv.Key) >= kv.Value))
            {
                var act = order.Type switch
                {
                    "weekly" => "deliver_weekly_order",
                    "festival" => "deliver_festival",
                    _ => "deliver_cross_order",
                };
                return new($"跨线订单 [{order.Type}] 物资已齐，建议交付", act,
                    new Dictionary<string, object?> { ["orderId"] = order.Id });
            }
        }

        var woodNeed = MissingWoodForUnlockable(world);
        if (woodNeed > 0 && world.Inventory.GetCount(ItemIds.CropWood) < woodNeed)
        {
            var tree = world.Forest.NearestChoppable(world.Player.X, world.Player.Y);
            if (tree != null)
            {
                var tpx = tree.Tx * ItemIds.TileSize + ItemIds.TileSize / 2;
                var tpy = tree.Ty * ItemIds.TileSize + ItemIds.TileSize / 2;
                return new($"缺木材 ({world.Inventory.GetCount(ItemIds.CropWood)}/{woodNeed})，建议砍树",
                    "chop_tree", new Dictionary<string, object?> { ["targetEntityId"] = tree.Id, ["x"] = tpx, ["y"] = tpy });
            }
        }

        if (world.Inventory.GetCount(ItemIds.CropWood) >= 9 &&
            world.Processing.ToState().UnlockedRecipes.Contains(ItemIds.CropPlank) &&
            world.Inventory.GetCount(ItemIds.CropPlank) < 4)
            return new("木材充足，建议加工木板", "process",
                new Dictionary<string, object?> { ["itemId"] = ItemIds.CropPlank });

        if (world.Inventory.GetCount(ItemIds.ToolAxe) < 1 &&
            world.Inventory.GetCount(ItemIds.IngotIron) >= 2 &&
            world.Inventory.GetCount(ItemIds.CropWood) >= 5)
            return new("可锻造斧头", "process",
                new Dictionary<string, object?> { ["itemId"] = ItemIds.ToolAxe });

        var px = world.Player.X;
        var py = world.Player.Y;
        var lands = world.GetLands();

        var harvest = Nearest(lands.Where(l => l.CanHarvest), px, py);
        if (harvest != null)
            return LandInteract($"地块 {harvest.Id} 可收割", harvest, "harvest");

        var thirsty = Nearest(lands.Where(l => l.NeedsWater || l.IsDry || l.State == "needs_water"), px, py);
        if (thirsty != null)
            return LandInteract($"地块 {thirsty.Id} 需要浇水", thirsty, "water");

        var pest = Nearest(lands.Where(l => l.HasPest), px, py);
        if (pest != null)
            return LandInteract($"地块 {pest.Id} 有虫害", pest, "tool_pesticide");

        var seed = PickSeed(world);
        var empty = Nearest(lands.Where(l => l.State == "empty"), px, py);
        if (empty != null && seed != null)
            return LandInteract($"地块 {empty.Id} 可播种 ({seed})", empty, seed);

        if (world.Mining.InMine)
        {
            if (world.Mining.Stamina < 25 && world.Inventory.GetCount(ItemIds.MealFishStew) > 0)
                return new("体力不足，建议进食", "eat_meal", new());
            if (world.Boss.Active)
                return new("Boss 战中，建议攻击", "attack_boss", new());
            return new("在矿洞中，建议继续挖掘", "mine_dig", new());
        }

        if (world.Inventory.GetCount(ItemIds.CropStrawberry) > 5)
            return new("作物较多，建议移至作物商出售", "move_to",
                new Dictionary<string, object?> { ["x"] = ItemIds.MerchantPos.X, ["y"] = ItemIds.MerchantPos.Y });

        var treeIdle = world.Forest.NearestChoppable(px, py);
        if (treeIdle != null && world.Inventory.GetCount(ItemIds.CropWood) < 20)
            return new("可去西侧树林砍树储备木材", "move_to",
                new Dictionary<string, object?> { ["x"] = ItemIds.ForestEntrancePos.X, ["y"] = ItemIds.ForestEntrancePos.Y });

        return new("暂无紧急事项，可读 meta 或执行 runTripleLineCycle()");
    }

    private static int MissingWoodForUnlockable(GameWorld world)
    {
        var wood = 0;
        foreach (var b in world.Buildings.All.Values.Where(b => !b.Unlocked))
        {
            if (!BuildingSystem.TryGetUnlockWoodNeed(b.Id, out var need)) continue;
            if (world.Inventory.Gold >= b.UpgradeCost)
                wood = Math.Max(wood, need);
        }
        return wood;
    }

    private static string? PickSeed(GameWorld world)
    {
        foreach (var s in ItemIds.AllSeeds)
            if (world.Inventory.GetCount(s) > 0) return s;
        return null;
    }

    private static LandState? Nearest(IEnumerable<LandState> lands, double px, double py) =>
        lands.OrderBy(l => Dist(l.X, l.Y, px, py)).FirstOrDefault();

    private static double Dist(int x, int y, double px, double py)
    {
        var dx = x - px;
        var dy = y - py;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static NextSuggestion LandInteract(string hint, LandState land, string itemId) =>
        new(hint, "interact", new Dictionary<string, object?>
        {
            ["targetEntityId"] = land.Id,
            ["itemId"] = itemId,
        });
}
