using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

/// <summary>三线强制消耗：日常维护、自然损耗、建筑/矿道耐久。</summary>
public sealed class UpkeepSystem
{
    public int MineIntegrity { get; private set; } = 100;
    public int BuildingDurability { get; private set; } = 100;
    public int PondEcology { get; private set; } = 100;
    public int MaintenanceDebt { get; private set; }
    public List<string> RecentLog { get; } = [];

    private const int MaxLog = 8;

    public void Log(string msg)
    {
        RecentLog.Insert(0, msg);
        if (RecentLog.Count > MaxLog) RecentLog.RemoveAt(RecentLog.Count - 1);
    }

    /// <summary>每游戏日 0 点执行刚性消耗。</summary>
    public void RunDaily(
        Inventory inv,
        BuildingSystem buildings,
        LivestockManager livestock,
        MiningSystem mining,
        IEnumerable<FarmLand> lands,
        int gameDay)
    {
        PayLivestockFeed(inv, livestock);
        PayGreenhouseHeating(inv, lands);
        PayPondEcology(inv);
        PayBuildingMaintenance(inv, buildings);
        PayLandUpkeep(inv, lands);
        DecayMineIntegrity(mining);
        ApplyPassiveDecay(inv);
        MaintenanceDebt = Math.Max(0, MaintenanceDebt - 1);
        if (gameDay % 7 == 0) PayWeeklyOverhead(inv, buildings);
    }

    /// <summary>每 tick 周期消耗（自动化设备运转）。</summary>
    public bool PayAutoSprinklerFuel(Inventory inv) =>
        TryConsume(inv, ItemIds.CropCarrot, 1, "auto_sprinkler fuel");

    public bool PayAutoHarvesterFuel(Inventory inv) =>
        TryConsume(inv, ItemIds.CropWheat, 1, "auto_harvester fuel");

    public (bool Ok, string Message) ReinforceMine(Inventory inv)
    {
        if (MineIntegrity >= 80) return (false, "mine integrity sufficient");
        if (!TryConsume(inv, ItemIds.OreStone, 5, "mine reinforce stone")) return (false, "need 5 ore_stone");
        if (!TryConsume(inv, ItemIds.CropCharcoal, 2, "mine reinforce charcoal")) return (false, "need 2 crop_charcoal");
        MineIntegrity = Math.Min(100, MineIntegrity + 35);
        Log("reinforced mine tunnels");
        return (true, $"mine integrity now {MineIntegrity}");
    }

    public (bool Ok, string Message) RepairBuildings(Inventory inv)
    {
        if (BuildingDurability >= 70) return (false, "buildings ok");
        if (!TryConsume(inv, ItemIds.IngotIron, 2, "building repair")) return (false, "need 2 ingot_iron");
        BuildingDurability = Math.Min(100, BuildingDurability + 40);
        Log("repaired farm buildings");
        return (true, $"building durability {BuildingDurability}");
    }

    public (bool Ok, string Message) FeedPondEcology(Inventory inv)
    {
        if (PondEcology >= 60) return (false, "pond ecology ok");
        if (!TryConsume(inv, ItemIds.FishCommon, 2, "pond ecology")) return (false, "need 2 fish_common");
        PondEcology = Math.Min(100, PondEcology + 30);
        Log("fed pond ecology");
        return (true, $"pond ecology {PondEcology}");
    }

    private void PayLivestockFeed(Inventory inv, LivestockManager livestock)
    {
        foreach (var animal in livestock.Animals)
        {
            var feed = animal.Type switch
            {
                "chicken" => ItemIds.CropWheat,
                "cow" => ItemIds.CropCorn,
                _ => ItemIds.CropWheat,
            };
            if (TryConsume(inv, feed, 1, $"feed {animal.Type}"))
                animal.AutoFeed();
            else
            {
                animal.Starve();
                MaintenanceDebt++;
                Log($"livestock {animal.Id} unfed");
            }
        }
    }

