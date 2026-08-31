using System.Collections.Generic;
using System.Linq;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Application.UseCases;
using NetStatAnalyzer.Domain.Entities;
using Xunit;

namespace NetStatAnalyzer.Tests
{
    public class InMemoryAllowlistRepository : IAllowlistRepository
    {
        private List<AllowlistRule> _rules = new();

        public IReadOnlyList<AllowlistRule> LoadAll() => _rules.ToList();

        public void SaveAll(IEnumerable<AllowlistRule> rules)
        {
            _rules = rules.ToList();
        }

        public string ExportToJson(IEnumerable<AllowlistRule> rules, string appVersion = "1.2.1")
        {
            return "{}";
        }

        public (bool Success, IReadOnlyList<AllowlistRule> Rules, string Version, string Message) ImportFromJson(string jsonContent)
        {
            return (true, new List<AllowlistRule>
            {
                new AllowlistRule { ProcessName = "imported.exe", IP = "10.0.0.1", ForeignAddress = "10.0.0.1:80" }
            }, "1.2.1", "OK");
        }
    }

    public class ManageAllowlistUseCaseTests
    {
        [Fact]
        public void AddRules_ShouldAddUniqueRulesAndRaiseEvent()
        {
            var repo = new InMemoryAllowlistRepository();
            var useCase = new ManageAllowlistUseCase(repo);

            bool eventFired = false;
            useCase.RulesChanged += (s, e) => eventFired = true;

            var entries = new List<(string, string, string, string?)>
            {
                ("firefox.exe", "1.1.1.1:443", "TCP", "192.168.1.10:50000"),
                ("chrome.exe", "8.8.8.8:53", "UDP", "192.168.1.10:50001")
            };

            int added = useCase.AddRules(entries);

            Assert.Equal(2, added);
            Assert.Equal(2, useCase.GetAllRules().Count);
            Assert.True(eventFired);
        }

        [Fact]
        public void AddRules_ShouldNotAddDuplicates()
        {
            var repo = new InMemoryAllowlistRepository();
            var useCase = new ManageAllowlistUseCase(repo);

            var entries = new List<(string, string, string, string?)>
            {
                ("firefox.exe", "1.1.1.1:443", "TCP", "192.168.1.10:50000")
            };

            useCase.AddRules(entries);
            int addedSecondTime = useCase.AddRules(entries);

            Assert.Equal(0, addedSecondTime);
            Assert.Single(useCase.GetAllRules());
        }

        [Fact]
        public void RemoveRules_ShouldRemoveMatchingRules()
        {
            var repo = new InMemoryAllowlistRepository();
            var useCase = new ManageAllowlistUseCase(repo);

            useCase.AddRules(new (string, string, string, string?)[]
            {
                ("firefox.exe", "1.1.1.1:443", "TCP", "192.168.1.10:50000"),
                ("chrome.exe", "8.8.8.8:53", "UDP", "192.168.1.10:50001")
            });

            int removed = useCase.RemoveRules(new (string, string, string?)[]
            {
                ("firefox.exe", "1.1.1.1:443", "192.168.1.10:50000")
            });

            Assert.Equal(1, removed);
            Assert.Single(useCase.GetAllRules());
            Assert.Equal("chrome.exe", useCase.GetAllRules()[0].ProcessName);
        }

        [Fact]
        public void ClearAll_ShouldRemoveAllRules()
        {
            var repo = new InMemoryAllowlistRepository();
            var useCase = new ManageAllowlistUseCase(repo);

            useCase.AddRules(new (string, string, string, string?)[]
            {
                ("app1.exe", "1.1.1.1:80", "TCP", "127.0.0.1:1000"),
                ("app2.exe", "2.2.2.2:80", "TCP", "127.0.0.1:1001")
            });

            Assert.Equal(2, useCase.GetAllRules().Count);

            useCase.ClearAll();

            Assert.Empty(useCase.GetAllRules());
        }
    }
}
