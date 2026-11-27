using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Application.UseCases;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;
using NetStatAnalyzer.Domain.Policies;
using Xunit;

namespace NetStatAnalyzer.Tests;

public class WindowsServicesTests
{
    [Fact]
    public void ActionPolicy_StoppedService_CanOnlyStartIfNotDisabled()
    {
        var normalStopped = new WindowsServiceItem
        {
            ServiceName = "TestSvc",
            State = WindowsServiceState.Stopped,
            StartType = WindowsServiceStartType.Manual,
            AcceptsStop = true
        };

        Assert.True(WindowsServiceActionPolicy.CanExecute(normalStopped, WindowsServiceAction.Start));
        Assert.False(WindowsServiceActionPolicy.CanExecute(normalStopped, WindowsServiceAction.Stop));
        Assert.False(WindowsServiceActionPolicy.CanExecute(normalStopped, WindowsServiceAction.Pause));
        Assert.False(WindowsServiceActionPolicy.CanExecute(normalStopped, WindowsServiceAction.Continue));
        Assert.False(WindowsServiceActionPolicy.CanExecute(normalStopped, WindowsServiceAction.Restart));

        var disabledStopped = new WindowsServiceItem
        {
            ServiceName = "DisabledSvc",
            State = WindowsServiceState.Stopped,
            StartType = WindowsServiceStartType.Disabled,
            AcceptsStop = true
        };

        Assert.False(WindowsServiceActionPolicy.CanExecute(disabledStopped, WindowsServiceAction.Start));
        Assert.False(WindowsServiceActionPolicy.CanExecute(disabledStopped, WindowsServiceAction.Stop));
        Assert.False(WindowsServiceActionPolicy.CanExecute(disabledStopped, WindowsServiceAction.Pause));
        Assert.False(WindowsServiceActionPolicy.CanExecute(disabledStopped, WindowsServiceAction.Continue));
        Assert.False(WindowsServiceActionPolicy.CanExecute(disabledStopped, WindowsServiceAction.Restart));
    }

    [Fact]
    public void ActionPolicy_RunningService_WithoutPause_CanOnlyStopAndRestart()
    {
        var runningSvc = new WindowsServiceItem
        {
            ServiceName = "Spooler",
            State = WindowsServiceState.Running,
            StartType = WindowsServiceStartType.Automatic,
            AcceptsStop = true,
            AcceptsPauseAndContinue = false
        };

        Assert.False(WindowsServiceActionPolicy.CanExecute(runningSvc, WindowsServiceAction.Start));
        Assert.True(WindowsServiceActionPolicy.CanExecute(runningSvc, WindowsServiceAction.Stop));
        Assert.False(WindowsServiceActionPolicy.CanExecute(runningSvc, WindowsServiceAction.Pause));
        Assert.False(WindowsServiceActionPolicy.CanExecute(runningSvc, WindowsServiceAction.Continue));
        Assert.True(WindowsServiceActionPolicy.CanExecute(runningSvc, WindowsServiceAction.Restart));
    }

    [Fact]
    public void ActionPolicy_RunningService_WithPause_CanPauseStopAndRestart()
    {
        var pausableSvc = new WindowsServiceItem
        {
            ServiceName = "LanmanWorkstation",
            State = WindowsServiceState.Running,
            StartType = WindowsServiceStartType.Automatic,
            AcceptsStop = true,
            AcceptsPauseAndContinue = true
        };

        Assert.False(WindowsServiceActionPolicy.CanExecute(pausableSvc, WindowsServiceAction.Start));
        Assert.True(WindowsServiceActionPolicy.CanExecute(pausableSvc, WindowsServiceAction.Stop));
        Assert.True(WindowsServiceActionPolicy.CanExecute(pausableSvc, WindowsServiceAction.Pause));
        Assert.False(WindowsServiceActionPolicy.CanExecute(pausableSvc, WindowsServiceAction.Continue));
        Assert.True(WindowsServiceActionPolicy.CanExecute(pausableSvc, WindowsServiceAction.Restart));
    }

