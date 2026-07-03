using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class SeasonSystem
{
    private static readonly string[] Seasons = ["spring", "summer", "autumn", "winter"];
    private const int DaysPerSeason = 7;

    private static readonly Dictionary<string, string[]> SeasonCrops = new()
    {
        ["spring"] = [ItemIds.SeedStrawberry, ItemIds.SeedCarrot, ItemIds.SeedWheat],
        ["summer"] = [ItemIds.SeedCorn, ItemIds.SeedStrawberry, ItemIds.SeedPumpkin],
        ["autumn"] = [ItemIds.SeedPumpkin, ItemIds.SeedWheat, ItemIds.SeedCorn],
        ["winter"] = [ItemIds.SeedWheat, ItemIds.SeedCarrot],
    };

    public int GameDay { get; private set; } = 1;

    public void Restore(int gameDay) => GameDay = Math.Max(1, gameDay);

    public void AdvanceDay()
    {
        GameDay++;
    }

    public string CurrentSeason => Seasons[(GameDay - 1) / DaysPerSeason % Seasons.Length];

    public int DayInSeason => (GameDay - 1) % DaysPerSeason + 1;

    public double GetCropMultiplier(string seedId)
    {
        var season = CurrentSeason;
        if (SeasonCrops.TryGetValue(season, out var preferred) && preferred.Contains(seedId))
            return 1.25;
        if (season == "winter" && seedId is not (ItemIds.SeedWheat or ItemIds.SeedCarrot))
            return 0.5;
        return 1.0;
    }

    public bool IsCropInSeason(string seedId) => GetCropMultiplier(seedId) >= 1.0;

    public SeasonState ToState()
    {
        var mults = ItemIds.AllSeeds.ToDictionary(s => s, GetCropMultiplier);
        return new SeasonState(CurrentSeason, DayInSeason, Array.IndexOf(Seasons, CurrentSeason), mults);
    }
}
