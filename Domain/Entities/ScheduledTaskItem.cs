using System;
using NetStatAnalyzer.Domain.Enums;

namespace NetStatAnalyzer.Domain.Entities
{
    public class ScheduledTaskItem
    {
        public string TaskName { get; set; } = string.Empty;
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
    }
}
