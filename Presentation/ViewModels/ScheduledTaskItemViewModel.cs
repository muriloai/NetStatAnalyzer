using System.Windows;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;
using NetStatAnalyzer.Presentation.Common;

namespace NetStatAnalyzer.Presentation.ViewModels
{
    public class ScheduledTaskItemViewModel : ViewModelBase
    {
        public ScheduledTaskItem Model { get; }

        public string TaskName => Model.TaskName;
        public string TaskPath => Model.TaskPath;
        public string State => Model.State;
        public string NextRunTime => Model.NextRunTime;
        public string LastRunTime => Model.LastRunTime;
        public string Author => Model.Author;
        public string TaskToRun => Model.TaskToRun;
        public string ScheduleType => Model.ScheduleType;
        public bool IsMicrosoftTask => Model.IsMicrosoftTask;
        public ThreatLevel ThreatLevel => Model.ThreatLevel;
        public string ThreatReason => Model.ThreatReason;

        public bool IsSuspicious => ThreatLevel >= ThreatLevel.Medium;

        public string ThreatBadgeText => ThreatLevel switch
        {
            ThreatLevel.Critical => "CRÍTICO",
            ThreatLevel.High => "ALTO RISCO",
            ThreatLevel.Medium => "SUSPEITO",
            ThreatLevel.Low => "ATENÇÃO",
            _ => IsMicrosoftTask ? "SISTEMA" : "NORMAL"
        };

        public string ThreatBadgeBackground => ThreatLevel switch
        {
            ThreatLevel.Critical => "#7F1D1D",
            ThreatLevel.High => "#991B1B",
            ThreatLevel.Medium => "#854D0E",
            ThreatLevel.Low => "#1E3A8A",
            _ => IsMicrosoftTask ? "#14532D" : "#1F2937"
        };

        public string ThreatBadgeForeground => ThreatLevel switch
        {
            ThreatLevel.Critical or ThreatLevel.High => "#FCA5A5",
            ThreatLevel.Medium => "#FDE047",
            ThreatLevel.Low => "#93C5FD",
            _ => IsMicrosoftTask ? "#86EFAC" : "#9CA3AF"
        };

        public Visibility ThreatReasonVisibility => string.IsNullOrWhiteSpace(ThreatReason) ? Visibility.Collapsed : Visibility.Visible;

        public ScheduledTaskItemViewModel(ScheduledTaskItem model)
        {
            Model = model;
        }
    }
}
