using NetStatAnalyzer.Domain.Enums;

namespace NetStatAnalyzer.Domain.Entities
{
    public class ScheduledTaskItem
    {
        public string TaskName { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public bool IsTrusted { get; set; }
        public string TaskPath { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string NextRunTime { get; set; } = string.Empty;
        public string LastRunTime { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string TaskToRun { get; set; } = string.Empty;
        public string ScheduleType { get; set; } = string.Empty;
        public bool IsMicrosoftTask { get; set; }
        public ThreatLevel ThreatLevel { get; set; } = ThreatLevel.Normal;
        public string ThreatReason { get; set; } = string.Empty;

        // File and Authenticode Audit Info
        public string ResolvedPath { get; set; } = string.Empty;
        public bool FileExists { get; set; } = true;
        public bool IsSigned { get; set; }
        public string? SignerName { get; set; }
        public string? CompanyName { get; set; }
    }
}
