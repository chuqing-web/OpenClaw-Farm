using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenClawFarm.Server.Services;

public static class BrowserLauncher
{
    public static void Open(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
                return;
            }
            Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = true });
        }
        catch
        {
            // 浏览器打开失败时静默忽略
        }
    }
}
