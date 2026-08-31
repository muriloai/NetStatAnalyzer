using System.Collections.Generic;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Application.UseCases
{
    public class ExportConnectionsUseCase
    {
        private readonly IConnectionExporter _exporter;

        public ExportConnectionsUseCase(IConnectionExporter exporter)
        {
            _exporter = exporter;
        }

        public void ExportToCsv(string filePath, IEnumerable<NetworkConnection> connections)
        {
            _exporter.ExportToCsv(filePath, connections);
        }

        public void ExportToTxt(string filePath, IEnumerable<NetworkConnection> connections)
        {
            _exporter.ExportToTxt(filePath, connections);
        }

        public void ExportToJson(string filePath, IEnumerable<NetworkConnection> connections, string appVersion)
        {
            _exporter.ExportToJson(filePath, connections, appVersion);
        }
    }
}
