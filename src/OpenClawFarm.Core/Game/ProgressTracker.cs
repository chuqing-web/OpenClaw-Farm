using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class ProgressTracker
{
    public long TotalGoldEarned { get; private set; }
    public int TotalHarvests { get; private set; }
    public int ContractsCompleted { get; private set; }
    public int BuildingsUnlocked { get; private set; } = 1;
    public int CropMasteries { get; private set; }
    public int AnimalsOwned { get; private set; }
    public string ActiveRoute { get; private set; } = "balanced";
    public int ProcessedCount { get; private set; }
    public int ActionCount { get; private set; }
    public bool MainVictory { get; private set; }
    public bool PerfectVictory { get; private set; }
    public bool PlantRoute { get; private set; }
    public bool LivestockRoute { get; private set; }
    public bool CraftRoute { get; private set; }

    private readonly Dictionary<string, int> _cropHarvestCounts = new();

    public void OnGoldEarned(int amount)
    {
        TotalGoldEarned += amount;
    }

    public void OnHarvest(string cropId)
    {
        TotalHarvests++;
        _cropHarvestCounts[cropId] = _cropHarvestCounts.GetValueOrDefault(cropId) + 1;
        if (_cropHarvestCounts[cropId] >= 100) CropMasteries = _cropHarvestCounts.Count(kv => kv.Value >= 100);
    }

    public void OnContractComplete() => ContractsCompleted++;
    public void OnBuildingUnlock() => BuildingsUnlocked++;
    public void OnAnimalOwned(int count) => AnimalsOwned = count;
    public void OnProcessed() => ProcessedCount++;
    public void OnAction() => ActionCount++;

    public void SetRoute(string route) => ActiveRoute = route;

    public int GetCropHarvestCount(string cropId) => _cropHarvestCounts.GetValueOrDefault(cropId);

    public ProgressSaveData Export() => new(
        TotalGoldEarned, TotalHarvests, ContractsCompleted, BuildingsUnlocked,
        ProcessedCount, ActionCount, new Dictionary<string, int>(_cropHarvestCounts));

    public void Restore(ProgressSaveData d)
    {
        TotalGoldEarned = d.TotalGoldEarned;
        TotalHarvests = d.TotalHarvests;
        ContractsCompleted = d.ContractsCompleted;
        BuildingsUnlocked = d.BuildingsUnlocked;
        ProcessedCount = d.ProcessedCount;
        ActionCount = d.ActionCount;
        _cropHarvestCounts.Clear();
        foreach (var (k, v) in d.CropHarvestCounts)
            _cropHarvestCounts[k] = v;
        CropMasteries = _cropHarvestCounts.Count(kv => kv.Value >= 100);
    }

    public void EvaluateVictories(int greenhouseCount, int animalCount, int achievementsUnlocked, int achievementsTotal)
    {
        var allGreenhouse = greenhouseCount >= 24;
        var mastery2 = _cropHarvestCounts.Count(kv => kv.Value >= 500);

        MainVictory = allGreenhouse
            && animalCount >= 9
            && BuildingsUnlocked >= 6
            && TotalGoldEarned >= 100_000
            && ContractsCompleted >= 10
            && mastery2 >= 5;

        PerfectVictory = MainVictory
            && CropMasteries >= ItemIds.AllSeeds.Length
            && TotalGoldEarned >= 1_000_000
            && achievementsUnlocked >= achievementsTotal - 2;

        PlantRoute = TotalHarvests > ProcessedCount * 3;
        LivestockRoute = animalCount >= 6 && ProcessedCount < TotalHarvests;
        CraftRoute = ProcessedCount >= TotalHarvests && ProcessedCount >= 50;
    }

    public ProgressState ToState()
    {
        var victories = new List<VictoryTier>
        {
            Tier("daily_profit", "单日盈利5000", "short", TotalGoldEarned >= 5000),
            Tier("first_greenhouse", "首座温室", "short", BuildingsUnlocked >= 2),
            Tier("first_process", "首次加工", "short", ProcessedCount >= 1),
            Tier("main_ending", "田园大亨", "main", MainVictory),
            Tier("perfect_ending", "农场传奇", "perfect", PerfectVictory),
            Tier("plant_route", "种植大亨", "branch", PlantRoute && MainVictory),
            Tier("livestock_route", "畜牧牧场", "branch", LivestockRoute && MainVictory),
            Tier("craft_route", "加工商人", "branch", CraftRoute && MainVictory),
        };

        var perfectPct = (int)Math.Min(100,
            (TotalGoldEarned / 10_000.0 +
             TotalHarvests / 10.0 +
             BuildingsUnlocked * 5 +
             CropMasteries * 8 +
             ContractsCompleted * 3) / 5);

        return new ProgressState(
            TotalGoldEarned, TotalHarvests, ContractsCompleted, BuildingsUnlocked,
            CropMasteries, AnimalsOwned, ActiveRoute, victories, perfectPct);
    }

    private static VictoryTier Tier(string id, string name, string tier, bool achieved) =>
        new(id, name, tier, achieved, achieved ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : 0,
            achieved ? "已达成" : "未达成");
}
