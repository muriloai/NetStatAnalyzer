using NetStatAnalyzer.Domain.Enums;

namespace NetStatAnalyzer.Domain.Entities;

public class WindowsServiceItem
{
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WindowsServiceState State { get; set; } = WindowsServiceState.Unknown;
    public WindowsServiceStartType StartType { get; set; } = WindowsServiceStartType.Unknown;
    public int ProcessId { get; set; }
    public string BinaryPathName { get; set; } = string.Empty;
    public string ResolvedPath { get; set; } = string.Empty;
    public string ServiceAccount { get; set; } = string.Empty;
    public bool AcceptsStop { get; set; }
    public bool AcceptsPauseAndContinue { get; set; }
    public bool IsPinned { get; set; }
    public string Tag { get; set; } = string.Empty;
    public bool IsTrusted { get; set; }
    public bool FileExists { get; set; }
    public bool IsSigned { get; set; }
    public bool IsMicrosoft { get; set; }
    public string? SignerName { get; set; }
    public string? CompanyName { get; set; }
    public ThreatLevel ThreatLevel { get; set; } = ThreatLevel.Normal;
    public string ThreatReason { get; set; } = string.Empty;
}
