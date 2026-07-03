using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class ConstructionSystem
{
    public const int MaxBuiltTiles = 48;

    private static readonly Dictionary<string, (int Wood, int Plank, string TileType)> Recipes = new()
    {
        ["wood_fence"] = (5, 0, "wood_fence"),
        ["wood_path"] = (3, 0, "wood_path"),
        ["lumber_platform"] = (0, 4, "lumber_platform"),
    };

    private readonly Dictionary<string, string> _built = new();

    public bool Blocks(int tx, int ty) =>
        _built.TryGetValue(Key(tx, ty), out var t) && t is "wood_fence";

    public (bool Ok, string Message, TileUpdate? Update) TryBuild(
        string buildType, int tx, int ty, Player player, Inventory inv)
    {
        if (!Recipes.TryGetValue(buildType, out var recipe))
            return (false, $"unknown build type: {buildType}", null);

        if (_built.Count >= MaxBuiltTiles)
            return (false, "construction limit reached", null);

        if (tx < 0 || ty < 0 || tx >= ItemIds.MapWidth || ty >= ItemIds.MapHeight)
            return (false, "out of map bounds", null);

        if (_built.ContainsKey(Key(tx, ty)))
            return (false, "tile already built", null);

        var baseTile = WorldMapData.GetBaseTile(tx, ty);
        if (baseTile is not "grass" and not "flower")
            return (false, $"cannot build on {baseTile}", null);

        if (baseTile == "tree" && WorldMapData.IsTreeChopped?.Invoke(tx, ty) != true)
            return (false, "cannot build on tree", null);

        var cx = tx * ItemIds.TileSize + ItemIds.TileSize / 2;
        var cy = ty * ItemIds.TileSize + ItemIds.TileSize / 2;
        if (player.DistanceTo(cx, cy) > ItemIds.TileSize * 2.5)
            return (false, "too far from build site", null);

        if (recipe.Wood > 0 && inv.GetCount(ItemIds.CropWood) < recipe.Wood)
            return (false, $"need {recipe.Wood}x {ItemIds.CropWood}", null);
        if (recipe.Plank > 0 && inv.GetCount(ItemIds.CropPlank) < recipe.Plank)
            return (false, $"need {recipe.Plank}x {ItemIds.CropPlank}", null);

        if (recipe.Wood > 0) inv.RemoveItem(ItemIds.CropWood, recipe.Wood);
        if (recipe.Plank > 0) inv.RemoveItem(ItemIds.CropPlank, recipe.Plank);

        _built[Key(tx, ty)] = recipe.TileType;
        return (true, $"built {buildType} at ({tx},{ty})", new TileUpdate(tx, ty, recipe.TileType));
    }

    public void ApplyToTiles(string[][] tiles)
    {
        foreach (var (key, type) in _built)
        {
            var parts = key.Split(',');
            var tx = int.Parse(parts[0]);
            var ty = int.Parse(parts[1]);
            tiles[ty][tx] = type;
        }
    }

    public ConstructionState ToState() => new(
        _built.Count,
        Recipes.Keys.Select(k => new BuildRecipeState(
            k,
            Recipes[k].Wood,
            Recipes[k].Plank,
            Recipes[k].TileType)).ToList(),
        _built.Select(kv =>
        {
            var p = kv.Key.Split(',');
            return new BuiltTileState(int.Parse(p[0]), int.Parse(p[1]), kv.Value);
        }).ToList());

    public ConstructionSaveData Export() =>
        new(_built.Select(kv =>
        {
            var p = kv.Key.Split(',');
            return new BuiltTileSave(int.Parse(p[0]), int.Parse(p[1]), kv.Value);
        }).ToList());

    public void Restore(ConstructionSaveData? save)
    {
        _built.Clear();
        if (save?.Tiles == null) return;
        foreach (var t in save.Tiles)
            _built[Key(t.Tx, t.Ty)] = t.Type;
    }

    private static string Key(int tx, int ty) => $"{tx},{ty}";
}
