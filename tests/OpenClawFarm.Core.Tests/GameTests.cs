using OpenClawFarm.Core.Game;
using OpenClawFarm.Core.Models;
using Xunit;

namespace OpenClawFarm.Core.Tests;

public class FarmLandTests
{
    [Fact]
    public void Plants_seed_on_empty_land()
    {
        var land = new FarmLand("land_01", 2, 3);
        var r = land.Plant(ItemIds.SeedStrawberry);
        Assert.True(r.Ok);
        Assert.Equal("planted", land.State);
        Assert.Equal(ItemIds.CropStrawberry, land.CropId);
    }

    [Fact]
    public void Grows_to_mature_after_ticks()
    {
        var land = new FarmLand("land_01", 2, 3);
        land.Plant(ItemIds.SeedWheat);
        for (var i = 0; i < 20; i++) land.Tick(() => 1);
        Assert.True(land.CanHarvest);
        Assert.Equal("mature", land.State);
    }

    [Fact]
    public void Harvest_resets_land()
    {
        var land = new FarmLand("land_01", 2, 3);
        land.Plant(ItemIds.SeedCarrot);
        for (var i = 0; i < 20; i++) land.Tick(() => 1);
        var r = land.Harvest();
        Assert.True(r.Ok);
        Assert.Equal(ItemIds.CropCarrot, r.CropId);
        Assert.Equal("empty", land.State);
    }
}

public class GameWorldTests
{
    [Fact]
    public void Has_24_lands()
    {
        var world = new GameWorld();
        Assert.Equal(24, world.GetLands().Count);
    }

    [Fact]
    public async Task Sell_requires_confirm_token()
    {
        var world = new GameWorld();
        world.Inventory.AddItem(ItemIds.CropWheat, 2);
        await world.MoveToAsync(new MoveToParams(ItemIds.MerchantPos.X, ItemIds.MerchantPos.Y));
        var fail = await world.SellItemAsync(new SellItemParams(ItemIds.CropWheat, 1));
        Assert.False(fail.Success);
        Assert.True(fail.Extra?.ContainsKey("needConfirm"));
    }
}
