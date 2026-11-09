using System.Windows;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;
using NetStatAnalyzer.Presentation.Common;

namespace NetStatAnalyzer.Presentation.ViewModels
{
    public class StartupItemViewModel : ViewModelBase
    {
        public StartupItem Model { get; }

        public string Name => Model.Name;
        public string Command => Model.Command;
        public string Location => Model.Location;
        public string SourceType => Model.SourceType;
        public bool IsEnabled => Model.IsEnabled;
        public ThreatLevel ThreatLevel => Model.ThreatLevel;
        public string ThreatReason => Model.ThreatReason;

        public bool IsSuspicious => ThreatLevel >= ThreatLevel.Medium;

        public string ThreatBadgeText => ThreatLevel switch
        {
            ThreatLevel.Critical => "CRÍTICO",
            ThreatLevel.High => "ALTO RISCO",
            ThreatLevel.Medium => "SUSPEITO",
            ThreatLevel.Low => "ATENÇÃO",
            _ => "CONFIÁVEL"
        };

        public string ThreatBadgeBackground => ThreatLevel switch
        {
            ThreatLevel.Critical => "#7F1D1D",
            ThreatLevel.High => "#991B1B",
            ThreatLevel.Medium => "#854D0E",
            ThreatLevel.Low => "#1E3A8A",
            _ => "#064E3B"
        };

        public string ThreatBadgeForeground => ThreatLevel switch
        {
            ThreatLevel.Critical or ThreatLevel.High => "#FCA5A5",
            ThreatLevel.Medium => "#FDE047",
            ThreatLevel.Low => "#93C5FD",
            _ => "#34D399"
        };

        public Visibility ThreatReasonVisibility => string.IsNullOrWhiteSpace(ThreatReason) ? Visibility.Collapsed : Visibility.Visible;

        public StartupItemViewModel(StartupItem model)
        {
            Model = model;
        }
    }
}
