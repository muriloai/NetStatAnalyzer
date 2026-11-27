using NetStatAnalyzer.Domain.Enums;

namespace NetStatAnalyzer.Application.Contracts;

public record ServiceBatchControlResult(
    int RequestedCount,
    int SucceededCount,
    IReadOnlyList<string> FailedMessages,
    bool ElevationCancelled);

public interface IWindowsServiceController
{
    Task<ServiceBatchControlResult> ExecuteBatchAsync(
        IReadOnlyList<string> serviceNames,
        WindowsServiceAction action,
        CancellationToken cancellationToken = default);
}
