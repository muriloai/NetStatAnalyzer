using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NetStatAnalyzer
{
    public class AllowlistService
    {
        private static readonly Lazy<AllowlistService> _instance = new(() => new AllowlistService());
        public static AllowlistService Instance => _instance.Value;

        public ObservableCollection<AllowlistRule> Rules { get; } = new();

        private readonly string _storageFilePath;
        private readonly string? _legacyStorageFilePath;
        private readonly object _lock = new();

        public event EventHandler? RulesChanged;

        private AllowlistService()
        {
            (_storageFilePath, _legacyStorageFilePath) = ResolveStoragePaths();
            Load();
        }

        private (string currentPath, string? legacyPath) ResolveStoragePaths()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string candidate = Path.Combine(baseDir, "allowlist.json");
                string legacyCandidate = Path.Combine(baseDir, "whitelist.json");

                // Test write permissions
                string testFile = Path.Combine(baseDir, $".write_test_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);

                return (candidate, legacyCandidate);
            }
            catch
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetStatAnalyzer");
                Directory.CreateDirectory(appData);
                return (Path.Combine(appData, "allowlist.json"), Path.Combine(appData, "whitelist.json"));
            }
        }

        public static string ExtractIP(string? address)
        {
            if (string.IsNullOrWhiteSpace(address)) return string.Empty;
            address = address.Trim();

            if (address == "*:*" || address == "*.*" || address == "0.0.0.0:0" || address == "[::]:0")
            {
                return address;
            }

            // IPv6 with brackets [fe80::1]:80
            if (address.StartsWith("[") && address.Contains("]"))
            {
                int endBracket = address.IndexOf(']');
                return address.Substring(1, endBracket - 1);
            }

            // IPv4 or hostname with port 192.168.1.1:8080
            int colonIndex = address.LastIndexOf(':');
            if (colonIndex > 0 && !address.Contains(":::"))
            {
                return address.Substring(0, colonIndex);
            }

            return address;
        }

        public static bool IsWildcardOrListeningAddress(string? address)
        {
            if (string.IsNullOrWhiteSpace(address)) return true;
            string trimmed = address.Trim();
            return trimmed == "0.0.0.0:0" || 
                   trimmed == "*:*" || 
                   trimmed == "*.*" || 
                   trimmed == "0.0.0.0" || 
                   trimmed == "[::]:0" || 
                   trimmed == "[::]" ||
                   trimmed == "::";
        }

        public bool IsAllowed(string processName, string foreignAddress, string localAddress, string protocol = "")
        {
            if (string.IsNullOrWhiteSpace(processName) || Rules.Count == 0) return false;

            bool isListeningOrWildcard = IsWildcardOrListeningAddress(foreignAddress);
            string foreignIP = ExtractIP(foreignAddress);

            lock (_lock)
            {
                return Rules.Any(r =>
                {
                    if (!r.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    // 1. Listening Sockets: MUST match specific Local Address & Port (e.g. 0.0.0.0:1801 != 0.0.0.0:2103)
                    if (isListeningOrWildcard)
                    {
                        return !string.IsNullOrEmpty(r.LocalAddress) &&
                               r.LocalAddress.Equals(localAddress, StringComparison.OrdinalIgnoreCase);
                    }

                    // 2. Remote Connections: Match specific Remote Address (IP:Port)
                    if (!string.IsNullOrEmpty(r.ForeignAddress) && !IsWildcardOrListeningAddress(r.ForeignAddress))
                    {
                        if (r.ForeignAddress.Equals(foreignAddress, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }

                    // 3. Remote IP match for established connections (e.g. specific IP without port constraint)
                    if (!string.IsNullOrEmpty(foreignIP) && !IsWildcardOrListeningAddress(foreignIP))
                    {
                        if (!string.IsNullOrEmpty(r.IP) && !IsWildcardOrListeningAddress(r.IP) &&
                            r.IP.Equals(foreignIP, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }

                    return false;
                });
            }
        }

        public bool AddRule(string processName, string foreignAddress, string protocol, string? localAddress, string notes = "")
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;

            bool isListening = IsWildcardOrListeningAddress(foreignAddress);
            string ip = isListening ? ExtractIP(localAddress) : ExtractIP(foreignAddress);
            if (string.IsNullOrWhiteSpace(ip)) ip = isListening ? localAddress ?? "0.0.0.0" : foreignAddress;

            lock (_lock)
            {
                bool exists = Rules.Any(r =>
                {
                    if (!r.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)) return false;

                    if (isListening)
                    {
                        return !string.IsNullOrEmpty(r.LocalAddress) &&
                               r.LocalAddress.Equals(localAddress, StringComparison.OrdinalIgnoreCase);
                    }

                    return !string.IsNullOrEmpty(r.ForeignAddress) &&
                           r.ForeignAddress.Equals(foreignAddress, StringComparison.OrdinalIgnoreCase);
                });

                if (exists) return false;

                var rule = new AllowlistRule
                {
                    ProcessName = processName,
                    IP = ip,
                    ForeignAddress = foreignAddress,
                    LocalAddress = localAddress,
                    Protocol = protocol,
                    AddedAt = DateTime.Now,
                    Notes = notes
                };

                Rules.Add(rule);
            }

            Save();
            RulesChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public int AddRules(IEnumerable<(string ProcessName, string ForeignAddress, string Protocol, string? LocalAddress)> entries)
        {
            int addedCount = 0;
            lock (_lock)
            {
                foreach (var entry in entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.ProcessName)) continue;

                    bool isListening = IsWildcardOrListeningAddress(entry.ForeignAddress);
                    string ip = isListening ? ExtractIP(entry.LocalAddress) : ExtractIP(entry.ForeignAddress);
                    if (string.IsNullOrWhiteSpace(ip)) ip = isListening ? entry.LocalAddress ?? "0.0.0.0" : entry.ForeignAddress;

                    bool exists = Rules.Any(r =>
                    {
                        if (!r.ProcessName.Equals(entry.ProcessName, StringComparison.OrdinalIgnoreCase)) return false;

                        if (isListening)
                        {
                            return !string.IsNullOrEmpty(r.LocalAddress) &&
                                   r.LocalAddress.Equals(entry.LocalAddress, StringComparison.OrdinalIgnoreCase);
                        }

                        return !string.IsNullOrEmpty(r.ForeignAddress) &&
                               r.ForeignAddress.Equals(entry.ForeignAddress, StringComparison.OrdinalIgnoreCase);
                    });

                    if (!exists)
                    {
                        Rules.Add(new AllowlistRule
                        {
                            ProcessName = entry.ProcessName,
                            IP = ip,
                            ForeignAddress = entry.ForeignAddress,
                            LocalAddress = entry.LocalAddress,
                            Protocol = entry.Protocol,
                            AddedAt = DateTime.Now
                        });
                        addedCount++;
                    }
                }
            }

            if (addedCount > 0)
            {
                Save();
                RulesChanged?.Invoke(this, EventArgs.Empty);
            }

            return addedCount;
        }

        public int RemoveRules(IEnumerable<(string ProcessName, string ForeignAddress, string? LocalAddress)> entries)
        {
            int removedCount = 0;
            lock (_lock)
            {
                foreach (var entry in entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.ProcessName)) continue;

                    bool isListening = IsWildcardOrListeningAddress(entry.ForeignAddress);
                    string ip = isListening ? ExtractIP(entry.LocalAddress) : ExtractIP(entry.ForeignAddress);

                    var matches = Rules.Where(r =>
                    {
                        if (!r.ProcessName.Equals(entry.ProcessName, StringComparison.OrdinalIgnoreCase)) return false;

                        if (isListening)
                        {
                            return !string.IsNullOrEmpty(r.LocalAddress) &&
                                   r.LocalAddress.Equals(entry.LocalAddress, StringComparison.OrdinalIgnoreCase);
                        }

                        return (!string.IsNullOrEmpty(r.ForeignAddress) && r.ForeignAddress.Equals(entry.ForeignAddress, StringComparison.OrdinalIgnoreCase)) ||
                               (!string.IsNullOrEmpty(r.IP) && r.IP.Equals(ip, StringComparison.OrdinalIgnoreCase));
                    }).ToList();

                    foreach (var match in matches)
                    {
                        Rules.Remove(match);
                        removedCount++;
                    }
                }
            }

            if (removedCount > 0)
            {
                Save();
                RulesChanged?.Invoke(this, EventArgs.Empty);
            }

            return removedCount;
        }

        public bool RemoveRule(AllowlistRule rule)
        {
            bool removed;
            lock (_lock)
            {
                removed = Rules.Remove(rule);
            }

            if (removed)
            {
                Save();
                RulesChanged?.Invoke(this, EventArgs.Empty);
            }

            return removed;
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                Rules.Clear();
            }

            Save();
            RulesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Load()
        {
            lock (_lock)
            {
                try
                {
                    Rules.Clear();

                    string? targetPath = null;
                    if (File.Exists(_storageFilePath))
                    {
                        targetPath = _storageFilePath;
                    }
                    else if (_legacyStorageFilePath != null && File.Exists(_legacyStorageFilePath))
                    {
                        targetPath = _legacyStorageFilePath;
                    }

                    if (targetPath == null) return;

                    string json = File.ReadAllText(targetPath);
                    var doc = JsonSerializer.Deserialize<AllowlistExportDocument>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (doc?.Rules != null)
                    {
                        foreach (var rule in doc.Rules)
                        {
                            if (!string.IsNullOrWhiteSpace(rule.ProcessName) && !string.IsNullOrWhiteSpace(rule.IP))
                            {
                                Rules.Add(rule);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Erro ao carregar regras de permissão: {ex.Message}");
                }
            }
        }

        public void Save()
        {
            lock (_lock)
            {
                try
                {
                    var doc = new AllowlistExportDocument
                    {
                        App = "NetStatAnalyzer",
                        Version = "1.1.0",
                        ExportedAt = DateTime.Now,
                        Rules = Rules.ToList()
                    };

                    string json = JsonSerializer.Serialize(doc, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    File.WriteAllText(_storageFilePath, json);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Erro ao salvar regras de permissão: {ex.Message}");
                }
            }
        }

        public string ExportToJson(string version = "1.1.0")
        {
            lock (_lock)
            {
                var doc = new AllowlistExportDocument
                {
                    App = "NetStatAnalyzer",
                    Version = version,
                    ExportedAt = DateTime.Now,
                    Rules = Rules.ToList()
                };

                return JsonSerializer.Serialize(doc, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
        }

        public (bool Success, int AddedCount, string Version, string Message) ImportFromJson(string json)
        {
            try
            {
                var doc = JsonSerializer.Deserialize<AllowlistExportDocument>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (doc == null || doc.Rules == null || doc.Rules.Count == 0)
                {
                    return (false, 0, doc?.Version ?? "Desconhecida", "Nenhuma regra válida encontrada no arquivo.");
                }

                int added = 0;
                lock (_lock)
                {
                    foreach (var rule in doc.Rules)
                    {
                        if (string.IsNullOrWhiteSpace(rule.ProcessName) || string.IsNullOrWhiteSpace(rule.IP))
                            continue;

                        bool exists = Rules.Any(r =>
                            r.ProcessName.Equals(rule.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                            (r.IP.Equals(rule.IP, StringComparison.OrdinalIgnoreCase) ||
                             (!string.IsNullOrEmpty(r.ForeignAddress) && !string.IsNullOrEmpty(rule.ForeignAddress) && r.ForeignAddress.Equals(rule.ForeignAddress, StringComparison.OrdinalIgnoreCase))));

                        if (!exists)
                        {
                            Rules.Add(rule);
                            added++;
                        }
                    }
                }

                if (added > 0)
                {
                    Save();
                    RulesChanged?.Invoke(this, EventArgs.Empty);
                }

                return (true, added, doc.Version ?? "1.1.0", $"Importação concluída! {added} novas regras adicionadas com sucesso (versão: {doc.Version ?? "1.1.0"}).");
            }
            catch (Exception ex)
            {
                return (false, 0, "Erro", $"Erro ao importar arquivo de regras: {ex.Message}");
            }
        }
    }
}
