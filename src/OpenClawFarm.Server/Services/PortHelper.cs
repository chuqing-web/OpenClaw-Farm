using System.Diagnostics;

namespace OpenClawFarm.Server.Services;

/// <summary>
/// 启动前 / 退出时释放游戏端口，避免调试残留进程占用 28080。
/// </summary>
public static class PortHelper
{
    public const int GamePort = 28080;

    public static void EnsureAvailable(int port = GamePort)
    {
        var killed = FreePort(port, Environment.ProcessId, onlyFarmServer: true);
        if (killed > 0)
            Thread.Sleep(300);
    }

    /// <summary>退出时释放端口，避免残留进程占用 28080。</summary>
    public static void Shutdown(int port = GamePort) =>
        FreePort(port, excludePid: null, onlyFarmServer: true);

    private static int FreePort(int port, int? excludePid, bool onlyFarmServer)
    {
        var killed = 0;
        foreach (var pid in FindListenerPids(port))
        {
            if (excludePid.HasValue && pid == excludePid.Value)
                continue;
            if (onlyFarmServer && !IsFarmServerProcess(pid))
                continue;
            if (TryKill(pid, port))
                killed++;
        }
        return killed;
    }

    private static bool IsFarmServerProcess(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            var name = p.ProcessName;
            return name.Equals("OpenClawFarm", StringComparison.OrdinalIgnoreCase)
                || name.Equals("OpenClawFarm.Server", StringComparison.OrdinalIgnoreCase)
                || (name.Equals("dotnet", StringComparison.OrdinalIgnoreCase)
                    && (p.MainModule?.FileName?.Contains("OpenClawFarm", StringComparison.OrdinalIgnoreCase) ?? false));
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<int> FindListenerPids(int port)
    {
        var pids = new HashSet<int>();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc == null)
                return pids;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            var portToken = $":{port}";
            foreach (var line in output.Split('\n'))
            {
                if (!line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!line.Contains(portToken, StringComparison.Ordinal))
                    continue;

                var parts = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5 && int.TryParse(parts[^1], out var pid) && pid > 0)
                    pids.Add(pid);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Port] 检查端口 {port} 失败: {ex.Message}");
        }

        return pids;
    }

    private static bool TryKill(int pid, int port)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            var name = p.ProcessName;
            p.Kill(entireProcessTree: true);
            p.WaitForExit(3000);
            Console.WriteLine($"[Port] 已释放 {port}（结束 {name} PID {pid}）");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Port] 无法结束 PID {pid}: {ex.Message}");
            return false;
        }
    }
}
