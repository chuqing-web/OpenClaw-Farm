using OpenClawFarm.Core.Game;
using OpenClawFarm.Core.Models;
using Xunit;

namespace OpenClawFarm.Core.Tests;

public class SaveGameTests
{
    [Fact]
    public void Save_roundtrip_preserves_gold_and_position()
    {
        var world = new GameWorld();
        world.BeginNewSession();
        world.Inventory.AddGold(500);
        world.Player.SetPosition(400, 300, facing: "left");

        var save = GameSaveManager.Export(world);

        var world2 = new GameWorld();
        world2.BeginLoadedSession(save);

        Assert.Equal(620, world2.Inventory.GetBag().Gold);
        Assert.Equal(400, world2.Player.ToState().X);
        Assert.Equal("left", world2.Player.ToState().Facing);
    }
}
