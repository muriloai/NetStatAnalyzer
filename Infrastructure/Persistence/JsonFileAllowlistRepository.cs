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
    public class JsonFileAllowlistRepository : IAllowlistRepository, IItemAnnotationRepository
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
                return ReadDocument().Rules.Where(r => !string.IsNullOrWhiteSpace(r.ProcessName) && !string.IsNullOrWhiteSpace(r.IP)).ToList();
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
                var doc = ReadDocument();
                doc.Rules = rules.ToList();
                WriteDocument(doc);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao salvar regras: {ex.Message}");
            }
        }

        public string ExportToJson(IEnumerable<AllowlistRule> rules, string appVersion = "1.4.1")
        {
            var doc = ReadDocument();
            doc.Version = appVersion;
            doc.ExportedAt = DateTime.Now;
            doc.Rules = rules.ToList();

            return JsonSerializer.Serialize(doc, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        public IReadOnlyList<ItemAnnotation> LoadAnnotations() => ReadDocument().Annotations;

        public string LoadTheme() => ReadDocument().Theme;

        public void SaveAnnotations(IEnumerable<ItemAnnotation> annotations)
        {
            var doc = ReadDocument();
            doc.Annotations = annotations.ToList();
            WriteDocument(doc);
        }

        public void SaveTheme(string theme)
        {
            var doc = ReadDocument();
            doc.Theme = theme;
            WriteDocument(doc);
        }

        private AllowlistDocument ReadDocument()
        {
            string? path = File.Exists(_storageFilePath) ? _storageFilePath :
                _legacyStorageFilePath is not null && File.Exists(_legacyStorageFilePath) ? _legacyStorageFilePath : null;
            if (path is null) return new AllowlistDocument();
            return JsonSerializer.Deserialize<AllowlistDocument>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("O arquivo de configurações está vazio ou inválido.");
        }

        private void WriteDocument(AllowlistDocument doc)
        {
            doc.Version = "1.4.1";
            doc.ExportedAt = DateTime.Now;
            string tempPath = _storageFilePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tempPath, _storageFilePath, true);
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
                    return (false, Array.Empty<AllowlistRule>(), doc?.Version ?? "1.4.1", "Nenhuma regra válida encontrada no arquivo.");
                }

                var validRules = doc.Rules
                    .Where(r => !string.IsNullOrWhiteSpace(r.ProcessName) && !string.IsNullOrWhiteSpace(r.IP))
                    .ToList();

                return (true, validRules, doc.Version ?? "1.4.1", $"{validRules.Count} regras extraídas do arquivo.");
            }
            catch (Exception ex)
            {
                return (false, Array.Empty<AllowlistRule>(), "Erro", $"Falha ao interpretar JSON: {ex.Message}");
            }
        }
    }
}
