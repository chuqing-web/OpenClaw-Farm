using System.Text.Json;
using OpenClawFarm.Core.Game;
using OpenClawFarm.Core.Models;
using OpenClawFarm.Server.Services;

namespace OpenClawFarm.Server;

internal static class GameEntry
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly string GameUrl = $"http://127.0.0.1:{PortHelper.GamePort}";

    [STAThread]
    public static int Main(string[] args)
    {
        if (OperatingSystem.IsWindows())
            ConsoleWindow.HideIfPresent();

        ApplicationConfiguration.Initialize();
        PortHelper.EnsureAvailable();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(GameUrl);

        var world = new GameWorld();
        var saveService = new SaveGameService(world);
        builder.Services.AddSingleton(world);
        builder.Services.AddSingleton(saveService);
        builder.Services.AddSingleton<AgentWebSocketHub>();

        var app = builder.Build();
        var hub = app.Services.GetRequiredService<AgentWebSocketHub>();
        hub.Attach(world);
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

        GameTrayIcon tray;
        ViewerLifecycle viewerLifecycle;
        Task? webTask = null;

        tray = new GameTrayIcon(GameUrl, () =>
        {
            if (!lifetime.ApplicationStopping.IsCancellationRequested)
                lifetime.StopApplication();
        });

        viewerLifecycle = new ViewerLifecycle(() =>
        {
            if (!lifetime.ApplicationStopping.IsCancellationRequested)
                lifetime.StopApplication();
            tray.RequestExit();
        });

        ConfigureRoutes(app, world, saveService, hub, viewerLifecycle);

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(600);
                BrowserLauncher.Open(GameUrl);
                tray.ShowRunningHint();
            });
        });

        app.Lifetime.ApplicationStopping.Register(() =>
        {
            if (world.SessionActive)
                saveService.Save();
            world.Stop();
            tray.RequestExit();
        });

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try { lifetime.StopApplication(); } catch { /* 进程退出中 */ }
        };

        webTask = Task.Run(() => app.Run());

        try
        {
            tray.RunMessageLoop();
        }
        finally
        {
            lifetime.StopApplication();
            try { webTask?.Wait(TimeSpan.FromSeconds(8)); } catch { /* 超时强制结束 */ }
            viewerLifecycle.Dispose();
            tray.Dispose();
            PortHelper.Shutdown();
        }

        return 0;
    }

    private static void ConfigureRoutes(
        WebApplication app,
        GameWorld world,
        SaveGameService saveService,
        AgentWebSocketHub hub,
        ViewerLifecycle viewerLifecycle)
    {
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapGet("/health", () => Results.Json(new { ok = true }));

        app.MapPost("/api/quit", () =>
        {
            viewerLifecycle.TriggerShutdown();
            return Results.Json(new { ok = true });
        });

        app.MapGet("/api/save/info", () => JsonState(saveService.GetInfo()));

        app.MapPost("/api/game/new", () =>
        {
            world.BeginNewSession();
            saveService.Save();
            return Results.Json(new { success = true, message = "new game started" });
        });

        app.MapPost("/api/game/load", () =>
        {
            var save = saveService.Load();
            if (save == null)
                return Results.Json(new { success = false, message = "no save file" });

            world.BeginLoadedSession(save);
            return Results.Json(new { success = true, message = "game loaded" });
        });

        app.MapPost("/api/game/save", () =>
        {
            if (!world.SessionActive)
                return Results.Json(new { success = false, message = "no active session" });
            saveService.Save();
            return Results.Json(new { success = true, message = "saved", savedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        });

        app.MapGet("/agent/state/farm_lands", (int? page) => SessionGuard(world, () =>
        {
            var p = page is > 0 ? page.Value : 0;
            var data = p > 0
                ? world.GetLandsPage(p)
                : world.GetLands();
            return JsonState(new { page = p, lands = data, total = world.GetLands().Count });
        }));
        app.MapGet("/agent/state/economy", () => SessionGuard(world, () => JsonState(world.Economy.ToState())));
        app.MapGet("/agent/state/mine", (int? layer) => SessionGuard(world, () => JsonState(world.Mining.ToState(layer))));
        app.MapGet("/agent/state/fish_pond", (string? id) => SessionGuard(world, () =>
            JsonState(world.Fishing.ToState(id ?? "p01"))));
        app.MapGet("/agent/state/bag", () => SessionGuard(world, () => JsonState(world.Inventory.GetBag())));
        app.MapGet("/agent/state/merchant_price", () => SessionGuard(world, () => JsonState(world.Merchants.Crop.GetPrices())));
        app.MapGet("/agent/state/merchants", () => SessionGuard(world, () => JsonState(world.Merchants.ToState())));
        app.MapGet("/agent/state/codex", () => SessionGuard(world, () => JsonState(world.Codex.ToState())));
        app.MapGet("/agent/state/decorations", () => SessionGuard(world, () => JsonState(world.Decorations.ToState())));
        app.MapGet("/agent/state/boss", () => SessionGuard(world, () => JsonState(world.Boss.ToState())));
        app.MapGet("/agent/state/farm_order", () => SessionGuard(world, () => JsonState(world.Orders.GetDailyState())));
        app.MapGet("/agent/state/orders", () => SessionGuard(world, () => JsonState(world.Orders.GetState())));
        app.MapGet("/agent/state/season", () => SessionGuard(world, () => JsonState(world.Seasons.ToState())));
        app.MapGet("/agent/state/progress", () => SessionGuard(world, () => JsonState(world.Progress.ToState())));
        app.MapGet("/agent/state/achievement", () => SessionGuard(world, () => JsonState(world.Achievements.ToState())));
        app.MapGet("/agent/state/prestige", () => SessionGuard(world, () => JsonState(world.Prestige.ToState(world.Progress.TotalGoldEarned, world.Progress.MainVictory))));
        app.MapGet("/agent/state/livestock", () => SessionGuard(world, () => JsonState(world.Livestock.ToState())));
        app.MapGet("/agent/state/processing", () => SessionGuard(world, () => JsonState(world.Processing.ToState())));
        app.MapGet("/agent/state/rewards", () => SessionGuard(world, () => JsonState(world.Rewards.ToState())));
        app.MapGet("/agent/state/buildings", () => SessionGuard(world, () => JsonState(world.Buildings.ToList())));
        app.MapGet("/agent/state/upkeep", () => SessionGuard(world, () => JsonState(world.Upkeep.ToState())));
        app.MapGet("/agent/state/forest", () => SessionGuard(world, () =>
            JsonState(world.Forest.ToState(world.Seasons.GameDay, world.Player.X, world.Player.Y))));
        app.MapGet("/agent/state/construction", () => SessionGuard(world, () => JsonState(world.Construction.ToState())));
        app.MapGet("/agent/state/meta", () => SessionGuard(world, () => JsonState(world.GetMetaSnapshot())));
        app.MapGet("/agent/state/action", () => SessionGuard(world, () => JsonState(world.GetActionState())));
        app.MapGet("/agent/state/player", () => SessionGuard(world, () => JsonState(world.Player.ToState())));
        app.MapGet("/agent/state/sell_confirm", () =>
        {
            if (!world.SessionActive)
                return Results.Json(new { error = "no_session", message = "start game first" });
            var token = world.IssueSellConfirm();
            return token == null
                ? Results.Json(new { error = "failed", message = "could not issue token" })
                : Results.Json(new { data = token, ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        });

        app.MapPost("/agent/reward/claim", async (HttpRequest req) =>
        {
            if (!world.SessionActive)
                return Results.Json(new { success = false, message = "no active session" });
            ClaimBody? body = null;
            try { body = await JsonSerializer.DeserializeAsync<ClaimBody>(req.Body, JsonOptions); } catch { }
            var result = world.ClaimRewards(body?.rewardId);
            saveService.Save();
            return Results.Json(new { success = result.Success, message = result.Message, extra = result.Extra, ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        });

        app.MapPost("/agent/action", async (HttpRequest req) =>
        {
            if (!world.SessionActive)
                return Results.Json(new { success = false, message = "no active session — start game first" });
            AgentActionBody? body;
            try { body = await JsonSerializer.DeserializeAsync<AgentActionBody>(req.Body, JsonOptions); }
            catch { return Results.Json(new { success = false, message = "invalid JSON" }); }

            if (string.IsNullOrEmpty(body?.ActionId))
                return Results.Json(new { success = false, message = "actionId required" });

            var result = await world.ExecuteActionAsync(body.ActionId, body.Params ?? new JsonParams());
            saveService.Save();
            return Results.Json(new
            {
                success = result.Success,
                message = result.Message,
                extra = result.Extra,
                action = world.GetActionState(),
                ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
        });

        app.MapGet("/api/snapshot", () =>
        {
            if (!world.SessionActive)
                return Results.Json(new { data = (object?)null, sessionActive = false, ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
            return Results.Json(new { data = world.GetSnapshot(), sessionActive = true, ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        });

        app.MapGet("/api/map", () =>
        {
            if (!world.SessionActive)
                return Results.Json(new
                {
                    data = new { width = ItemIds.MapWidth, height = ItemIds.MapHeight, tiles = WorldMapData.GetTilesFlat() },
                    ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                });
            return Results.Json(new
            {
                data = new { width = ItemIds.MapWidth, height = ItemIds.MapHeight, tiles = world.GetDynamicTiles() },
                ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
        });

        app.UseWebSockets();
        app.Map("/ws/view", viewerLifecycle.HandleWebSocketAsync);
        app.Map("/ws/agent", hub.HandleAsync);
    }

    private static IResult SessionGuard(GameWorld world, Func<IResult> handler) =>
        world.SessionActive ? handler() : Results.Json(new { error = "no_session", message = "start game first" });

    private static IResult JsonState<T>(T data) =>
        Results.Json(new { data, ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });

    private record AgentActionBody(string? ActionId, JsonParams? Params);
    private record ClaimBody(string? rewardId);
}
