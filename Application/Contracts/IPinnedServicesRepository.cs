namespace NetStatAnalyzer.Application.Contracts;

public interface IPinnedServicesRepository
{
    IReadOnlyList<string> LoadPinnedServices();
    void SavePinnedServices(IEnumerable<string> serviceNames);
}
