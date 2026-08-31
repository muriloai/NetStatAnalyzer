using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Infrastructure.Persistence
{
    public class JsonFileAllowlistRepository : IAllowlistRepository
    {
        private readonly string _storageFilePath;
        private readonly string? _legacyStorageFilePath;

        public JsonFileAllowlistRepository()
        {
            (_storageFilePath, _legacyStorageFilePath) = ResolveStoragePaths();
        }

        private static (string currentPath, string? legacyPath) ResolveStoragePaths()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string candidate = Path.Combine(baseDir, "allowlist.json");
                string legacyCandidate = Path.Combine(baseDir, "whitelist.json");

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

        public IReadOnlyList<AllowlistRule> LoadAll()
        {
            try
            {
                string? targetPath = null;
                if (File.Exists(_storageFilePath))
                {
                    targetPath = _storageFilePath;
                }
                else if (_legacyStorageFilePath != null && File.Exists(_legacyStorageFilePath))
                {
                    targetPath = _legacyStorageFilePath;
                }

                if (targetPath == null) return Array.Empty<AllowlistRule>();

                string json = File.ReadAllText(targetPath);
                var doc = JsonSerializer.Deserialize<AllowlistDocument>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (doc?.Rules != null)
                {
                    return doc.Rules.Where(r => !string.IsNullOrWhiteSpace(r.ProcessName) && !string.IsNullOrWhiteSpace(r.IP)).ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao ler repositório de regras: {ex.Message}");
            }

            return Array.Empty<AllowlistRule>();
        }

        public void SaveAll(IEnumerable<AllowlistRule> rules)
        {
            try
            {
                var doc = new AllowlistDocument
                {
                    App = "NetStatAnalyzer",
                    Version = "1.2.1",
                    ExportedAt = DateTime.Now,
                    Rules = rules.ToList()
                };

                string json = JsonSerializer.Serialize(doc, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_storageFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao salvar regras: {ex.Message}");
            }
        }

        public string ExportToJson(IEnumerable<AllowlistRule> rules, string appVersion = "1.2.1")
        {
            var doc = new AllowlistDocument
            {
                App = "NetStatAnalyzer",
                Version = appVersion,
                ExportedAt = DateTime.Now,
                Rules = rules.ToList()
            };

            return JsonSerializer.Serialize(doc, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        public (bool Success, IReadOnlyList<AllowlistRule> Rules, string Version, string Message) ImportFromJson(string jsonContent)
        {
            try
            {
                var doc = JsonSerializer.Deserialize<AllowlistDocument>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (doc == null || doc.Rules == null || doc.Rules.Count == 0)
                {
                    return (false, Array.Empty<AllowlistRule>(), doc?.Version ?? "1.2.1", "Nenhuma regra válida encontrada no arquivo.");
                }

                var validRules = doc.Rules
                    .Where(r => !string.IsNullOrWhiteSpace(r.ProcessName) && !string.IsNullOrWhiteSpace(r.IP))
                    .ToList();

                return (true, validRules, doc.Version ?? "1.2.1", $"{validRules.Count} regras extraídas do arquivo.");
            }
            catch (Exception ex)
            {
                return (false, Array.Empty<AllowlistRule>(), "Erro", $"Falha ao interpretar JSON: {ex.Message}");
            }
        }
    }
}
