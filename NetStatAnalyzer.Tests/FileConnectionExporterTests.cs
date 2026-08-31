using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;
using NetStatAnalyzer.Infrastructure.Exporting;
using Xunit;

namespace NetStatAnalyzer.Tests
{
    public class FileConnectionExporterTests : IDisposable
    {
        private readonly string _tempDir;

        public FileConnectionExporterTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "NetStatAnalyzerTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, true); } catch { }
            }
        }

        private List<NetworkConnection> CreateSampleConnections()
        {
            return new List<NetworkConnection>
            {
                new NetworkConnection
                {
                    ProcessName = "chrome.exe",
                    PID = 1234,
                    ProcessPath = @"C:\Program Files\Google\Chrome\chrome.exe",
                    Protocol = NetworkProtocol.TCP,
                    ProtocolRaw = "TCP",
                    LocalAddress = "192.168.1.100:50000",
                    ForeignAddress = "142.250.190.46:443",
                    State = ConnectionState.Established,
                    StateRaw = "ESTABLISHED",
                    IsTrusted = true
                }
            };
        }

        [Fact]
        public void ExportToCsv_ShouldCreateValidCsvWithHeaders()
        {
            var exporter = new FileConnectionExporter();
            string filePath = Path.Combine(_tempDir, "export.csv");

            exporter.ExportToCsv(filePath, CreateSampleConnections());

            Assert.True(File.Exists(filePath));
            string content = File.ReadAllText(filePath);
            Assert.Contains("Processo;PID;Caminho;Protocolo;Endereço Local;Endereço Remoto;Estado;Confiável", content);
            Assert.Contains("\"chrome.exe\";1234;", content);
            Assert.Contains("\"Sim\"", content);
        }

        [Fact]
        public void ExportToTxt_ShouldCreateFormattedReport()
        {
            var exporter = new FileConnectionExporter();
            string filePath = Path.Combine(_tempDir, "export.txt");

            exporter.ExportToTxt(filePath, CreateSampleConnections());

            Assert.True(File.Exists(filePath));
            string content = File.ReadAllText(filePath);
            Assert.Contains("NetStatAnalyzer - Relatório de Conexões de Rede", content);
            Assert.Contains("chrome.exe", content);
            Assert.Contains("1234", content);
        }

        [Fact]
        public void ExportToJson_ShouldCreateValidJson()
        {
            var exporter = new FileConnectionExporter();
            string filePath = Path.Combine(_tempDir, "export.json");

            exporter.ExportToJson(filePath, CreateSampleConnections(), "1.2.1");

            Assert.True(File.Exists(filePath));
            string content = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            Assert.Equal("NetStatAnalyzer", root.GetProperty("app").GetString());
            Assert.Equal("1.2.1", root.GetProperty("version").GetString());
            Assert.Equal(1, root.GetProperty("totalConnections").GetInt32());
            Assert.Equal(1, root.GetProperty("connections").GetArrayLength());
        }
    }
}
