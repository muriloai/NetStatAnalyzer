using System.Collections.Generic;
using System.Threading.Tasks;
using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Application.Contracts
{
    public interface IScheduledTaskScanner
    {
        Task<IReadOnlyList<ScheduledTaskItem>> ScanScheduledTasksAsync();
    }
}
