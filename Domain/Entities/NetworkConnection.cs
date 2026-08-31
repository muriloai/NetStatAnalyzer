using NetStatAnalyzer.Domain.Enums;

namespace NetStatAnalyzer.Domain.Entities
{
    public class NetworkConnection
    {
        public NetworkProtocol Protocol { get; set; } = NetworkProtocol.Unknown;
        public string ProtocolRaw { get; set; } = string.Empty;
        public string LocalAddress { get; set; } = string.Empty;
        public string ForeignAddress { get; set; } = string.Empty;
        public ConnectionState State { get; set; } = ConnectionState.Unknown;
        public string StateRaw { get; set; } = string.Empty;
        public int PID { get; set; }
        public string ProcessName { get; set; } = "Desconhecido";
        public string? ProcessPath { get; set; }
        public bool IsTrusted { get; set; }

        public string DisplayState => State switch
        {
            ConnectionState.Established => "ESTABLISHED",
            ConnectionState.Listening => "LISTENING",
            ConnectionState.TimeWait => "TIME_WAIT",
            ConnectionState.CloseWait => "CLOSE_WAIT",
            ConnectionState.SynSent => "SYN_SENT",
            ConnectionState.SynReceived => "SYN_RECEIVED",
            ConnectionState.FinWait1 => "FIN_WAIT_1",
            ConnectionState.FinWait2 => "FIN_WAIT_2",
            ConnectionState.Closing => "CLOSING",
            ConnectionState.LastAck => "LAST_ACK",
            ConnectionState.NotApplicable => "N/A",
            _ => string.IsNullOrWhiteSpace(StateRaw) ? "UNKNOWN" : StateRaw
        };

        public string DisplayProtocol => Protocol switch
        {
            NetworkProtocol.TCP => "TCP",
            NetworkProtocol.UDP => "UDP",
            _ => string.IsNullOrWhiteSpace(ProtocolRaw) ? "UNKNOWN" : ProtocolRaw
        };
    }
}
