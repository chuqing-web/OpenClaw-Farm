using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using OpenClawFarm.Core.Game;
using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Server.Services;

public sealed class AgentWebSocketHub
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ConcurrentDictionary<WebSocket, SemaphoreSlim> _queues = new();
    private readonly List<WebSocket> _clients = [];
    private readonly object _clientLock = new();
    private GameWorld? _world;

    public void Attach(GameWorld world)
    {
        _world = world;
        world.OnPatch += BroadcastPatch;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var ws = await context.WebSockets.AcceptWebSocketAsync();
        var sem = new SemaphoreSlim(1, 1);
        _queues[ws] = sem;
        lock (_clientLock) _clients.Add(ws);

        try
        {
            var snap = _world!.GetSnapshot();
            await SendJson(ws, new WorldPatch("world_patch", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                snap.Player, snap.Lands, snap.Bag));

            var buffer = new byte[8192];
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(buffer, context.RequestAborted);
                if (result.MessageType == WebSocketMessageType.Close) break;
                if (result.MessageType != WebSocketMessageType.Text) continue;

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                _ = ProcessMessageAsync(ws, sem, json);
            }
        }
        finally
        {
            lock (_clientLock) _clients.Remove(ws);
            _queues.TryRemove(ws, out _);
            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        }
    }

    private async Task ProcessMessageAsync(WebSocket ws, SemaphoreSlim sem, string json)
    {
        ActionMessage? msg;
        try
        {
            msg = JsonSerializer.Deserialize<ActionMessage>(json, JsonOptions);
        }
        catch
        {
            await SendJson(ws, new ActionResultMessage("action_result", "unknown", false, "invalid JSON"));
            return;
        }

        if (msg?.Type != "action" || string.IsNullOrEmpty(msg.ReqId) || msg.Payload?.ActionId == null)
        {
            await SendJson(ws, new ActionResultMessage("action_result", msg?.ReqId ?? "unknown", false, "invalid action message"));
            return;
        }

        await sem.WaitAsync();
        try
        {
            var actionId = msg.Payload.ActionId;
            Console.WriteLine($"[Agent] WS action: {actionId}");
            var result = await _world!.ExecuteActionAsync(actionId, msg.Payload.Params ?? new JsonParams());
            if (!result.Success)
                Console.WriteLine($"[Agent] WS failed: {actionId} -> {result.Message}");
            var extra = new Dictionary<string, object?>(result.Extra ?? new Dictionary<string, object?>())
            {
                ["action"] = _world.GetActionState(),
            };
            await SendJson(ws, new ActionResultMessage("action_result", msg.ReqId, result.Success, result.Message, extra));
        }
        catch (Exception ex)
        {
            await SendJson(ws, new ActionResultMessage("action_result", msg.ReqId, false, ex.Message));
        }
        finally
        {
            sem.Release();
        }
    }

    private void BroadcastPatch(WorldPatch patch)
    {
        List<WebSocket> copy;
        lock (_clientLock) copy = [.._clients];
        var json = JsonSerializer.Serialize(patch, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        foreach (var ws in copy)
        {
            if (ws.State == WebSocketState.Open)
                _ = ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    private static Task SendJson<T>(WebSocket ws, T obj)
    {
        var json = JsonSerializer.Serialize(obj, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        return ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
