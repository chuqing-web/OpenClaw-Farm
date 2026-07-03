using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class GameWorld
{
    private const int TickIntervalMs = 4000;
    private readonly object _lock = new();
    private Timer? _tickTimer;
    private string? _sellConfirmToken;
    private long _sellConfirmExpires;
    private long _lastActionAt;
    private int _gameHour = 8;
    private int _tickCount;

    private readonly SemaphoreSlim _actionSem = new(1, 1);
    private string? _currentActionId;
    private long _actionStartedAt;
    private string? _lastActionMessage;
    private string? _lastNextHint;
    private string? _lastNextActionId;
    private Dictionary<string, object?>? _lastNextParams;

    public Player Player { get; }
    public Inventory Inventory { get; }
    public MerchantSystem Merchants { get; }
    public OrderHub Orders { get; }
    public Dictionary<string, FarmLand> Lands { get; } = new();
    public Well Well { get; }
    public SeasonSystem Seasons { get; } = new();
    public ProgressTracker Progress { get; } = new();
    public AchievementSystem Achievements { get; } = new();
    public PrestigeSystem Prestige { get; } = new();
    public ProcessingSystem Processing { get; } = new();
    public LivestockManager Livestock { get; } = new();
    public RewardQueue Rewards { get; } = new();
    public BuildingSystem Buildings { get; } = new();
    public EconomySystem Economy { get; } = new();
    public MiningSystem Mining { get; } = new();
    public FishingSystem Fishing { get; } = new();
    public UpkeepSystem Upkeep { get; } = new();
    public HybridSystem Hybrid { get; } = new();
    public LandBondSystem LandBonds { get; } = new();
    public MineBossSystem Boss { get; } = new();
    public FishCodexSystem Codex { get; } = new();
    public DecorationSystem Decorations { get; } = new();
    public ForestSystem Forest { get; }
    public ConstructionSystem Construction { get; } = new();

    public event Action<WorldPatch>? OnPatch;
    public event Action? OnAutoSave;

    public bool SessionActive { get; private set; }

    public GameWorld()
    {
        Player = Player.CreateDefault();
        Inventory = new Inventory(120,
        [
            new BagItem(ItemIds.SeedStrawberry, 20),
            new BagItem(ItemIds.SeedWheat, 15),
            new BagItem(ItemIds.SeedCarrot, 15),
            new BagItem(ItemIds.SeedCorn, 10),
            new BagItem(ItemIds.SeedPumpkin, 8),
            new BagItem(ItemIds.ToolWateringCan, 1),
            new BagItem(ItemIds.ToolPesticide, 5),
            new BagItem(ItemIds.ToolFertilizer, 3),
            new BagItem(ItemIds.ToolAxe, 1),
        ]);
        var (mx, my) = ItemIds.MerchantPos;
        Merchants = new MerchantSystem();
        var (wx, wy) = ItemIds.WellPos;
        Well = new Well(ItemIds.WellId, wx, wy);
        Orders = new OrderHub();
        foreach (var (id, tx, ty) in ItemIds.LandLayout)
            Lands[id] = new FarmLand(id, tx, ty);
        Forest = new ForestSystem();
        WorldMapData.IsTreeChopped = Forest.IsChopped;
        WorldMapData.BlocksMovement = Construction.Blocks;
        Progress.OnAnimalOwned(Livestock.Animals.Count);
    }

    public int GetGameHour() => _gameHour;
    public int GetTickCount() => _tickCount;

    public AgentActionState GetActionState() => new(
        _actionSem.CurrentCount == 0,
        _currentActionId,
        _currentActionId != null ? _actionStartedAt : null,
        _lastActionMessage,
        _lastNextHint,
        _lastNextActionId,
        _lastNextParams);

    public async Task<ActionResult> ExecuteActionAsync(string actionId, JsonParams p)
    {
        if (!await _actionSem.WaitAsync(0))
        {
            return new ActionResult(false,
                $"上一个操作「{_currentActionId}」还未结束，请等待",
                new Dictionary<string, object?>
                {
                    ["busy"] = true,
                    ["currentAction"] = _currentActionId,
                    ["startedAt"] = _actionStartedAt,
                    ["hint"] = "请等待当前动作完成后再发送下一条指令，或 GET /agent/state/action 查询状态",
                });
        }

        _currentActionId = actionId;
        _actionStartedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        try
        {
            var result = await ActionExecutor.RunAsync(this, actionId, p);
            _lastActionMessage = result.Message;

            if (result.Success)
            {
                ActionAdvisor.NextSuggestion next;
                lock (_lock) { next = ActionAdvisor.Suggest(this); }
                _lastNextHint = next.Hint;
                _lastNextActionId = next.ActionId;
                _lastNextParams = next.Params;

                var extra = new Dictionary<string, object?>(result.Extra ?? new Dictionary<string, object?>())
                {
                    ["completed"] = true,
                    ["nextHint"] = next.Hint,
                    ["nextActionId"] = next.ActionId,
                    ["nextParams"] = next.Params,
                };
                return new ActionResult(true, $"{result.Message} → 下一步：{next.Hint}", extra);
            }

            return result;
        }
        finally
        {
            _currentActionId = null;
            _actionSem.Release();
        }
    }

    public void SetGameHour(int hour) => _gameHour = hour % 24;
    public void SetTickCount(int count) => _tickCount = count;

    public void BeginNewSession()
    {
        lock (_lock)
        {
            ResetToNewGame();
            SessionActive = true;
            Start();
            EmitFullPatch();
        }
    }

    public void BeginLoadedSession(FarmSaveData save)
    {
        lock (_lock)
        {
            Stop();
            _tickTimer = null;
            GameSaveManager.Apply(this, save);
            SessionActive = true;
            Start();
            EmitFullPatch();
        }
    }

    public FarmSaveData ExportSave() => GameSaveManager.Export(this);

    private void ResetToNewGame()
    {
        Stop();
        _tickTimer = null;
        _gameHour = 8;
        _tickCount = 0;
        Player.SetPosition(ItemIds.PlayerSpawn.X, ItemIds.PlayerSpawn.Y, facing: "down");
        Inventory.LoadFrom(120, new Dictionary<string, int>
        {
            [ItemIds.SeedStrawberry] = 20,
            [ItemIds.SeedWheat] = 15,
            [ItemIds.SeedCarrot] = 15,
            [ItemIds.SeedCorn] = 10,
            [ItemIds.SeedPumpkin] = 8,
            [ItemIds.ToolWateringCan] = 1,
            [ItemIds.ToolPesticide] = 5,
            [ItemIds.ToolFertilizer] = 3,
            [ItemIds.ToolAxe] = 1,
        });
        foreach (var land in Lands.Values)
            land.ResetLand();
        Seasons.Restore(1);
        Progress.Restore(new ProgressSaveData(0, 0, 0, 1, 0, 0, new()));
        Achievements.Restore([]);
        Prestige.Restore(new PrestigeSaveData(0, 0, new()));
        Buildings.Restore(["factory", "barn"]);
        Livestock.Restore(Livestock.Export().Select(a => a with { Hunger = 50, Happiness = 80, HasProduct = false }).ToList());
        Processing.Restore([
            ItemIds.CropJam, ItemIds.CropFlour, ItemIds.CropCheese, ItemIds.CropCharcoal,
            ItemIds.BaitBasic, ItemIds.ToolAxe,
        ]);
        Mining.Restore(null);
        Fishing.Restore(null);
        Economy.Restore(null);
        Upkeep.Restore(null);
        Orders.Restore(null);
        Hybrid.Restore(null);
        Boss.Restore(null);
        Codex.Restore(null);
        Decorations.Restore(null);
        Forest.Restore(null);
        Construction.Restore(null);
        Progress.OnAnimalOwned(Livestock.Animals.Count);
    }

    private void EmitFullPatch()
    {
        Emit(new WorldPatch("world_patch", Now(),
            Player: Player.ToState(),
            Lands: GetLands(),
            Bag: Inventory.GetBag(),
            GameHour: _gameHour));
    }

    public void Start()
    {
        _tickTimer ??= new Timer(_ => Tick(), null, TickIntervalMs, TickIntervalMs);
    }

    public void Stop() => _tickTimer?.Dispose();

    private void Tick()
    {
        lock (_lock)
        {
            foreach (var land in Lands.Values)
                land.SetBondBonus(LandBonds.GetGrowthBonus(land.Id, Lands));

            foreach (var land in Lands.Values)
            {
                land.Tick();
                if (Buildings.All["auto_sprinkler"].Unlocked && land.NeedsWater)
                {
                    if (Upkeep.PayAutoSprinklerFuel(Inventory))
                        land.Water();
                }
            }

            Livestock.Tick();
            _tickCount++;
            _gameHour = (_gameHour + 1) % 24;

            if (_gameHour == 0)
            {
                Seasons.AdvanceDay();
                Economy.OnNewDay(Seasons.GameDay);
                Fishing.OnNewDay(Seasons.GameDay);
                Orders.OnNewDay(Seasons.GameDay);
                var regrow = Forest.OnNewDay(Seasons.GameDay);
                Upkeep.RunDaily(Inventory, Buildings, Livestock, Mining, Lands.Values, Seasons.GameDay);
                Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag(),
                    TileUpdates: regrow.Count > 0 ? regrow : null));
            }

            if (_tickCount % 6 == 0)
                TryAutoHarvest();

            if (_tickCount % 15 == 0)
                OnAutoSave?.Invoke();

            var ach = Achievements.ToState();
            Progress.EvaluateVictories(
                Lands.Values.Count(l => l.IsGreenhouse),
                Livestock.Animals.Count,
                ach.UnlockedCount,
                ach.TotalCount);

            Emit(new WorldPatch("world_patch", Now(), Lands: GetLands(), GameHour: _gameHour));
        }
    }

    private void TryAutoHarvest()
    {
        if (!Buildings.All["auto_harvester"].Unlocked) return;
        if (!Upkeep.PayAutoHarvesterFuel(Inventory)) return;
        foreach (var land in Lands.Values.Where(l => l.CanHarvest))
        {
            var r = land.Harvest();
            if (r.Ok && r.CropId != null)
            {
                Inventory.AddItem(r.CropId, 1);
                OnHarvest(r.CropId);
            }
        }
        EmitPatchLandsAndBag();
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private void Emit(WorldPatch patch) => OnPatch?.Invoke(patch);

    private void EmitActivity(string kind, int x, int y, int durationMs, BagState? bag = null,
        List<LandState>? lands = null, PlayerState? player = null, List<TileUpdate>? tileUpdates = null)
    {
        Emit(new WorldPatch("world_patch", Now(), player, lands, bag,
            Activity: new ActivityEvent(kind, x, y, durationMs, Player.Facing),
            TileUpdates: tileUpdates));
    }

    public List<LandState> GetLandsPage(int page, int pageSize = 8)
    {
        var all = GetLands();
        var skip = Math.Max(0, (page - 1) * pageSize);
        return all.Skip(skip).Take(pageSize).ToList();
    }

    public FarmMetaSnapshot GetMetaSnapshot()
    {
        var ach = Achievements.ToState();
        Progress.EvaluateVictories(
            Lands.Values.Count(l => l.IsGreenhouse),
            Livestock.Animals.Count,
            ach.UnlockedCount,
            ach.TotalCount);
        return new FarmMetaSnapshot(
            Seasons.ToState(),
            Orders.GetState(),
            Livestock.ToState(),
            Processing.ToState(),
            ach,
            Progress.ToState(),
            Prestige.ToState(Progress.TotalGoldEarned, Progress.MainVictory),
            Rewards.ToState(),
            Buildings.ToList(),
            Economy.ToState(),
            Mining.ToState(),
            Fishing.AllPondSummaries(),
            Upkeep.ToState(),
            Merchants.ToState(),
            Hybrid.ToState(),
            LandBonds.ToState(Lands),
            Boss.ToState(),
            Codex.ToState(),
            Decorations.ToState(),
            Forest.ToState(Seasons.GameDay, Player.X, Player.Y),
            Construction.ToState());
    }

    public string[][] GetDynamicTiles()
    {
        var tiles = WorldMapData.GetTilesFlat();
        Forest.ApplyToTiles(tiles);
        Construction.ApplyToTiles(tiles);
        return tiles;
    }

    public List<LandState> GetLands() => Lands.Values.Select(l => l.ToState()).ToList();

    public WorldSnapshot GetSnapshot()
    {
        var crop = Merchants.Crop;
        return new(
            Player.ToState(),
            GetLands(),
            Inventory.GetBag(),
            crop.GetPrices(),
            Orders.GetDailyState(),
            new MerchantInfo(crop.Id, crop.X, crop.Y, crop.SceneId),
            ItemIds.MapWidth,
            ItemIds.MapHeight,
            GetDynamicTiles(),
            _gameHour,
            new WellInfo(Well.Id, Well.X, Well.Y));
    }

    public SellConfirmResult? IssueSellConfirm()
    {
        lock (_lock)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            _sellConfirmToken = token;
            _sellConfirmExpires = Environment.TickCount64 + 60_000;
            return new SellConfirmResult(token, 60);
        }
    }

    private async Task ApplyCooldownAsync()
    {
        Progress.OnAction();
        Achievements.SetProgress("auto_48h", Progress.ActionCount / 40);
        var jitter = 150 + Random.Shared.Next(300);
        var elapsed = Environment.TickCount64 - _lastActionAt;
        var wait = Math.Max(0, 500 + jitter - elapsed);
        if (wait > 0) await Task.Delay((int)wait);
        _lastActionAt = Environment.TickCount64;
    }

    public async Task<ActionResult> MoveToAsync(MoveToParams p)
    {
        await ApplyCooldownAsync();
        List<(int X, int Y)>? path;
        lock (_lock) { path = Player.FindPath(p.X, p.Y); }
        if (path == null || path.Count == 0)
            return new ActionResult(false, $"no path to ({p.X},{p.Y})");

        foreach (var waypoint in path.Skip(1))
        {
            lock (_lock)
            {
                Player.SetPosition(waypoint.X, waypoint.Y, p.SceneId);
                Emit(new WorldPatch("world_patch", Now(), Player: Player.ToState()));
            }
            await Task.Delay(95);
        }
        lock (_lock)
        {
            return new ActionResult(true,
                $"moved to ({(int)Math.Round(Player.X)},{(int)Math.Round(Player.Y)})",
                new Dictionary<string, object?> { ["player"] = Player.ToState() });
        }
    }

    public async Task<ActionResult> InteractAsync(InteractParams p)
    {
        await ApplyCooldownAsync();
        lock (_lock) { return InteractCore(p); }
    }

    public async Task<ActionResult> ChopTreeAsync(JsonParams p)
    {
        await ApplyCooldownAsync();
        lock (_lock)
        {
            var lumberBonus = Buildings.All["lumber_camp"].Unlocked;
            (bool Ok, string Message, int Wood, List<TileUpdate> Updates) r;

            if (!string.IsNullOrEmpty(p.TargetEntityId) && p.TargetEntityId.StartsWith("tree_", StringComparison.Ordinal))
                r = Forest.TryChopById(p.TargetEntityId, Player, Inventory, lumberBonus, Seasons.GameDay);
            else
            {
                var tx = p.TileX ?? (p.X.HasValue ? p.X.Value / ItemIds.TileSize : -1);
                var ty = p.TileY ?? (p.Y.HasValue ? p.Y.Value / ItemIds.TileSize : -1);
                if (tx < 0 || ty < 0)
                    return new ActionResult(false, "need tileX/tileY or targetEntityId tree_*");
                r = Forest.TryChop(tx, ty, Player, Inventory, lumberBonus, Seasons.GameDay);
            }

            if (!r.Ok) return new ActionResult(false, r.Message);

            Economy.RecordActivity("farm");
            if (r.Wood > 0)
            {
                Progress.OnHarvest(ItemIds.CropWood);
                Achievements.Increment("chop_first");
            }

            var treeRef = !string.IsNullOrEmpty(p.TargetEntityId)
                ? Forest.FindById(p.TargetEntityId)
                : Forest.NearestChoppable(Player.X, Player.Y);
            var px = (treeRef?.Tx ?? (p.TileX ?? 0)) * ItemIds.TileSize + ItemIds.TileSize / 2;
            var py = (treeRef?.Ty ?? (p.TileY ?? 0)) * ItemIds.TileSize + ItemIds.TileSize / 2;
            EmitActivity("chop", px, py, 700, Inventory.GetBag(), tileUpdates: r.Updates.Count > 0 ? r.Updates : null);
            return new ActionResult(true, r.Message,
                new Dictionary<string, object?> { ["wood"] = r.Wood, ["tileUpdates"] = r.Updates });
        }
    }

    public ActionResult BuildTile(JsonParams p)
    {
        lock (_lock)
        {
            var buildType = p.BuildType ?? p.ItemId;
            if (string.IsNullOrEmpty(buildType))
                return new ActionResult(false, "need buildType (wood_fence, wood_path, lumber_platform)");

            var tx = p.TileX ?? (p.X.HasValue ? p.X.Value / ItemIds.TileSize : -1);
            var ty = p.TileY ?? (p.Y.HasValue ? p.Y.Value / ItemIds.TileSize : -1);
            if (tx < 0 || ty < 0)
                return new ActionResult(false, "need tileX/tileY or x/y pixel coords");

            var r = Construction.TryBuild(buildType, tx, ty, Player, Inventory);
            if (!r.Ok) return new ActionResult(false, r.Message);

            Progress.OnBuildingUnlock();
            Achievements.Increment("build_first");
            var cx = tx * ItemIds.TileSize + ItemIds.TileSize / 2;
            var cy = ty * ItemIds.TileSize + ItemIds.TileSize / 2;
            var updates = r.Update != null ? new List<TileUpdate> { r.Update } : null;
            EmitActivity("build", cx, cy, 800, Inventory.GetBag(), tileUpdates: updates);
            return new ActionResult(true, r.Message!,
                new Dictionary<string, object?> { ["tileUpdate"] = r.Update });
        }
    }

    public async Task<ActionResult> SellItemAsync(SellItemParams p)
    {
        await ApplyCooldownAsync();
        lock (_lock) { return SellItemCore(p); }
    }

    public async Task<ActionResult> WaitAsync(WaitParams p)
    {
        var ms = Math.Clamp(p.Ms, 0, 30_000);
        await Task.Delay(ms);
        return new ActionResult(true, $"waited {ms}ms");
    }

    public ActionResult ClaimRewards(string? rewardId)
    {
        lock (_lock)
        {
            var result = Rewards.Claim(rewardId, Inventory);
            if (!result.Ok) return new ActionResult(false, result.Message);

            foreach (var a in Achievements.Unclaimed())
            {
                Achievements.MarkClaimed(a.Id);
                Rewards.Grant("achievement", a.RewardDescription, gold: 50);
            }

            Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));
            return new ActionResult(true, result.Message,
                new Dictionary<string, object?> { ["gold"] = result.Gold, ["items"] = result.Items });
        }
    }

    public ActionResult ProcessItem(string outputId)
    {
        lock (_lock)
        {
            if (Player.DistanceTo(Processing.X, Processing.Y) > ItemIds.TileSize * 2)
                return new ActionResult(false, "too far from factory");

            var r = Processing.Process(outputId, Inventory);
            if (!r.Ok) return new ActionResult(false, r.Message);

            Progress.OnProcessed();
            Achievements.Increment("process_first");
            Rewards.Grant("processing", $"加工 {outputId}", gold: 10);
            EmitActivity("process", Processing.X, Processing.Y, 850, Inventory.GetBag());
            return new ActionResult(true, r.Message!, new Dictionary<string, object?> { ["outputId"] = r.OutputId });
        }
    }

    public ActionResult FeedAnimal(string animalId, string? itemId)
    {
        lock (_lock)
        {
            var animal = Livestock.Animals.FirstOrDefault(a => a.Id == animalId);
            if (animal == null) return new ActionResult(false, "unknown animal");
            if (Player.DistanceTo(animal.X, animal.Y) > ItemIds.TileSize * 2)
                return new ActionResult(false, "too far from animal");

            var r = Livestock.Feed(animalId, itemId ?? "feed", Inventory);
            if (r.Ok) Achievements.Increment("livestock_feed");
            Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));
            return new ActionResult(r.Ok, r.Message);
        }
    }

    public ActionResult CollectAnimal(string animalId)
    {
        lock (_lock)
        {
            var animal = Livestock.Animals.FirstOrDefault(a => a.Id == animalId);
            if (animal == null) return new ActionResult(false, "unknown animal");
            if (Player.DistanceTo(animal.X, animal.Y) > ItemIds.TileSize * 2)
                return new ActionResult(false, "too far from animal");

            var r = Livestock.Collect(animalId, Inventory);
            Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));
            return new ActionResult(r.Ok, r.Message,
                r.ProductId != null
                    ? new Dictionary<string, object?> { ["productId"] = r.ProductId, ["count"] = r.Count }
                    : null);
        }
    }

    public ActionResult UnlockBuilding(string buildingId)
    {
        lock (_lock)
        {
            var r = Buildings.TryUnlock(buildingId, Inventory);
            if (!r.Ok) return new ActionResult(false, r.Message);

            Progress.OnBuildingUnlock();
            if (buildingId == "greenhouse")
            {
                var land = Lands.Values.FirstOrDefault(l => !l.IsGreenhouse);
                land?.UpgradeGreenhouse();
            }
            if (buildingId == "sawmill")
                Processing.Unlock(ItemIds.CropPlank);
            if (buildingId == "lumber_camp")
                Achievements.Increment("lumber_camp");

            Rewards.Grant("building", r.Message, gold: 0);
            Emit(new WorldPatch("world_patch", Now(), Lands: GetLands(), Bag: Inventory.GetBag()));
            return new ActionResult(true, r.Message);
        }
    }

    public ActionResult PrestigeReset()
    {
        lock (_lock)
        {
            if (!Prestige.CanPrestige(Progress.TotalGoldEarned, Progress.MainVictory))
                return new ActionResult(false, "main victory required before prestige");

            var cost = new Dictionary<string, int>
            {
                [ItemIds.OreCrystal] = 5,
                [ItemIds.FishGlow] = 2,
                [ItemIds.CropJam] = 10,
            };
            foreach (var (item, need) in cost)
            {
                if (Inventory.GetCount(item) < need)
                    return new ActionResult(false, $"prestige needs {need}x {item}");
            }
            foreach (var (item, need) in cost)
                Inventory.RemoveItem(item, need);

            var r = Prestige.PrestigeReset(Inventory, Progress.TotalGoldEarned, Progress.MainVictory);
            Achievements.Increment("prestige_1");
            foreach (var land in Lands.Values)
                if (Random.Shared.NextDouble() < 0.3) land.UpgradeGreenhouse();

            EmitPatchLandsAndBag();
            return new ActionResult(r.Ok, r.Message);
        }
    }

    private ActionResult SellItemCore(SellItemParams p)
    {
        var shop = Merchants.Get(p.MerchantId) ?? Merchants.Crop;
        if (Player.DistanceTo(shop.X, shop.Y) > ItemIds.MerchantInteractRange)
            return new ActionResult(false, $"too far from {shop.Name}");

        if (!shop.Accepts(p.ItemId))
            return new ActionResult(false, $"{shop.Name} does not buy {p.ItemId}");

        if (string.IsNullOrEmpty(p.ConfirmToken))
            return new ActionResult(false,
                "sell requires confirmToken — GET /agent/state/sell_confirm first",
                new Dictionary<string, object?> { ["needConfirm"] = true });

        if (_sellConfirmToken == null || p.ConfirmToken != _sellConfirmToken ||
            Environment.TickCount64 > _sellConfirmExpires)
        {
            _sellConfirmToken = null;
            return new ActionResult(false, "invalid or expired confirmToken",
                new Dictionary<string, object?> { ["needConfirm"] = true });
        }

        var count = p.Count ?? Inventory.GetCount(p.ItemId);
        if (count <= 0 || Inventory.GetCount(p.ItemId) < count)
            return new ActionResult(false, $"not enough {p.ItemId}");

        var price = Economy.GetSellPrice(p.ItemId, shop.PriceMultiplier, Prestige.GlobalMultiplier);
        if (price <= 0)
            return new ActionResult(false, $"{p.ItemId} has no sell price");
        var total = price * count;
        Inventory.RemoveItem(p.ItemId, count);
        Inventory.AddGold(total);
        Economy.RecordSale(p.ItemId, count);
        Progress.OnGoldEarned(total);
        Achievements.SetProgress("gold_10k", (int)Math.Min(int.MaxValue, Progress.TotalGoldEarned));
        Achievements.SetProgress("gold_100k", (int)Math.Min(int.MaxValue, Progress.TotalGoldEarned));

        var orderBonus = Orders.OnSell(p.ItemId, count);
        if (orderBonus > 0)
        {
            Inventory.AddGold(orderBonus);
            Progress.OnGoldEarned(orderBonus);
            Achievements.Increment("order_daily");
            Rewards.Grant("order", "订单奖励", gold: orderBonus);
        }

        _sellConfirmToken = null;
        Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));

        return new ActionResult(true,
            $"sold {count}x {p.ItemId} for {total} gold" + (orderBonus > 0 ? $" (+{orderBonus} bonus)" : ""),
            new Dictionary<string, object?> { ["gold"] = Inventory.Gold, ["earned"] = total + orderBonus });
    }

    private ActionResult InteractCore(InteractParams p)
    {
        if (p.TargetEntityId == ProcessingSystem.FactoryId)
            return ProcessItem(p.ItemId ?? ItemIds.CropJam);

        if (p.TargetEntityId.StartsWith("tree_", StringComparison.Ordinal))
        {
            if (Inventory.GetCount(ItemIds.ToolAxe) < 1)
                return new ActionResult(false, "need tool_axe to chop");
            var lumberBonus = Buildings.All["lumber_camp"].Unlocked;
            var r = Forest.TryChopById(p.TargetEntityId, Player, Inventory, lumberBonus, Seasons.GameDay);
            if (!r.Ok) return new ActionResult(false, r.Message);
            if (r.Wood > 0)
            {
                Progress.OnHarvest(ItemIds.CropWood);
                Achievements.Increment("chop_first");
            }
            var tree = Forest.FindById(p.TargetEntityId)!;
            var px = tree.Tx * ItemIds.TileSize + ItemIds.TileSize / 2;
            var py = tree.Ty * ItemIds.TileSize + ItemIds.TileSize / 2;
            EmitActivity("chop", px, py, 700, Inventory.GetBag(), tileUpdates: r.Updates);
            return new ActionResult(true, r.Message,
                new Dictionary<string, object?> { ["wood"] = r.Wood });
        }

        if (p.TargetEntityId.StartsWith("animal_", StringComparison.Ordinal))
        {
            if (p.ItemId == "collect")
                return CollectAnimal(p.TargetEntityId);
            return FeedAnimal(p.TargetEntityId, p.ItemId);
        }

        if (p.TargetEntityId.StartsWith("building_", StringComparison.Ordinal))
            return UnlockBuilding(p.TargetEntityId.Replace("building_", ""));

        if (p.TargetEntityId == Merchants.Crop.Id || p.TargetEntityId == Merchants.Ore.Id ||
            p.TargetEntityId == Merchants.Fish.Id)
            return new ActionResult(false, "use sell_item with merchantId");

        if (!Lands.TryGetValue(p.TargetEntityId, out var land))
            return new ActionResult(false, $"unknown target: {p.TargetEntityId}");

        Economy.RecordActivity("farm");

        if (Player.DistanceTo(land.X, land.Y) > ItemIds.TileSize * 1.6)
            return new ActionResult(false, $"too far from {p.TargetEntityId}");

        var itemId = p.ItemId;

        if (itemId == ItemIds.ToolPesticide || itemId == "pesticide")
        {
            var pr = land.ApplyPesticide();
            if (pr.Ok) Emit(new WorldPatch("world_patch", Now(), Lands: GetLands()));
            return new ActionResult(pr.Ok, pr.Message);
        }

        if (itemId == ItemIds.ToolFertilizer || itemId == "fertilizer")
        {
            if (!Inventory.RemoveItem(ItemIds.ToolFertilizer, 1))
                return new ActionResult(false, "no fertilizer");
            var fr = land.Fertilize();
            Emit(new WorldPatch("world_patch", Now(), Lands: GetLands(), Bag: Inventory.GetBag()));
            return new ActionResult(fr.Ok, fr.Message);
        }

        if (itemId != null && itemId.StartsWith("seed_", StringComparison.Ordinal))
        {
            if (Inventory.GetCount(itemId) < 1)
                return new ActionResult(false, $"no {itemId} in bag");
            var mult = Seasons.GetCropMultiplier(itemId);
            var r = land.Plant(itemId, mult);
            if (!r.Ok) return new ActionResult(false, r.Message);
            Inventory.RemoveItem(itemId, 1);
            EmitActivity("plant", land.X, land.Y, 550, Inventory.GetBag(), GetLands());
            return new ActionResult(true, r.Message);
        }

        if (itemId == "harvest" || (itemId == null && land.CanHarvest))
        {
            var r = land.Harvest();
            if (!r.Ok) return new ActionResult(false, r.Message);
            if (r.CropId != null)
            {
                Inventory.AddItem(r.CropId, 1);
                OnHarvest(r.CropId);
            }
            EmitActivity("harvest", land.X, land.Y, 650, Inventory.GetBag(), GetLands());
            return new ActionResult(true, r.Message,
                r.CropId != null ? new Dictionary<string, object?> { ["cropId"] = r.CropId } : null);
        }

        if (itemId is "clear" or "clear_withered")
        {
            var r = land.Clear();
            if (!r.Ok) return new ActionResult(false, r.Message);
            Emit(new WorldPatch("world_patch", Now(), Lands: GetLands()));
            return new ActionResult(true, r.Message);
        }

        if (itemId == null || itemId is "water" or ItemIds.ToolWateringCan)
        {
            var r = land.Water();
            if (!r.Ok) return new ActionResult(false, r.Message);
            EmitActivity("water", land.X, land.Y, 450, null, GetLands());
            return new ActionResult(true, r.Message);
        }

        if (land.CanHarvest)
        {
            var r = land.Harvest();
            if (!r.Ok) return new ActionResult(false, r.Message);
            if (r.CropId != null)
            {
                Inventory.AddItem(r.CropId, 1);
                OnHarvest(r.CropId);
            }
            EmitActivity("harvest", land.X, land.Y, 650, Inventory.GetBag(), GetLands());
            return new ActionResult(true, r.Message);
        }

        if (land.NeedsWater || land.State == "needs_water" || land.IsDry)
        {
            var r = land.Water();
            if (!r.Ok) return new ActionResult(false, r.Message);
            EmitActivity("water", land.X, land.Y, 450, null, GetLands());
            return new ActionResult(true, r.Message);
        }

        return new ActionResult(false, $"no valid interact for {p.TargetEntityId}");
    }

    private void OnHarvest(string cropId)
    {
        Progress.OnHarvest(cropId);
        Achievements.Increment("harvest_100");
        var count = Progress.GetCropHarvestCount(cropId);
        if (count >= 100) Achievements.Increment("mastery_crop");
        if (Progress.MainVictory) Achievements.Increment("victory_main");
        if (Progress.PerfectVictory) Achievements.Increment("victory_perfect");
    }

    private void EmitPatchLandsAndBag() =>
        Emit(new WorldPatch("world_patch", Now(), Lands: GetLands(), Bag: Inventory.GetBag()));

    public ActionResult MineEnter()
    {
        lock (_lock)
        {
            if (Player.DistanceTo(Mining.X, Mining.Y) > ItemIds.TileSize * 2)
                return new ActionResult(false, "too far from mine entrance");
            var r = Mining.Enter(Inventory);
            if (r.Ok)
                EmitActivity("enter_mine", Mining.X, Mining.Y, 700, Inventory.GetBag(), null, Player.ToState());
            return new ActionResult(r.Ok, r.Message);
        }
    }

    public ActionResult MineLeave()
    {
        lock (_lock)
        {
            var r = Mining.Leave();
            return new ActionResult(r.Ok, r.Message);
        }
    }

    public ActionResult MineDig()
    {
        lock (_lock)
        {
            if (Upkeep.MineIntegrity < 20)
                return new ActionResult(false, "mine collapsed — reinforce_mine required");
            var r = Mining.Mine(Economy, Inventory);
            if (r.Ok)
                EmitActivity("mine", (int)Player.X, (int)Player.Y, 800, Inventory.GetBag(), null, Player.ToState());
            return new ActionResult(r.Ok, r.Message,
                r.OreId != null ? new Dictionary<string, object?> { ["oreId"] = r.OreId } : null);
        }
    }

    public ActionResult MineChangeLayer(string? direction)
    {
        lock (_lock)
        {
            var delta = direction == "down" ? 1 : direction == "up" ? -1 : 0;
            if (delta == 0) return new ActionResult(false, "direction must be up or down");
            var r = Mining.ChangeLayer(delta);
            return new ActionResult(r.Ok, r.Message,
                new Dictionary<string, object?> { ["layer"] = Mining.CurrentLayer });
        }
    }

    public ActionResult FishCast(string? pondId, string? baitId)
    {
        lock (_lock)
        {
            var pond = Fishing.GetPond(pondId ?? "p01");
            if (pond == null) return new ActionResult(false, "unknown pond");
            if (Player.DistanceTo(pond.X, pond.Y) > ItemIds.TileSize * 3)
                return new ActionResult(false, "too far from water");
            var r = Fishing.Fish(pond.Id, baitId, Economy, Inventory);
            if (r.Ok && r.FishId != null)
                Codex.OnCatch(r.FishId);
            if (r.Ok)
                EmitActivity("fish", pond.X, pond.Y, 900, Inventory.GetBag(), null, Player.ToState());
            return new ActionResult(r.Ok, r.Message,
                r.FishId != null ? new Dictionary<string, object?> { ["fishId"] = r.FishId } : null);
        }
    }

    public ActionResult EatMeal()
    {
        lock (_lock)
        {
            if (!Inventory.RemoveItem(ItemIds.MealFishStew, 1))
                return new ActionResult(false, "need meal_fish_stew");
            Mining.RestoreStamina(40);
            return new ActionResult(true, "stamina restored", new Dictionary<string, object?> { ["stamina"] = Mining.Stamina });
        }
    }

    public ActionResult ForgePickaxe(int tier)
    {
        lock (_lock)
        {
            if (Player.DistanceTo(ItemIds.BlacksmithPos.X, ItemIds.BlacksmithPos.Y) > ItemIds.TileSize * 3)
                return new ActionResult(false, "too far from blacksmith");
            var r = Mining.ForgePickaxe(Inventory, tier);
            if (r.Ok)
                EmitActivity("forge", ItemIds.BlacksmithPos.X, ItemIds.BlacksmithPos.Y, 1000, Inventory.GetBag());
            return new ActionResult(r.Ok, r.Message);
        }
    }

    public ActionResult RefuelLantern()
    {
        lock (_lock)
        {
            var r = Mining.RefuelLantern(Inventory);
            if (r.Ok) Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));
            return new ActionResult(r.Ok, r.Message);
        }
    }

    public ActionResult UpgradeBuilding(string buildingId)
    {
        lock (_lock)
        {
            var r = Buildings.TryUpgrade(buildingId, Inventory);
            if (r.Ok) Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));
            return new ActionResult(r.Ok, r.Message);
        }
    }

    public ActionResult DeliverCrossOrder(string? orderId)
    {
        lock (_lock)
        {
            var r = Orders.DeliverCrossLine(orderId, Inventory);
            if (r.Ok)
            {
                Progress.OnGoldEarned(r.Gold);
                Progress.OnContractComplete();
                Achievements.Increment("order_daily");
                Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));
            }
            return new ActionResult(r.Ok, r.Message,
                r.Ok ? new Dictionary<string, object?> { ["gold"] = r.Gold } : null);
        }
    }

    public ActionResult ReinforceMine()
    {
        lock (_lock)
        {
            var r = Upkeep.ReinforceMine(Inventory);
            if (r.Ok) Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));
            return new ActionResult(r.Ok, r.Message,
                new Dictionary<string, object?> { ["mineIntegrity"] = Upkeep.MineIntegrity });
        }
    }

    public ActionResult RepairBuildings()
    {
        lock (_lock)
        {
            var r = Upkeep.RepairBuildings(Inventory);
            if (r.Ok) Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));
            return new ActionResult(r.Ok, r.Message,
                new Dictionary<string, object?> { ["buildingDurability"] = Upkeep.BuildingDurability });
        }
    }

    public ActionResult FeedPondEcology()
    {
        lock (_lock)
        {
            var r = Upkeep.FeedPondEcology(Inventory);
            if (r.Ok) Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));
            return new ActionResult(r.Ok, r.Message,
                new Dictionary<string, object?> { ["pondEcology"] = Upkeep.PondEcology });
        }
    }

    public ActionResult HybridSeeds(string? seedA, string? seedB)
    {
        lock (_lock)
        {
            if (Player.DistanceTo(Processing.X, Processing.Y) > ItemIds.TileSize * 3)
                return new ActionResult(false, "too far from factory");
            var r = Hybrid.Hybrid(seedA ?? "", seedB ?? "", Inventory);
            if (r.Ok) Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));
            return new ActionResult(r.Ok, r.Message,
                r.SeedId != null ? new Dictionary<string, object?> { ["seedId"] = r.SeedId } : null);
        }
    }

    public ActionResult DeliverWeeklyOrder(string? orderId)
    {
        lock (_lock)
        {
            var r = Orders.DeliverWeekly(orderId, Inventory);
            if (r.Ok)
            {
                Progress.OnGoldEarned(r.Gold);
                Progress.OnContractComplete();
                Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));
            }
            return new ActionResult(r.Ok, r.Message,
                r.Ok ? new Dictionary<string, object?> { ["gold"] = r.Gold } : null);
        }
    }

    public ActionResult DeliverFestivalOrder(string? orderId)
    {
        lock (_lock)
        {
            var r = Orders.DeliverFestival(orderId, Inventory);
            if (r.Ok)
            {
                Progress.OnGoldEarned(r.Gold);
                Achievements.Increment("order_daily");
                Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));
            }
            return new ActionResult(r.Ok, r.Message,
                r.Ok ? new Dictionary<string, object?> { ["gold"] = r.Gold } : null);
        }
    }

    public ActionResult SummonBoss()
    {
        lock (_lock)
        {
            var r = Boss.Summon(Mining, Inventory);
            if (r.Ok) Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));
            return new ActionResult(r.Ok, r.Message);
        }
    }

    public ActionResult AttackBoss()
    {
        lock (_lock)
        {
            var r = Boss.Attack(Mining, Inventory);
            if (r.Ok) Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));
            return new ActionResult(r.Ok, r.Message,
                r.RewardGold != null ? new Dictionary<string, object?> { ["gold"] = r.RewardGold } : null);
        }
    }

    public ActionResult PlaceDecoration(string? decorId)
    {
        lock (_lock)
        {
            var r = Decorations.Place(decorId ?? "", Inventory);
            if (r.Ok) Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));
            return new ActionResult(r.Ok, r.Message);
        }
    }
}
