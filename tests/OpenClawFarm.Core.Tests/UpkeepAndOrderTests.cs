using OpenClawFarm.Core.Game;
using OpenClawFarm.Core.Models;
using Xunit;

namespace OpenClawFarm.Core.Tests;

public class UpkeepAndOrderTests
{
    [Fact]
    public void Upkeep_daily_feeds_livestock_or_starves()
    {
        var upkeep = new UpkeepSystem();
        var livestock = new LivestockManager();
        var inv = new Inventory(0,
        [
            new BagItem(ItemIds.CropWheat, 10),
            new BagItem(ItemIds.CropCorn, 5),
            new BagItem(ItemIds.FishCommon, 5),
            new BagItem(ItemIds.IngotIron, 5),
        ]);
        var buildings = new BuildingSystem();
        var mining = new MiningSystem();
        var lands = new List<FarmLand> { new("land_01", 10, 10) };

        upkeep.RunDaily(inv, buildings, livestock, mining, lands, 1);

        Assert.True(livestock.Animals.All(a => a.Hunger >= 50));
        Assert.True(inv.GetCount(ItemIds.CropWheat) < 10);
    }

    [Fact]
    public void Cross_line_order_requires_all_three_lines()
    {
        var hub = new OrderHub();
        var inv = new Inventory(0,
        [
            new BagItem(ItemIds.CropStrawberry, 20),
            new BagItem(ItemIds.IngotIron, 5),
            new BagItem(ItemIds.FishMedium, 8),
        ]);

        var r = hub.DeliverCrossLine(null, inv);
        Assert.True(r.Ok);
        Assert.Equal(1200, r.Gold);
        Assert.Equal(0, inv.GetCount(ItemIds.CropStrawberry));
    }

    [Fact]
    public void Cross_line_order_fails_without_mixed_resources()
    {
        var hub = new OrderHub();
        var inv = new Inventory(0, [new BagItem(ItemIds.CropStrawberry, 100)]);

        var r = hub.DeliverCrossLine(null, inv);
        Assert.False(r.Ok);
    }

    [Fact]
    public void Processing_flour_has_synthesis_loss()
    {
        var proc = new ProcessingSystem();
        var inv = new Inventory(0, [new BagItem(ItemIds.CropWheat, 5)]);
        var r = proc.Process(ItemIds.CropFlour, inv);
        Assert.True(r.Ok);
        Assert.Equal(0, inv.GetCount(ItemIds.CropWheat));
        Assert.Equal(3, inv.GetCount(ItemIds.CropFlour));
    }

    [Fact]
    public void Building_unlock_consumes_materials()
    {
        var buildings = new BuildingSystem();
        var inv = new Inventory(5000,
        [
            new BagItem(ItemIds.CropWheat, 40),
            new BagItem(ItemIds.CropCorn, 20),
            new BagItem(ItemIds.CropWood, 20),
            new BagItem(ItemIds.IngotIron, 3),
        ]);

        var r = buildings.TryUnlock("greenhouse", inv);
        Assert.True(r.Ok);
        Assert.Equal(3000, inv.Gold);
        Assert.Equal(0, inv.GetCount(ItemIds.CropWheat));
    }

    [Fact]
    public void Upkeep_save_roundtrip()
    {
        var world = new GameWorld();
        world.Upkeep.Restore(new UpkeepSaveData(42, 55, 66, 3));
        var save = GameSaveManager.Export(world);
        var world2 = new GameWorld();
        GameSaveManager.Apply(world2, save);
        Assert.Equal(42, world2.Upkeep.MineIntegrity);
        Assert.Equal(55, world2.Upkeep.BuildingDurability);
    }
}
