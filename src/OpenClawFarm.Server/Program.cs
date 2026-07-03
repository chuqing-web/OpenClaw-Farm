using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using OpenClawFarm.Core.Game;
using OpenClawFarm.Core.Models;
using OpenClawFarm.Server.Services;

var JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

PortHelper.EnsureAvailable();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://127.0.0.1:{PortHelper.GamePort}");

var world = new GameWorld();
builder.Services.AddSingleton(world);
builder.Services.AddSingleton<AgentWebSocketHub>();

var app = builder.Build();
var hub = app.Services.GetRequiredService<AgentWebSocketHub>();
hub.Attach(world);
world.Start();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Json(new { ok = true }));

app.MapGet("/agent/state/farm_lands", () => JsonState(world.GetLands()));
app.MapGet("/agent/state/bag", () => JsonState(world.Inventory.GetBag()));
app.MapGet("/agent/state/merchant_price", () => JsonState(world.Merchant.GetPrices()));
app.MapGet("/agent/state/farm_order", () => JsonState(world.DailyOrder.GetState()));
app.MapGet("/agent/state/player", () => JsonState(world.Player.ToState()));
app.MapGet("/agent/state/sell_confirm", () =>
{
    var token = world.IssueSellConfirm();
    return token == null
        ? Results.Json(new { error = "failed", message = "could not issue token" })
        : Results.Json(new { data = token, ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
});

app.MapGet("/agent/state/manual_mode", () =>
    JsonState(new { manualMode = world.ManualMode }));

app.MapPost("/agent/action", async (HttpRequest req) =>
{
    AgentActionBody? body;
    try
    {
        body = await JsonSerializer.DeserializeAsync<AgentActionBody>(req.Body, JsonOptions);
    }
    catch
    {
        return Results.Json(new { success = false, message = "invalid JSON" });
    }

    if (string.IsNullOrEmpty(body?.ActionId))
        return Results.Json(new { success = false, message = "actionId required" });

    world.EnsureAgentControl();
    Console.WriteLine($"[Agent] HTTP action: {body.ActionId}");
    var result = await ActionExecutor.RunAsync(world, body.ActionId, body.Params ?? new JsonParams());
    if (!result.Success)
        Console.WriteLine($"[Agent] HTTP failed: {body.ActionId} -> {result.Message}");

    return Results.Json(new
    {
        success = result.Success,
        message = result.Message,
        extra = result.Extra,
        ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    });
});

app.MapGet("/api/snapshot", () => JsonState(world.GetSnapshot()));

app.MapPost("/api/manual_mode", async (HttpRequest req) =>
{
    var body = await JsonSerializer.DeserializeAsync<ManualModeBody>(req.Body);
    world.SetManualMode(body?.enabled ?? false);
    return Results.Json(new { data = new { manualMode = world.ManualMode }, ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
});

app.MapPost("/api/manual_move", async (HttpRequest req) =>
{
    var body = await JsonSerializer.DeserializeAsync<MoveBody>(req.Body);
    var ok = world.ManualMove(body?.dx ?? 0, body?.dy ?? 0);
    return Results.Json(new { success = ok, player = world.Player.ToState() });
});

app.MapPost("/api/manual_interact", async (HttpRequest req) =>
{
    InteractBody? body = null;
    try { body = await JsonSerializer.DeserializeAsync<InteractBody>(req.Body); } catch { }
    return Results.Json(world.ManualInteract(body?.landId));
});

app.MapPost("/api/select_slot", async (HttpRequest req) =>
{
    var body = await JsonSerializer.DeserializeAsync<SlotBody>(req.Body);
    world.SelectSlot(body?.slot ?? 0);
    return Results.Json(new { success = true, selectedSlot = world.SelectedSlot });
});

app.MapGet("/api/map", () => Results.Json(new
{
    data = new
    {
        width = ItemIds.MapWidth,
        height = ItemIds.MapHeight,
        tiles = WorldMapData.GetTilesFlat(),
    },
    ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
}));

app.UseWebSockets();
app.Map("/ws/agent", hub.HandleAsync);

Console.WriteLine($"OpenClaw Farm running at http://127.0.0.1:{PortHelper.GamePort}");
Console.WriteLine($"Agent WebSocket: ws://127.0.0.1:{PortHelper.GamePort}/ws/agent");

app.Lifetime.ApplicationStopping.Register(() => world.Stop());
app.Run();

static IResult JsonState<T>(T data) =>
    Results.Json(new { data, ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });

record ManualModeBody(bool? enabled);
record MoveBody(int? dx, int? dy);
record InteractBody(string? landId);
record SlotBody(int? slot);
record AgentActionBody(string? ActionId, JsonParams? Params);
