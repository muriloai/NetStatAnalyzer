using System.Collections.Generic;
using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Application.Contracts
{
    public interface IConnectionExporter
    {
        void ExportToCsv(string filePath, IEnumerable<NetworkConnection> connections);
        void ExportToTxt(string filePath, IEnumerable<NetworkConnection> connections);
        void ExportToJson(string filePath, IEnumerable<NetworkConnection> connections, string appVersion);
    }
}
