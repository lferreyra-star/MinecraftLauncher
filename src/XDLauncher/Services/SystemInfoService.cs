using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace XdLauncher.Services;

public sealed class SystemSummary
{
    public string CpuName { get; init; } = "No detectado";
    public string GpuName { get; init; } = "No detectado";
    public ulong TotalRamBytes { get; init; }
    public ulong AvailableRamBytes { get; init; }
    public string WindowsName { get; init; } = "Windows";
    public string OsArchitecture { get; init; } = "x64";
    public string DotNetVersion { get; init; } = Environment.Version.ToString();
    public string ComputerName { get; init; } = Environment.MachineName;

    public string TotalRamText => $"{TotalRamBytes / 1024d / 1024d / 1024d:0.#} GB";
    public string AvailableRamText => $"{AvailableRamBytes / 1024d / 1024d / 1024d:0.#} GB";
    public string RamUsageText => TotalRamBytes == 0
        ? "—"
        : $"{Math.Round((TotalRamBytes - AvailableRamBytes) * 100d / TotalRamBytes)}% en uso";
}

public sealed class SystemInfoService
{
    public SystemSummary GetSummary()
    {
        var memory = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        GlobalMemoryStatusEx(ref memory);
        var build = Environment.OSVersion.Version.Build;
        var windowsName = build >= 22000 ? "Windows 11" : "Windows 10";

        return new SystemSummary
        {
            CpuName = ReadRegistryString(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString") ?? "No detectado",
            GpuName = ReadGpuName(),
            TotalRamBytes = memory.ullTotalPhys,
            AvailableRamBytes = memory.ullAvailPhys,
            WindowsName = $"{windowsName} · {Environment.OSVersion.Version}",
            OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            ComputerName = Environment.MachineName
        };
    }

    public long GetLauncherMemoryBytes()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            return process.WorkingSet64;
        }
        catch
        {
            return 0;
        }
    }

    private static string? ReadRegistryString(string path, string name)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            return key?.GetValue(name) as string;
        }
        catch
        {
            return null;
        }
    }

    private static string ReadGpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (key is null)
                return "No detectada";

            var names = new List<string>();
            foreach (var subKeyName in key.GetSubKeyNames().Where(n => n.Length == 4 && n.All(char.IsDigit)).OrderBy(n => n))
            {
                using var sub = key.OpenSubKey(subKeyName);
                if (sub?.GetValue("DriverDesc") is string name && !string.IsNullOrWhiteSpace(name))
                    names.Add(name.Trim());
            }

            return names.Count == 0 ? "No detectada" : string.Join(" · ", names.Distinct());
        }
        catch
        {
            return "No detectada";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
