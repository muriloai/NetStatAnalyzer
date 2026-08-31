using System.Collections.Generic;
using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Application.Contracts
{
    public interface IAllowlistRepository
    {
        IReadOnlyList<AllowlistRule> LoadAll();
        void SaveAll(IEnumerable<AllowlistRule> rules);
        string ExportToJson(IEnumerable<AllowlistRule> rules, string appVersion = "1.2.0");
        (bool Success, IReadOnlyList<AllowlistRule> Rules, string Version, string Message) ImportFromJson(string jsonContent);
    }
}
