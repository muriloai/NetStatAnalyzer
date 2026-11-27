using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Enums;

namespace NetStatAnalyzer.Infrastructure.System;

public sealed class Win32WindowsServiceController : IWindowsServiceController
{
    private const uint SC_MANAGER_CONNECT = 0x0001;

    private const uint SERVICE_QUERY_STATUS = 0x0004;
    private const uint SERVICE_START = 0x0010;
    private const uint SERVICE_STOP = 0x0020;
    private const uint SERVICE_PAUSE_CONTINUE = 0x0040;

    private const uint SERVICE_CONTROL_STOP = 0x00000001;
    private const uint SERVICE_CONTROL_PAUSE = 0x00000002;
    private const uint SERVICE_CONTROL_CONTINUE = 0x00000003;

    private const uint SERVICE_STOPPED = 0x00000001;
    private const uint SERVICE_RUNNING = 0x00000004;
    private const uint SERVICE_PAUSED = 0x00000007;

    private const int ERROR_ACCESS_DENIED = 5;
    private const int ERROR_SERVICE_ALREADY_RUNNING = 1056;

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
    }

    [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint dwAccess);

    [DllImport("advapi32.dll", EntryPoint = "OpenServiceW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

    [DllImport("advapi32.dll", EntryPoint = "CloseServiceHandle", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr hSCObject);

    [DllImport("advapi32.dll", EntryPoint = "StartServiceW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool StartService(IntPtr hService, uint dwNumServiceArgs, string[]? lpServiceArgVectors);

    [DllImport("advapi32.dll", EntryPoint = "ControlService", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ControlService(IntPtr hService, uint dwControl, ref SERVICE_STATUS lpServiceStatus);

    [DllImport("advapi32.dll", EntryPoint = "QueryServiceStatus", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatus(IntPtr hService, ref SERVICE_STATUS lpServiceStatus);

    public async Task<ServiceBatchControlResult> ExecuteBatchAsync(
        IReadOnlyList<string> serviceNames,
        WindowsServiceAction action,
        CancellationToken cancellationToken = default)
    {
        if (serviceNames is null || serviceNames.Count == 0)
        {
            return new ServiceBatchControlResult(0, 0, Array.Empty<string>(), false);
        }

        var distinctNames = serviceNames.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var failedMessages = new List<string>();
        var needsElevationList = new List<string>();
        int succeededCount = 0;

        IntPtr scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);
        if (scm == IntPtr.Zero)
        {
            int scmErr = Marshal.GetLastWin32Error();
            if (scmErr == ERROR_ACCESS_DENIED)
            {
                needsElevationList.AddRange(distinctNames);
            }
            else
            {
                return new ServiceBatchControlResult(
                    distinctNames.Count, 0, new[] { $"Não foi possível conectar ao Service Control Manager (código {scmErr})." }, false);
            }
        }
        else
        {
            try
            {
                foreach (var serviceName in distinctNames)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    uint access = action switch
                    {
                        WindowsServiceAction.Start => SERVICE_START | SERVICE_QUERY_STATUS,
                        WindowsServiceAction.Stop => SERVICE_STOP | SERVICE_QUERY_STATUS,
                        WindowsServiceAction.Pause or WindowsServiceAction.Continue => SERVICE_PAUSE_CONTINUE | SERVICE_QUERY_STATUS,
                        WindowsServiceAction.Restart => SERVICE_START | SERVICE_STOP | SERVICE_QUERY_STATUS,
                        _ => SERVICE_QUERY_STATUS
                    };

                    IntPtr serviceHandle = OpenService(scm, serviceName, access);
                    if (serviceHandle == IntPtr.Zero)
                    {
                        int err = Marshal.GetLastWin32Error();
                        if (err == ERROR_ACCESS_DENIED)
                        {
                            needsElevationList.Add(serviceName);
                        }
                        else
                        {
                            failedMessages.Add($"[{serviceName}] Erro ao abrir serviço (código {err}).");
                        }
                        continue;
                    }

                    try
                    {
                        bool ok = await ExecuteDirectServiceActionAsync(serviceHandle, serviceName, action, cancellationToken);
                        if (ok)
                        {
                            succeededCount++;
                        }
                        else
                        {
                            int err = Marshal.GetLastWin32Error();
                            if (err == ERROR_ACCESS_DENIED)
                            {
                                needsElevationList.Add(serviceName);
                            }
                            else if (err == ERROR_SERVICE_ALREADY_RUNNING && action == WindowsServiceAction.Start)
                            {
                                succeededCount++;
                            }
                            else
                            {
                                failedMessages.Add($"[{serviceName}] Falha ao executar ação {action} (código {err}).");
                            }
                        }
                    }
                    finally
                    {
                        CloseServiceHandle(serviceHandle);
                    }
                }
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        }

        // If some or all services require Administrator privilege, run elevated in a single batch
        if (needsElevationList.Count > 0)
        {
            try
            {
                bool elevatedSuccess = await ExecuteElevatedBatchAsync(needsElevationList, action, cancellationToken);
                if (elevatedSuccess)
                {
                    succeededCount += needsElevationList.Count;
                }
                else
                {
                    failedMessages.Add($"Não foi possível aplicar ação aos {needsElevationList.Count} serviço(s) que exigiam Administrador.");
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED (User dismissed UAC)
            {
                return new ServiceBatchControlResult(distinctNames.Count, succeededCount, failedMessages, true);
            }
            catch (Exception ex)
            {
                failedMessages.Add($"Falha na elevação UAC: {ex.Message}");
            }
        }

        return new ServiceBatchControlResult(distinctNames.Count, succeededCount, failedMessages, false);
    }

    private static async Task<bool> ExecuteDirectServiceActionAsync(
        IntPtr serviceHandle,
        string serviceName,
        WindowsServiceAction action,
        CancellationToken cancellationToken)
    {
        var status = new SERVICE_STATUS();

        switch (action)
        {
            case WindowsServiceAction.Start:
                if (!StartService(serviceHandle, 0, null))
                {
                    return false;
                }
                return await WaitForStateAsync(serviceHandle, SERVICE_RUNNING, TimeSpan.FromSeconds(10), cancellationToken);

            case WindowsServiceAction.Stop:
                if (!ControlService(serviceHandle, SERVICE_CONTROL_STOP, ref status))
                {
                    return false;
                }
                return await WaitForStateAsync(serviceHandle, SERVICE_STOPPED, TimeSpan.FromSeconds(10), cancellationToken);

            case WindowsServiceAction.Pause:
                if (!ControlService(serviceHandle, SERVICE_CONTROL_PAUSE, ref status))
                {
                    return false;
                }
                return await WaitForStateAsync(serviceHandle, SERVICE_PAUSED, TimeSpan.FromSeconds(10), cancellationToken);

            case WindowsServiceAction.Continue:
                if (!ControlService(serviceHandle, SERVICE_CONTROL_CONTINUE, ref status))
                {
                    return false;
                }
                return await WaitForStateAsync(serviceHandle, SERVICE_RUNNING, TimeSpan.FromSeconds(10), cancellationToken);

            case WindowsServiceAction.Restart:
                // First stop
                if (ControlService(serviceHandle, SERVICE_CONTROL_STOP, ref status))
                {
                    await WaitForStateAsync(serviceHandle, SERVICE_STOPPED, TimeSpan.FromSeconds(12), cancellationToken);
                }
                // Then start
                if (!StartService(serviceHandle, 0, null))
                {
                    return false;
                }
                return await WaitForStateAsync(serviceHandle, SERVICE_RUNNING, TimeSpan.FromSeconds(12), cancellationToken);

            default:
                return false;
        }
    }

    private static async Task<bool> WaitForStateAsync(
        IntPtr serviceHandle,
        uint desiredState,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var status = new SERVICE_STATUS();
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (QueryServiceStatus(serviceHandle, ref status))
            {
                if (status.dwCurrentState == desiredState)
                {
                    return true;
                }
            }

            await Task.Delay(250, cancellationToken);
        }

        return false;
    }

    private static async Task<bool> ExecuteElevatedBatchAsync(
        IReadOnlyList<string> serviceNames,
        WindowsServiceAction action,
        CancellationToken cancellationToken)
    {
        string psVerbCmd = action switch
        {
            WindowsServiceAction.Start => "Start-Service",
            WindowsServiceAction.Stop => "Stop-Service -Force",
            WindowsServiceAction.Pause => "Suspend-Service",
            WindowsServiceAction.Continue => "Resume-Service",
            WindowsServiceAction.Restart => "Restart-Service -Force",
            _ => "Start-Service"
        };

        var escapedNames = string.Join(",", serviceNames.Select(s => $"'{s.Replace("'", "''")}'"));
        string script = $"$services = @({escapedNames}); foreach ($s in $services) {{ try {{ {psVerbCmd} -Name $s -ErrorAction SilentlyContinue }} catch {{ }} }}";

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
            Verb = "runas",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var process = Process.Start(psi);
        if (process is null) return false;

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode == 0;
    }
}
