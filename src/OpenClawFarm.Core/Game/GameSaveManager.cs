using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public static class GameSaveManager
{
    public static FarmSaveData Export(GameWorld world)
    {
        var bag = world.Inventory.GetBag();
        return new FarmSaveData(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            world.Player.ToState().X,
            world.Player.ToState().Y,
            world.Player.ToState().Facing ?? "down",
            bag.Gold,
            bag.Items.ToDictionary(i => i.ItemId, i => i.Count),
            world.Lands.Values.Select(l => new LandSaveData(
                l.Id, l.ToState().State, l.ToState().CropId, l.ToState().Growth,
                l.ToState().NeedsWater, l.ToState().IsDry, l.ToState().HasPest,
                l.ToState().HasFrost, l.ToState().Fertility, l.ToState().IsGreenhouse,
                l.ToState().LastCropId)).ToList(),
            world.GetGameHour(),
            world.Seasons.GameDay,
            world.GetTickCount(),
            world.Progress.Export(),
            world.Achievements.Export(),
            world.Prestige.Export(),
            world.Buildings.ExportUnlocked(),
            world.Livestock.Export(),
            world.Processing.ExportUnlocked(),
            world.Mining.Export(),
            world.Fishing.Export(),
            world.Economy.Export(),
            world.Upkeep.Export(),
            world.Orders.Export(),
            world.Hybrid.Export(),
            world.Boss.Export(),
            world.Codex.Export(),
            world.Decorations.Export(),
            world.Forest.Export(),
            world.Construction.Export());
    }

    public static void Apply(GameWorld world, FarmSaveData save)
    {
        world.Player.SetPosition(save.PlayerX, save.PlayerY, facing: save.PlayerFacing);
        world.Inventory.LoadFrom(save.Gold, save.Items);
        foreach (var landSave in save.Lands)
        {
            if (world.Lands.TryGetValue(landSave.Id, out var land))
                land.Restore(landSave);
        }
        world.SetGameHour(save.GameHour);
        world.Seasons.Restore(save.GameDay);
        world.SetTickCount(save.TickCount);
        world.Progress.Restore(save.Progress);
        world.Achievements.Restore(save.Achievements);
        world.Prestige.Restore(save.Prestige);
        world.Buildings.Restore(save.UnlockedBuildings);
        world.Livestock.Restore(save.Animals);
        world.Processing.Restore(save.UnlockedRecipes);
        world.Mining.Restore(save.Mining);
        world.Fishing.Restore(save.Fishing);
        world.Economy.Restore(save.Economy);
        world.Upkeep.Restore(save.Upkeep);
        world.Orders.Restore(save.OrderHub);
        world.Hybrid.Restore(save.Hybrid);
        world.Boss.Restore(save.Boss);
        world.Codex.Restore(save.Codex);
        world.Decorations.Restore(save.Decorations);
        world.Forest.Restore(save.Forest);
        world.Construction.Restore(save.Construction);
        world.Progress.OnAnimalOwned(world.Livestock.Animals.Count);
    }
}
