using System.Collections.Generic;
using System.Threading.Tasks;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Application.UseCases
{
    public class ScanScheduledTasksUseCase
    {
        private readonly IScheduledTaskScanner _scanner;

        public ScanScheduledTasksUseCase(IScheduledTaskScanner scanner)
        {
            _scanner = scanner;
        }

        public async Task<IReadOnlyList<ScheduledTaskItem>> ExecuteAsync()
        {
            return await _scanner.ScanScheduledTasksAsync();
        }
    }
}
