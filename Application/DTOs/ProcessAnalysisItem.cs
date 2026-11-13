using NetStatAnalyzer.Domain.Enums;

namespace NetStatAnalyzer.Application.DTOs;

public sealed record ProcessAnalysisItem(
    int PID,
    string ProcessName,
    string? ProcessPath,
    double CpuPercent,
    double ReadBytesPerSec,
    double WriteBytesPerSec,
    int ActiveConnectionsCount,
    ThreatLevel ThreatLevel,
    string ThreatReason,
    string Tag = "",
    bool IsTrusted = false);
