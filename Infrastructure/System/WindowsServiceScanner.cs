using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;

namespace NetStatAnalyzer.Infrastructure.System;

public sealed class WindowsServiceScanner : IWindowsServiceScanner
{
    private const uint SC_MANAGER_CONNECT = 0x0001;
    private const uint SC_MANAGER_ENUMERATE_SERVICE = 0x0004;

    private const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
    private const uint SERVICE_WIN32_SHARE_PROCESS = 0x00000020;
    private const uint SERVICE_WIN32 = SERVICE_WIN32_OWN_PROCESS | SERVICE_WIN32_SHARE_PROCESS;

    private const uint SERVICE_STATE_ALL = 0x00000003;

    private const uint SERVICE_STOPPED = 0x00000001;
    private const uint SERVICE_START_PENDING = 0x00000002;
    private const uint SERVICE_STOP_PENDING = 0x00000003;
    private const uint SERVICE_RUNNING = 0x00000004;
    private const uint SERVICE_CONTINUE_PENDING = 0x00000005;
    private const uint SERVICE_PAUSE_PENDING = 0x00000006;
    private const uint SERVICE_PAUSED = 0x00000007;

    private const uint SERVICE_ACCEPT_STOP = 0x00000001;
    private const uint SERVICE_ACCEPT_PAUSE_CONTINUE = 0x00000002;

    private const int ERROR_MORE_DATA = 234;

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS_PROCESS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
        public uint dwProcessId;
        public uint dwServiceFlags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ENUM_SERVICE_STATUS_PROCESS
    {
        public string lpServiceName;
        public string lpDisplayName;
        public SERVICE_STATUS_PROCESS ServiceStatusProcess;
    }

    [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint dwAccess);

