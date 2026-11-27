using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;

namespace NetStatAnalyzer.Application.UseCases;

public sealed class ScanWindowsServicesUseCase
{
    private readonly IWindowsServiceScanner _scanner;
    private readonly ManagePinnedServicesUseCase _pinnedUseCase;

    public ScanWindowsServicesUseCase(
        IWindowsServiceScanner scanner,
        ManagePinnedServicesUseCase pinnedUseCase)
    {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        _pinnedUseCase = pinnedUseCase ?? throw new ArgumentNullException(nameof(pinnedUseCase));
    }

    public async Task<IReadOnlyList<WindowsServiceItem>> ExecuteAsync()
    {
        var scannedServices = await _scanner.ScanAllAsync();
        var pinnedNames = _pinnedUseCase.GetPinnedNames();

        var resultMap = new Dictionary<string, WindowsServiceItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var service in scannedServices)
        {
            service.IsPinned = pinnedNames.Contains(service.ServiceName);
            resultMap[service.ServiceName] = service;
        }

        // Add any pinned services that might not have been returned by SCM (e.g. uninstalled)
        foreach (var pinnedName in pinnedNames)
        {
            if (!resultMap.ContainsKey(pinnedName))
            {
                resultMap[pinnedName] = new WindowsServiceItem
                {
                    ServiceName = pinnedName,
                    DisplayName = pinnedName,
                    Description = "Serviço fixado anteriormente não foi encontrado no sistema (possivelmente desinstalado).",
                    State = WindowsServiceState.NotInstalled,
                    StartType = WindowsServiceStartType.Unknown,
                    IsPinned = true,
                    AcceptsStop = false,
                    AcceptsPauseAndContinue = false
                };
            }
        }

        return resultMap.Values
            .OrderByDescending(s => s.IsPinned)
            .ThenBy(s => s.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
