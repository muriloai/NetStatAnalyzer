using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;
using NetStatAnalyzer.Domain.Policies;

namespace NetStatAnalyzer.Application.UseCases;

public record ServiceControlExecutionSummary(
    WindowsServiceAction Action,
    int TotalSelected,
    int EligibleCount,
    int SkippedCount,
    int SucceededCount,
    IReadOnlyList<string> FailedMessages,
    bool ElevationCancelled);

public sealed class ControlWindowsServicesUseCase
{
    private readonly IWindowsServiceController _controller;

    public ControlWindowsServicesUseCase(IWindowsServiceController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
    }

    public async Task<ServiceControlExecutionSummary> ExecuteAsync(
        IEnumerable<WindowsServiceItem> selectedItems,
        WindowsServiceAction action,
        CancellationToken cancellationToken = default)
    {
        var allSelected = selectedItems?.ToList() ?? new List<WindowsServiceItem>();
        if (allSelected.Count == 0)
        {
            return new ServiceControlExecutionSummary(action, 0, 0, 0, 0, Array.Empty<string>(), false);
        }

        var eligible = WindowsServiceActionPolicy.FilterEligible(allSelected, action);
        int skippedCount = allSelected.Count - eligible.Count;

        if (eligible.Count == 0)
        {
            return new ServiceControlExecutionSummary(action, allSelected.Count, 0, skippedCount, 0, Array.Empty<string>(), false);
        }

        var serviceNames = eligible.Select(e => e.ServiceName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var result = await _controller.ExecuteBatchAsync(serviceNames, action, cancellationToken);

        return new ServiceControlExecutionSummary(
            Action: action,
            TotalSelected: allSelected.Count,
            EligibleCount: eligible.Count,
            SkippedCount: skippedCount,
            SucceededCount: result.SucceededCount,
            FailedMessages: result.FailedMessages,
            ElevationCancelled: result.ElevationCancelled);
    }
}
