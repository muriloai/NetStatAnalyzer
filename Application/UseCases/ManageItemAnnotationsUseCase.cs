using System.Text.RegularExpressions;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Application.UseCases;

public sealed class ManageItemAnnotationsUseCase
{
    public const int MaxTagLength = 24;
    public const int MaxTagsPerItem = 6;
    private static readonly Regex ValidTagRegex = new(@"^[\p{L}\p{N}_\-\.]+$", RegexOptions.Compiled);

    private readonly IItemAnnotationRepository _repository;
    private readonly List<ItemAnnotation> _annotations;

    public ManageItemAnnotationsUseCase(IItemAnnotationRepository repository)
    {
        _repository = repository;
        _annotations = repository.LoadAnnotations().ToList();
    }

    public ItemAnnotation? Find(string itemType, string key) => _annotations.FirstOrDefault(item =>
        item.ItemType.Equals(itemType, StringComparison.OrdinalIgnoreCase) &&
        item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<string> Tags => _annotations
        .SelectMany(item => (item.Tag ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(tag => tag, StringComparer.CurrentCultureIgnoreCase).ToArray();

    public string Theme => _repository.LoadTheme();

    public void SaveTheme(string theme) => _repository.SaveTheme(theme);

    public bool VerifyExecutableHashByDefault => _repository.LoadVerifyExecutableHashByDefault();

    public void SaveVerifyExecutableHashByDefault(bool verify) => _repository.SaveVerifyExecutableHashByDefault(verify);

    public void SetTags(IEnumerable<(string ItemType, string Key)> items, string? tag)
    {
        string cleanedTag = CleanTag(tag);
        Update(items, annotation => annotation.Tag = cleanedTag);
    }

    public void UpdateTags(
        IEnumerable<(string ItemType, string Key)> items,
        IEnumerable<string> tagsToAdd,
        IEnumerable<string> tagsToRemove)
    {
        var normalizedAdd = new List<string>();
        foreach (string raw in tagsToAdd)
        {
            if (!TryValidateSingleTag(raw, out string valid, out string? error))
            {
                throw new ArgumentException(error, nameof(tagsToAdd));
            }
            if (!normalizedAdd.Contains(valid, StringComparer.OrdinalIgnoreCase))
            {
                normalizedAdd.Add(valid);
            }
        }

        var removeSet = new HashSet<string>(
            tagsToRemove.Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)),
            StringComparer.OrdinalIgnoreCase);

        Update(items, annotation =>
        {
            var current = ParseTags(annotation.Tag)
                .Where(t => !removeSet.Contains(t))
                .ToList();

            foreach (string tag in normalizedAdd)
            {
                if (!current.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    current.Add(tag);
                }
            }

            if (current.Count > MaxTagsPerItem)
            {
                throw new ArgumentException($"Cada item pode ter no máximo {MaxTagsPerItem} tags.");
            }

            annotation.Tag = string.Join(", ", current);
        });
    }

    public int DeleteTagGlobally(string tag)
    {
        string target = (tag ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(target)) return 0;

        int affected = 0;
        var updated = _annotations.Select(item => new ItemAnnotation
        {
            ItemType = item.ItemType,
            Key = item.Key,
            Tag = item.Tag,
            IsTrusted = item.IsTrusted,
            ExecutablePath = item.ExecutablePath,
            ExecutableHash = item.ExecutableHash
        }).ToList();

        foreach (var item in updated)
        {
            var current = ParseTags(item.Tag);
            if (!current.Any(t => t.Equals(target, StringComparison.OrdinalIgnoreCase)))
                continue;

            var remaining = current.Where(t => !t.Equals(target, StringComparison.OrdinalIgnoreCase)).ToArray();
            item.Tag = string.Join(", ", remaining);
            affected++;
        }

        if (affected > 0)
        {
            updated.RemoveAll(item => string.IsNullOrEmpty(item.Tag) && !item.IsTrusted);
            _repository.SaveAnnotations(updated);
            _annotations.Clear();
            _annotations.AddRange(updated);
        }

        return affected;
    }

    public static IReadOnlyList<string> ParseTags(string? tagText)
    {
        if (string.IsNullOrWhiteSpace(tagText)) return Array.Empty<string>();
        return tagText.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool TryValidateSingleTag(string? rawTag, out string normalizedTag, out string? errorMessage)
    {
        normalizedTag = (rawTag ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(normalizedTag))
        {
            errorMessage = "Digite um nome de tag válido.";
            return false;
        }

        if (normalizedTag.Length > MaxTagLength)
        {
            errorMessage = $"A tag '{normalizedTag}' excede o limite de {MaxTagLength} caracteres.";
            return false;
        }

        if (!ValidTagRegex.IsMatch(normalizedTag))
        {
            errorMessage = $"A tag '{normalizedTag}' contém caracteres inválidos. Use apenas letras, números, hífen (-), sublinhado (_) ou ponto (.).";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public void SetTrust(IEnumerable<(string ItemType, string Key)> items, bool trusted) =>
        SetTrust(items.Select(i => (i.ItemType, i.Key, (string?)null, (string?)null)), trusted);

    public void SetTrust(IEnumerable<(string ItemType, string Key, string? ExecutablePath, string? ExecutableHash)> items, bool trusted) =>
        UpdateWithDetails(items, (annotation, path, hash) =>
        {
            annotation.IsTrusted = trusted;
            if (trusted)
            {
                annotation.ExecutablePath = path;
                annotation.ExecutableHash = hash;
            }
            else
            {
                annotation.ExecutableHash = null;
            }
        });

    private void Update(IEnumerable<(string ItemType, string Key)> items, Action<ItemAnnotation> change) =>
        UpdateWithDetails(items.Select(i => (i.ItemType, i.Key, (string?)null, (string?)null)), (a, _, _) => change(a));

    private void UpdateWithDetails(
        IEnumerable<(string ItemType, string Key, string? ExecutablePath, string? ExecutableHash)> items,
        Action<ItemAnnotation, string?, string?> change)
    {
        var updated = _annotations.Select(item => new ItemAnnotation
        {
            ItemType = item.ItemType,
            Key = item.Key,
            Tag = item.Tag,
            IsTrusted = item.IsTrusted,
            ExecutablePath = item.ExecutablePath,
            ExecutableHash = item.ExecutableHash
        }).ToList();

        foreach (var (itemType, key, path, hash) in items)
        {
            if (string.IsNullOrWhiteSpace(itemType) || string.IsNullOrWhiteSpace(key)) continue;
            var existing = updated.FirstOrDefault(item =>
                item.ItemType.Equals(itemType, StringComparison.OrdinalIgnoreCase) &&
                item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                existing = new ItemAnnotation { ItemType = itemType, Key = key };
                updated.Add(existing);
            }
            change(existing, path, hash);
        }

        updated.RemoveAll(item => string.IsNullOrEmpty(item.Tag) && !item.IsTrusted);
        _repository.SaveAnnotations(updated);
        _annotations.Clear();
        _annotations.AddRange(updated);
    }

    private static string CleanTag(string? tag)
    {
        string raw = (tag ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var parts = ParseTags(raw);
        if (parts.Count > MaxTagsPerItem)
        {
            throw new ArgumentException($"Cada item pode ter no máximo {MaxTagsPerItem} tags.", nameof(tag));
        }

        var validated = new List<string>(parts.Count);
        foreach (var p in parts)
        {
            if (!TryValidateSingleTag(p, out string valid, out string? error))
            {
                throw new ArgumentException(error, nameof(tag));
            }
            validated.Add(valid);
        }

        return string.Join(", ", validated);
    }
}
