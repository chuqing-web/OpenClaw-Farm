using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class ForestSystem
{
    public const int RegrowDays = 5;
    public const int TreeMaxHp = 3;

    public int X { get; } = ItemIds.TileToPixel(2, 12).X;
    public int Y { get; } = ItemIds.TileToPixel(2, 12).Y;

    private readonly Dictionary<string, TreeRecord> _trees = new();
    private int _choppedToday;

    private record TreeRecord(string Id, int Tx, int Ty, int Hp, int? RegrowAtDay);

    public ForestSystem()
    {
        foreach (var (tx, ty) in WorldMapData.GetTreePositions())
            _trees[Key(tx, ty)] = new TreeRecord(TreeId(tx, ty), tx, ty, TreeMaxHp, null);
    }

    public static string TreeId(int tx, int ty) => $"tree_{tx}_{ty}";

    private static string Key(int tx, int ty) => $"{tx},{ty}";

    public bool IsChopped(int tx, int ty) =>
        _trees.TryGetValue(Key(tx, ty), out var t) && t.Hp <= 0;

    public bool IsActiveTree(int tx, int ty) =>
        _trees.TryGetValue(Key(tx, ty), out var t) && t.Hp > 0;

    public TreeState? FindById(string id)
    {
        var t = _trees.Values.FirstOrDefault(t => t.Id == id);
        return t == null ? null : ToTreeState(t);
    }

    public TreeState? NearestChoppable(double px, double py) =>
        _trees.Values
            .Where(t => t.Hp > 0)
            .OrderBy(t => Dist(t.Tx, t.Ty, px, py))
            .Select(ToTreeState)
            .FirstOrDefault();

    private static TreeState ToTreeState(TreeRecord t) =>
        new(t.Id, t.Tx, t.Ty, t.Hp, t.Hp > 0, null);

    public (bool Ok, string Message, int Wood, List<TileUpdate> Updates) TryChop(
        int tx, int ty, Player player, Inventory inv, bool lumberCampBonus, int gameDay)
    {
        if (!_trees.TryGetValue(Key(tx, ty), out var tree))
            return (false, "no tree here", 0, []);
        if (tree.Hp <= 0)
            return (false, "tree already chopped (stump)", 0, []);

        var cx = tree.Tx * ItemIds.TileSize + ItemIds.TileSize / 2;
        var cy = tree.Ty * ItemIds.TileSize + ItemIds.TileSize / 2;
        if (player.DistanceTo(cx, cy) > ItemIds.TileSize * 2.5)
            return (false, "too far from tree", 0, []);

        if (inv.GetCount(ItemIds.ToolAxe) < 1)
            return (false, "need tool_axe (craft at factory: iron + wood)", 0, []);

        var newHp = tree.Hp - 1;
        var updates = new List<TileUpdate>();

        if (newHp <= 0)
        {
            _trees[Key(tx, ty)] = tree with { Hp = 0, RegrowAtDay = gameDay + RegrowDays };
            updates.Add(new(tree.Tx, tree.Ty, "stump"));
            var wood = Random.Shared.Next(2, 5);
            if (lumberCampBonus)
                wood = Math.Max(wood, (int)Math.Ceiling(wood * 1.25));
            inv.AddItem(ItemIds.CropWood, wood);
            _choppedToday++;
            return (true, $"chopped tree +{wood} {ItemIds.CropWood}", wood, updates);
        }

        _trees[Key(tx, ty)] = tree with { Hp = newHp };
        return (true, $"chopped tree ({newHp}/{TreeMaxHp} hp left)", 0, updates);
    }

    public (bool Ok, string Message, int Wood, List<TileUpdate> Updates) TryChopById(
        string treeId, Player player, Inventory inv, bool lumberCampBonus, int gameDay)
    {
        var tree = FindById(treeId);
        if (tree == null)
            return (false, $"unknown tree: {treeId}", 0, []);
        return TryChop(tree.Tx, tree.Ty, player, inv, lumberCampBonus, gameDay);
    }

    public List<TileUpdate> OnNewDay(int gameDay)
    {
        var updates = new List<TileUpdate>();
        foreach (var key in _trees.Keys.ToList())
        {
            var t = _trees[key];
            if (t.Hp > 0 || t.RegrowAtDay == null || t.RegrowAtDay > gameDay)
                continue;
            _trees[key] = t with { Hp = TreeMaxHp, RegrowAtDay = null };
            updates.Add(new(t.Tx, t.Ty, "tree"));
        }
        _choppedToday = 0;
        return updates;
    }

    public void ApplyToTiles(string[][] tiles)
    {
        foreach (var t in _trees.Values)
        {
            if (t.Hp <= 0)
                tiles[t.Ty][t.Tx] = "stump";
        }
    }

    public ForestState ToState(int gameDay, double playerX, double playerY)
    {
        var active = _trees.Values.Count(t => t.Hp > 0);
        var nearby = _trees.Values
            .Where(t => Dist(t.Tx, t.Ty, playerX, playerY) < ItemIds.TileSize * 8)
            .Select(t => new TreeState(
                t.Id, t.Tx, t.Ty, t.Hp, t.Hp > 0,
                t.RegrowAtDay != null && t.Hp <= 0 ? Math.Max(0, t.RegrowAtDay.Value - gameDay) : null))
            .OrderBy(t => Dist(t.Tx, t.Ty, playerX, playerY))
            .Take(12)
            .ToList();
        return new ForestState(_trees.Count, active, _choppedToday, X, Y, nearby);
    }

    public ForestSaveData Export() => new(
        _trees.Values.Select(t => new TreeSaveEntry(t.Id, t.Tx, t.Ty, t.Hp, t.RegrowAtDay)).ToList(),
        _choppedToday);

    public void Restore(ForestSaveData? save)
    {
        _trees.Clear();
        if (save?.Trees is { Count: > 0 })
        {
            foreach (var e in save.Trees)
                _trees[Key(e.Tx, e.Ty)] = new TreeRecord(e.Id, e.Tx, e.Ty, e.Hp, e.RegrowAtDay);
            _choppedToday = save.ChoppedToday;
            return;
        }

        foreach (var (tx, ty) in WorldMapData.GetTreePositions())
            _trees[Key(tx, ty)] = new TreeRecord(TreeId(tx, ty), tx, ty, TreeMaxHp, null);
        _choppedToday = 0;
    }

    private static double Dist(int tx, int ty, double px, double py)
    {
        var cx = tx * ItemIds.TileSize + ItemIds.TileSize / 2.0;
        var cy = ty * ItemIds.TileSize + ItemIds.TileSize / 2.0;
        var dx = cx - px;
        var dy = cy - py;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
