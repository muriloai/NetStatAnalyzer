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

        public string ResolvedPath => Model.ResolvedPath;
        public bool FileExists => Model.FileExists;
        public bool IsSigned => Model.IsSigned;
        public string? SignerName => Model.SignerName;
        public bool IsMicrosoft => Model.IsMicrosoft;

        public bool IsSuspicious => ThreatLevel >= ThreatLevel.Medium || !Model.FileExists;

        public string ThreatBadgeText => ThreatLevel switch
        {
            ThreatLevel.Critical => "🚨 CRÍTICO",
            ThreatLevel.High => "⚠️ ALTO RISCO",
            ThreatLevel.Medium when !FileExists => "❓ ÓRFÃO",
            ThreatLevel.Medium => "⚠️ SUSPEITO",
            ThreatLevel.Low when !IsSigned => "⚠️ NÃO ASSINADO",
            ThreatLevel.Low => "ℹ️ ATENÇÃO",
            _ when IsMicrosoft => "🛡️ MICROSOFT",
            _ when IsSigned => "🛡️ ASSINADO",
            _ => "ℹ️ NÃO ASSINADO"
        };

        public string ThreatBadgeBackground => ThreatLevel switch
        {
            ThreatLevel.Critical => "#7F1D1D",
            ThreatLevel.High => "#991B1B",
            ThreatLevel.Medium => "#854D0E",
            ThreatLevel.Low when !IsSigned => "#78350F",
            ThreatLevel.Low => "#1E3A8A",
            _ when IsMicrosoft => "#14532D",
            _ when IsSigned => "#064E3B",
            _ => "#78350F"
        };

        public string ThreatBadgeForeground => ThreatLevel switch
        {
            ThreatLevel.Critical or ThreatLevel.High => "#FCA5A5",
            ThreatLevel.Medium => "#FDE047",
            ThreatLevel.Low when !IsSigned => "#FCD34D",
            ThreatLevel.Low => "#93C5FD",
            _ when IsMicrosoft => "#86EFAC",
            _ when IsSigned => "#6EE7B7",
            _ => "#FCD34D"
        };

        public Visibility ThreatReasonVisibility => string.IsNullOrWhiteSpace(ThreatReason) ? Visibility.Collapsed : Visibility.Visible;

        public StartupItemViewModel(StartupItem model)
        {
            Model = model;
        }
    }
}
