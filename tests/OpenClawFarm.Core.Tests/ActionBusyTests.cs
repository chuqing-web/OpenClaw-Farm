using OpenClawFarm.Core.Game;
using OpenClawFarm.Core.Models;
using Xunit;

namespace OpenClawFarm.Core.Tests;

public class ActionBusyTests
{
    [Fact]
    public async Task Concurrent_action_returns_busy()
    {
        var world = new GameWorld();
        world.BeginNewSession();

        var slow = world.ExecuteActionAsync("wait", new JsonParams { Ms = 2000 });
        await Task.Delay(100);
        var busy = await world.ExecuteActionAsync("move_to", new JsonParams { X = 100, Y = 100 });

        Assert.False(busy.Success);
        Assert.Contains("还未结束", busy.Message);
        Assert.True(busy.Extra?["busy"] as bool?);

        await slow;
        var ok = await world.ExecuteActionAsync("wait", new JsonParams { Ms = 10 });
        Assert.True(ok.Success);
        Assert.Contains("下一步", ok.Message);
        Assert.NotNull(ok.Extra?["nextHint"]);
    }

    [Fact]
    public void Action_state_reflects_hint_after_success()
    {
        var world = new GameWorld();
        world.BeginNewSession();
        var r = world.ExecuteActionAsync("wait", new JsonParams { Ms = 1 }).GetAwaiter().GetResult();
        Assert.True(r.Success);
        var state = world.GetActionState();
        Assert.False(state.Busy);
        Assert.NotNull(state.NextHint);
    }
}
