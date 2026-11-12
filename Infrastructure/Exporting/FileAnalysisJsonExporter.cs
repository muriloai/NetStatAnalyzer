using System.Text;
using System.Text.Json;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Application.DTOs;
using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Infrastructure.Exporting;

public sealed class FileAnalysisJsonExporter : IAnalysisJsonExporter
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public void ExportConnections(string filePath, IEnumerable<NetworkConnection> items, string version, DateTime? dataUpdatedAt) =>
        Write(filePath, "connections", version, dataUpdatedAt, items.Select(item => new
        {
            processName = item.ProcessName, pid = item.PID, processPath = item.ProcessPath,
            protocol = item.DisplayProtocol, protocolRaw = item.ProtocolRaw,
            localAddress = item.LocalAddress, foreignAddress = item.ForeignAddress,
            state = item.DisplayState, stateRaw = item.StateRaw,
            isTrusted = item.IsTrusted, trustStatus = item.IsTrusted ? "Confiável" : "Não verificada",
            cpuPercent = item.CpuPercent, bandwidthBytesPerSec = item.BandwidthBytesPerSec,
            threatLevel = item.ThreatLevel.ToString(), threatReason = item.ThreatReason
        }));

    public void ExportProcesses(string filePath, IEnumerable<ProcessAnalysisItem> items, string version, DateTime? dataUpdatedAt) =>
        Write(filePath, "processes", version, dataUpdatedAt, items.Select(item => new
        {
            processName = item.ProcessName, pid = item.PID, processPath = item.ProcessPath,
            cpuPercent = item.CpuPercent, readBytesPerSec = item.ReadBytesPerSec,
            writeBytesPerSec = item.WriteBytesPerSec,
            totalBytesPerSec = item.ReadBytesPerSec + item.WriteBytesPerSec,
            activeConnectionsCount = item.ActiveConnectionsCount,
            threatLevel = item.ThreatLevel.ToString(), threatReason = item.ThreatReason
        }));

    public void ExportTasks(string filePath, IEnumerable<ScheduledTaskItem> items, string version, DateTime? dataUpdatedAt) =>
        Write(filePath, "scheduledTasks", version, dataUpdatedAt, items.Select(item => new
        {
            taskName = item.TaskName, taskPath = item.TaskPath, state = item.State,
            nextRunTime = item.NextRunTime, lastRunTime = item.LastRunTime,
            author = item.Author, taskToRun = item.TaskToRun, scheduleType = item.ScheduleType,
            isMicrosoftTask = item.IsMicrosoftTask, threatLevel = item.ThreatLevel.ToString(),
            threatReason = item.ThreatReason, resolvedPath = item.ResolvedPath,
            fileExists = item.FileExists, isSigned = item.IsSigned,
            signerName = item.SignerName, companyName = item.CompanyName
        }));

    public void ExportStartupItems(string filePath, IEnumerable<StartupItem> items, string version, DateTime? dataUpdatedAt) =>
        Write(filePath, "startupItems", version, dataUpdatedAt, items.Select(item => new
        {
            name = item.Name, command = item.Command, location = item.Location,
            sourceType = item.SourceType, isEnabled = item.IsEnabled,
            threatLevel = item.ThreatLevel.ToString(), threatReason = item.ThreatReason,
            resolvedPath = item.ResolvedPath, fileExists = item.FileExists,
            isSigned = item.IsSigned, signerName = item.SignerName,
            isMicrosoft = item.IsMicrosoft, companyName = item.CompanyName
        }));

    private static void Write<T>(string filePath, string dataType, string version, DateTime? dataUpdatedAt, IEnumerable<T> items)
    {
        var data = items.ToArray();
        var document = new
        {
            app = "NetStatAnalyzer", version, dataType,
            exportedAt = DateTimeOffset.Now,
            dataUpdatedAt,
            scope = "visibleItems",
            totalItems = data.Length,
            items = data
        };
        File.WriteAllText(filePath, JsonSerializer.Serialize(document, Options), new UTF8Encoding(false));
    }
}
