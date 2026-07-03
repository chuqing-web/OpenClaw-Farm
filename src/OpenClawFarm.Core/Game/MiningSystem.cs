using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

/// <summary>多层矿井：体力、镐耐久、层数越深产出越好。</summary>
public sealed class MiningSystem
{
    public const int MaxLayer = 3;
    private static readonly Dictionary<int, (string[] Ores, int StaminaCost, int PickReq)> LayerTable = new()
    {
        [1] = ([OreStone, OreIron], 8, 0),
        [2] = ([OreIron, OreSilver, OreGold], 14, 1),
        [3] = ([OreSilver, OreGold, OreCrystal, CropMushroom], 22, 2),
    };

    private const string OreStone = ItemIds.OreStone;
    private const string OreIron = ItemIds.OreIron;
    private const string OreSilver = ItemIds.OreSilver;
    private const string OreGold = ItemIds.OreGold;
    private const string OreCrystal = ItemIds.OreCrystal;
    private const string CropMushroom = ItemIds.CropMushroom;

    public int X { get; } = ItemIds.MineEntrancePos.X;
    public int Y { get; } = ItemIds.MineEntrancePos.Y;
    public int CurrentLayer { get; private set; } = 1;
    public int Stamina { get; private set; } = 100;
    public int PickaxeTier { get; private set; }
    public int PickaxeDurability { get; private set; } = 100;
    public int LanternFuel { get; private set; } = 50;
    public bool InMine { get; private set; }
    public int VeinBonusTicks { get; private set; }

    public (bool Ok, string Message, string? OreId) Mine(EconomySystem economy, Inventory inv)
    {
        if (!InMine) return (false, "enter mine first (mine_enter)", null);
        if (Stamina < LayerTable[CurrentLayer].StaminaCost)
            return (false, "stamina depleted — eat meal or leave mine", null);

        if (PickaxeDurability <= 0)
            return (false, "pickaxe broken — forge new pickaxe", null);

        if (PickaxeTier < LayerTable[CurrentLayer].PickReq)
            return (false, $"layer {CurrentLayer} needs pickaxe tier {LayerTable[CurrentLayer].PickReq}", null);

        if (LanternFuel <= 0 && CurrentLayer >= 2)
            return (false, "lantern fuel empty — need crop_charcoal", null);

        Stamina -= LayerTable[CurrentLayer].StaminaCost;
        PickaxeDurability -= CurrentLayer * 3 + 2;
        if (CurrentLayer >= 2) LanternFuel -= 2;

        var ores = LayerTable[CurrentLayer].Ores;
        var ore = ores[Random.Shared.Next(ores.Length)];
        if (VeinBonusTicks > 0 && Random.Shared.NextDouble() < 0.35)
        {
            ore = CurrentLayer >= 3 ? OreCrystal : OreGold;
            VeinBonusTicks--;
        }

        var yield = Random.Shared.NextDouble() < economy.GetYieldMultiplier("mine") ? 1 : 0;
        if (yield == 0) return (true, "vein exhausted this swing", null);

        if (!inv.TryAddItem(ore, 1))
            return (false, "ore storage full", null);

        economy.RecordActivity("mine");
        return (true, $"mined {ore}", ore);
    }

    public (bool Ok, string Message) Enter(Inventory inv)
    {
        if (InMine) return (false, "already in mine");
        if (inv.GetCount(ItemIds.CropWheat) < 1 && inv.GetCount(ItemIds.CropCorn) < 1)
            return (false, "need crop food to enter mine");
        if (inv.GetCount(ItemIds.CropWheat) >= 1) inv.RemoveItem(ItemIds.CropWheat, 1);
        else inv.RemoveItem(ItemIds.CropCorn, 1);

        InMine = true;
        CurrentLayer = 1;
        return (true, "entered mine layer 1");
    }

    public (bool Ok, string Message) Leave()
    {
        if (!InMine) return (false, "not in mine");
        InMine = false;
        return (true, "left mine");
    }

    public (bool Ok, string Message) ChangeLayer(int delta)
    {
        if (!InMine) return (false, "not in mine");
        var next = CurrentLayer + delta;
        if (next < 1 || next > MaxLayer) return (false, "invalid layer");
        if (delta > 0 && Stamina < 15) return (false, "not enough stamina to descend");
        CurrentLayer = next;
        if (delta > 0) Stamina -= 10;
        if (Random.Shared.NextDouble() < 0.15) VeinBonusTicks = 5;
        return (true, $"now at layer {CurrentLayer}");
    }

    public (bool Ok, string Message) RefuelLantern(Inventory inv)
    {
        if (inv.GetCount(ItemIds.CropCharcoal) < 2)
            return (false, "need 2x crop_charcoal");
        inv.RemoveItem(ItemIds.CropCharcoal, 2);
        LanternFuel = Math.Min(100, LanternFuel + 40);
        return (true, "lantern refueled");
    }

    public (bool Ok, string Message) ForgePickaxe(Inventory inv, int tier)
    {
        if (tier == 1)
        {
            if (inv.GetCount(ItemIds.IngotIron) < 2 || inv.GetCount(ItemIds.CropCharcoal) < 3)
                return (false, "need 2 ingot_iron + 3 crop_charcoal");
            inv.RemoveItem(ItemIds.IngotIron, 2);
            inv.RemoveItem(ItemIds.CropCharcoal, 3);
        }
        else
        {
            if (inv.GetCount(ItemIds.IngotSilver) < 2 || inv.GetCount(ItemIds.IngotIron) < 3 || inv.GetCount(ItemIds.CropCharcoal) < 5)
                return (false, "need 2 ingot_silver + 3 ingot_iron + 5 crop_charcoal");
            inv.RemoveItem(ItemIds.IngotSilver, 2);
            inv.RemoveItem(ItemIds.IngotIron, 3);
            inv.RemoveItem(ItemIds.CropCharcoal, 5);
        }
        PickaxeTier = tier;
        PickaxeDurability = 100;
        return (true, $"forged tier {tier} pickaxe");
    }

    public void RestoreStamina(int amount) => Stamina = Math.Clamp(Stamina + amount, 0, 100);

    public MineState ToState(int? layerFilter = null) => new(
        InMine, layerFilter ?? CurrentLayer, Stamina, PickaxeTier, PickaxeDurability,
        LanternFuel, VeinBonusTicks, X, Y);

    public MiningSaveData Export() => new(
        CurrentLayer, Stamina, PickaxeTier, PickaxeDurability, LanternFuel, InMine, VeinBonusTicks);

    public void Restore(MiningSaveData? data)
    {
        CurrentLayer = 1; Stamina = 100; PickaxeTier = 0; PickaxeDurability = 100;
        LanternFuel = 50; InMine = false; VeinBonusTicks = 0;
        if (data == null) return;
        CurrentLayer = data.Layer;
        Stamina = data.Stamina;
        PickaxeTier = data.PickaxeTier;
        PickaxeDurability = data.PickaxeDurability;
        LanternFuel = data.LanternFuel;
        InMine = data.InMine;
        VeinBonusTicks = data.VeinBonusTicks;
    }
}
