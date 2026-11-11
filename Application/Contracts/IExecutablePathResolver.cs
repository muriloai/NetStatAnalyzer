namespace NetStatAnalyzer.Application.Contracts
{
    public interface IExecutablePathResolver
    {
        string Resolve(string rawCommand);
    }
}
