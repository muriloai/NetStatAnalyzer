using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;

namespace NetStatAnalyzer.Domain.Policies;

public static class WindowsServiceActionPolicy
{
    public static bool CanExecute(WindowsServiceItem? item, WindowsServiceAction action)
    {
        if (item is null) return false;
        if (item.State is WindowsServiceState.Unknown or WindowsServiceState.NotInstalled) return false;

        // Pending states do not accept any actions
        if (item.State is WindowsServiceState.StartPending
            or WindowsServiceState.StopPending
            or WindowsServiceState.PausePending
            or WindowsServiceState.ContinuePending)
        {
            return false;
        }

        return action switch
        {
            WindowsServiceAction.Start =>
                item.State == WindowsServiceState.Stopped && item.StartType != WindowsServiceStartType.Disabled,

            WindowsServiceAction.Stop =>
                (item.State == WindowsServiceState.Running || item.State == WindowsServiceState.Paused) && item.AcceptsStop,

            WindowsServiceAction.Pause =>
                item.State == WindowsServiceState.Running && item.AcceptsPauseAndContinue,

            WindowsServiceAction.Continue =>
                item.State == WindowsServiceState.Paused && item.AcceptsPauseAndContinue,

            WindowsServiceAction.Restart =>
                (item.State == WindowsServiceState.Running || item.State == WindowsServiceState.Paused)
                && item.AcceptsStop
                && item.StartType != WindowsServiceStartType.Disabled,

            _ => false
        };
    }

    public static IReadOnlyList<WindowsServiceItem> FilterEligible(IEnumerable<WindowsServiceItem> items, WindowsServiceAction action)
    {
        return items.Where(item => CanExecute(item, action)).ToList();
    }
}