    private void PayGreenhouseHeating(Inventory inv, IEnumerable<FarmLand> lands)
    {
        if (!lands.Any(l => l.IsGreenhouse)) return;
        if (!TryConsume(inv, ItemIds.CropCharcoal, 2, "greenhouse heating"))
        {
            MaintenanceDebt += 2;
            Log("greenhouse heating unpaid — fertility penalty");
            foreach (var l in lands.Where(l => l.IsGreenhouse))
                l.ApplyHeatingPenalty();
        }
    }

    private void PayPondEcology(Inventory inv)
    {
        PondEcology = Math.Max(0, PondEcology - 12);
        if (PondEcology < 50 && !TryConsume(inv, ItemIds.FishCommon, 1, "pond ecology daily"))
        {
            MaintenanceDebt++;
            Log("pond ecology declining");
        }
    }

    private void PayBuildingMaintenance(Inventory inv, BuildingSystem buildings)
    {
        BuildingDurability = Math.Max(0, BuildingDurability - 8);
        var unlocked = buildings.All.Count(kv => kv.Value.Unlocked);
        if (unlocked <= 2) return;

        if (!TryConsume(inv, ItemIds.IngotIron, 1, "building maintenance"))
        {
            if (!TryConsume(inv, ItemIds.OreIron, 3, "building maintenance ore"))
            {
                MaintenanceDebt += 3;
                Log("building maintenance skipped");
            }
        }
    }

    private void PayLandUpkeep(Inventory inv, IEnumerable<FarmLand> lands)
    {
        var active = lands.Count(l => l.State != "empty");
        var fee = Math.Max(1, active / 6);
        if (!TryConsume(inv, ItemIds.CropWheat, fee, "land upkeep"))
            MaintenanceDebt += fee;
    }

    private void PayWeeklyOverhead(Inventory inv, BuildingSystem buildings)
    {
        if (buildings.All["auto_sprinkler"].Unlocked)
            TryConsume(inv, ItemIds.CropCharcoal, 3, "weekly sprinkler service");
        if (buildings.All["auto_harvester"].Unlocked)
            TryConsume(inv, ItemIds.CropCorn, 5, "weekly harvester service");
    }

    private void DecayMineIntegrity(MiningSystem mining)
    {
        if (mining.InMine || mining.CurrentLayer > 1)
            MineIntegrity = Math.Max(0, MineIntegrity - 10);
        if (MineIntegrity < 30)
            Log("mine integrity critical — reinforce required");
    }

    private void ApplyPassiveDecay(Inventory inv)
    {
        foreach (var fish in new[] { ItemIds.FishCommon, ItemIds.FishMedium, ItemIds.FishRare, ItemIds.FishGlow })
        {
            var n = inv.GetCount(fish);
            if (n > 0) inv.RemoveItem(fish, Math.Max(1, (int)(n * 0.12)));
        }
        foreach (var ore in new[] { ItemIds.IngotIron, ItemIds.IngotSilver, ItemIds.OreCrystal })
        {
            var n = inv.GetCount(ore);
            if (n > 8) inv.RemoveItem(ore, Math.Max(1, n / 25));
        }
        foreach (var crop in new[] { ItemIds.CropStrawberry, ItemIds.CropCarrot, ItemIds.CropPumpkin })
        {
            var n = inv.GetCount(crop);
            if (n > 20) inv.RemoveItem(crop, Math.Max(1, n / 15));
        }
    }

    private bool TryConsume(Inventory inv, string itemId, int count, string reason)
    {
        if (inv.GetCount(itemId) < count) return false;
        inv.RemoveItem(itemId, count);
        Log($"-{count} {itemId} ({reason})");
        return true;
    }

    public UpkeepState ToState() => new(
        MineIntegrity, BuildingDurability, PondEcology, MaintenanceDebt,
        RecentLog.ToList());

    public UpkeepSaveData Export() => new(MineIntegrity, BuildingDurability, PondEcology, MaintenanceDebt);

    public void Restore(UpkeepSaveData? data)
    {
        MineIntegrity = BuildingDurability = PondEcology = 100;
        MaintenanceDebt = 0;
        if (data == null) return;
        MineIntegrity = data.MineIntegrity;
        BuildingDurability = data.BuildingDurability;
        PondEcology = data.PondEcology;
        MaintenanceDebt = data.MaintenanceDebt;
    }
}
