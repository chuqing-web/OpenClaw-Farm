using OpenClawFarm.Core.Game;
using OpenClawFarm.Core.Models;
using Xunit;

namespace OpenClawFarm.Core.Tests;

public class PhaseSystemsTests
{
    [Fact]
    public void Merchant_ore_accepts_ingots_not_crops()
    {
        var ms = new MerchantSystem(12345);
        Assert.True(ms.Ore.Accepts(ItemIds.IngotIron));
        Assert.False(ms.Ore.Accepts(ItemIds.CropWheat));
        Assert.True(ms.Fish.Accepts(ItemIds.FishCommon));
    }

    [Fact]
    public void Hybrid_creates_star_seed()
    {
        var h = new HybridSystem();
        var inv = new Inventory(0,
        [
            new BagItem(ItemIds.SeedStrawberry, 5),
            new BagItem(ItemIds.SeedPumpkin, 5),
        ]);
        var r = h.Hybrid(ItemIds.SeedStrawberry, ItemIds.SeedPumpkin, inv);
        Assert.True(r.Ok);
        Assert.Equal(1, inv.GetCount(ItemIds.SeedHybridStar));
    }

    [Fact]
    public void Weekly_order_delivers()
    {
        var hub = new OrderHub();
        var inv = new Inventory(0,
        [
            new BagItem(ItemIds.CropStrawberry, 200),
            new BagItem(ItemIds.IngotIron, 80),
            new BagItem(ItemIds.FishMedium, 80),
        ]);
        var r = hub.DeliverWeekly(null, inv);
        Assert.True(r.Ok, r.Message);
        Assert.True(r.Gold > 0);
    }

    [Fact]
    public void Boss_summon_and_defeat()
    {
        var boss = new MineBossSystem();
        var mining = new MiningSystem();
        mining.Enter(new Inventory(0, [new BagItem(ItemIds.CropWheat, 1)]));
        mining.ChangeLayer(1); mining.ChangeLayer(1);
        var inv = new Inventory(0,
        [
            new BagItem(ItemIds.IngotIron, 10),
            new BagItem(ItemIds.OreCrystal, 5),
        ]);
        Assert.True(boss.Summon(mining, inv).Ok);
        while (boss.Active)
        {
            mining.RestoreStamina(50);
            var atk = boss.Attack(mining, inv);
            if (!atk.Ok && atk.Message.Contains("stamina")) mining.RestoreStamina(100);
        }
        Assert.Equal(1, boss.DefeatedCount);
    }

    [Fact]
    public void Decoration_permanently_consumes_items()
    {
        var d = new DecorationSystem();
        var inv = new Inventory(0,
        [
            new BagItem(ItemIds.CropStrawberry, 30),
            new BagItem(ItemIds.CropHybridStar, 5),
        ]);
        var r = d.Place(ItemIds.DecorFlowerBed, inv);
        Assert.True(r.Ok);
        Assert.Equal(0, inv.GetCount(ItemIds.CropStrawberry));
    }
}
