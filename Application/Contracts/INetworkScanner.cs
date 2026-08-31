using System.Collections.Generic;
using System.Threading.Tasks;
using NetStatAnalyzer.Application.DTOs;

namespace NetStatAnalyzer.Application.Contracts
{
    public interface INetworkScanner
    {
        Task<IReadOnlyList<RawSocketInfo>> ScanSocketsAsync();
    }
}
