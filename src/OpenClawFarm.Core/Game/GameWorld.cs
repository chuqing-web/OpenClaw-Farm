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

    public Player Player { get; }
    public Inventory Inventory { get; }
    public Merchant Merchant { get; }
    public DailyOrder DailyOrder { get; }
    public Dictionary<string, FarmLand> Lands { get; } = new();
    public bool ManualMode { get; private set; } = true;
    public int SelectedSlot { get; private set; }
    public Well Well { get; }

    public event Action<WorldPatch>? OnPatch;

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
            new BagItem(ItemIds.ToolHoe, 1),
            new BagItem(ItemIds.ToolSickle, 1),
        ]);
        var (mx, my) = ItemIds.MerchantPos;
        Merchant = new Merchant(ItemIds.MerchantId, mx, my, "farm_main");
        var (wx, wy) = ItemIds.WellPos;
        Well = new Well(ItemIds.WellId, wx, wy);
        DailyOrder = new DailyOrder();
        foreach (var (id, tx, ty) in ItemIds.LandLayout)
            Lands[id] = new FarmLand(id, tx, ty);
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
                land.Tick();
            _tickCount++;
            if (_tickCount % 3 == 0)
            {
                _gameHour = (_gameHour + 1) % 24;
                Emit(new WorldPatch("world_patch", Now(), GameHour: _gameHour));
            }
            else
            {
                Emit(new WorldPatch("world_patch", Now(), Lands: GetLands()));
            }
        }
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private void Emit(WorldPatch patch) => OnPatch?.Invoke(patch);

    public List<LandState> GetLands() => Lands.Values.Select(l => l.ToState()).ToList();

    public WorldSnapshot GetSnapshot() => new(
        Player.ToState(),
        GetLands(),
        Inventory.GetBag(),
        Merchant.GetPrices(),
        DailyOrder.GetState(),
        ManualMode,
        new MerchantInfo(Merchant.Id, Merchant.X, Merchant.Y, Merchant.SceneId),
        ItemIds.MapWidth,
        ItemIds.MapHeight,
        WorldMapData.GetTilesFlat(),
        _gameHour,
        SelectedSlot,
        new WellInfo(Well.Id, Well.X, Well.Y));

    public void SetManualMode(bool enabled)
    {
        lock (_lock)
        {
            ManualMode = enabled;
            Emit(new WorldPatch("world_patch", Now(), ManualMode: enabled));
        }
    }

    /// <summary>Agent 发动作时自动切到 Agent 模式，否则 move_to 等会返回 manual_mode。</summary>
    public void EnsureAgentControl()
    {
        if (ManualMode)
            SetManualMode(false);
    }

    public void SelectSlot(int slot)
    {
        lock (_lock)
        {
            SelectedSlot = Math.Clamp(slot, 0, Hotbar.SlotCount - 1);
            Emit(new WorldPatch("world_patch", Now(), SelectedSlot: SelectedSlot));
        }
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
        var jitter = 150 + Random.Shared.Next(300);
        var elapsed = Environment.TickCount64 - _lastActionAt;
        var wait = Math.Max(0, 500 + jitter - elapsed);
        if (wait > 0) await Task.Delay((int)wait);
        _lastActionAt = Environment.TickCount64;
    }

    public async Task<ActionResult> MoveToAsync(MoveToParams p)
    {
        if (ManualMode) return new ActionResult(false, "manual_mode");
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
            await Task.Delay(60);
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
        if (ManualMode) return new ActionResult(false, "manual_mode");
        await ApplyCooldownAsync();
        lock (_lock) { return InteractCore(p, false); }
    }

    public async Task<ActionResult> SellItemAsync(SellItemParams p) =>
        await SellItemInternalAsync(p, requireManualOff: true);

    private async Task<ActionResult> SellItemInternalAsync(SellItemParams p, bool requireManualOff)
    {
        if (requireManualOff && ManualMode) return new ActionResult(false, "manual_mode");
        await ApplyCooldownAsync();
        lock (_lock) { return SellItemCore(p); }
    }

    public async Task<ActionResult> WaitAsync(WaitParams p)
    {
        if (ManualMode) return new ActionResult(false, "manual_mode");
        var ms = Math.Clamp(p.Ms, 0, 30_000);
        await Task.Delay(ms);
        return new ActionResult(true, $"waited {ms}ms");
    }

    public bool ManualMove(int dx, int dy)
    {
        lock (_lock)
        {
            if (dx == 0 && dy == 0) return false;
            // 一次只走一格（禁止斜向瞬移穿墙）
            if (dx != 0 && dy != 0)
            {
                if (Math.Abs(dy) >= Math.Abs(dx)) dx = 0;
                else dy = 0;
            }

            var tx = (int)Math.Floor(Player.X / ItemIds.TileSize) + dx;
            var ty = (int)Math.Floor(Player.Y / ItemIds.TileSize) + dy;
            if (!WorldMapData.IsWalkable(tx, ty)) return false;
            var facing = dy < 0 ? "up" : dy > 0 ? "down" : dx < 0 ? "left" : "right";
            Player.SetPosition(tx * ItemIds.TileSize + ItemIds.TileSize / 2,
                ty * ItemIds.TileSize + ItemIds.TileSize / 2, facing: facing);
            Emit(new WorldPatch("world_patch", Now(), Player: Player.ToState()));
            return true;
        }
    }

    public ActionResult ManualInteract(string? landId = null)
    {
        lock (_lock)
        {
            if (Player.DistanceTo(Well.X, Well.Y) <= ItemIds.TileSize * 1.8)
                return Well.Interact();

            if (Player.DistanceTo(Merchant.X, Merchant.Y) <= ItemIds.MerchantInteractRange)
                return ManualSellAll();

            FarmLand? target = null;
            if (landId != null && Lands.TryGetValue(landId, out var byId))
                target = byId;
            else
            {
                var nearestDist = double.MaxValue;
                foreach (var land in Lands.Values)
                {
                    var d = Player.DistanceTo(land.X, land.Y);
                    if (d < nearestDist && d <= ItemIds.TileSize * 1.6)
                    {
                        nearestDist = d;
                        target = land;
                    }
                }
            }

            if (target == null)
                return new ActionResult(false, "附近没有可交互的目标");

            return ApplyHotbarToLand(target);
        }
    }

    private ActionResult ApplyHotbarToLand(FarmLand land)
    {
        var tool = Hotbar.GetItem(SelectedSlot);

        if (land.CanHarvest)
            return InteractCore(new InteractParams(land.Id, "harvest"), true);
        if (land.NeedsWater || land.State == "needs_water")
            return InteractCore(new InteractParams(land.Id, "water"), true);
        if (land.State == "withered")
            return InteractCore(new InteractParams(land.Id, "clear"), true);
        if (land.State == "empty")
        {
            if (tool != null && tool.StartsWith("seed_", StringComparison.Ordinal))
                return InteractCore(new InteractParams(land.Id, tool), true);
            return new ActionResult(false, "选择种子（1-5）后播种");
        }

        if (tool == ItemIds.ToolWateringCan)
            return InteractCore(new InteractParams(land.Id, "water"), true);
        if (tool == ItemIds.ToolSickle)
            return InteractCore(new InteractParams(land.Id, "harvest"), true);
        return new ActionResult(false, "当前工具无法操作这块地");
    }

    private ActionResult ManualSellAll()
    {
        var total = 0;
        var sold = 0;
        foreach (var crop in ItemIds.CropBasePrices.Keys.ToList())
        {
            var count = Inventory.GetCount(crop);
            if (count <= 0) continue;
            var price = Merchant.GetPrice(crop);
            Inventory.RemoveItem(crop, count);
            var earn = price * count;
            total += earn;
            sold += count;
            DailyOrder.OnSell(crop, count);
        }
        if (sold == 0) return new ActionResult(false, "背包里没有可出售的作物");
        Inventory.AddGold(total);
        Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));
        return new ActionResult(true, $"出售了 {sold} 件作物，获得 {total} 金币");
    }

    private ActionResult SellItemCore(SellItemParams p)
    {
        if (Player.DistanceTo(Merchant.X, Merchant.Y) > ItemIds.MerchantInteractRange)
            return new ActionResult(false, "too far from merchant");

        if (!Merchant.IsCrop(p.ItemId))
            return new ActionResult(false, $"{p.ItemId} is not sellable");

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

        var price = Merchant.GetPrice(p.ItemId);
        var total = price * count;
        Inventory.RemoveItem(p.ItemId, count);
        Inventory.AddGold(total);
        var orderBonus = DailyOrder.OnSell(p.ItemId, count);
        if (orderBonus > 0) Inventory.AddGold(orderBonus);
        _sellConfirmToken = null;
        Emit(new WorldPatch("world_patch", Now(), Bag: Inventory.GetBag()));

        return new ActionResult(true,
            $"sold {count}x {p.ItemId} for {total} gold" + (orderBonus > 0 ? $" (+{orderBonus} bonus)" : ""),
            new Dictionary<string, object?> { ["gold"] = Inventory.Gold, ["earned"] = total + orderBonus });
    }

    private ActionResult InteractCore(InteractParams p, bool skipManualCheck)
    {
        if (!skipManualCheck && ManualMode)
            return new ActionResult(false, "manual_mode");

        if (p.TargetEntityId == Merchant.Id)
            return new ActionResult(false, "use sell_item for merchant");

        if (!Lands.TryGetValue(p.TargetEntityId, out var land))
            return new ActionResult(false, $"unknown target: {p.TargetEntityId}");

        if (Player.DistanceTo(land.X, land.Y) > ItemIds.TileSize * 1.6)
            return new ActionResult(false, $"too far from {p.TargetEntityId}");

        var itemId = p.ItemId;

        if (itemId != null && itemId.StartsWith("seed_", StringComparison.Ordinal))
        {
            if (Inventory.GetCount(itemId) < 1)
                return new ActionResult(false, $"no {itemId} in bag");
            var r = land.Plant(itemId);
            if (!r.Ok) return new ActionResult(false, r.Message);
            Inventory.RemoveItem(itemId, 1);
            EmitPatchLandsAndBag();
            return new ActionResult(true, r.Message);
        }

        if (itemId == "harvest" || (itemId == null && land.CanHarvest))
        {
            var r = land.Harvest();
            if (!r.Ok) return new ActionResult(false, r.Message);
            if (r.CropId != null) Inventory.AddItem(r.CropId, 1);
            EmitPatchLandsAndBag();
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
            Emit(new WorldPatch("world_patch", Now(), Lands: GetLands()));
            return new ActionResult(true, r.Message);
        }

        if (land.CanHarvest)
        {
            var r = land.Harvest();
            if (!r.Ok) return new ActionResult(false, r.Message);
            if (r.CropId != null) Inventory.AddItem(r.CropId, 1);
            EmitPatchLandsAndBag();
            return new ActionResult(true, r.Message);
        }

        if (land.NeedsWater || land.State == "needs_water")
        {
            var r = land.Water();
            if (!r.Ok) return new ActionResult(false, r.Message);
            Emit(new WorldPatch("world_patch", Now(), Lands: GetLands()));
            return new ActionResult(true, r.Message);
        }

        return new ActionResult(false, $"no valid interact for {p.TargetEntityId}");
    }

    private void EmitPatchLandsAndBag() =>
        Emit(new WorldPatch("world_patch", Now(), Lands: GetLands(), Bag: Inventory.GetBag()));
}
