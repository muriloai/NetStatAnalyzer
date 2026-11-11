using System.Windows;
using System.Windows.Media.Imaging;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;
using NetStatAnalyzer.Presentation.Common;
using NetStatAnalyzer.Presentation.Services;

namespace NetStatAnalyzer.Presentation.ViewModels
{
    public class ProcessAggregateItemViewModel : ViewModelBase
    {
        public int PID { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string? ProcessPath { get; set; }
        public double CpuPercent { get; set; }
        public double ReadBytesPerSec { get; set; }
        public double WriteBytesPerSec { get; set; }
        public double TotalBytesPerSec => ReadBytesPerSec + WriteBytesPerSec;
        public int ActiveConnectionsCount { get; set; }
        public ThreatLevel ThreatLevel { get; set; }
        public string ThreatReason { get; set; } = string.Empty;

        public BitmapImage? ProcessIcon => ProcessIconCache.Instance.GetIcon(ProcessPath);

        public string FormattedCpu => $"{CpuPercent:F1}%";
        public string FormattedTotalBandwidth => ProcessMetricInfo.FormatBytesPerSec(TotalBytesPerSec);
        public string FormattedReadBandwidth => ProcessMetricInfo.FormatBytesPerSec(ReadBytesPerSec);
        public string FormattedWriteBandwidth => ProcessMetricInfo.FormatBytesPerSec(WriteBytesPerSec);

        public string CpuBadgeBackground => CpuPercent switch
        {
            >= 50.0 => "#7F1D1D",
            >= 20.0 => "#854D0E",
            >= 5.0 => "#1E3A8A",
            _ => "#1F2937"
        };

        public string CpuBadgeForeground => CpuPercent switch
        {
            >= 50.0 => "#FCA5A5",
            >= 20.0 => "#FDE047",
            >= 5.0 => "#93C5FD",
            _ => "#E2E8F0"
        };

        public string ThreatBadgeText => ThreatLevel switch
        {
            ThreatLevel.Critical => "🚨 CRÍTICO",
            ThreatLevel.High => "⚠️ ALTO RISCO",
            ThreatLevel.Medium => "⚠️ SUSPEITO",
            ThreatLevel.Low => "ℹ️ ATENÇÃO",
            _ => "NORMAL"
        };

        public string ThreatBadgeBackground => ThreatLevel switch
        {
            ThreatLevel.Critical => "#7F1D1D",
            ThreatLevel.High => "#991B1B",
            ThreatLevel.Medium => "#854D0E",
            ThreatLevel.Low => "#1E3A8A",
            _ => "#161B26"
        };

        public string ThreatBadgeForeground => ThreatLevel switch
        {
            ThreatLevel.Critical or ThreatLevel.High => "#FCA5A5",
            ThreatLevel.Medium => "#FDE047",
            ThreatLevel.Low => "#93C5FD",
            _ => "#94A3B8"
        };

        public string ThreatBadgeToolTip => ThreatLevel switch
        {
            ThreatLevel.Critical or ThreatLevel.High or ThreatLevel.Medium or ThreatLevel.Low when !string.IsNullOrWhiteSpace(ThreatReason) => ThreatReason,
            _ => ThreatLevel == ThreatLevel.Normal ? "Processo íntegro e sem anomalias detectadas" : ThreatBadgeText
        };
        public Visibility ThreatBadgeVisibility => Visibility.Visible;
        public Visibility ThreatReasonVisibility => string.IsNullOrWhiteSpace(ThreatReason) ? Visibility.Collapsed : Visibility.Visible;
    }
}
