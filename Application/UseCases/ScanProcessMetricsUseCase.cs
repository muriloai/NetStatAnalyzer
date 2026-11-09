using System.Collections.Generic;
using System.Threading.Tasks;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Application.UseCases
{
    public class ScanProcessMetricsUseCase
    {
        private readonly IProcessMetricsProvider _provider;

        public ScanProcessMetricsUseCase(IProcessMetricsProvider provider)
        {
            _provider = provider;
        }

        public async Task<IReadOnlyDictionary<int, ProcessMetricInfo>> ExecuteAsync(IEnumerable<int> pids)
        {
            return await _provider.SampleMetricsAsync(pids);
        }
    }
}