    [DllImport("advapi32.dll", EntryPoint = "CloseServiceHandle", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr hSCObject);

    [DllImport("advapi32.dll", EntryPoint = "EnumServicesStatusExW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumServicesStatusEx(
        IntPtr hSCManager,
        int infoLevel,
        uint dwServiceType,
        uint dwServiceState,
        IntPtr lpServices,
        uint cbBufSize,
        out uint pcbBytesNeeded,
        out uint lpServicesReturned,
        ref uint lpResumeHandle,
        string? pszGroupName);

    [DllImport("shlwapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false, ThrowOnUnmappableChar = true)]
    private static extern int SHLoadIndirectString(string pszSourceBuf, StringBuilder pszOutBuf, uint cchOutBuf, IntPtr ppvReserved);

    public Task<IReadOnlyList<WindowsServiceItem>> ScanAllAsync()
    {
        return Task.Run<IReadOnlyList<WindowsServiceItem>>(() =>
        {
            var rawList = EnumServicesFromScm();
            var securityCache = new ConcurrentDictionary<string, FileSecurityInfo>(StringComparer.OrdinalIgnoreCase);

            var items = new List<WindowsServiceItem>(rawList.Count);

            foreach (var raw in rawList)
            {
                var item = new WindowsServiceItem
                {
                    ServiceName = raw.ServiceName,
                    DisplayName = string.IsNullOrWhiteSpace(raw.DisplayName) ? raw.ServiceName : raw.DisplayName,
                    State = MapState(raw.CurrentState),
                    ProcessId = (int)raw.ProcessId,
                    AcceptsStop = (raw.ControlsAccepted & SERVICE_ACCEPT_STOP) != 0,
                    AcceptsPauseAndContinue = (raw.ControlsAccepted & SERVICE_ACCEPT_PAUSE_CONTINUE) != 0
                };

                EnrichFromRegistry(item, securityCache);
                items.Add(item);
            }

            return items;
        });
    }

    private static WindowsServiceState MapState(uint state) => state switch
    {
        SERVICE_STOPPED => WindowsServiceState.Stopped,
        SERVICE_START_PENDING => WindowsServiceState.StartPending,
        SERVICE_STOP_PENDING => WindowsServiceState.StopPending,
        SERVICE_RUNNING => WindowsServiceState.Running,
        SERVICE_CONTINUE_PENDING => WindowsServiceState.ContinuePending,
        SERVICE_PAUSE_PENDING => WindowsServiceState.PausePending,
        SERVICE_PAUSED => WindowsServiceState.Paused,
        _ => WindowsServiceState.Unknown
    };

    private record ScmServiceSnapshot(
        string ServiceName,
        string DisplayName,
        uint CurrentState,
        uint ControlsAccepted,
        uint ProcessId);

    private static List<ScmServiceSnapshot> EnumServicesFromScm()
    {
        var result = new List<ScmServiceSnapshot>();
        IntPtr scm = OpenSCManager(null, null, SC_MANAGER_CONNECT | SC_MANAGER_ENUMERATE_SERVICE);
        if (scm == IntPtr.Zero)
        {
            Debug.WriteLine($"Falha ao abrir Service Control Manager. Erro: {Marshal.GetLastWin32Error()}");
            return result;
        }

        IntPtr buffer = IntPtr.Zero;
        try
        {
            uint bytesNeeded = 0;
            uint servicesReturned = 0;
            uint resumeHandle = 0;

            // First call to determine buffer size
            EnumServicesStatusEx(
                scm,
                0, // SC_ENUM_PROCESS_INFO
                SERVICE_WIN32,
                SERVICE_STATE_ALL,
                IntPtr.Zero,
                0,
                out bytesNeeded,
                out servicesReturned,
                ref resumeHandle,
                null);

            int error = Marshal.GetLastWin32Error();
            if (error != ERROR_MORE_DATA && error != 0)
            {
                Debug.WriteLine($"Erro ao dimensionar buffer para EnumServicesStatusEx: {error}");
                return result;
            }

            buffer = Marshal.AllocHGlobal((int)bytesNeeded);

            if (EnumServicesStatusEx(
                scm,
                0,
                SERVICE_WIN32,
                SERVICE_STATE_ALL,
                buffer,
                bytesNeeded,
                out bytesNeeded,
                out servicesReturned,
                ref resumeHandle,
                null))
            {
                int structSize = Marshal.SizeOf<ENUM_SERVICE_STATUS_PROCESS>();
                for (int i = 0; i < servicesReturned; i++)
                {
                    IntPtr currentPtr = IntPtr.Add(buffer, i * structSize);
                    var svc = Marshal.PtrToStructure<ENUM_SERVICE_STATUS_PROCESS>(currentPtr);

                    result.Add(new ScmServiceSnapshot(
                        svc.lpServiceName ?? string.Empty,
                        svc.lpDisplayName ?? string.Empty,
                        svc.ServiceStatusProcess.dwCurrentState,
                        svc.ServiceStatusProcess.dwControlsAccepted,
                        svc.ServiceStatusProcess.dwProcessId));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exceção ao enumerar serviços via SCM: {ex.Message}");
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
            CloseServiceHandle(scm);
        }

        return result;
    }

    private static void EnrichFromRegistry(WindowsServiceItem item, ConcurrentDictionary<string, FileSecurityInfo> securityCache)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{item.ServiceName}");
            if (key is null) return;

            string? imagePath = key.GetValue("ImagePath") as string;
            item.BinaryPathName = imagePath ?? string.Empty;

            string? account = key.GetValue("ObjectName") as string;
            item.ServiceAccount = account ?? string.Empty;

            // Start type
            object? startVal = key.GetValue("Start");
            if (startVal is int startInt)
            {
                object? delayedVal = key.GetValue("DelayedAutostart");
                bool isDelayed = delayedVal is int delayedInt && delayedInt == 1;

                item.StartType = startInt switch
                {
                    0 => WindowsServiceStartType.Boot,
                    1 => WindowsServiceStartType.System,
                    2 => isDelayed ? WindowsServiceStartType.AutomaticDelayed : WindowsServiceStartType.Automatic,
                    3 => WindowsServiceStartType.Manual,
                    4 => WindowsServiceStartType.Disabled,
                    _ => WindowsServiceStartType.Unknown
                };
            }

            // Description
            string? rawDesc = key.GetValue("Description") as string;
            item.Description = ResolveIndirectString(rawDesc);

            // Binary security inspection
            if (!string.IsNullOrWhiteSpace(item.BinaryPathName))
            {
                var sec = securityCache.GetOrAdd(item.BinaryPathName, path => FileSecurityInspector.Inspect(path));
                item.ResolvedPath = sec.ResolvedPath;
                item.FileExists = sec.Exists;
                item.IsSigned = sec.IsSigned;
                item.IsMicrosoft = sec.IsMicrosoft;
                item.SignerName = sec.SignerName;
                item.CompanyName = sec.CompanyName;

                if (!sec.Exists && !string.IsNullOrWhiteSpace(item.BinaryPathName))
                {
                    item.ThreatLevel = ThreatLevel.Medium;
                    item.ThreatReason = "Arquivo executável do serviço não encontrado no disco (Órfão)";
                }
                else if (!sec.IsSigned && !sec.IsMicrosoft)
                {
                    item.ThreatLevel = ThreatLevel.Low;
                    item.ThreatReason = "Executável de terceiro sem assinatura digital detectada";
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Erro ao ler Registro do serviço '{item.ServiceName}': {ex.Message}");
        }
    }

    private static string ResolveIndirectString(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string trimmed = input.Trim();
        if (!trimmed.StartsWith("@"))
        {
            return trimmed;
        }

        try
        {
            var sb = new StringBuilder(1024);
            int hr = SHLoadIndirectString(trimmed, sb, (uint)sb.Capacity, IntPtr.Zero);
            if (hr == 0 && sb.Length > 0)
            {
                return sb.ToString();
            }
        }
        catch
        {
            // Ignore error and return string as is
        }

        return trimmed;
    }
}
