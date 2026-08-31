using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using NetStatAnalyzer.Application.UseCases;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Policies;
using NetStatAnalyzer.Presentation.Common;

namespace NetStatAnalyzer.Presentation.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ScanConnectionsUseCase _scanUseCase;
        private readonly ManageAllowlistUseCase _allowlistUseCase;
        private readonly ExportConnectionsUseCase _exportUseCase;

        private readonly List<ConnectionItemViewModel> _allEntries = new();
        public ObservableCollection<ConnectionItemViewModel> FilteredEntries { get; } = new();

        public ObservableCollection<string> StateOptions { get; } = new();

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _statusMessage = "Pronto";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _searchQuery = string.Empty;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    ApplyFilters();
                }
            }
        }

        private string _selectedProtocol = "Todos";
        public string SelectedProtocol
        {
            get => _selectedProtocol;
            set
            {
                if (SetProperty(ref _selectedProtocol, value))
                {
                    ApplyFilters();
                }
            }
        }

        private string _selectedState = "Todos os Estados";
        public string SelectedState
        {
            get => _selectedState;
            set
            {
                string safeValue = string.IsNullOrWhiteSpace(value) ? "Todos os Estados" : value;
                if (SetProperty(ref _selectedState, safeValue))
                {
                    ApplyFilters();
                }
            }
        }

        private string _selectedTrustFilter = "Todas";
        public string SelectedTrustFilter
        {
            get => _selectedTrustFilter;
            set
            {
                string safeValue = string.IsNullOrWhiteSpace(value) ? "Todas" : value;
                if (SetProperty(ref _selectedTrustFilter, safeValue))
                {
                    ApplyFilters();
                }
            }
        }

        private int _metricTotal;
        public int MetricTotal
        {
            get => _metricTotal;
            set => SetProperty(ref _metricTotal, value);
        }

        private int _metricEstablished;
        public int MetricEstablished
        {
            get => _metricEstablished;
            set => SetProperty(ref _metricEstablished, value);
        }

        private int _metricListening;
        public int MetricListening
        {
            get => _metricListening;
            set => SetProperty(ref _metricListening, value);
        }

        private int _metricAllowed;
        public int MetricAllowed
        {
            get => _metricAllowed;
            set => SetProperty(ref _metricAllowed, value);
        }

        private string _connectionCountSummary = "0 conexões encontradas";
        public string ConnectionCountSummary
        {
            get => _connectionCountSummary;
            set => SetProperty(ref _connectionCountSummary, value);
        }

        private bool _isEmptyState;
        public bool IsEmptyState
        {
            get => _isEmptyState;
            set => SetProperty(ref _isEmptyState, value);
        }

        public string AppVersion { get; }

        public ICommand ReloadCommand { get; }
        public ICommand ClearFiltersCommand { get; }

        public ManageAllowlistUseCase AllowlistUseCase => _allowlistUseCase;

        public MainViewModel(
            ScanConnectionsUseCase scanUseCase,
            ManageAllowlistUseCase allowlistUseCase,
            ExportConnectionsUseCase exportUseCase)
        {
            _scanUseCase = scanUseCase;
            _allowlistUseCase = allowlistUseCase;
            _exportUseCase = exportUseCase;

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            AppVersion = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.2.1";

            ReloadCommand = new AsyncRelayCommand(LoadConnectionsAsync);
            ClearFiltersCommand = new RelayCommand(ClearFilters);

            _allowlistUseCase.RulesChanged += (s, e) =>
            {
                ReevaluateTrustedStatus();
                UpdateMetrics();
                ApplyFilters();
            };
        }

        public async Task LoadConnectionsAsync()
        {
            if (IsLoading) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Carregando conexões de rede...";

                var rules = _allowlistUseCase.GetAllRules();
                var domainConnections = await _scanUseCase.ExecuteAsync(rules);

                _allEntries.Clear();
                foreach (var conn in domainConnections)
                {
                    _allEntries.Add(new ConnectionItemViewModel(conn));
                }

                UpdateStateFilterOptions();
                UpdateMetrics();
                ApplyFilters();

                StatusMessage = "Pronto";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro ao carregar: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void ReevaluateTrustedStatus()
        {
            var rules = _allowlistUseCase.GetAllRules();
            foreach (var item in _allEntries)
            {
                item.IsAllowed = TrustEvaluationPolicy.IsTrusted(
                    item.ProcessName,
                    item.ForeignAddress,
                    item.LocalAddress,
                    rules,
                    item.Protocol);
            }
        }

        private void UpdateMetrics()
        {
            MetricTotal = _allEntries.Count;
            MetricEstablished = _allEntries.Count(e => string.Equals(e.State, "ESTABLISHED", StringComparison.OrdinalIgnoreCase));
            MetricListening = _allEntries.Count(e => string.Equals(e.State, "LISTENING", StringComparison.OrdinalIgnoreCase));
            MetricAllowed = _allEntries.Count(e => e.IsAllowed);
        }

        private void UpdateStateFilterOptions()
        {
            string current = SelectedState;

            var states = _allEntries
                .Select(e => string.IsNullOrWhiteSpace(e.State) ? "N/A" : e.State)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            StateOptions.Clear();
            StateOptions.Add("Todos os Estados");
            foreach (var state in states)
            {
                StateOptions.Add(state);
            }

            if (!string.IsNullOrWhiteSpace(current) && StateOptions.Contains(current))
            {
                SelectedState = current;
            }
            else
            {
                SelectedState = "Todos os Estados";
            }
        }

        public void ApplyFilters()
        {
            string query = SearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;

            var filtered = _allEntries.Where(entry =>
            {
                if (SelectedProtocol != "Todos" && !entry.Protocol.Equals(SelectedProtocol, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(SelectedState) && 
                    !SelectedState.Equals("Todos os Estados", StringComparison.OrdinalIgnoreCase) && 
                    !entry.State.Equals(SelectedState, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (SelectedTrustFilter == "Apenas Confiáveis" && !entry.IsAllowed)
                {
                    return false;
                }
                if (SelectedTrustFilter == "Ocultar Confiáveis" && entry.IsAllowed)
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(query))
                {
                    bool matchesName = entry.ProcessName.ToLowerInvariant().Contains(query);
                    bool matchesPid = entry.PID.ToString().Contains(query);
                    bool matchesLocal = entry.LocalAddress.ToLowerInvariant().Contains(query);
                    bool matchesForeign = entry.ForeignAddress.ToLowerInvariant().Contains(query);
                    bool matchesProtocol = entry.Protocol.ToLowerInvariant().Contains(query);
                    bool matchesState = entry.State.ToLowerInvariant().Contains(query);
                    bool matchesAllowed = entry.IsAllowed && "confiavel".Contains(query);

                    if (!matchesName && !matchesPid && !matchesLocal && !matchesForeign && !matchesProtocol && !matchesState && !matchesAllowed)
                    {
                        return false;
                    }
                }

                return true;
            }).ToList();

            FilteredEntries.Clear();
            foreach (var item in filtered)
            {
                FilteredEntries.Add(item);
            }

            ConnectionCountSummary = $"Exibindo {FilteredEntries.Count} de {_allEntries.Count} conexões";
            IsEmptyState = FilteredEntries.Count == 0;
        }

        public void ClearFilters()
        {
            SearchQuery = string.Empty;
            SelectedProtocol = "Todos";
            SelectedState = "Todos os Estados";
            SelectedTrustFilter = "Todas";
            ApplyFilters();
        }

        public int AddSelectedToAllowlist(IEnumerable<ConnectionItemViewModel> items)
        {
            var list = items.ToList();
            if (list.Count == 0) return 0;

            int added = _allowlistUseCase.AddRules(list.Select(e =>
                (e.ProcessName, e.ForeignAddress, e.Protocol, (string?)e.LocalAddress)));

            ReevaluateTrustedStatus();
            UpdateMetrics();
            ApplyFilters();

            StatusMessage = added > 0
                ? $"{added} conexão(ões) adicionada(s) à lista de confiáveis."
                : "Itens selecionados já estão na lista de confiáveis.";

            return added;
        }

        public int RemoveSelectedFromAllowlist(IEnumerable<ConnectionItemViewModel> items)
        {
            var list = items.ToList();
            if (list.Count == 0) return 0;

            int removed = _allowlistUseCase.RemoveRules(list.Select(e =>
                (e.ProcessName, e.ForeignAddress, (string?)e.LocalAddress)));

            ReevaluateTrustedStatus();
            UpdateMetrics();
            ApplyFilters();

            StatusMessage = removed > 0
                ? $"{removed} conexão(ões) removida(s) da lista de confiáveis."
                : "Nenhuma regra correspondente encontrada.";

            return removed;
        }

        public void ExportCurrentConnections(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            var connections = FilteredEntries.Select(vm => vm.Model);

            if (ext == ".csv")
            {
                _exportUseCase.ExportToCsv(filePath, connections);
            }
            else if (ext == ".txt")
            {
                _exportUseCase.ExportToTxt(filePath, connections);
            }
            else
            {
                _exportUseCase.ExportToJson(filePath, connections, AppVersion);
            }

            StatusMessage = $"{FilteredEntries.Count} conexões exportadas para '{Path.GetFileName(filePath)}'!";
        }
    }
}
