using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Application.Contracts;

public interface IItemAnnotationRepository
{
    IReadOnlyList<ItemAnnotation> LoadAnnotations();
    string LoadTheme();
    bool LoadVerifyExecutableHashByDefault();
    void SaveAnnotations(IEnumerable<ItemAnnotation> annotations);
    void SaveTheme(string theme);
    void SaveVerifyExecutableHashByDefault(bool verify);
}
