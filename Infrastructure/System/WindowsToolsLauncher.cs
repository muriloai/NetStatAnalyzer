using System;
using System.Diagnostics;
using System.IO;

namespace NetStatAnalyzer.Infrastructure.System
{
    public static class WindowsToolsLauncher
    {
        public static bool OpenTaskScheduler()
        {
            return LaunchTool("taskschd.msc");
        }

        public static bool OpenStartupAppsSettings()
        {
            // Windows 10 / 11 modern settings URI for startup apps
            return LaunchTool("ms-settings:startupapps");
        }

        public static bool OpenTaskManager(string? tab = null)
        {
            return LaunchTool("taskmgr.exe");
        }

        public static bool OpenResourceMonitor()
        {
            return LaunchTool("resmon.exe");
        }

        public static bool OpenStartupFolder()
        {
            string userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (Directory.Exists(userStartup))
            {
                return LaunchTool("explorer.exe", $"\"{userStartup}\"");
            }
            return LaunchTool("explorer.exe", "shell:startup");
        }

        public static bool OpenRegistryEditor()
        {
            return LaunchTool("regedit.exe");
        }

        private static bool LaunchTool(string fileName, string arguments = "")
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true
                };
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao abrir ferramenta do Windows '{fileName}': {ex.Message}");
                return false;
            }
        }
    }
}
