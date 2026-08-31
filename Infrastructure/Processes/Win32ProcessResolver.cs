using System;
using System.Diagnostics;
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

            try
            {
                using var process = Process.GetProcessById(pid);
                return process.MainModule?.FileName;
            }
            catch
            {
                return null;
            }
        }
    }
}
