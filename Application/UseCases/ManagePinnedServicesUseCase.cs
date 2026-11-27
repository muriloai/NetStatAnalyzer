using NetStatAnalyzer.Application.Contracts;

namespace NetStatAnalyzer.Application.UseCases;

public sealed class ManagePinnedServicesUseCase
{
    private readonly IPinnedServicesRepository _repository;
    private readonly HashSet<string> _pinnedServices;
    private readonly object _lock = new();

    public event EventHandler? PinnedServicesChanged;

    public ManagePinnedServicesUseCase(IPinnedServicesRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _pinnedServices = new HashSet<string>(repository.LoadPinnedServices(), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlySet<string> GetPinnedNames()
    {
        lock (_lock)
        {
            return new HashSet<string>(_pinnedServices, StringComparer.OrdinalIgnoreCase);
        }
    }

    public bool IsPinned(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName)) return false;
        lock (_lock)
        {
            return _pinnedServices.Contains(serviceName.Trim());
        }
    }

    public bool SetPinned(string serviceName, bool isPinned)
    {
        return SetPinned(new[] { serviceName }, isPinned) > 0;
    }

    public int SetPinned(IEnumerable<string> serviceNames, bool isPinned)
    {
        if (serviceNames is null) return 0;

        int changedCount = 0;
        lock (_lock)
        {
            foreach (var rawName in serviceNames)
            {
                if (string.IsNullOrWhiteSpace(rawName)) continue;
                string cleanName = rawName.Trim();

                if (isPinned)
                {
                    if (_pinnedServices.Add(cleanName))
                    {
                        changedCount++;
                    }
                }
                else
                {
                    if (_pinnedServices.Remove(cleanName))
                    {
                        changedCount++;
                    }
                }
            }

            if (changedCount > 0)
            {
                _repository.SavePinnedServices(_pinnedServices.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
            }
        }

        if (changedCount > 0)
        {
            PinnedServicesChanged?.Invoke(this, EventArgs.Empty);
        }

        return changedCount;
    }
}
