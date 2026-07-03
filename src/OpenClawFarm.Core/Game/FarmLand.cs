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
    public bool IsDry { get; private set; }
    public bool HasPest { get; private set; }
    public bool HasFrost { get; private set; }
    public int Fertility { get; private set; } = 100;
    public bool IsGreenhouse { get; private set; }
    public string? LastCropId { get; private set; }

    private int _dryTicks;

    public bool CanHarvest => State == "mature" && !HasFrost;

    public FarmLand(string id, int tileX, int tileY)
    {
        Id = id;
        (X, Y) = ItemIds.TileToPixel(tileX, tileY);
    }

    public LandState ToState() => new(Id, X, Y, State, CropId,
        Math.Round(Growth, 2), NeedsWater, CanHarvest,
        IsDry, HasPest, HasFrost, Fertility, IsGreenhouse, LastCropId);

    public void UpgradeGreenhouse()
    {
        IsGreenhouse = true;
    }

    public (bool Ok, string Message) Plant(string seedId, double seasonMultiplier = 1.0)
    {
        if (State != "empty")
            return (false, $"{Id} is not empty");
        if (HasFrost)
            return (false, $"{Id} is frozen");
        if (!ItemIds.SeedToCrop.TryGetValue(seedId, out var crop))
            return (false, $"unknown seed: {seedId}");

        if (LastCropId == crop)
            Fertility = Math.Max(40, Fertility - 5);
        else if (LastCropId != null)
            Fertility = Math.Min(100, Fertility + 3);

        State = "planted";
        CropId = crop;
        Growth = 0;
        NeedsWater = false;
        IsDry = false;
        HasPest = false;
        _dryTicks = 0;
        _seasonMultiplier = seasonMultiplier;
        return (true, $"planted {seedId} on {Id}");
    }

    private double _seasonMultiplier = 1.0;
    private double _bondBonus = 1.0;

    public void SetBondBonus(double bonus) => _bondBonus = Math.Clamp(bonus, 1.0, 1.5);

    public (bool Ok, string Message) Water()
    {
        if (State is "empty" or "mature")
            return (false, $"{Id} does not need water");
        IsDry = false;
        if (!NeedsWater && State != "withered" && !HasPest)
            return (false, $"{Id} is not thirsty");
        NeedsWater = false;
        _dryTicks = 0;
        if (State == "withered") State = "growing";
        return (true, $"watered {Id}");
    }

    public (bool Ok, string Message) ApplyPesticide()
    {
        if (!HasPest) return (false, $"{Id} has no pests");
        HasPest = false;
        return (true, $"cleared pests on {Id}");
    }

    public (bool Ok, string Message) Fertilize()
    {
        Fertility = Math.Min(100, Fertility + 15);
        return (true, $"fertility now {Fertility}");
    }

    public void ApplyHeatingPenalty() => Fertility = Math.Max(20, Fertility - 15);

    public (bool Ok, string Message, string? CropId) Harvest()
    {
        if (!CanHarvest || CropId == null)
            return (false, $"{Id} is not ready to harvest", null);
        var crop = CropId;
        LastCropId = crop;
        Reset(crop);
        return (true, $"harvested {crop} from {Id}", crop);
    }

    public (bool Ok, string Message) Clear()
    {
        if (State != "withered")
            return (false, $"{Id} is not withered");
        Reset(LastCropId);
        return (true, $"cleared {Id}");
    }

    public void TickDisaster(Func<double>? rng = null)
    {
        rng ??= Random.Shared.NextDouble;
        if (State is "empty" or "mature") return;

        if (!IsGreenhouse && rng() < 0.08)
            IsDry = true;
        if (!IsGreenhouse && rng() < 0.06)
            HasPest = true;
        if (!IsGreenhouse && rng() < 0.04)
            HasFrost = true;
    }

    public void TickGrowth(Func<double>? rng = null)
    {
        rng ??= Random.Shared.NextDouble;
        if (State is "empty" or "mature") return;
        if (HasFrost) return;

        if (State == "planted") State = "growing";

        if (IsDry || HasPest)
        {
            NeedsWater = true;
            State = "needs_water";
            return;
        }

        if (State is "growing" or "needs_water")
        {
            if (NeedsWater && !IsGreenhouse)
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
                var rate = GrowthPerTick * _seasonMultiplier * _bondBonus * (Fertility / 100.0);
                if (IsGreenhouse) rate *= 1.2;
                Growth = Math.Min(1, Growth + rate);
                if (Growth >= 1)
                {
                    State = "mature";
                    NeedsWater = false;
                }
                else if (!IsGreenhouse && rng() < 0.25)
                {
                    NeedsWater = true;
                    State = "needs_water";
                }
            }
        }
    }

    public void Tick(Func<double>? rng = null)
    {
        TickDisaster(rng);
        TickGrowth(rng);
    }

    public void Restore(LandSaveData d)
    {
        State = d.State;
        CropId = d.CropId;
        Growth = d.Growth;
        NeedsWater = d.NeedsWater;
        IsDry = d.IsDry;
        HasPest = d.HasPest;
        HasFrost = d.HasFrost;
        Fertility = d.Fertility;
        IsGreenhouse = d.IsGreenhouse;
        LastCropId = d.LastCropId;
        _dryTicks = 0;
        _seasonMultiplier = 1.0;
    }

    public void ResetLand()
    {
        IsGreenhouse = false;
        Reset(null);
    }

    private void Reset(string? keepLastCrop = null)
    {
        State = "empty";
        CropId = null;
        Growth = 0;
        NeedsWater = false;
        IsDry = false;
        HasPest = false;
        HasFrost = false;
        _dryTicks = 0;
        _bondBonus = 1.0;
        if (keepLastCrop != null) LastCropId = keepLastCrop;
    }
}
