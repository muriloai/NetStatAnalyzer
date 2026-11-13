using NetStatAnalyzer.Application.UseCases;
using NetStatAnalyzer.Infrastructure.Exporting;
using NetStatAnalyzer.Infrastructure.Monitoring;
using NetStatAnalyzer.Infrastructure.Persistence;
using NetStatAnalyzer.Infrastructure.Processes;
using NetStatAnalyzer.Infrastructure.Scanning;
using NetStatAnalyzer.Infrastructure.System;
using NetStatAnalyzer.Presentation.ViewModels;

namespace NetStatAnalyzer
{
    public static class ApplicationCompositionRoot
    {
        public static MainWindow CreateMainWindow()
        {
            var networkScanner = new NetStatCliScanner();
            var processResolver = new Win32ProcessResolver();
            var allowlistRepository = new JsonFileAllowlistRepository();
            var connectionExporter = new FileConnectionExporter();
            var metricsProvider = new Win32ProcessMetricsProvider();
            var taskScanner = new WindowsScheduledTaskScanner();
            var startupScanner = new WindowsStartupScanner();
            var windowsToolsLauncher = new WindowsToolsLauncher();
            var executablePathResolver = new WindowsExecutablePathResolver();

            var allowlistUseCase = new ManageAllowlistUseCase(allowlistRepository);
            var viewModel = new MainViewModel(
                new ScanConnectionsUseCase(networkScanner, processResolver),
                allowlistUseCase,
                new ManageItemAnnotationsUseCase(allowlistRepository),
                new ExportConnectionsUseCase(connectionExporter),
                new FileAnalysisJsonExporter(),
                new ScanProcessMetricsUseCase(metricsProvider),
                new ScanScheduledTasksUseCase(taskScanner),
                new ScanStartupItemsUseCase(startupScanner),
                windowsToolsLauncher);

            return new MainWindow(
                viewModel,
                executablePathResolver,
                () => new AllowlistManagerViewModel(allowlistUseCase));
        }
    }
}
