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
            _ when IsMicrosoft => "🛡️ SISTEMA",
            _ when IsSigned => "🛡️ ASSINADO",
            _ => "⚠️ NÃO ASSINADO"
        };

        public string ThreatBadgeBackground => ThreatLevel switch
        {
            ThreatLevel.Critical => "#7F1D1D",
            ThreatLevel.High => "#991B1B",
            ThreatLevel.Medium when !FileExists => "#7C2D12",
            ThreatLevel.Medium => "#713F12",
            ThreatLevel.Low when !IsSigned => "#451A03",
            ThreatLevel.Low => "#1E3A8A",
            _ when IsMicrosoft => "#064E3B", // Emerald green for OS component
            _ when IsSigned => "#0C3B5E",   // Distinct Tech Blue for signed third-party
            _ => "#451A03"
        };

        public string ThreatBadgeForeground => ThreatLevel switch
        {
            ThreatLevel.Critical or ThreatLevel.High => "#FCA5A5",
            ThreatLevel.Medium when !FileExists => "#FDBA74",
            ThreatLevel.Medium => "#FDE047",
            ThreatLevel.Low when !IsSigned => "#FCD34D",
            ThreatLevel.Low => "#93C5FD",
            _ when IsMicrosoft => "#6EE7B7",
            _ when IsSigned => "#7DD3FC",
            _ => "#FCD34D"
        };

        public string ThreatBadgeToolTip => ThreatLevel switch
        {
            ThreatLevel.Critical or ThreatLevel.High => ThreatReason,
            ThreatLevel.Medium when !FileExists => "Arquivo executável não foi encontrado no disco (Órfão)",
            ThreatLevel.Medium => ThreatReason,
            ThreatLevel.Low when !IsSigned => "Executável de terceiro sem assinatura digital detectada",
            ThreatLevel.Low => ThreatReason,
            _ when IsMicrosoft => "Componente oficial do sistema operacional Windows (Microsoft)",
            _ when IsSigned => !string.IsNullOrWhiteSpace(SignerName) ? $"Assinado digitalmente por: {SignerName}" : "Executável de terceiro com assinatura digital válida",
            _ => "Executável de terceiro sem assinatura digital detectada"
        };

        public Visibility ThreatBadgeVisibility => Visibility.Visible;
        public Visibility ThreatReasonVisibility => string.IsNullOrWhiteSpace(ThreatReason) ? Visibility.Collapsed : Visibility.Visible;

        public StartupItemViewModel(StartupItem model)
        {
            Model = model;
        }
    }
}