    [Fact]
    public void ActionPolicy_RunningService_WithoutStop_CannotStopOrRestart()
    {
        var criticalSystemSvc = new WindowsServiceItem
        {
            ServiceName = "RpcSs",
            State = WindowsServiceState.Running,
            StartType = WindowsServiceStartType.Automatic,
            AcceptsStop = false,
            AcceptsPauseAndContinue = false
        };

        Assert.False(WindowsServiceActionPolicy.CanExecute(criticalSystemSvc, WindowsServiceAction.Start));
        Assert.False(WindowsServiceActionPolicy.CanExecute(criticalSystemSvc, WindowsServiceAction.Stop));
        Assert.False(WindowsServiceActionPolicy.CanExecute(criticalSystemSvc, WindowsServiceAction.Pause));
        Assert.False(WindowsServiceActionPolicy.CanExecute(criticalSystemSvc, WindowsServiceAction.Continue));
        Assert.False(WindowsServiceActionPolicy.CanExecute(criticalSystemSvc, WindowsServiceAction.Restart));
    }

    [Fact]
    public void ActionPolicy_PausedService_CanContinueStopAndRestart()
    {
        var pausedSvc = new WindowsServiceItem
        {
            ServiceName = "PausableSvc",
            State = WindowsServiceState.Paused,
            StartType = WindowsServiceStartType.Automatic,
            AcceptsStop = true,
            AcceptsPauseAndContinue = true
        };

        Assert.False(WindowsServiceActionPolicy.CanExecute(pausedSvc, WindowsServiceAction.Start));
        Assert.True(WindowsServiceActionPolicy.CanExecute(pausedSvc, WindowsServiceAction.Stop));
        Assert.False(WindowsServiceActionPolicy.CanExecute(pausedSvc, WindowsServiceAction.Pause));
        Assert.True(WindowsServiceActionPolicy.CanExecute(pausedSvc, WindowsServiceAction.Continue));
        Assert.True(WindowsServiceActionPolicy.CanExecute(pausedSvc, WindowsServiceAction.Restart));
    }

    [Theory]
    [InlineData(WindowsServiceState.StartPending)]
    [InlineData(WindowsServiceState.StopPending)]
    [InlineData(WindowsServiceState.PausePending)]
    [InlineData(WindowsServiceState.ContinuePending)]
    [InlineData(WindowsServiceState.NotInstalled)]
    [InlineData(WindowsServiceState.Unknown)]
    public void ActionPolicy_PendingOrUninstalled_AllActionsDisabled(WindowsServiceState state)
    {
        var pendingSvc = new WindowsServiceItem
        {
            ServiceName = "PendingSvc",
            State = state,
            AcceptsStop = true,
            AcceptsPauseAndContinue = true
        };

        Assert.False(WindowsServiceActionPolicy.CanExecute(pendingSvc, WindowsServiceAction.Start));
        Assert.False(WindowsServiceActionPolicy.CanExecute(pendingSvc, WindowsServiceAction.Stop));
        Assert.False(WindowsServiceActionPolicy.CanExecute(pendingSvc, WindowsServiceAction.Pause));
        Assert.False(WindowsServiceActionPolicy.CanExecute(pendingSvc, WindowsServiceAction.Continue));
        Assert.False(WindowsServiceActionPolicy.CanExecute(pendingSvc, WindowsServiceAction.Restart));
    }

    [Fact]
    public async Task ControlUseCase_BatchWithMixedStates_OnlyExecutesOnEligible()
    {
        var controllerMock = new FakeServiceController();
        var useCase = new ControlWindowsServicesUseCase(controllerMock);

        var list = new List<WindowsServiceItem>();

        // 8 stopped services
        for (int i = 1; i <= 8; i++)
        {
            list.Add(new WindowsServiceItem
            {
                ServiceName = $"StoppedSvc{i}",
                State = WindowsServiceState.Stopped,
                StartType = WindowsServiceStartType.Manual,
                AcceptsStop = true
            });
        }

        // 2 running services that accept stop
        list.Add(new WindowsServiceItem
        {
            ServiceName = "RunningSvc1",
            State = WindowsServiceState.Running,
            StartType = WindowsServiceStartType.Automatic,
            AcceptsStop = true
        });
        list.Add(new WindowsServiceItem
        {
            ServiceName = "RunningSvc2",
            State = WindowsServiceState.Running,
            StartType = WindowsServiceStartType.Automatic,
            AcceptsStop = true
        });

        // Action: Stop
        var summary = await useCase.ExecuteAsync(list, WindowsServiceAction.Stop);

        Assert.Equal(10, summary.TotalSelected);
        Assert.Equal(2, summary.EligibleCount);
        Assert.Equal(8, summary.SkippedCount);
        Assert.Equal(2, summary.SucceededCount);

        // Verify controller only received the 2 running services!
        Assert.Equal(2, controllerMock.LastRequestedServices.Count);
        Assert.Contains("RunningSvc1", controllerMock.LastRequestedServices);
        Assert.Contains("RunningSvc2", controllerMock.LastRequestedServices);
    }

