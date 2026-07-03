using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class Player
{
    public double X { get; private set; }
    public double Y { get; private set; }
    public string SceneId { get; private set; }
    public string Facing { get; private set; } = "down";

    public Player(double x, double y, string sceneId = "farm_main")
    {
        X = x;
        Y = y;
        SceneId = sceneId;
    }

    public static Player CreateDefault()
    {
        var (px, py) = ItemIds.PlayerSpawn;
        return new Player(px, py);
    }

    public PlayerState ToState() => new((int)Math.Round(X), (int)Math.Round(Y), SceneId, Facing);

    public void SetPosition(double x, double y, string? sceneId = null, string? facing = null)
    {
        if (facing != null) Facing = facing;
        else if (Math.Abs(x - X) > Math.Abs(y - Y))
            Facing = x > X ? "right" : "left";
        else if (Math.Abs(y - Y) > 0.1)
            Facing = y > Y ? "down" : "up";
        X = x;
        Y = y;
        if (sceneId != null) SceneId = sceneId;
    }

    public double DistanceTo(double x, double y)
    {
        var dx = X - x;
        var dy = Y - y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private int TileX => (int)Math.Floor(X / ItemIds.TileSize);
    private int TileY => (int)Math.Floor(Y / ItemIds.TileSize);

    public List<(int X, int Y)>? FindPath(int targetX, int targetY, HashSet<string>? blocked = null)
    {
        blocked ??= [];
        var endTx = targetX / ItemIds.TileSize;
        var endTy = targetY / ItemIds.TileSize;

        if (!IsWalkable(endTx, endTy, blocked))
        {
            var near = NearestWalkable(endTx, endTy, blocked);
            if (near == null) return null;
            return FindPath(near.Value.X * ItemIds.TileSize + ItemIds.TileSize / 2,
                near.Value.Y * ItemIds.TileSize + ItemIds.TileSize / 2, blocked);
        }

        var startTx = TileX;
        var startTy = TileY;
        string Key(int tx, int ty) => $"{tx},{ty}";

        var open = new List<(int Tx, int Ty, int G, int F)>();
        var cameFrom = new Dictionary<string, string>();
        var gScore = new Dictionary<string, int>();

        var startKey = Key(startTx, startTy);
        var endKey = Key(endTx, endTy);
        gScore[startKey] = 0;
        open.Add((startTx, startTy, 0, Heuristic(startTx, startTy, endTx, endTy)));

        int[][] dirs = [[0, 1], [0, -1], [1, 0], [-1, 0]];

        while (open.Count > 0)
        {
            open.Sort((a, b) => a.F.CompareTo(b.F));
            var current = open[0];
            open.RemoveAt(0);
            var cKey = Key(current.Tx, current.Ty);
            if (cKey == endKey)
            {
                return ReconstructPath(cameFrom, cKey, startTx, startTy)
                    .Select(p => (p.Tx * ItemIds.TileSize + ItemIds.TileSize / 2,
                        p.Ty * ItemIds.TileSize + ItemIds.TileSize / 2))
                    .ToList();
            }

            foreach (var d in dirs)
            {
                var ntx = current.Tx + d[0];
                var nty = current.Ty + d[1];
                if (!IsWalkable(ntx, nty, blocked)) continue;
                var nKey = Key(ntx, nty);
                var tentative = gScore.GetValueOrDefault(cKey, int.MaxValue) + 1;
                if (tentative < gScore.GetValueOrDefault(nKey, int.MaxValue))
                {
                    cameFrom[nKey] = cKey;
                    gScore[nKey] = tentative;
                    var f = tentative + Heuristic(ntx, nty, endTx, endTy);
                    if (!open.Any(n => n.Tx == ntx && n.Ty == nty))
                        open.Add((ntx, nty, tentative, f));
                }
            }
        }
        return null;
    }

    private static int Heuristic(int tx, int ty, int ex, int ey) =>
        Math.Abs(tx - ex) + Math.Abs(ty - ey);

    private static List<(int Tx, int Ty)> ReconstructPath(
        Dictionary<string, string> cameFrom, string endKey, int startTx, int startTy)
    {
        var path = new List<(int, int)>();
        string? current = endKey;
        while (current != null)
        {
            var parts = current.Split(',');
            path.Insert(0, (int.Parse(parts[0]), int.Parse(parts[1])));
            cameFrom.TryGetValue(current, out current);
        }
        if (path.Count == 0 || path[0].Item1 != startTx || path[0].Item2 != startTy)
            path.Insert(0, (startTx, startTy));
        return path;
    }

    private static bool IsWalkable(int tileX, int tileY, HashSet<string> blocked)
    {
        if (!WorldMapData.IsWalkable(tileX, tileY)) return false;
        return !blocked.Contains($"{tileX},{tileY}");
    }

    private static (int X, int Y)? NearestWalkable(int tx, int ty, HashSet<string> blocked)
    {
        for (var r = 1; r <= 5; r++)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    if (IsWalkable(tx + dx, ty + dy, blocked))
                        return (tx + dx, ty + dy);
                }
            }
        }
        return null;
    }
}
