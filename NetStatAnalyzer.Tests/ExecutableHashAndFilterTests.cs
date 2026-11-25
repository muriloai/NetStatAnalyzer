using System;
using System.IO;
using System.Text;
using NetStatAnalyzer.Infrastructure.System;
using Xunit;

namespace NetStatAnalyzer.Tests;

public class ExecutableHashAndFilterTests
{
    [Fact]
    public void ExecutableHashResolver_ShouldComputeValidSha256_AndUseCache()
    {
        var resolver = new ExecutableHashResolver();
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_bin_{Guid.NewGuid():N}.exe");

        try
        {
            File.WriteAllText(tempFile, "Hello World NetStatAnalyzer");

            string? hash1 = resolver.ComputeSha256(tempFile);
            Assert.NotNull(hash1);
            Assert.Equal(64, hash1.Length);

            // Segunda chamada deve usar o cache e retornar idêntico
            string? hash2 = resolver.ComputeSha256(tempFile);
            Assert.Equal(hash1, hash2);

            // Ao alterar o conteúdo e timestamp, o hash é recalculado
            File.WriteAllText(tempFile, "Modified Content");
            File.SetLastWriteTimeUtc(tempFile, DateTime.UtcNow.AddSeconds(5));

            string? hash3 = resolver.ComputeSha256(tempFile);
            Assert.NotNull(hash3);
            Assert.NotEqual(hash1, hash3);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void ExecutableHashResolver_WhenFileDoesNotExist_ShouldReturnNull()
    {
        var resolver = new ExecutableHashResolver();
        string? result = resolver.ComputeSha256("C:\\NonExistentPath\\FakeApp123.exe");
        Assert.Null(result);
    }

    [Fact]
    public void ViewModelBase_TagsList_ShouldParseMultipleTagsCorrectly()
    {
        var item = new NetStatAnalyzer.Presentation.ViewModels.ConnectionItemViewModel(new NetStatAnalyzer.Domain.Entities.NetworkConnection());
        item.TagText = "sistema-crm, saida; api";

        Assert.Equal(3, item.TagsList.Count);
        Assert.Contains("sistema-crm", item.TagsList);
        Assert.Contains("saida", item.TagsList);
        Assert.Contains("api", item.TagsList);
        Assert.True(item.HasTags);
    }

    [Fact]
    public void ViewModelBase_TagsList_WhenEmpty_ShouldReturnEmptyList()
    {
        var item = new NetStatAnalyzer.Presentation.ViewModels.ConnectionItemViewModel(new NetStatAnalyzer.Domain.Entities.NetworkConnection());
        item.TagText = "  ";

        Assert.Empty(item.TagsList);
        Assert.False(item.HasTags);
    }

    [Theory]
    [InlineData("sistema-crm", true)]
    [InlineData("saída_01", true)]
    [InlineData("api.v2", true)]
    [InlineData("tag com espaco", false)]
    [InlineData("tag<script>", false)]
    [InlineData("tag@invalida!", false)]
    [InlineData("tag_extremamente_longa_acima_de_24_chars", false)]
    public void ManageItemAnnotations_TryValidateSingleTag_ShouldEnforceStrictRules(string input, bool expectedValid)
    {
        bool valid = NetStatAnalyzer.Application.UseCases.ManageItemAnnotationsUseCase.TryValidateSingleTag(input, out _, out _);
        Assert.Equal(expectedValid, valid);
    }

    [Fact]
    public void ManageItemAnnotations_UpdateTags_ShouldPreserveExistingAndApplyDelta()
    {
        var repo = new InMemoryAnnotationRepo();
        var useCase = new NetStatAnalyzer.Application.UseCases.ManageItemAnnotationsUseCase(repo);

        var item1 = ("connection", "TCP|svchost.exe|LOCAL:0.0.0.0:135");
        var item2 = ("connection", "TCP|svchost.exe|LOCAL:0.0.0.0:445");

        useCase.SetTags(new[] { item1 }, "sistema-crm, entrada");
        useCase.SetTags(new[] { item2 }, "monitoramento, entrada");

        // Adiciona 'saida' e remove 'entrada' nos dois itens simultaneamente
        useCase.UpdateTags(new[] { item1, item2 }, new[] { "saida" }, new[] { "entrada" });

        Assert.Equal("sistema-crm, saida", useCase.Find(item1.Item1, item1.Item2)?.Tag);
        Assert.Equal("monitoramento, saida", useCase.Find(item2.Item1, item2.Item2)?.Tag);
    }

    [Fact]
    public void ManageItemAnnotations_SetTags_WhenExceedingMaxTags_ShouldThrow()
    {
        var repo = new InMemoryAnnotationRepo();
        var useCase = new NetStatAnalyzer.Application.UseCases.ManageItemAnnotationsUseCase(repo);

        Assert.Throws<ArgumentException>(() =>
            useCase.SetTags(new[] { ("process", "app.exe") }, "t1, t2, t3, t4, t5, t6, t7"));
    }

    [Fact]
    public void ManageItemAnnotations_DeleteTagGlobally_ShouldRemoveTagFromAllRecordsAndCatalog()
    {
        var repo = new InMemoryAnnotationRepo();
        var useCase = new NetStatAnalyzer.Application.UseCases.ManageItemAnnotationsUseCase(repo);

        var item1 = ("connection", "TCP|app.exe|LOCAL:0.0.0.0:80");
        var item2 = ("connection", "TCP|old.exe|10.0.0.5:443");

        useCase.SetTags(new[] { item1 }, "ads, saida");
        useCase.SetTags(new[] { item2 }, "saida");

        int removedFromCount = useCase.DeleteTagGlobally("saida");

        Assert.Equal(2, removedFromCount);
        Assert.Equal("ads", useCase.Find(item1.Item1, item1.Item2)?.Tag);
        Assert.Null(useCase.Find(item2.Item1, item2.Item2));
        Assert.DoesNotContain("saida", useCase.Tags);
        Assert.Contains("ads", useCase.Tags);
    }

    private sealed class InMemoryAnnotationRepo : NetStatAnalyzer.Application.Contracts.IItemAnnotationRepository
    {
        private System.Collections.Generic.List<NetStatAnalyzer.Domain.Entities.ItemAnnotation> _items = new();
        private string _theme = "Dark";
        private bool _verifyHash = true;

        public System.Collections.Generic.IReadOnlyList<NetStatAnalyzer.Domain.Entities.ItemAnnotation> LoadAnnotations() => _items;
        public string LoadTheme() => _theme;
        public bool LoadVerifyExecutableHashByDefault() => _verifyHash;
        public void SaveAnnotations(System.Collections.Generic.IEnumerable<NetStatAnalyzer.Domain.Entities.ItemAnnotation> annotations) => _items = new(annotations);
        public void SaveTheme(string theme) => _theme = theme;
        public void SaveVerifyExecutableHashByDefault(bool verify) => _verifyHash = verify;
    }
}
