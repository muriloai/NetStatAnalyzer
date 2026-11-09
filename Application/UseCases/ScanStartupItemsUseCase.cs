using System.Collections.Generic;
using System.Threading.Tasks;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Application.UseCases
{
    public class ScanStartupItemsUseCase
    {
        private readonly IStartupItemScanner _scanner;

        public ScanStartupItemsUseCase(IStartupItemScanner scanner)
        {
            _scanner = scanner;
        }

        public async Task<IReadOnlyList<StartupItem>> ExecuteAsync()
        {
            return await _scanner.ScanStartupItemsAsync();
        }
    }
}
