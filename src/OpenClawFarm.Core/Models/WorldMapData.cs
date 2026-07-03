namespace OpenClawFarm.Core.Models;

public static class WorldMapData
{
    public const int FarmOriginX = 9;
    public const int FarmOriginY = 11;
    public const int FarmCols = 6;
    public const int FarmRows = 4;

    public static readonly int Width = ItemIds.MapWidth;
    public static readonly int Height = ItemIds.MapHeight;

    private static readonly string[,] Tiles = Generate();

    public static (int X, int Y) MerchantPixel => ItemIds.TileToPixel(30, 15);
    public static (int X, int Y) WellPixel => ItemIds.TileToPixel(34, 12);
    public static (int X, int Y) PlayerSpawnPixel => ItemIds.TileToPixel(7, 9);

    public static string[][] GetTilesFlat()
    {
        var rows = new string[Height][];
        for (var y = 0; y < Height; y++)
        {
            rows[y] = new string[Width];
            for (var x = 0; x < Width; x++)
                rows[y][x] = Tiles[x, y];
        }
        return rows;
    }

    public static bool IsWalkable(int tx, int ty)
    {
        if (tx < 0 || ty < 0 || tx >= Width || ty >= Height) return false;
        var t = Tiles[tx, ty];
        return t is "grass" or "path" or "bridge" or "soil";
    }

    public static bool IsWater(int tx, int ty)
    {
        if (tx < 0 || ty < 0 || tx >= Width || ty >= Height) return false;
        return Tiles[tx, ty] == "water";
    }

    private static string[,] Generate()
    {
        var m = new string[Width, Height];
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
            m[x, y] = "grass";

        // Farm soil patch
        for (var y = FarmOriginY; y < FarmOriginY + FarmRows; y++)
        for (var x = FarmOriginX; x < FarmOriginX + FarmCols; x++)
            m[x, y] = "soil";

        // Paths — house to farm to market
        CarvePath(m, 7, 9, 9, 11);
        CarvePath(m, 14, 13, 22, 15);
        CarvePath(m, 22, 15, 30, 15);
        CarvePath(m, 30, 15, 34, 12);

        // Main plaza
        for (var y = 14; y <= 16; y++)
        for (var x = 28; x <= 32; x++)
            if (m[x, y] == "grass") m[x, y] = "path";

        // Pond
        for (var y = 6; y <= 9; y++)
        for (var x = 33; x <= 37; x++)
            m[x, y] = "water";
        m[35, 10] = "bridge";
        m[36, 10] = "bridge";

        // House footprint
        for (var y = 4; y <= 6; y++)
        for (var x = 4; x <= 7; x++)
            m[x, y] = "house";

        // Fence around farm
        for (var x = FarmOriginX - 1; x <= FarmOriginX + FarmCols; x++)
        {
            m[x, FarmOriginY - 1] = "fence";
            m[x, FarmOriginY + FarmRows] = "fence";
        }
        for (var y = FarmOriginY - 1; y <= FarmOriginY + FarmRows; y++)
        {
            m[FarmOriginX - 1, y] = "fence";
            m[FarmOriginX + FarmCols, y] = "fence";
        }
        m[FarmOriginX - 1, FarmOriginY - 1] = "grass";
        m[FarmOriginX + FarmCols, FarmOriginY + 1] = "grass";

        // Trees & flowers (decorative walkable grass kept, trees block)
        int[] treeXs = [2, 3, 18, 19, 25, 26, 38, 39];
        int[] treeYs = [8, 20, 5, 22, 8, 24, 12, 18];
        for (var i = 0; i < treeXs.Length; i++)
            if (m[treeXs[i], treeYs[i]] == "grass") m[treeXs[i], treeYs[i]] = "tree";

        int[] flowerXs = [8, 16, 23, 27, 31];
        int[] flowerYs = [8, 17, 12, 18, 8];
        for (var i = 0; i < flowerXs.Length; i++)
            if (m[flowerXs[i], flowerYs[i]] == "grass") m[flowerXs[i], flowerYs[i]] = "flower";

        // Market stall tiles
        m[30, 14] = "path";
        m[31, 14] = "path";

        return m;
    }

    private static void CarvePath(string[,] m, int x0, int y0, int x1, int y1)
    {
        var x = x0;
        var y = y0;
        while (x != x1)
        {
            if (m[x, y] is "grass" or "flower") m[x, y] = "path";
            x += Math.Sign(x1 - x);
        }
        while (y != y1)
        {
            if (m[x, y] is "grass" or "flower") m[x, y] = "path";
            y += Math.Sign(y1 - y);
        }
        if (m[x, y] is "grass" or "flower") m[x, y] = "path";
    }
}
