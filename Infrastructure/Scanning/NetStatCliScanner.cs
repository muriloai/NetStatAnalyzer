using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Application.DTOs;

namespace NetStatAnalyzer.Infrastructure.Scanning
{
    public class NetStatCliScanner : INetworkScanner
    {
        public async Task<IReadOnlyList<RawSocketInfo>> ScanSocketsAsync()
        {
            return await Task.Run(() =>
            {
                var list = new List<RawSocketInfo>();
                var outputLines = RunCommand("netstat", "-ano");

                foreach (var line in outputLines)
                {
                    var socket = ParseLine(line);
                    if (socket != null)
                    {
                        list.Add(socket);
                    }
                }

                return (IReadOnlyList<RawSocketInfo>)list;
            });
        }

        private static string[] RunCommand(string command, string args)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao executar netstat: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        private static RawSocketInfo? ParseLine(string line)
        {
            try
            {
                string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) return null;

                string protocol = parts[0].ToUpperInvariant();
                if (protocol != "TCP" && protocol != "UDP") return null;

                if (protocol == "TCP" && parts.Length >= 5)
                {
                    if (int.TryParse(parts[^1], out int pid))
                    {
                        return new RawSocketInfo
                        {
                            Protocol = protocol,
                            LocalAddress = parts[1],
                            ForeignAddress = parts[2],
                            State = parts[3],
                            PID = pid
                        };
                    }
                }
                else if (protocol == "UDP" && parts.Length >= 4)
                {
                    if (int.TryParse(parts[^1], out int pid))
                    {
                        return new RawSocketInfo
                        {
                            Protocol = protocol,
                            LocalAddress = parts[1],
                            ForeignAddress = parts[2],
                            State = "N/A",
                            PID = pid
                        };
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
