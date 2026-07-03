namespace OpenClawFarm.Core.Models;

public record SeasonState(string Season, int DayInSeason, int SeasonIndex, Dictionary<string, double> CropMultipliers);

public record LandStateExtended(
    string Id, int X, int Y, string State, string? CropId, double Growth,
    bool NeedsWater, bool CanHarvest,
    bool IsDry, bool HasPest, bool HasFrost, int Fertility, bool IsGreenhouse, string? LastCropId);

public record FarmOrderExtended(
    string Id, string Type, string CropId, int Required, int Delivered, int Reward,
    string? ItemId, string ExpiresAt, bool Completed, bool Claimed);

public record OrderHubState(List<FarmOrderExtended> Orders, List<CrossLineOrder> CrossLineOrders);

public record CrossLineOrder(
    string Id,
    Dictionary<string, int> Required,
    Dictionary<string, int> Delivered,
    int GoldReward,
    bool Completed,
    bool Claimed,
    string ExpiresAt,
    string Type = "daily");

public record MerchantState(
    string Id, string Name, string Category, int X, int Y, string SceneId,
    Dictionary<string, int> Prices);

public record HybridRecipe(string OutputSeed, string SeedA, string SeedB, bool Unlocked);
public record HybridState(List<HybridRecipe> Recipes);

public record LandBondEntry(string LandId, double Bonus);
public record LandBondState(List<LandBondEntry> ActiveBonds);

public record MineBossState(bool Active, int BossHp, int MaxHp, int DefeatedCount);

public record FishCodexEntry(string FishId, bool Discovered);
public record FishCodexState(List<FishCodexEntry> Entries, int DiscoveredCount, int TotalCount);

public record DecorationEntry(string Id, string Name, bool Placed, Dictionary<string, int> Cost);
public record DecorationState(List<DecorationEntry> Catalog);

public record TreeState(string Id, int Tx, int Ty, int Hp, bool Active, int? RegrowInDays);

public record ForestState(
    int TotalTrees, int ActiveTrees, int ChoppedToday, int X, int Y, List<TreeState> NearbyTrees);

public record TreeSaveEntry(string Id, int Tx, int Ty, int Hp, int? RegrowAtDay);

public record ForestSaveData(List<TreeSaveEntry> Trees, int ChoppedToday);

public record BuildRecipeState(string BuildType, int WoodCost, int PlankCost, string TileType);

public record BuiltTileState(int Tx, int Ty, string Type);

public record ConstructionState(int BuiltCount, List<BuildRecipeState> Recipes, List<BuiltTileState> Tiles);

public record BuiltTileSave(int Tx, int Ty, string Type);

public record ConstructionSaveData(List<BuiltTileSave> Tiles);

public record AnimalState(
    string Id, string Type, int X, int Y, int Hunger, int Happiness,
    bool HasProduct, string? ProductId, int ProductCount, bool CanBreed);

public record LivestockState(List<AnimalState> Animals, int BarnLevel);

public record RecipeState(string OutputId, string OutputName, Dictionary<string, int> Inputs, int SellPrice);

public record ProcessingState(
    string FactoryId, int X, int Y, List<RecipeState> Recipes, List<string> UnlockedRecipes);

public record AchievementEntry(
    string Id, string Name, string Category, int Progress, int Target,
    bool Unlocked, bool RewardClaimed, string RewardDescription);

public record AchievementState(List<AchievementEntry> Achievements, int UnlockedCount, int TotalCount);

public record VictoryTier(string Id, string Name, string Tier, bool Achieved, long AchievedAt, string Description);

public record ProgressState(
    long TotalGoldEarned, int TotalHarvests, int ContractsCompleted, int BuildingsUnlocked,
    int CropMasteries, int AnimalsOwned, string ActiveRoute,
    List<VictoryTier> Victories, int PerfectPercent);

public record PrestigeState(
    int PrestigeLevel, int PrestigeGems, double GlobalMultiplier,
    bool CanPrestige, Dictionary<string, double> PermanentBonuses);

public record PendingReward(
    string Id, string Source, string Description, Dictionary<string, int> Items, int Gold, bool Claimed);

public record RewardState(List<PendingReward> Pending, int UnclaimedCount);

public record BuildingState(string Id, string Name, bool Unlocked, int Level, int UpgradeCost);

public record FarmMetaSnapshot(
    SeasonState Season,
    OrderHubState Orders,
    LivestockState Livestock,
    ProcessingState Processing,
    AchievementState Achievements,
    ProgressState Progress,
    PrestigeState Prestige,
    RewardState Rewards,
    List<BuildingState> Buildings,
    EconomyState Economy,
    MineState Mine,
    List<FishPondState> FishPonds,
    UpkeepState Upkeep,
    List<MerchantState> Merchants,
    HybridState Hybrid,
    LandBondState LandBonds,
    MineBossState Boss,
    FishCodexState Codex,
    DecorationState Decorations,
    ForestState Forest,
    ConstructionState Construction);
