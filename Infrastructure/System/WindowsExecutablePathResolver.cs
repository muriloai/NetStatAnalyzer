using NetStatAnalyzer.Application.Contracts;

namespace NetStatAnalyzer.Infrastructure.System
{
    public sealed class WindowsExecutablePathResolver : IExecutablePathResolver
    {
        public string Resolve(string rawCommand)
        {
            return FileSecurityInspector.ExtractExecutablePath(rawCommand);
        }
    }
}
