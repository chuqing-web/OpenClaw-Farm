using System.Net.WebSockets;

namespace OpenClawFarm.Server.Services;

/// <summary>追踪浏览器观战页连接；最后一个页面关闭后延迟退出进程（刷新页可重连取消）。</summary>
public sealed class ViewerLifecycle : IDisposable
{
    private static readonly TimeSpan ExitDebounce = TimeSpan.FromSeconds(2);

    private readonly Action _onAllViewersGone;
    private readonly object _lock = new();
    private int _viewerCount;
    private System.Threading.Timer? _exitTimer;
    private bool _shuttingDown;

    public ViewerLifecycle(Action onAllViewersGone) => _onAllViewersGone = onAllViewersGone;

    public void ViewerConnected()
    {
        lock (_lock)
        {
            if (_shuttingDown) return;
            _exitTimer?.Dispose();
            _exitTimer = null;
            _viewerCount++;
        }
    }

    public void ViewerDisconnected()
    {
        lock (_lock)
        {
            if (_shuttingDown) return;
            _viewerCount = Math.Max(0, _viewerCount - 1);
            if (_viewerCount > 0) return;

            _exitTimer?.Dispose();
            _exitTimer = new System.Threading.Timer(_ => TriggerShutdown(), null, ExitDebounce, Timeout.InfiniteTimeSpan);
        }
    }

    public void TriggerShutdown()
    {
        lock (_lock)
        {
            if (_shuttingDown) return;
            _shuttingDown = true;
            _exitTimer?.Dispose();
            _exitTimer = null;
        }
        _onAllViewersGone();
    }

    public async Task HandleWebSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var ws = await context.WebSockets.AcceptWebSocketAsync();
        ViewerConnected();
        try
        {
            var buffer = new byte[256];
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(buffer, context.RequestAborted);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            ViewerDisconnected();
            if (ws.State == WebSocketState.Open)
            {
                try
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                }
                catch { /* 页面已关闭 */ }
            }
        }
    }

    public void Dispose() => _exitTimer?.Dispose();
}
