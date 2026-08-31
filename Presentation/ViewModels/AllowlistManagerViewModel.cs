using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using NetStatAnalyzer.Application.UseCases;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Presentation.Common;

namespace NetStatAnalyzer.Presentation.ViewModels
{
    public class AllowlistManagerViewModel : ViewModelBase
    {
        private readonly ManageAllowlistUseCase _useCase;
        public ObservableCollection<AllowlistRule> DisplayRules { get; } = new();

        private string _searchQuery = string.Empty;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    RefreshRules();
                }
            }
        }

        private string _ruleCountSummary = "0 Conexões Confiáveis";
        public string RuleCountSummary
        {
            get => _ruleCountSummary;
            set => SetProperty(ref _ruleCountSummary, value);
        }

        public ICommand ClearFilterCommand { get; }
        public ICommand ClearAllRulesCommand { get; }

        public AllowlistManagerViewModel(ManageAllowlistUseCase useCase)
        {
            _useCase = useCase;
            ClearFilterCommand = new RelayCommand(() => SearchQuery = string.Empty);
            ClearAllRulesCommand = new RelayCommand(ClearAll);

            RefreshRules();
            _useCase.RulesChanged += (s, e) => RefreshRules();
        }

        public void RefreshRules()
        {
            string query = SearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;
            var allRules = _useCase.GetAllRules();

            var filtered = allRules.Where(r =>
            {
                if (string.IsNullOrEmpty(query)) return true;
                return r.ProcessName.ToLowerInvariant().Contains(query) ||
                       (r.LocalAddress != null && r.LocalAddress.ToLowerInvariant().Contains(query)) ||
                       (r.ForeignAddress != null && r.ForeignAddress.ToLowerInvariant().Contains(query)) ||
                       r.IP.ToLowerInvariant().Contains(query) ||
                       (r.Protocol != null && r.Protocol.ToLowerInvariant().Contains(query));
            }).ToList();

            DisplayRules.Clear();
            foreach (var rule in filtered)
            {
                DisplayRules.Add(rule);
            }

            RuleCountSummary = $"{allRules.Count} Conexões Confiáveis";
        }

        public void DeleteRule(AllowlistRule rule)
        {
            _useCase.RemoveRule(rule);
            RefreshRules();
        }

        public void ClearAll()
        {
            _useCase.ClearAll();
            RefreshRules();
        }

        public string ExportToJson()
        {
            return _useCase.ExportToJson();
        }

        public (bool Success, int AddedCount, string Version, string Message) ImportFromJson(string json)
        {
            var result = _useCase.ImportFromJson(json);
            if (result.Success)
            {
                RefreshRules();
            }
            return result;
        }

        public int TotalRulesCount => _useCase.GetAllRules().Count;
    }
}
