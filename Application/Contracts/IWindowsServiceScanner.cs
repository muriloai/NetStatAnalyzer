using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Application.Contracts;

public interface IWindowsServiceScanner
{
    Task<IReadOnlyList<WindowsServiceItem>> ScanAllAsync();
}
