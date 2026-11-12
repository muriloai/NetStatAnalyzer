using NetStatAnalyzer.Application.DTOs;
using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Application.Contracts;

public interface IAnalysisJsonExporter
{
    void ExportConnections(string filePath, IEnumerable<NetworkConnection> items, string version, DateTime? dataUpdatedAt);
    void ExportProcesses(string filePath, IEnumerable<ProcessAnalysisItem> items, string version, DateTime? dataUpdatedAt);
    void ExportTasks(string filePath, IEnumerable<ScheduledTaskItem> items, string version, DateTime? dataUpdatedAt);
    void ExportStartupItems(string filePath, IEnumerable<StartupItem> items, string version, DateTime? dataUpdatedAt);
}
