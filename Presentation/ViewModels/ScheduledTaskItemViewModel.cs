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

        public string ResolvedPath => Model.ResolvedPath;
        public bool FileExists => Model.FileExists;
        public bool IsSigned => Model.IsSigned;
        public string? SignerName => Model.SignerName;

        public bool IsSuspicious => ThreatLevel >= ThreatLevel.Medium || !Model.FileExists;

        public string ThreatBadgeText => ThreatLevel switch
        {
            ThreatLevel.Critical => "🚨 CRÍTICO",
            ThreatLevel.High => "⚠️ ALTO RISCO",
            ThreatLevel.Medium when !FileExists => "❓ ÓRFÃO",
            ThreatLevel.Medium => "⚠️ SUSPEITO",
            ThreatLevel.Low when !IsSigned => "⚠️ NÃO ASSINADO",
            ThreatLevel.Low => "ℹ️ ATENÇÃO",
            _ when IsMicrosoftTask => "🛡️ SISTEMA",
            _ when IsSigned => "🛡️ ASSINADO",
            _ => "ℹ️ NORMAL"
        };

        public string ThreatBadgeBackground => ThreatLevel switch
        {
            ThreatLevel.Critical => "#7F1D1D",
            ThreatLevel.High => "#991B1B",
            ThreatLevel.Medium when !FileExists => "#7C2D12",
            ThreatLevel.Medium => "#713F12",
            ThreatLevel.Low when !IsSigned => "#451A03",
            ThreatLevel.Low => "#1E3A8A",
            _ when IsMicrosoftTask => "#064E3B", // Emerald green for OS component
            _ when IsSigned => "#0C3B5E",       // Distinct Tech Blue for signed third-party
            _ => "#1F2937"
        };

        public string ThreatBadgeForeground => ThreatLevel switch
        {
            ThreatLevel.Critical or ThreatLevel.High => "#FCA5A5",
            ThreatLevel.Medium when !FileExists => "#FDBA74",
            ThreatLevel.Medium => "#FDE047",
            ThreatLevel.Low when !IsSigned => "#FCD34D",
            ThreatLevel.Low => "#93C5FD",
            _ when IsMicrosoftTask => "#6EE7B7",
            _ when IsSigned => "#7DD3FC",
            _ => "#9CA3AF"
        };

        public string ThreatBadgeToolTip => ThreatLevel switch
        {
            ThreatLevel.Critical or ThreatLevel.High => ThreatReason,
            ThreatLevel.Medium when !FileExists => "Arquivo executável da tarefa não foi encontrado no disco (Órfão)",
            ThreatLevel.Medium => ThreatReason,
            ThreatLevel.Low when !IsSigned => "Executável de terceiro sem assinatura digital detectada",
            ThreatLevel.Low => ThreatReason,
            _ when IsMicrosoftTask => "Componente oficial do sistema operacional Windows (Microsoft)",
            _ when IsSigned => !string.IsNullOrWhiteSpace(SignerName) ? $"Assinado digitalmente por: {SignerName}" : "Executável de terceiro com assinatura digital válida",
            _ => "Tarefa agendada em conformidade"
        };

        public Visibility ThreatReasonVisibility => string.IsNullOrWhiteSpace(ThreatReason) ? Visibility.Collapsed : Visibility.Visible;

        public ScheduledTaskItemViewModel(ScheduledTaskItem model)
        {
            Model = model;
        }
    }
}
