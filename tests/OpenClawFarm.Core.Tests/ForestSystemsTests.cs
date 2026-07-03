using OpenClawFarm.Core.Game;
using OpenClawFarm.Core.Models;
using Xunit;

namespace OpenClawFarm.Core.Tests;

public class ForestSystemsTests
{
    [Fact]
    public void Chop_tree_yields_wood_when_felled()
    {
        var world = new GameWorld();
        world.BeginNewSession();
        world.Player.SetPosition(ItemIds.ForestEntrancePos.X, ItemIds.ForestEntrancePos.Y);

        var tree = world.Forest.NearestChoppable(world.Player.X, world.Player.Y);
        Assert.NotNull(tree);

        for (var i = 0; i < ForestSystem.TreeMaxHp; i++)
        {
            var r = world.Forest.TryChop(tree!.Tx, tree.Ty, world.Player, world.Inventory, false, 1);
            Assert.True(r.Ok);
        }

        Assert.True(world.Inventory.GetCount(ItemIds.CropWood) >= 2);
        Assert.True(world.Forest.IsChopped(tree.Tx, tree.Ty));
        Assert.True(WorldMapData.IsWalkable(tree.Tx, tree.Ty));
    }

    [Fact]
    public void Build_tile_consumes_wood()
    {
        var world = new GameWorld();
        world.BeginNewSession();
        world.Inventory.AddItem(ItemIds.CropWood, 10);
        var (bx, by) = ItemIds.TileToPixel(4, 10);
        world.Player.SetPosition(bx, by);

        var r = world.Construction.TryBuild("wood_path", 4, 10, world.Player, world.Inventory);
        Assert.True(r.Ok, r.Message);
        Assert.Equal(7, world.Inventory.GetCount(ItemIds.CropWood));
    }

    [Fact]
    public async Task Chop_tree_action_via_executor()
    {
        var world = new GameWorld();
        world.BeginNewSession();
        await world.ExecuteActionAsync("move_to", new JsonParams
        {
            X = ItemIds.ForestEntrancePos.X,
            Y = ItemIds.ForestEntrancePos.Y,
        });

        var tree = world.Forest.NearestChoppable(world.Player.X, world.Player.Y);
        Assert.NotNull(tree);

        for (var i = 0; i < ForestSystem.TreeMaxHp; i++)
        {
            var r = await world.ExecuteActionAsync("chop_tree", new JsonParams { TargetEntityId = tree!.Id });
            Assert.True(r.Success, r.Message);
        }

        Assert.True(world.Inventory.GetCount(ItemIds.CropWood) > 0);
    }

    [Fact]
    public void Forest_regrows_after_game_days()
    {
        var forest = new ForestSystem();
        var player = Player.CreateDefault();
        var inv = new Inventory(0, [new BagItem(ItemIds.ToolAxe, 1)]);

        var tree = forest.NearestChoppable(player.X, player.Y);
        Assert.NotNull(tree);
        for (var i = 0; i < ForestSystem.TreeMaxHp; i++)
            forest.TryChop(tree!.Tx, tree.Ty, player, inv, false, 1);

        Assert.True(forest.IsChopped(tree!.Tx, tree.Ty));

        var regrow = forest.OnNewDay(1 + ForestSystem.RegrowDays);
        Assert.Contains(regrow, u => u.Tx == tree.Tx && u.Ty == tree.Ty && u.Type == "tree");
        Assert.False(forest.IsChopped(tree.Tx, tree.Ty));
    }

    [Fact]
    public void Processing_plank_from_wood()
    {
        var proc = new ProcessingSystem();
        proc.Unlock(ItemIds.CropPlank);
        var inv = new Inventory(0, [new BagItem(ItemIds.CropWood, 6)]);
        var r = proc.Process(ItemIds.CropPlank, inv);
        Assert.True(r.Ok);
        Assert.Equal(2, inv.GetCount(ItemIds.CropPlank));
        Assert.Equal(3, inv.GetCount(ItemIds.CropWood));
    }
}
