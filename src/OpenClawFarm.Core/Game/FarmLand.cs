using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class FarmLand
{
    private const double GrowthPerTick = 0.15;
    private const int WitherTicksWithoutWater = 3;

    public string Id { get; }
    public int X { get; }
    public int Y { get; }
    public string State { get; private set; } = "empty";
    public string? CropId { get; private set; }
    public double Growth { get; private set; }
    public bool NeedsWater { get; private set; }
    private int _dryTicks;

    public bool CanHarvest => State == "mature";

    public FarmLand(string id, int tileX, int tileY)
    {
        Id = id;
        (X, Y) = ItemIds.TileToPixel(tileX, tileY);
    }

    public LandState ToState() => new(Id, X, Y, State, CropId,
        Math.Round(Growth, 2), NeedsWater, CanHarvest);

    public (bool Ok, string Message) Plant(string seedId)
    {
        if (State != "empty")
            return (false, $"{Id} is not empty");
        if (!ItemIds.SeedToCrop.TryGetValue(seedId, out var crop))
            return (false, $"unknown seed: {seedId}");
        State = "planted";
        CropId = crop;
        Growth = 0;
        NeedsWater = false;
        _dryTicks = 0;
        return (true, $"planted {seedId} on {Id}");
    }

    public (bool Ok, string Message) Water()
    {
        if (State is "empty" or "mature")
            return (false, $"{Id} does not need water");
        if (!NeedsWater && State != "withered")
            return (false, $"{Id} is not thirsty");
        NeedsWater = false;
        _dryTicks = 0;
        if (State == "withered") State = "growing";
        return (true, $"watered {Id}");
    }

    public (bool Ok, string Message, string? CropId) Harvest()
    {
        if (!CanHarvest || CropId == null)
            return (false, $"{Id} is not ready to harvest", null);
        var crop = CropId;
        Reset();
        return (true, $"harvested {crop} from {Id}", crop);
    }

    public (bool Ok, string Message) Clear()
    {
        if (State != "withered")
            return (false, $"{Id} is not withered");
        Reset();
        return (true, $"cleared {Id}");
    }

    public void Tick(Func<double>? rng = null)
    {
        rng ??= Random.Shared.NextDouble;
        if (State is "empty" or "mature") return;

        if (State == "planted") State = "growing";

        if (State is "growing" or "needs_water")
        {
            if (NeedsWater)
            {
                _dryTicks++;
                if (_dryTicks >= WitherTicksWithoutWater)
                {
                    State = "withered";
                    return;
                }
            }
            else
            {
                Growth = Math.Min(1, Growth + GrowthPerTick);
                if (Growth >= 1)
                {
                    State = "mature";
                    NeedsWater = false;
                }
                else if (rng() < 0.3)
                {
                    NeedsWater = true;
                    State = "needs_water";
                }
            }
        }
    }

    private void Reset()
    {
        State = "empty";
        CropId = null;
        Growth = 0;
        NeedsWater = false;
        _dryTicks = 0;
    }
}
