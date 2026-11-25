namespace NetStatAnalyzer.Application.Contracts;

public interface IExecutableHashResolver
{
    string? ComputeSha256(string? executablePath);
}
