using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public static class ActionExecutor
{
    public static Task<ActionResult> RunAsync(GameWorld world, string actionId, JsonParams p) =>
        actionId switch
        {
            "move_to" => world.MoveToAsync(new MoveToParams(p.X ?? 0, p.Y ?? 0, p.SceneId)),
            "interact" => world.InteractAsync(new InteractParams(p.TargetEntityId ?? "", p.ItemId)),
            "sell_item" => world.SellItemAsync(new SellItemParams(p.ItemId ?? "", p.Count, p.ConfirmToken)),
            "wait" => world.WaitAsync(new WaitParams(p.Ms ?? 0)),
            _ => Task.FromResult(new ActionResult(false, $"unknown action: {actionId}")),
        };
}
