using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using NetStatAnalyzer.Application.Contracts;

namespace NetStatAnalyzer.Infrastructure.System;

public sealed class ExecutableHashResolver : IExecutableHashResolver
{
    private readonly ConcurrentDictionary<string, (DateTime LastWriteUtc, long Length, string Hash)> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public string? ComputeSha256(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return null;

        string path = executablePath.Trim('\"', ' ');
        if (!File.Exists(path))
        {
            path = FileSecurityInspector.ExtractExecutablePath(path);
            if (!File.Exists(path)) return null;
        }

        try
        {
            var fileInfo = new FileInfo(path);
            DateTime lastWrite = fileInfo.LastWriteTimeUtc;
            long length = fileInfo.Length;

            if (_cache.TryGetValue(path, out var cached) &&
                cached.LastWriteUtc == lastWrite &&
                cached.Length == length)
            {
                return cached.Hash;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(stream);
            string hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            _cache[path] = (lastWrite, length, hash);
            return hash;
        }
        catch
        {
            // Arquivo bloqueado ou sem permissão de leitura
            return null;
        }
    }
}
