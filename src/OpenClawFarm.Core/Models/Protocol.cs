namespace OpenClawFarm.Core.Models;

public record LandState(
    string Id,
    int X,
    int Y,
    string State,
    string? CropId,
    double Growth,
    bool NeedsWater,
    bool CanHarvest);

public record BagItem(string ItemId, int Count);

public record BagState(int Gold, List<BagItem> Items);

public record FarmOrder(string CropId, int Required, int Delivered, int Reward);

public record FarmOrderState(List<FarmOrder> Orders, string ExpiresAt);

public record PlayerState(int X, int Y, string SceneId, string? Facing = "down");

public record MerchantInfo(string Id, int X, int Y, string SceneId);

public record WellInfo(string Id, int X, int Y);

public record WorldSnapshot(
    PlayerState Player,
    List<LandState> Lands,
    BagState Bag,
    Dictionary<string, int> MerchantPrices,
    FarmOrderState FarmOrder,
    bool ManualMode,
    MerchantInfo Merchant,
    int MapWidth,
    int MapHeight,
    string[][] Tiles,
    int GameHour,
    int SelectedSlot,
    WellInfo Well);

public record ActionResult(bool Success, string Message, Dictionary<string, object?>? Extra = null);

public record MoveToParams(int X, int Y, string? SceneId = null);

public record InteractParams(string TargetEntityId, string? ItemId = null);

public record SellItemParams(string ItemId, int? Count = null, string? ConfirmToken = null);

public record WaitParams(int Ms);

public record SellConfirmResult(string ConfirmToken, int ExpiresIn);

public record WorldPatch(
    string Type,
    long Ts,
    PlayerState? Player = null,
    List<LandState>? Lands = null,
    BagState? Bag = null,
    bool? ManualMode = null,
    int? SelectedSlot = null,
    int? GameHour = null);

public record ActionMessage(string Type, string ReqId, ActionPayload Payload);

public record ActionPayload(string ActionId, JsonParams Params);

public record JsonParams
{
    public int? X { get; init; }
    public int? Y { get; init; }
    public string? SceneId { get; init; }
    public string? TargetEntityId { get; init; }
    public string? ItemId { get; init; }
    public int? Count { get; init; }
    public string? ConfirmToken { get; init; }
    public int? Ms { get; init; }
}

public record ActionResultMessage(
    string Type,
    string ReqId,
    bool Success,
    string Message,
    Dictionary<string, object?>? Extra = null);
