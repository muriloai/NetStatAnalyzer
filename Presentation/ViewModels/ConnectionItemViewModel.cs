using System.Windows;
using System.Windows.Media.Imaging;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;
using NetStatAnalyzer.Presentation.Common;
using NetStatAnalyzer.Presentation.Services;

namespace NetStatAnalyzer.Presentation.ViewModels
{
    public class ConnectionItemViewModel : ViewModelBase
    {
        public NetworkConnection Model { get; }

        public string Protocol => Model.DisplayProtocol;
        public string LocalAddress => Model.LocalAddress;
        public string ForeignAddress => Model.ForeignAddress;
        public string State => Model.DisplayState;
        public int PID => Model.PID;
        public string ProcessName => Model.ProcessName;
        public string? ProcessPath => Model.ProcessPath;

        public double CpuPercent => Model.CpuPercent;
        public double BandwidthBytesPerSec => Model.BandwidthBytesPerSec;
        public string DisplayCpu => Model.DisplayCpu;
        public string DisplayBandwidth => Model.DisplayBandwidth;
        public ThreatLevel ThreatLevel => Model.ThreatLevel;
        public string ThreatReason => Model.ThreatReason;

        public bool IsAllowed
        {
            get => Model.IsTrusted;
            set
            {
                if (Model.IsTrusted != value)
                {
                    Model.IsTrusted = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AllowBadgeText));
                    OnPropertyChanged(nameof(AllowBadgeBackground));
                    OnPropertyChanged(nameof(AllowBadgeForeground));
                    OnPropertyChanged(nameof(AllowBadgeBorder));
                    OnPropertyChanged(nameof(AllowBadgeToolTip));
                    OnPropertyChanged(nameof(AllowBadgeVisibility));
                }
            }
        }

        public BitmapImage? ProcessIcon => ProcessIconCache.Instance.GetIcon(Model.ProcessPath);

        public string AllowBadgeText => IsAllowed ? "🛡️ Confiável" : "Monitorado";
        public string AllowBadgeBackground => IsAllowed ? "#064E3B" : "#141A24";
        public string AllowBadgeForeground => IsAllowed ? "#34D399" : "#64748B";
        public string AllowBadgeBorder => IsAllowed ? "#059669" : "#263244";
        public string AllowBadgeToolTip => IsAllowed 
            ? "Conexão autorizada pelo usuário na Lista de Permissão (Allowlist)" 
            : "Conexão sob monitoramento padrão. Clique com botão direito ou em 'Marcar como Confiável' para autorizar.";
        public Visibility AllowBadgeVisibility => Visibility.Visible;

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
            ThreatLevel.High => "⚠️ ALERTA",
            ThreatLevel.Medium => "⚠️ PICO/PROXY",
            ThreatLevel.Low => "ℹ️ ATENÇÃO",
            _ => string.Empty
        };

        public string ThreatBadgeBackground => ThreatLevel switch
        {
            ThreatLevel.Critical => "#7F1D1D",
            ThreatLevel.High => "#991B1B",
            ThreatLevel.Medium => "#854D0E",
            ThreatLevel.Low => "#1E3A8A",
            _ => "Transparent"
        };

        public string ThreatBadgeForeground => ThreatLevel switch
        {
            ThreatLevel.Critical or ThreatLevel.High => "#FCA5A5",
            ThreatLevel.Medium => "#FDE047",
            ThreatLevel.Low => "#93C5FD",
            _ => "Transparent"
        };

        public Visibility ThreatBadgeVisibility => ThreatLevel != ThreatLevel.Normal ? Visibility.Visible : Visibility.Collapsed;

        public string StateBadgeBackground => Model.State switch
        {
            ConnectionState.Established => "#14532D",
            ConnectionState.Listening => "#1E3A8A",
            ConnectionState.TimeWait => "#713F12",
            ConnectionState.CloseWait => "#7C2D12",
            ConnectionState.SynSent or ConnectionState.SynReceived => "#581C87",
            _ => "#334155"
        };

        public string StateBadgeForeground => Model.State switch
        {
            ConnectionState.Established => "#4ADE80",
            ConnectionState.Listening => "#60A5FA",
            ConnectionState.TimeWait => "#FDE047",
            ConnectionState.CloseWait => "#FB923C",
            ConnectionState.SynSent or ConnectionState.SynReceived => "#C084FC",
            _ => "#CBD5E1"
        };

        public string ProtocolBadgeBackground => Model.Protocol switch
        {
            NetworkProtocol.TCP => "#0369A1",
            NetworkProtocol.UDP => "#4338CA",
            _ => "#334155"
        };

        public string ProtocolBadgeForeground => "#F0F9FF";

        public ConnectionItemViewModel(NetworkConnection model)
        {
            Model = model;
        }

        public void NotifyMetricsChanged()
        {
            OnPropertyChanged(nameof(CpuPercent));
            OnPropertyChanged(nameof(BandwidthBytesPerSec));
            OnPropertyChanged(nameof(DisplayCpu));
            OnPropertyChanged(nameof(DisplayBandwidth));
            OnPropertyChanged(nameof(CpuBadgeBackground));
            OnPropertyChanged(nameof(CpuBadgeForeground));
            OnPropertyChanged(nameof(ThreatLevel));
            OnPropertyChanged(nameof(ThreatReason));
            OnPropertyChanged(nameof(ThreatBadgeText));
            OnPropertyChanged(nameof(ThreatBadgeBackground));
            OnPropertyChanged(nameof(ThreatBadgeForeground));
            OnPropertyChanged(nameof(ThreatBadgeVisibility));
        }
    }
}
