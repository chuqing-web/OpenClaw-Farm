namespace OpenClawFarm.Core.Models;

public record LandState(
    string Id,
    int X,
    int Y,
    string State,
    string? CropId,
    double Growth,
    bool NeedsWater,
    bool CanHarvest,
    bool IsDry = false,
    bool HasPest = false,
    bool HasFrost = false,
    int Fertility = 100,
    bool IsGreenhouse = false,
    string? LastCropId = null);

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
    MerchantInfo Merchant,
    int MapWidth,
    int MapHeight,
    string[][] Tiles,
    int GameHour,
    WellInfo Well);

public record AgentActionState(
    bool Busy,
    string? CurrentAction,
    long? StartedAt,
    string? LastMessage,
    string? NextHint,
    string? NextActionId,
    Dictionary<string, object?>? NextParams);

public record ActionResult(bool Success, string Message, Dictionary<string, object?>? Extra = null);

public record MoveToParams(int X, int Y, string? SceneId = null);

public record InteractParams(string TargetEntityId, string? ItemId = null);

public record SellItemParams(string ItemId, int? Count = null, string? ConfirmToken = null, string? MerchantId = null);

public record WaitParams(int Ms);

public record SellConfirmResult(string ConfirmToken, int ExpiresIn);

public record ActivityEvent(string Kind, int X, int Y, int DurationMs, string? Facing = null);

public record TileUpdate(int Tx, int Ty, string Type);

public record WorldPatch(
    string Type,
    long Ts,
    PlayerState? Player = null,
    List<LandState>? Lands = null,
    BagState? Bag = null,
    int? GameHour = null,
    ActivityEvent? Activity = null,
    List<TileUpdate>? TileUpdates = null);

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
    public string? RewardId { get; init; }
    public string? BuildingId { get; init; }
    public int? Layer { get; init; }
    public int? Page { get; init; }
    public string? PondId { get; init; }
    public string? Direction { get; init; }
    public string? OrderId { get; init; }
    public string? MerchantId { get; init; }
    public string? SeedA { get; init; }
    public string? SeedB { get; init; }
    public string? DecorationId { get; init; }
    public int? TileX { get; init; }
    public int? TileY { get; init; }
    public string? BuildType { get; init; }
}

public record ActionResultMessage(
    string Type,
    string ReqId,
    bool Success,
    string Message,
    Dictionary<string, object?>? Extra = null);
