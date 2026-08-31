using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Infrastructure.Exporting
{
    public class FileConnectionExporter : IConnectionExporter
    {
        public void ExportToCsv(string filePath, IEnumerable<NetworkConnection> connections)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Processo;PID;Caminho;Protocolo;Endereço Local;Endereço Remoto;Estado;Confiável");

            foreach (var entry in connections)
            {
                string path = (entry.ProcessPath ?? string.Empty).Replace("\"", "\"\"");
                sb.AppendLine($"\"{entry.ProcessName}\";{entry.PID};\"{path}\";\"{entry.DisplayProtocol}\";\"{entry.LocalAddress}\";\"{entry.ForeignAddress}\";\"{entry.DisplayState}\";\"{(entry.IsTrusted ? "Sim" : "Não")}\"");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public void ExportToTxt(string filePath, IEnumerable<NetworkConnection> connections)
        {
            var list = connections.ToList();
            var sb = new StringBuilder();
            sb.AppendLine("=== NetStatAnalyzer - Relatório de Conexões de Rede ===");
            sb.AppendLine($"Data/Hora: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine($"Total de Conexões Exibidas: {list.Count}");
            sb.AppendLine(new string('-', 120));
            sb.AppendLine($"{"PROCESSO",-24} {"PID",-8} {"PROTO",-7} {"ENDEREÇO LOCAL",-24} {"ENDEREÇO REMOTO",-24} {"ESTADO",-14} {"CONFIÁVEL",-10}");
            sb.AppendLine(new string('-', 120));

            foreach (var entry in list)
            {
                sb.AppendLine($"{entry.ProcessName,-24} {entry.PID,-8} {entry.DisplayProtocol,-7} {entry.LocalAddress,-24} {entry.ForeignAddress,-24} {entry.DisplayState,-14} {(entry.IsTrusted ? "Sim" : "Não"),-10}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public void ExportToJson(string filePath, IEnumerable<NetworkConnection> connections, string appVersion)
        {
            var list = connections.ToList();
            var exportData = new
            {
                app = "NetStatAnalyzer",
                version = appVersion,
                exportedAt = DateTime.Now,
                totalConnections = list.Count,
                connections = list.Select(entry => new
                {
                    processName = entry.ProcessName,
                    pid = entry.PID,
                    processPath = entry.ProcessPath ?? string.Empty,
                    protocol = entry.DisplayProtocol,
                    localAddress = entry.LocalAddress,
                    foreignAddress = entry.ForeignAddress,
                    state = entry.DisplayState,
                    isTrusted = entry.IsTrusted
                })
            };

            string json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }
    }
}
