using System;
using System.Diagnostics;
using System.IO;
using NetStatAnalyzer.Application.Contracts;

namespace NetStatAnalyzer.Infrastructure.Processes
{
    public class Win32ProcessResolver : IProcessResolver
    {
        public string GetProcessName(int pid)
        {
            if (pid == 0) return "Sistema Ocioso";
            if (pid == 4) return "System";

            try
            {
                using var process = Process.GetProcessById(pid);
                return process.ProcessName;
            }
            catch
            {
                return "Desconhecido";
            }
        }

        public string? GetProcessPath(int pid)
        {
            if (pid <= 4) return null;

            string? processName = null;
            try
            {
                using var process = Process.GetProcessById(pid);
                processName = process.ProcessName;
                string? mainModulePath = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(mainModulePath))
                {
                    return mainModulePath;
                }
            }
            catch
            {
                // Access Denied (system services, protected processes) or process exited
            }

            // Fallback for system services and elevated binaries when running without administrator privileges:
            if (!string.IsNullOrWhiteSpace(processName) && !processName.Equals("Desconhecido", StringComparison.OrdinalIgnoreCase))
            {
                string exeName = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? processName : processName + ".exe";

                // 1. Check System32
                string sys32Path = Path.Combine(Environment.SystemDirectory, exeName);
                if (File.Exists(sys32Path)) return sys32Path;

                // 2. Check Windows root
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string winPath = Path.Combine(winDir, exeName);
                if (File.Exists(winPath)) return winPath;

                // 3. Check SysWOW64
                string sysWowPath = Path.Combine(winDir, "SysWOW64", exeName);
                if (File.Exists(sysWowPath)) return sysWowPath;
            }

            return null;
        }
    }
}
