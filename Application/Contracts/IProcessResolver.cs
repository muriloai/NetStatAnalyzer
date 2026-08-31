namespace NetStatAnalyzer.Application.Contracts
{
    public interface IProcessResolver
    {
        string GetProcessName(int pid);
        string? GetProcessPath(int pid);
    }
}
