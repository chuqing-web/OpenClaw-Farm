using OpenClawFarm.Core.Game;
using OpenClawFarm.Core.Models;
using Xunit;

namespace OpenClawFarm.Core.Tests;

public class MetaSystemsTests
{
    [Fact]
    public void Season_favors_in_season_crops()
    {
        var season = new SeasonSystem();
        var mult = season.GetCropMultiplier(ItemIds.SeedStrawberry);
        Assert.True(mult >= 0.5);
    }

    [Fact]
    public void Processing_makes_jam_from_strawberries()
    {
        var proc = new ProcessingSystem();
        var inv = new Inventory(0, [new BagItem(ItemIds.CropStrawberry, 5)]);
        var r = proc.Process(ItemIds.CropJam, inv);
        Assert.True(r.Ok);
        Assert.Equal(2, inv.GetCount(ItemIds.CropStrawberry));
        Assert.Equal(1, inv.GetCount(ItemIds.CropJam));
    }

    [Fact]
    public void Progress_tracks_harvests()
    {
        var p = new ProgressTracker();
        p.OnHarvest(ItemIds.CropWheat);
        p.OnHarvest(ItemIds.CropWheat);
        Assert.Equal(2, p.TotalHarvests);
    }
}
