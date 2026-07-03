using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class MineBossSystem
{
    public const int MaxHp = 250;
    public bool Active { get; private set; }
    public int BossHp { get; private set; }
    public int DefeatedCount { get; private set; }

    public (bool Ok, string Message) Summon(MiningSystem mining, Inventory inv)
    {
        if (!mining.InMine || mining.CurrentLayer < 3)
            return (false, "must be in mine layer 3");
        if (Active) return (false, "boss already active");

        if (inv.GetCount(ItemIds.IngotIron) < 5 || inv.GetCount(ItemIds.OreCrystal) < 3)
            return (false, "need 5 ingot_iron + 3 ore_crystal to summon");

        inv.RemoveItem(ItemIds.IngotIron, 5);
        inv.RemoveItem(ItemIds.OreCrystal, 3);
        Active = true;
        BossHp = MaxHp;
        return (true, "mine boss summoned");
    }

    public (bool Ok, string Message, int? RewardGold) Attack(MiningSystem mining, Inventory inv)
    {
        if (!Active) return (false, "no active boss", null);
        if (mining.Stamina < 15) return (false, "stamina too low", null);
        if (mining.PickaxeDurability <= 0) return (false, "pickaxe broken", null);

        mining.RestoreStamina(-15);
        var dmg = 18 + mining.PickaxeTier * 12;
        BossHp = Math.Max(0, BossHp - dmg);

        if (BossHp > 0)
            return (true, $"hit boss for {dmg} ({BossHp} hp left)", null);

        Active = false;
        DefeatedCount++;
        inv.AddItem(ItemIds.OreCrystal, 2);
        inv.AddItem(ItemIds.OreGold, 3);
        inv.AddGold(800);
        return (true, "boss defeated +800g + loot", 800);
    }

    public MineBossState ToState() => new(Active, BossHp, MaxHp, DefeatedCount);

    public MineBossSaveData Export() => new(Active, BossHp, DefeatedCount);

    public void Restore(MineBossSaveData? data)
    {
        Active = false;
        BossHp = 0;
        DefeatedCount = 0;
        if (data == null) return;
        Active = data.Active;
        BossHp = data.BossHp;
        DefeatedCount = data.DefeatedCount;
    }
}
