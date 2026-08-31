using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;
using NetStatAnalyzer.Domain.Policies;

namespace NetStatAnalyzer.Application.UseCases
{
    public class ScanConnectionsUseCase
    {
        private readonly INetworkScanner _scanner;
        private readonly IProcessResolver _processResolver;

        public ScanConnectionsUseCase(INetworkScanner scanner, IProcessResolver processResolver)
        {
            _scanner = scanner;
            _processResolver = processResolver;
        }

        public async Task<List<NetworkConnection>> ExecuteAsync(IEnumerable<AllowlistRule> trustedRules)
        {
            var rawSockets = await _scanner.ScanSocketsAsync();
            var connections = new List<NetworkConnection>(rawSockets.Count);

            foreach (var raw in rawSockets)
            {
                var protocol = ParseProtocol(raw.Protocol);
                var state = ParseState(raw.State);
                string processName = _processResolver.GetProcessName(raw.PID);
                string? processPath = _processResolver.GetProcessPath(raw.PID);

                bool isTrusted = TrustEvaluationPolicy.IsTrusted(
                    processName,
                    raw.ForeignAddress,
                    raw.LocalAddress,
                    trustedRules,
                    raw.Protocol);

                connections.Add(new NetworkConnection
                {
                    Protocol = protocol,
                    ProtocolRaw = raw.Protocol,
                    LocalAddress = raw.LocalAddress,
                    ForeignAddress = raw.ForeignAddress,
                    State = state,
                    StateRaw = raw.State,
                    PID = raw.PID,
                    ProcessName = processName,
                    ProcessPath = processPath,
                    IsTrusted = isTrusted
                });
            }

            return connections;
        }

        private static NetworkProtocol ParseProtocol(string raw)
        {
            return raw?.ToUpperInvariant() switch
            {
                "TCP" => NetworkProtocol.TCP,
                "UDP" => NetworkProtocol.UDP,
                _ => NetworkProtocol.Unknown
            };
        }

        private static ConnectionState ParseState(string raw)
        {
            return raw?.ToUpperInvariant() switch
            {
                "ESTABLISHED" => ConnectionState.Established,
                "LISTENING" => ConnectionState.Listening,
                "TIME_WAIT" => ConnectionState.TimeWait,
                "CLOSE_WAIT" => ConnectionState.CloseWait,
                "SYN_SENT" => ConnectionState.SynSent,
                "SYN_RECEIVED" or "SYN_RECV" => ConnectionState.SynReceived,
                "FIN_WAIT_1" or "FIN_WAIT1" => ConnectionState.FinWait1,
                "FIN_WAIT_2" or "FIN_WAIT2" => ConnectionState.FinWait2,
                "CLOSING" => ConnectionState.Closing,
                "LAST_ACK" => ConnectionState.LastAck,
                "N/A" => ConnectionState.NotApplicable,
                _ => ConnectionState.Unknown
            };
        }
    }
}
