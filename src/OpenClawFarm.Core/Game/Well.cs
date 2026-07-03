namespace OpenClawFarm.Core.Game;

public sealed class Well
{
    public string Id { get; }
    public int X { get; }
    public int Y { get; }

    public Well(string id, int x, int y)
    {
        Id = id;
        X = x;
        Y = y;
    }

    public Models.ActionResult Interact() =>
        new(true, "从水井打满了水，洒水壶已就绪！");
}
