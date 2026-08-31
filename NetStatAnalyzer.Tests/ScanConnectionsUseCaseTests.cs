using System.Collections.Generic;
using System.Threading.Tasks;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Application.DTOs;
using NetStatAnalyzer.Application.UseCases;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;
using Xunit;

namespace NetStatAnalyzer.Tests
{
    public class MockNetworkScanner : INetworkScanner
    {
        private readonly List<RawSocketInfo> _sockets;

        public MockNetworkScanner(List<RawSocketInfo> sockets)
        {
            _sockets = sockets;
        }

        public Task<IReadOnlyList<RawSocketInfo>> ScanSocketsAsync()
        {
            return Task.FromResult<IReadOnlyList<RawSocketInfo>>(_sockets);
        }
    }

    public class MockProcessResolver : IProcessResolver
    {
        public string GetProcessName(int pid)
        {
            return pid switch
            {
                1001 => "chrome.exe",
                1002 => "code.exe",
                0 => "Sistema (Idle)",
                4 => "System",
                _ => "desconhecido.exe"
            };
        }

        public string? GetProcessPath(int pid)
        {
            return pid switch
            {
                1001 => @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                1002 => @"C:\Users\User\AppData\Local\Programs\Microsoft VS Code\Code.exe",
                _ => null
            };
        }
    }

    public class ScanConnectionsUseCaseTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldMapSocketsAndEvaluateTrustCorrectly()
        {
            var rawSockets = new List<RawSocketInfo>
            {
                new RawSocketInfo
                {
                    Protocol = "TCP",
                    LocalAddress = "192.168.1.50:52341",
                    ForeignAddress = "142.250.190.46:443",
                    State = "ESTABLISHED",
                    PID = 1001
                },
                new RawSocketInfo
                {
                    Protocol = "TCP",
                    LocalAddress = "0.0.0.0:8080",
                    ForeignAddress = "0.0.0.0:0",
                    State = "LISTENING",
                    PID = 1002
                },
                new RawSocketInfo
                {
                    Protocol = "UDP",
                    LocalAddress = "0.0.0.0:5353",
                    ForeignAddress = "*:*",
                    State = "N/A",
                    PID = 1001
                }
            };

            var trustedRules = new List<AllowlistRule>
            {
                new AllowlistRule
                {
                    ProcessName = "chrome.exe",
                    IP = "142.250.190.46",
                    ForeignAddress = "142.250.190.46:443"
                }
            };

            var scanner = new MockNetworkScanner(rawSockets);
            var resolver = new MockProcessResolver();
            var useCase = new ScanConnectionsUseCase(scanner, resolver);

            var connections = await useCase.ExecuteAsync(trustedRules);

            Assert.Equal(3, connections.Count);

            // Item 1: Chrome TCP Established (Trusted)
            var chromeTcp = connections[0];
            Assert.Equal(NetworkProtocol.TCP, chromeTcp.Protocol);
            Assert.Equal(ConnectionState.Established, chromeTcp.State);
            Assert.Equal("chrome.exe", chromeTcp.ProcessName);
            Assert.Equal(1001, chromeTcp.PID);
            Assert.True(chromeTcp.IsTrusted);

            // Item 2: Code TCP Listening (Not Trusted)
            var codeTcp = connections[1];
            Assert.Equal(NetworkProtocol.TCP, codeTcp.Protocol);
            Assert.Equal(ConnectionState.Listening, codeTcp.State);
            Assert.Equal("code.exe", codeTcp.ProcessName);
            Assert.False(codeTcp.IsTrusted);

            // Item 3: Chrome UDP (Not in trusted list for UDP wildcard)
            var chromeUdp = connections[2];
            Assert.Equal(NetworkProtocol.UDP, chromeUdp.Protocol);
            Assert.Equal(ConnectionState.NotApplicable, chromeUdp.State);
        }
    }
}
