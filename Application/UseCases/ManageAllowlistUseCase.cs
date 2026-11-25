using System;
using System.Collections.Generic;
using System.Linq;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Policies;

namespace NetStatAnalyzer.Application.UseCases
{
    public class ManageAllowlistUseCase
    {
        private readonly IAllowlistRepository _repository;
        private readonly List<AllowlistRule> _rules = new();
        private readonly object _syncLock = new();

        public event EventHandler? RulesChanged;

        public ManageAllowlistUseCase(IAllowlistRepository repository)
        {
            _repository = repository;
            Load();
        }

        public IReadOnlyList<AllowlistRule> GetAllRules()
        {
            lock (_syncLock)
            {
                return _rules.ToList();
            }
        }

        public void Load()
        {
            lock (_syncLock)
            {
                _rules.Clear();
                var loaded = _repository.LoadAll();
                _rules.AddRange(loaded);
            }
        }

        public int AddRules(IEnumerable<(string ProcessName, string ForeignAddress, string Protocol, string? LocalAddress)> entries)
        {
            return AddRules(entries.Select(e => (e.ProcessName, e.ForeignAddress, e.Protocol, e.LocalAddress, (string?)null, (string?)null)));
        }

        public int AddRules(IEnumerable<(string ProcessName, string ForeignAddress, string Protocol, string? LocalAddress, string? ExecutablePath, string? ExecutableHash)> entries)
        {
            int modifiedCount = 0;
            lock (_syncLock)
            {
                foreach (var entry in entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.ProcessName)) continue;

                    bool isListening = TrustEvaluationPolicy.IsWildcardOrListeningAddress(entry.ForeignAddress);
                    string ip = isListening ? TrustEvaluationPolicy.ExtractIP(entry.LocalAddress) : TrustEvaluationPolicy.ExtractIP(entry.ForeignAddress);
                    if (string.IsNullOrWhiteSpace(ip)) ip = isListening ? entry.LocalAddress ?? "0.0.0.0" : entry.ForeignAddress;

                    var existing = _rules.FirstOrDefault(r =>
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

                    if (existing is null)
                    {
                        _rules.Add(new AllowlistRule
                        {
                            ProcessName = entry.ProcessName,
                            IP = ip,
                            ForeignAddress = entry.ForeignAddress,
                            LocalAddress = entry.LocalAddress,
                            Protocol = entry.Protocol,
                            ExecutablePath = entry.ExecutablePath,
                            ExecutableHash = entry.ExecutableHash,
                            AddedAt = DateTime.Now
                        });
                        modifiedCount++;
                    }
                    else
                    {
                        bool hashChanged = existing.ExecutableHash != entry.ExecutableHash;
                        bool pathChanged = existing.ExecutablePath != entry.ExecutablePath;
                        if (hashChanged || pathChanged)
                        {
                            existing.ExecutablePath = entry.ExecutablePath;
                            existing.ExecutableHash = entry.ExecutableHash;
                            existing.AddedAt = DateTime.Now;
                            modifiedCount++;
                        }
                    }
                }

                if (modifiedCount > 0)
                {
                    _repository.SaveAll(_rules);
                }
            }

            if (modifiedCount > 0)
            {
                RulesChanged?.Invoke(this, EventArgs.Empty);
            }

            return modifiedCount;
        }

        public int RemoveRules(IEnumerable<(string ProcessName, string ForeignAddress, string? LocalAddress)> entries)
        {
            int removedCount = 0;
            lock (_syncLock)
            {
                foreach (var entry in entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.ProcessName)) continue;

                    bool isListening = TrustEvaluationPolicy.IsWildcardOrListeningAddress(entry.ForeignAddress);
                    string ip = isListening ? TrustEvaluationPolicy.ExtractIP(entry.LocalAddress) : TrustEvaluationPolicy.ExtractIP(entry.ForeignAddress);

                    var matches = _rules.Where(r =>
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
                        _rules.Remove(match);
                        removedCount++;
                    }
                }

                if (removedCount > 0)
                {
                    _repository.SaveAll(_rules);
                }
            }

            if (removedCount > 0)
            {
                RulesChanged?.Invoke(this, EventArgs.Empty);
            }

            return removedCount;
        }

        public bool RemoveRule(AllowlistRule rule)
        {
            bool removed;
            lock (_syncLock)
            {
                removed = _rules.Remove(rule);
                if (removed)
                {
                    _repository.SaveAll(_rules);
                }
            }

            if (removed)
            {
                RulesChanged?.Invoke(this, EventArgs.Empty);
            }

            return removed;
        }

        public void ClearAll()
        {
            lock (_syncLock)
            {
                _rules.Clear();
                _repository.SaveAll(_rules);
            }

            RulesChanged?.Invoke(this, EventArgs.Empty);
        }

        public string ExportToJson(string version = "1.5.1")
        {
            lock (_syncLock)
            {
                return _repository.ExportToJson(_rules, version);
            }
        }

        public (bool Success, int AddedCount, string Version, string Message) ImportFromJson(string json)
        {
            var result = _repository.ImportFromJson(json);
            if (!result.Success || result.Rules == null || result.Rules.Count == 0)
            {
                return (false, 0, result.Version, result.Message);
            }

            int added = 0;
            lock (_syncLock)
            {
                foreach (var rule in result.Rules)
                {
                    if (string.IsNullOrWhiteSpace(rule.ProcessName) || string.IsNullOrWhiteSpace(rule.IP))
                        continue;

                    bool exists = _rules.Any(r =>
                        r.ProcessName.Equals(rule.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                        (r.IP.Equals(rule.IP, StringComparison.OrdinalIgnoreCase) ||
                         (!string.IsNullOrEmpty(r.ForeignAddress) && !string.IsNullOrEmpty(rule.ForeignAddress) && r.ForeignAddress.Equals(rule.ForeignAddress, StringComparison.OrdinalIgnoreCase))));

                    if (!exists)
                    {
                        _rules.Add(rule);
                        added++;
                    }
                }

                if (added > 0)
                {
                    _repository.SaveAll(_rules);
                }
            }

            if (added > 0)
            {
                RulesChanged?.Invoke(this, EventArgs.Empty);
            }

            return (true, added, result.Version, $"Importação concluída com sucesso! {added} novas regras adicionadas.");
        }
    }
}
