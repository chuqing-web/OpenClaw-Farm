using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public static class Hotbar
{
    public const int SlotCount = 8;

    public static readonly string?[] DefaultSlots =
    [
        ItemIds.ToolWateringCan,
        ItemIds.SeedStrawberry,
        ItemIds.SeedWheat,
        ItemIds.SeedCarrot,
        ItemIds.SeedCorn,
        ItemIds.SeedPumpkin,
        ItemIds.ToolSickle,
        ItemIds.ToolHoe,
    ];

    public static string? GetItem(int slot) =>
        slot >= 0 && slot < SlotCount ? DefaultSlots[slot] : null;

    public static string DisplayName(string? itemId) => itemId switch
    {
        ItemIds.ToolWateringCan => "洒水壶",
        ItemIds.ToolSickle => "镰刀",
        ItemIds.ToolHoe => "锄头",
        ItemIds.SeedStrawberry => "草莓种",
        ItemIds.SeedWheat => "小麦种",
        ItemIds.SeedCarrot => "胡萝卜种",
        ItemIds.SeedCorn => "玉米种",
        ItemIds.SeedPumpkin => "南瓜种",
        "sell" => "出售",
        _ => itemId ?? "空",
    };
}
