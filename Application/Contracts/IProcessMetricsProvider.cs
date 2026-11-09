using System.Collections.Generic;
using System.Threading.Tasks;
using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Application.Contracts
{
    public interface IProcessMetricsProvider
    {
        Task<IReadOnlyDictionary<int, ProcessMetricInfo>> SampleMetricsAsync(IEnumerable<int> pids);
    }
}
