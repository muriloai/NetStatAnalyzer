using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Application.UseCases;

public sealed class ManageItemAnnotationsUseCase
{
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

    public IReadOnlyList<string> Tags => _annotations.Select(item => item.Tag)
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(tag => tag, StringComparer.CurrentCultureIgnoreCase).ToArray();

    public string Theme => _repository.LoadTheme();

    public void SaveTheme(string theme) => _repository.SaveTheme(theme);

    public void SetTags(IEnumerable<(string ItemType, string Key)> items, string? tag)
    {
        string cleanedTag = CleanTag(tag);
        Update(items, annotation => annotation.Tag = cleanedTag);
    }

    public void SetTrust(IEnumerable<(string ItemType, string Key)> items, bool trusted) =>
        Update(items, annotation => annotation.IsTrusted = trusted);

    private void Update(IEnumerable<(string ItemType, string Key)> items, Action<ItemAnnotation> change)
    {
        var updated = _annotations.Select(item => new ItemAnnotation
        {
            ItemType = item.ItemType, Key = item.Key, Tag = item.Tag, IsTrusted = item.IsTrusted
        }).ToList();

        foreach (var (itemType, key) in items.Distinct())
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
            change(existing);
        }

        updated.RemoveAll(item => string.IsNullOrEmpty(item.Tag) && !item.IsTrusted);
        _repository.SaveAnnotations(updated);
        _annotations.Clear();
        _annotations.AddRange(updated);
    }

    private static string CleanTag(string? tag)
    {
        string cleanedTag = (tag ?? string.Empty).Trim();
        if (cleanedTag.Length > 64) throw new ArgumentException("A tag deve ter no máximo 64 caracteres.", nameof(tag));
        return cleanedTag;
    }
}
