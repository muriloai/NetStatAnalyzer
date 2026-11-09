using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Infrastructure.Monitoring
{
    public class Win32ProcessMetricsProvider : IProcessMetricsProvider
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS lpIoCounters);

        private class ProcessSnapshot
        {
            public DateTime Timestamp { get; set; }
            public TimeSpan TotalCpuTime { get; set; }
            public ulong ReadBytes { get; set; }
            public ulong WriteBytes { get; set; }
        }

        private readonly ConcurrentDictionary<int, ProcessSnapshot> _snapshots = new();
        private static readonly int ProcessorCount = Math.Max(1, Environment.ProcessorCount);

        public async Task<IReadOnlyDictionary<int, ProcessMetricInfo>> SampleMetricsAsync(IEnumerable<int> pids)
        {
            return await Task.Run(async () =>
            {
                var pidSet = new HashSet<int>(pids);
                var result = new Dictionary<int, ProcessMetricInfo>();

                // Check if we need an initial dual-sample
                bool needsInitialBaseline = false;
                foreach (int pid in pidSet)
                {
                    if (!_snapshots.ContainsKey(pid) && pid > 4)
                    {
                        needsInitialBaseline = true;
                        break;
                    }
                }

                if (needsInitialBaseline)
                {
                    TakeRawSample(pidSet);
                    await Task.Delay(150);
                }

                DateTime now = DateTime.UtcNow;

                foreach (int pid in pidSet)
                {
                    if (pid <= 0)
                    {
                        result[pid] = new ProcessMetricInfo { PID = pid, ProcessName = "Sistema Ocioso" };
                        continue;
                    }
                    if (pid == 4)
                    {
                        result[pid] = new ProcessMetricInfo { PID = pid, ProcessName = "System" };
                        continue;
                    }

                    try
                    {
                        using var process = Process.GetProcessById(pid);
                        string procName = process.ProcessName;
                        string? procPath = null;
                        try { procPath = process.MainModule?.FileName; } catch { }

                        TimeSpan currentCpu = process.TotalProcessorTime;
                        ulong currentRead = 0;
                        ulong currentWrite = 0;

                        if (GetProcessIoCounters(process.Handle, out var ioCounters))
                        {
                            currentRead = ioCounters.ReadTransferCount;
                            currentWrite = ioCounters.WriteTransferCount;
                        }

                        double cpuPercent = 0.0;
                        double readRate = 0.0;
                        double writeRate = 0.0;

                        if (_snapshots.TryGetValue(pid, out var prev))
                        {
                            double elapsedSec = (now - prev.Timestamp).TotalSeconds;
                            if (elapsedSec > 0.05)
                            {
                                double cpuDeltaMs = (currentCpu - prev.TotalCpuTime).TotalMilliseconds;
                                cpuPercent = Math.Clamp((cpuDeltaMs / (elapsedSec * 1000.0 * ProcessorCount)) * 100.0, 0.0, 100.0);

                                if (currentRead >= prev.ReadBytes)
                                    readRate = (currentRead - prev.ReadBytes) / elapsedSec;

                                if (currentWrite >= prev.WriteBytes)
                                    writeRate = (currentWrite - prev.WriteBytes) / elapsedSec;
                            }
                        }

                        _snapshots[pid] = new ProcessSnapshot
                        {
                            Timestamp = now,
                            TotalCpuTime = currentCpu,
                            ReadBytes = currentRead,
                            WriteBytes = currentWrite
                        };

                        result[pid] = new ProcessMetricInfo
                        {
                            PID = pid,
                            ProcessName = procName,
                            ProcessPath = procPath,
                            CpuPercent = Math.Round(cpuPercent, 1),
                            ReadBytesPerSec = readRate,
                            WriteBytesPerSec = writeRate
                        };
                    }
                    catch
                    {
                        // Process terminated or access denied
                        result[pid] = new ProcessMetricInfo
                        {
                            PID = pid,
                            ProcessName = "Desconhecido",
                            CpuPercent = 0,
                            ReadBytesPerSec = 0,
                            WriteBytesPerSec = 0
                        };
                    }
                }

                return (IReadOnlyDictionary<int, ProcessMetricInfo>)result;
            });
        }

        private void TakeRawSample(IEnumerable<int> pids)
        {
            DateTime now = DateTime.UtcNow;
            foreach (int pid in pids)
            {
                if (pid <= 4) continue;
                try
                {
                    using var process = Process.GetProcessById(pid);
                    ulong r = 0, w = 0;
                    if (GetProcessIoCounters(process.Handle, out var io))
                    {
                        r = io.ReadTransferCount;
                        w = io.WriteTransferCount;
                    }
                    _snapshots[pid] = new ProcessSnapshot
                    {
                        Timestamp = now,
                        TotalCpuTime = process.TotalProcessorTime,
                        ReadBytes = r,
                        WriteBytes = w
                    };
                }
                catch
                {
                }
            }
        }
    }
}
