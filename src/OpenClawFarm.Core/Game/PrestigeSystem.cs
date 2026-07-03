using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class PrestigeSystem
{
    public int Level { get; private set; }
    public int Gems { get; private set; }
    public double GlobalMultiplier => 1.0 + Level * 0.1 + Gems * 0.01;

    private readonly Dictionary<string, double> _bonuses = new()
    {
        ["plant_speed"] = 0,
        ["sell_price"] = 0,
        ["livestock_output"] = 0,
    };

    public bool CanPrestige(long totalGold, bool mainVictory) => mainVictory && totalGold >= 100_000;

    public (bool Ok, string Message) PrestigeReset(Inventory inventory, long totalGold, bool mainVictory)
    {
        if (!CanPrestige(totalGold, mainVictory))
            return (false, "prestige not available");

        var earned = Math.Max(1, (int)(inventory.Gold / 50_000));
        Gems += earned;
        Level++;
        inventory.SetGold(500);
        return (true, $"prestige level {Level}, +{earned} gems");
    }

    public void ApplyBonus(string key, double amount)
    {
        if (_bonuses.ContainsKey(key))
            _bonuses[key] += amount;
    }

    public double GetBonus(string key) => _bonuses.GetValueOrDefault(key);

    public PrestigeState ToState(long totalGold, bool mainVictory) => new(
        Level, Gems, GlobalMultiplier, CanPrestige(totalGold, mainVictory),
        new Dictionary<string, double>(_bonuses));

    public PrestigeSaveData Export() => new(Level, Gems, new Dictionary<string, double>(_bonuses));

    public void Restore(PrestigeSaveData d)
    {
        Level = d.Level;
        Gems = d.Gems;
        _bonuses.Clear();
        foreach (var (k, v) in d.Bonuses)
            _bonuses[k] = v;
    }
}
