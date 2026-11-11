using System.Collections.Generic;
using System.Linq;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Application.DTOs;
using NetStatAnalyzer.Application.UseCases;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;
using NetStatAnalyzer.Domain.Policies;
using NetStatAnalyzer.Presentation.ViewModels;
using Xunit;

namespace NetStatAnalyzer.Tests
{
    public class ThreatDetectionAndMetricsTests
    {
        [Fact]
        public void EvaluateProcess_ShouldDetectResidentialProxyMonetization()
        {
            var (level, reason) = ThreatDetectionPolicy.EvaluateProcess(
                processName: "WorldProxyPool.exe",
                processPath: @"C:\ProgramData\WorldProxyPool\app.exe",
                cpuPercent: 2.5,
                bandwidthBytesPerSec: 150000,
                activeConnectionsCount: 8);

            Assert.Equal(ThreatLevel.Critical, level);
            Assert.Contains("Proxy Residencial", reason);
        }

        [Fact]
        public void EvaluateProcess_ShouldDetectDistributedComputingMiners()
        {
            var (level1, reason1) = ThreatDetectionPolicy.EvaluateProcess(
                processName: "charityengine.exe",
                processPath: null,
                cpuPercent: 40.0,
                bandwidthBytesPerSec: 20000,
                activeConnectionsCount: 2);

            Assert.Equal(ThreatLevel.Critical, level1);
            Assert.Contains("Computação distribuída", reason1);

            var (level2, reason2) = ThreatDetectionPolicy.EvaluateProcess(
                processName: "boinc.exe",
                processPath: @"C:\Program Files\BOINC\boinc.exe",
                cpuPercent: 80.0,
                bandwidthBytesPerSec: 5000,
                activeConnectionsCount: 1);

            Assert.Equal(ThreatLevel.Critical, level2);
            Assert.Contains("Mineração", reason2);
        }

        [Fact]
        public void EvaluateProcess_ShouldDetectAnomalousNetworkBandwidthSpike()
        {
            // 3.5 MB/s transfer rate on unknown process
            var (level, reason) = ThreatDetectionPolicy.EvaluateProcess(
                processName: "datasync.exe",
                processPath: @"C:\Users\User\datasync.exe",
                cpuPercent: 4.0,
                bandwidthBytesPerSec: 3.5 * 1024 * 1024,
                activeConnectionsCount: 5);

            Assert.Equal(ThreatLevel.Medium, level);
            Assert.Contains("Pico anormal de uso de rede", reason);
        }

        [Fact]
        public void EvaluateProcess_ShouldDetectHighConnectionCount()
        {
            var (level, reason) = ThreatDetectionPolicy.EvaluateProcess(
                processName: "worker.exe",
                processPath: null,
                cpuPercent: 5.0,
                bandwidthBytesPerSec: 10000,
                activeConnectionsCount: 35);

            Assert.Equal(ThreatLevel.Medium, level);
            Assert.Contains("Alto volume de conexões ativas", reason);
        }

        [Fact]
        public void EvaluateScheduledTask_ShouldDetectHiddenPowerShellMonitor()
        {
            var (level, reason, isMicrosoft) = ThreatDetectionPolicy.EvaluateScheduledTask(
                taskName: "SystemMonitorHelper",
                taskPath: @"\",
                taskToRun: "powershell.exe -w hidden -enc JAB3AG8AcgBr...",
                author: "User",
                scheduleType: "A cada 5 minutos");

            Assert.Equal(ThreatLevel.Critical, level);
            Assert.Contains("PowerShell oculto", reason);
            Assert.False(isMicrosoft);
        }

        [Fact]
        public void EvaluateScheduledTask_ShouldRecognizeLegitimateMicrosoftTask()
        {
            var (level, reason, isMicrosoft) = ThreatDetectionPolicy.EvaluateScheduledTask(
                taskName: "PcaPatchDbTask",
                taskPath: @"\Microsoft\Windows\Application Experience\",
                taskToRun: @"%SystemRoot%\system32\rundll32.exe",
                author: "Microsoft Corporation",
                scheduleType: "Diário");

            Assert.Equal(ThreatLevel.Normal, level);
            Assert.True(isMicrosoft);
        }

        [Fact]
        public void EvaluateStartupItem_ShouldDetectSuspiciousScriptsInStartup()
        {
            var (level, reason) = ThreatDetectionPolicy.EvaluateStartupItem(
                name: "UpdateService",
                command: @"wscript.exe C:\Users\User\AppData\Local\Temp\agent.vbs",
                location: "HKCU Run");

            Assert.Equal(ThreatLevel.High, level);
            Assert.Contains("Script de inicialização furtivo", reason);
        }

        [Fact]
        public void ProcessMetricInfo_FormatBytesPerSec_ShouldFormatCorrectly()
        {
            Assert.Equal("0 B/s", ProcessMetricInfo.FormatBytesPerSec(0));
            Assert.Equal("500 B/s", ProcessMetricInfo.FormatBytesPerSec(500));
            Assert.Equal("1.5 KB/s", ProcessMetricInfo.FormatBytesPerSec(1536));
            Assert.Equal("2.00 MB/s", ProcessMetricInfo.FormatBytesPerSec(2 * 1024 * 1024));
            Assert.Equal("1.25 GB/s", ProcessMetricInfo.FormatBytesPerSec(1.25 * 1024 * 1024 * 1024));
        }

