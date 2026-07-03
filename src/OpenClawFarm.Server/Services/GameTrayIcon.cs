namespace OpenClawFarm.Server.Services;

public sealed class GameTrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Form _syncForm;
    private readonly ApplicationContext _context = new();
    private readonly Action _onExit;
    private bool _exiting;

    public GameTrayIcon(string gameUrl, Action onExit)
    {
        _onExit = onExit;

        _syncForm = new Form
        {
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            Size = new Size(0, 0),
            Opacity = 0,
        };
        _ = _syncForm.Handle;

        var menu = new ContextMenuStrip();
        menu.Items.Add("打开游戏", null, (_, _) => BrowserLauncher.Open(gameUrl));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => RequestExit());

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "OpenClaw Farm",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => BrowserLauncher.Open(gameUrl);
    }

    public void ShowRunningHint()
    {
        _icon.ShowBalloonTip(
            4000,
            "OpenClaw Farm",
            "关闭浏览器窗口即可退出游戏。",
            ToolTipIcon.Info);
    }

    public void RequestExit()
    {
        if (_syncForm.InvokeRequired)
        {
            _syncForm.BeginInvoke(RequestExit);
            return;
        }
        RequestExitCore();
    }

    private void RequestExitCore()
    {
        if (_exiting)
            return;

        _exiting = true;
        _icon.Visible = false;
        _onExit();
        _context.ExitThread();
    }

    public void RunMessageLoop() => Application.Run(_context);

    public void Dispose()
    {
        _icon.Dispose();
        _syncForm.Dispose();
    }
}
