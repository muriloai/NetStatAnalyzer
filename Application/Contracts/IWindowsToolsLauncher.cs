namespace NetStatAnalyzer.Application.Contracts
{
    public interface IWindowsToolsLauncher
    {
        bool OpenTaskScheduler();
        bool OpenStartupAppsSettings();
        bool OpenTaskManager();
        bool OpenResourceMonitor();
        bool OpenStartupFolder();
        bool OpenRegistryEditor(string? keyPath = null);
    }
}
