using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public static class ActionExecutor
{
    public static Task<ActionResult> RunAsync(GameWorld world, string actionId, JsonParams p) =>
        actionId switch
        {
            "move_to" => world.MoveToAsync(new MoveToParams(p.X ?? 0, p.Y ?? 0, p.SceneId)),
            "interact" => world.InteractAsync(new InteractParams(p.TargetEntityId ?? "", p.ItemId)),
            "sell_item" => world.SellItemAsync(new SellItemParams(p.ItemId ?? "", p.Count, p.ConfirmToken, p.MerchantId)),
            "wait" => world.WaitAsync(new WaitParams(p.Ms ?? 0)),
            "claim_reward" => Task.FromResult(world.ClaimRewards(p.RewardId)),
            "process" => Task.FromResult(world.ProcessItem(p.ItemId ?? ItemIds.CropJam)),
            "feed_animal" => Task.FromResult(world.FeedAnimal(p.TargetEntityId ?? "", p.ItemId)),
            "collect_animal" => Task.FromResult(world.CollectAnimal(p.TargetEntityId ?? "")),
            "unlock_building" => Task.FromResult(world.UnlockBuilding(p.BuildingId ?? "")),
            "prestige_reset" => Task.FromResult(world.PrestigeReset()),
            "mine_enter" => Task.FromResult(world.MineEnter()),
            "mine_leave" => Task.FromResult(world.MineLeave()),
            "mine_dig" => Task.FromResult(world.MineDig()),
            "mine_layer" => Task.FromResult(world.MineChangeLayer(p.Direction)),
            "fish" => Task.FromResult(world.FishCast(p.PondId, p.ItemId)),
            "eat_meal" => Task.FromResult(world.EatMeal()),
            "forge_pickaxe" => Task.FromResult(world.ForgePickaxe(p.Layer ?? 1)),
            "refuel_lantern" => Task.FromResult(world.RefuelLantern()),
            "deliver_cross_order" => Task.FromResult(world.DeliverCrossOrder(p.OrderId)),
            "reinforce_mine" => Task.FromResult(world.ReinforceMine()),
            "repair_buildings" => Task.FromResult(world.RepairBuildings()),
            "feed_pond" => Task.FromResult(world.FeedPondEcology()),
            "upgrade_building" => Task.FromResult(world.UpgradeBuilding(p.BuildingId ?? "")),
            "hybrid_seed" => Task.FromResult(world.HybridSeeds(p.SeedA, p.SeedB)),
            "deliver_weekly_order" => Task.FromResult(world.DeliverWeeklyOrder(p.OrderId)),
            "deliver_festival" => Task.FromResult(world.DeliverFestivalOrder(p.OrderId)),
            "summon_boss" => Task.FromResult(world.SummonBoss()),
            "attack_boss" => Task.FromResult(world.AttackBoss()),
            "place_decoration" => Task.FromResult(world.PlaceDecoration(p.DecorationId)),
            "chop_tree" => world.ChopTreeAsync(p),
            "build_tile" => Task.FromResult(world.BuildTile(p)),
            _ => Task.FromResult(new ActionResult(false, $"unknown action: {actionId}")),
        };
}
