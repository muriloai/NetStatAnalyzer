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

        public bool IsAllowed
        {
            get => Model.IsTrusted;
            set
            {
                if (Model.IsTrusted != value)
                {
                    Model.IsTrusted = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AllowBadgeBackground));
                    OnPropertyChanged(nameof(AllowBadgeForeground));
                    OnPropertyChanged(nameof(AllowBadgeBorder));
                    OnPropertyChanged(nameof(AllowBadgeVisibility));
                }
            }
        }

        public BitmapImage? ProcessIcon => ProcessIconCache.Instance.GetIcon(Model.ProcessPath);

        public string AllowBadgeBackground => IsAllowed ? "#064E3B" : "Transparent";
        public string AllowBadgeForeground => IsAllowed ? "#34D399" : "Transparent";
        public string AllowBadgeBorder => IsAllowed ? "#059669" : "Transparent";
        public Visibility AllowBadgeVisibility => IsAllowed ? Visibility.Visible : Visibility.Collapsed;

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
    }
}
