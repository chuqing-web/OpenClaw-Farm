using OpenClawFarm.Core.Game;
using OpenClawFarm.Core.Models;
using Xunit;

namespace OpenClawFarm.Core.Tests;

public class TripleLineTests
{
    [Fact]
    public void Mining_requires_food_to_enter()
    {
        var world = new GameWorld();
        world.BeginNewSession();
        world.Inventory.LoadFrom(100, new Dictionary<string, int>());
        var r = world.MineEnter();
        Assert.False(r.Success);
    }

    [Fact]
    public void Economy_price_drops_after_bulk_sell()
    {
        var eco = new EconomySystem();
        for (var i = 0; i < 120; i++) eco.RecordSale(ItemIds.CropWheat, 1);
        Assert.True(eco.GetPriceMultiplier(ItemIds.CropWheat) <= 0.4);
    }

    [Fact]
    public void Fishing_requires_bait()
    {
        var eco = new EconomySystem();
        var fish = new FishingSystem();
        var inv = new Inventory(0, [new BagItem(ItemIds.CropWheat, 5)]);
        var r = fish.Fish("p01", ItemIds.BaitBasic, eco, inv);
        Assert.False(r.Ok);
    }

    [Fact]
    public void Cross_line_charcoal_recipe_exists()
    {
        var proc = new ProcessingSystem();
        var state = proc.ToState();
        Assert.Contains(state.UnlockedRecipes, id => id == ItemIds.CropCharcoal);
    }
}