    [Fact]
    public void ManagePinnedServices_PinAndUnpin_PersistsCorrectly()
    {
        var repo = new InMemoryPinnedRepo();
        var useCase = new ManagePinnedServicesUseCase(repo);

        useCase.SetPinned(new[] { "wuauserv", "spooler" }, true);

        Assert.True(useCase.IsPinned("wuauserv"));
        Assert.True(useCase.IsPinned("WUAUSERV")); // Case-insensitive
        Assert.True(useCase.IsPinned("Spooler"));
        Assert.False(useCase.IsPinned("nonexistent"));

        // Unpin one
        useCase.SetPinned("wuauserv", false);
        Assert.False(useCase.IsPinned("wuauserv"));
        Assert.True(useCase.IsPinned("spooler"));
    }

    [Fact]
    public async Task ScanUseCase_CombinesScannerWithPinned_AndMarksMissingAsNotInstalled()
    {
        var repo = new InMemoryPinnedRepo(new[] { "InstalledSvc", "DeletedAppSvc" });
        var pinnedUseCase = new ManagePinnedServicesUseCase(repo);

        var scannerMock = new FakeServiceScanner(new[]
        {
            new WindowsServiceItem
            {
                ServiceName = "InstalledSvc",
                DisplayName = "Installed Service",
                State = WindowsServiceState.Running
            },
            new WindowsServiceItem
            {
                ServiceName = "OtherSvc",
                DisplayName = "Other Service",
                State = WindowsServiceState.Stopped
            }
        });

        var scanUseCase = new ScanWindowsServicesUseCase(scannerMock, pinnedUseCase);
        var result = await scanUseCase.ExecuteAsync();

        Assert.Equal(3, result.Count);

        var installed = result.First(s => s.ServiceName.Equals("InstalledSvc", StringComparison.OrdinalIgnoreCase));
        Assert.True(installed.IsPinned);
        Assert.Equal(WindowsServiceState.Running, installed.State);

        var deleted = result.First(s => s.ServiceName.Equals("DeletedAppSvc", StringComparison.OrdinalIgnoreCase));
        Assert.True(deleted.IsPinned);
        Assert.Equal(WindowsServiceState.NotInstalled, deleted.State);

        var other = result.First(s => s.ServiceName.Equals("OtherSvc", StringComparison.OrdinalIgnoreCase));
        Assert.False(other.IsPinned);
    }

    private sealed class InMemoryPinnedRepo : IPinnedServicesRepository
    {
        private readonly List<string> _list;

        public InMemoryPinnedRepo(IEnumerable<string>? initial = null)
        {
            _list = initial?.ToList() ?? new List<string>();
        }

        public IReadOnlyList<string> LoadPinnedServices() => _list.ToList();

        public void SavePinnedServices(IEnumerable<string> serviceNames)
        {
            _list.Clear();
            _list.AddRange(serviceNames);
        }
    }

    private sealed class FakeServiceScanner : IWindowsServiceScanner
    {
        private readonly IReadOnlyList<WindowsServiceItem> _items;

        public FakeServiceScanner(IEnumerable<WindowsServiceItem> items)
        {
            _items = items.ToList();
        }

        public Task<IReadOnlyList<WindowsServiceItem>> ScanAllAsync() => Task.FromResult(_items);
    }

    private sealed class FakeServiceController : IWindowsServiceController
    {
        public List<string> LastRequestedServices { get; } = new();

        public Task<ServiceBatchControlResult> ExecuteBatchAsync(
            IReadOnlyList<string> serviceNames,
            WindowsServiceAction action,
            CancellationToken cancellationToken = default)
        {
            LastRequestedServices.Clear();
            LastRequestedServices.AddRange(serviceNames);
            return Task.FromResult(new ServiceBatchControlResult(serviceNames.Count, serviceNames.Count, Array.Empty<string>(), false));
        }
    }
}
