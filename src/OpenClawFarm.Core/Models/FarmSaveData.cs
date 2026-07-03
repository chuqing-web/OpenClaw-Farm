namespace OpenClawFarm.Core.Models;

public record FarmSaveData(
    long SavedAt,
    double PlayerX,
    double PlayerY,
    string PlayerFacing,
    int Gold,
    Dictionary<string, int> Items,
    List<LandSaveData> Lands,
    int GameHour,
    int GameDay,
    int TickCount,
    ProgressSaveData Progress,
    List<AchievementSaveData> Achievements,
    PrestigeSaveData Prestige,
    List<string> UnlockedBuildings,
    List<AnimalSaveData> Animals,
    List<string> UnlockedRecipes,
    MiningSaveData? Mining = null,
    FishingSaveData? Fishing = null,
    EconomySaveData? Economy = null,
    UpkeepSaveData? Upkeep = null,
    OrderHubSaveData? OrderHub = null,
    HybridSaveData? Hybrid = null,
    MineBossSaveData? Boss = null,
    FishCodexSaveData? Codex = null,
    DecorationSaveData? Decorations = null,
    ForestSaveData? Forest = null,
    ConstructionSaveData? Construction = null);

public record HybridSaveData(List<string> Unlocked);
public record MineBossSaveData(bool Active, int BossHp, int DefeatedCount);
public record FishCodexSaveData(List<string> Discovered);
public record DecorationSaveData(List<string> Placed);

public record LandSaveData(
    string Id,
    string State,
    string? CropId,
    double Growth,
    bool NeedsWater,
    bool IsDry,
    bool HasPest,
    bool HasFrost,
    int Fertility,
    bool IsGreenhouse,
    string? LastCropId);

public record ProgressSaveData(
    long TotalGoldEarned,
    int TotalHarvests,
    int ContractsCompleted,
    int BuildingsUnlocked,
    int ProcessedCount,
    int ActionCount,
    Dictionary<string, int> CropHarvestCounts);

public record AchievementSaveData(string Id, int Progress, bool Unlocked, bool RewardClaimed);

public record PrestigeSaveData(int Level, int Gems, Dictionary<string, double> Bonuses);

public record AnimalSaveData(string Id, int Hunger, int Happiness, bool HasProduct);

public record EconomySaveData(
    Dictionary<string, int> DailySold,
    int FarmTicks, int MineTicks, int FishTicks,
    int LastGameDay);

public record MiningSaveData(
    int Layer, int Stamina, int PickaxeTier, int PickaxeDurability,
    int LanternFuel, bool InMine, int VeinBonusTicks);

public record FishingSaveData(
    Dictionary<string, int> DailyCaught,
    Dictionary<string, int> PondFatigue,
    int LastGameDay);

public record UpkeepSaveData(
    int MineIntegrity, int BuildingDurability, int PondEcology, int MaintenanceDebt);

public record OrderHubSaveData(
    List<FarmOrderExtended> Contracts,
    List<FarmOrderExtended> Festivals,
    List<CrossLineOrder> CrossLine,
    List<CrossLineOrder> WeeklyCross,
    List<CrossLineOrder> FestivalDeliveries);

public record UpkeepState(
    int MineIntegrity, int BuildingDurability, int PondEcology,
    int MaintenanceDebt, List<string> RecentLog);

public record EconomyState(
    Dictionary<string, int> DailySold,
    int FarmTicks, int MineTicks, int FishTicks,
    double FarmYield, double MineYield, double FishYield);

public record MineState(
    bool InMine, int Layer, int Stamina, int PickaxeTier, int PickDur,
    int LanternFuel, int VeinBonus, int X, int Y);

public record FishPondState(
    string Id, string Type, int CaughtToday, int DailyCap,
    int FatigueTicks, int X, int Y, string[] FishTypes, string BaitReq);

public record SaveInfo(bool HasSave, long? SavedAt, int? Gold, int? GameDay, string? Season);