        [Fact]
        public async System.Threading.Tasks.Task MainViewModel_CpuCutoffFilter_ShouldFilterConnectionsAccurately()
        {
            var mockScanner = new MockNetworkScanner(new List<RawSocketInfo>
            {
                new RawSocketInfo { Protocol = "TCP", LocalAddress = "127.0.0.1:80", ForeignAddress = "127.0.0.1:5000", State = "ESTABLISHED", PID = 1001 },
                new RawSocketInfo { Protocol = "TCP", LocalAddress = "127.0.0.1:81", ForeignAddress = "127.0.0.1:5001", State = "ESTABLISHED", PID = 1002 },
                new RawSocketInfo { Protocol = "TCP", LocalAddress = "127.0.0.1:82", ForeignAddress = "127.0.0.1:5002", State = "ESTABLISHED", PID = 1003 }
            });

            var mockMetrics = new MockProcessMetricsProvider(new Dictionary<int, ProcessMetricInfo>
            {
                [1001] = new ProcessMetricInfo { PID = 1001, ProcessName = "low-cpu.exe", CpuPercent = 0.5 },
                [1002] = new ProcessMetricInfo { PID = 1002, ProcessName = "med-cpu.exe", CpuPercent = 7.5 },
                [1003] = new ProcessMetricInfo { PID = 1003, ProcessName = "high-cpu.exe", CpuPercent = 35.0 }
            });

            var scanUseCase = new ScanConnectionsUseCase(mockScanner, new MockProcessResolver());
            var allowlistUseCase = new ManageAllowlistUseCase(new MockAllowlistRepository());
            var exportUseCase = new ExportConnectionsUseCase(new MockConnectionExporter());
            var metricsUseCase = new ScanProcessMetricsUseCase(mockMetrics);
            var tasksUseCase = new ScanScheduledTasksUseCase(new MockScheduledTaskScanner());
            var startupUseCase = new ScanStartupItemsUseCase(new MockStartupItemScanner());

            var vm = new MainViewModel(scanUseCase, allowlistUseCase, exportUseCase, metricsUseCase, tasksUseCase, startupUseCase, new MockWindowsToolsLauncher());

            // Initial load
            await vm.LoadAllDataAsync();
            Assert.Equal(3, vm.FilteredEntries.Count);

            // Cutoff at > 5% CPU
            vm.CpuCutoffThreshold = 5.0;
            Assert.Equal(2, vm.FilteredEntries.Count);
            Assert.DoesNotContain(vm.FilteredEntries, e => e.PID == 1001);

            // Cutoff at > 20% CPU
            vm.CpuCutoffThreshold = 20.0;
            Assert.Single(vm.FilteredEntries);
            Assert.Equal(1003, vm.FilteredEntries[0].PID);
        }
    }

    public class MockProcessMetricsProvider : IProcessMetricsProvider
    {
        private readonly IReadOnlyDictionary<int, ProcessMetricInfo> _metrics;

        public MockProcessMetricsProvider(IReadOnlyDictionary<int, ProcessMetricInfo> metrics)
        {
            _metrics = metrics;
        }

        public System.Threading.Tasks.Task<IReadOnlyDictionary<int, ProcessMetricInfo>> SampleMetricsAsync(IEnumerable<int> pids)
        {
            return System.Threading.Tasks.Task.FromResult(_metrics);
        }
    }

    public class MockScheduledTaskScanner : IScheduledTaskScanner
    {
        public System.Threading.Tasks.Task<IReadOnlyList<ScheduledTaskItem>> ScanScheduledTasksAsync()
        {
            return System.Threading.Tasks.Task.FromResult<IReadOnlyList<ScheduledTaskItem>>(new List<ScheduledTaskItem>());
        }
    }

    public class MockStartupItemScanner : IStartupItemScanner
    {
        public System.Threading.Tasks.Task<IReadOnlyList<StartupItem>> ScanStartupItemsAsync()
        {
            return System.Threading.Tasks.Task.FromResult<IReadOnlyList<StartupItem>>(new List<StartupItem>());
        }
    }

    public class MockAllowlistRepository : IAllowlistRepository
    {
        private readonly List<AllowlistRule> _rules = new();
        public IReadOnlyList<AllowlistRule> LoadAll() => _rules;
        public void SaveAll(IEnumerable<AllowlistRule> rules) { _rules.Clear(); _rules.AddRange(rules); }
        public string ExportToJson(IEnumerable<AllowlistRule> rules, string appVersion = "1.3.0") => "{}";
        public (bool Success, IReadOnlyList<AllowlistRule> Rules, string Version, string Message) ImportFromJson(string jsonContent) => (true, new List<AllowlistRule>(), "1.3.0", "OK");
    }

    public class MockConnectionExporter : IConnectionExporter
    {
        public void ExportToJson(string path, IEnumerable<NetworkConnection> connections, string version) { }
        public void ExportToCsv(string path, IEnumerable<NetworkConnection> connections) { }
        public void ExportToTxt(string path, IEnumerable<NetworkConnection> connections) { }
    }

    public class MockWindowsToolsLauncher : IWindowsToolsLauncher
    {
        public bool OpenTaskScheduler() => true;
        public bool OpenStartupAppsSettings() => true;
        public bool OpenTaskManager() => true;
        public bool OpenResourceMonitor() => true;
        public bool OpenStartupFolder() => true;
        public bool OpenRegistryEditor() => true;
    }
}
