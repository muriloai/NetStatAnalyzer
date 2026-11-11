using System;
using System.Diagnostics;
using System.IO;
using NetStatAnalyzer.Application.Contracts;

namespace NetStatAnalyzer.Infrastructure.System
{
    public sealed class WindowsToolsLauncher : IWindowsToolsLauncher
    {
        public bool OpenTaskScheduler()
        {
            return LaunchTool("taskschd.msc");
        }

        public bool OpenStartupAppsSettings()
        {
            return LaunchTool("ms-settings:startupapps");
        }

        public bool OpenTaskManager()
        {
            return LaunchTool("taskmgr.exe");
        }

        public bool OpenResourceMonitor()
        {
            return LaunchTool("resmon.exe");
        }

        public bool OpenStartupFolder()
        {
            string userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (Directory.Exists(userStartup))
            {
                return LaunchTool("explorer.exe", $"\"{userStartup}\"");
            }
            return LaunchTool("explorer.exe", "shell:startup");
        }

        public bool OpenRegistryEditor()
        {
            return LaunchTool("regedit.exe");
        }

        private bool LaunchTool(string fileName, string arguments = "")
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
