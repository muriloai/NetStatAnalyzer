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
using NetStatAnalyzer.Domain.Enums;
using NetStatAnalyzer.Domain.Policies;
using NetStatAnalyzer.Infrastructure.Monitoring;
using NetStatAnalyzer.Infrastructure.System;
using NetStatAnalyzer.Presentation.Common;

namespace NetStatAnalyzer.Presentation.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ScanConnectionsUseCase _scanUseCase;
        private readonly ManageAllowlistUseCase _allowlistUseCase;
        private readonly ExportConnectionsUseCase _exportUseCase;
        private readonly ScanProcessMetricsUseCase _metricsUseCase;
        private readonly ScanScheduledTasksUseCase _tasksUseCase;
        private readonly ScanStartupItemsUseCase _startupUseCase;

        // 1. Network Connections Tab
        private readonly List<ConnectionItemViewModel> _allEntries = new();
        public ObservableCollection<ConnectionItemViewModel> FilteredEntries { get; } = new();
        public ObservableCollection<string> StateOptions { get; } = new();

        // 2. Aggregated Processes Tab
        private readonly List<ProcessAggregateItemViewModel> _allProcesses = new();
        public ObservableCollection<ProcessAggregateItemViewModel> FilteredProcesses { get; } = new();

        // 3. Scheduled Tasks Tab
        private readonly List<ScheduledTaskItemViewModel> _allTasks = new();
        public ObservableCollection<ScheduledTaskItemViewModel> FilteredTasks { get; } = new();

        // 4. Startup Items Tab
        private readonly List<StartupItemViewModel> _allStartupItems = new();
        public ObservableCollection<StartupItemViewModel> FilteredStartupItems { get; } = new();

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
                    ApplyAllFilters();
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

        // Live CPU Cutoff Filter
        private double _cpuCutoffThreshold = 0.0;
        public double CpuCutoffThreshold
        {
            get => _cpuCutoffThreshold;
            set
            {
                if (SetProperty(ref _cpuCutoffThreshold, value))
                {
                    OnPropertyChanged(nameof(CpuCutoffSummary));
                    ApplyFilters();
                    ApplyProcessFilters();
                }
            }
        }

        public string CpuCutoffSummary => _cpuCutoffThreshold <= 0.0
            ? "Corte CPU: Desativado (0%)"
            : $"Corte CPU: > {_cpuCutoffThreshold:F0}%";

        // Filter: Only Anomalies / Threat Spikes
        private bool _onlyAnomalies;
        public bool OnlyAnomalies
        {
            get => _onlyAnomalies;
            set
            {
                if (SetProperty(ref _onlyAnomalies, value))
                {
                    ApplyFilters();
                    ApplyProcessFilters();
                }
            }
        }

        // Filter for Scheduled Tasks
        private string _selectedTaskFilter = "Todas";
        public string SelectedTaskFilter
        {
            get => _selectedTaskFilter;
            set
            {
                if (SetProperty(ref _selectedTaskFilter, value))
                {
                    ApplyTaskFilters();
                }
            }
        }

        // Filter for Startup Items
        private string _selectedStartupFilter = "Todos";
        public string SelectedStartupFilter
        {
            get => _selectedStartupFilter;
            set
            {
                if (SetProperty(ref _selectedStartupFilter, value))
                {
                    ApplyStartupFilters();
                }
            }
        }

        // Tab selection (0: Conexões, 1: Processos, 2: Tarefas Agendadas, 3: Itens Inicialização, 4: Insights)
        private int _selectedTabIndex = 0;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (SetProperty(ref _selectedTabIndex, value))
                {
                    OnPropertyChanged(nameof(IsConnectionsTabActive));
                    OnPropertyChanged(nameof(IsProcessesTabActive));
                    OnPropertyChanged(nameof(IsTasksTabActive));
                    OnPropertyChanged(nameof(IsStartupTabActive));
                    OnPropertyChanged(nameof(CurrentTabSummary));
                    OnPropertyChanged(nameof(CurrentTabRefreshText));
                    OnPropertyChanged(nameof(CurrentTabRefreshToolTip));
                }
            }
        }

        public bool IsConnectionsTabActive => SelectedTabIndex == 0;
        public bool IsProcessesTabActive => SelectedTabIndex == 1;
        public bool IsTasksTabActive => SelectedTabIndex == 2;
        public bool IsStartupTabActive => SelectedTabIndex == 3;
        
        public string CurrentTabSummary
        {
            get
            {
                return SelectedTabIndex switch
                {
                    0 => ConnectionCountSummary,
                    1 => ProcessCountSummary,
                    2 => TaskCountSummary,
                    3 => StartupCountSummary,
                    _ => ConnectionCountSummary
                };
            }
        }

        public string CurrentTabRefreshText => SelectedTabIndex switch
        {
            0 => "🔄 Atualizar Conexões",
            1 => "🔄 Atualizar Processos",
            2 => "🔄 Atualizar Tarefas",
            3 => "🔄 Atualizar Inicialização",
            _ => "🔄 Atualizar Conexões"
        };

        public string CurrentTabRefreshToolTip => SelectedTabIndex switch
        {
            0 => "Reexaminar conexões de rede ativas (netstat) da aba atual",
            1 => "Coletar nova amostra instantânea de consumo de CPU e Banda dos processos",
            2 => "Recarregar lista de tarefas agendadas do Windows",
            3 => "Reexaminar programas de inicialização no Registro e Pastas",
            _ => "Atualizar dados da aba atual"
        };

        // Bandwidth Cutoff Filter (Processos)
        private double _bandwidthCutoffThreshold = 0.0;
        public double BandwidthCutoffThreshold
        {
            get => _bandwidthCutoffThreshold;
            set
            {
                if (SetProperty(ref _bandwidthCutoffThreshold, value))
                {
                    ApplyProcessFilters();
                }
            }
        }

        // Live Reading / Continuous Monitoring
        private bool _isLiveReading;
        public bool IsLiveReading
        {
            get => _isLiveReading;
            set => SetProperty(ref _isLiveReading, value);
        }

        // Metrics & Counters - Tab 0: Conexões
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

        private int _metricThreatConnections;
        public int MetricThreatConnections
        {
            get => _metricThreatConnections;
            set => SetProperty(ref _metricThreatConnections, value);
        }

        // Metrics & Counters - Tab 1: Processos & Consumo
        private int _metricProcessesTotal;
        public int MetricProcessesTotal
        {
            get => _metricProcessesTotal;
            set => SetProperty(ref _metricProcessesTotal, value);
        }

        private int _metricProcessesHighCpu;
        public int MetricProcessesHighCpu
        {
            get => _metricProcessesHighCpu;
            set => SetProperty(ref _metricProcessesHighCpu, value);
        }

        private int _metricProcessesHighNetwork;
        public int MetricProcessesHighNetwork
        {
            get => _metricProcessesHighNetwork;
            set => SetProperty(ref _metricProcessesHighNetwork, value);
        }

        private int _metricProcessesThreats;
        public int MetricProcessesThreats
        {
            get => _metricProcessesThreats;
            set => SetProperty(ref _metricProcessesThreats, value);
        }

        private string _metricProcessesBandwidthSum = "0 B/s";
        public string MetricProcessesBandwidthSum
        {
            get => _metricProcessesBandwidthSum;
            set => SetProperty(ref _metricProcessesBandwidthSum, value);
        }

        private string _processCountSummary = "0 processos monitorados";
        public string ProcessCountSummary
        {
            get => _processCountSummary;
            set
            {
                if (SetProperty(ref _processCountSummary, value))
                {
                    OnPropertyChanged(nameof(CurrentTabSummary));
                }
            }
        }

        // Metrics & Counters - Tab 2: Tarefas Agendadas
        private int _metricTasksTotal;
        public int MetricTasksTotal
        {
            get => _metricTasksTotal;
            set => SetProperty(ref _metricTasksTotal, value);
        }

        private int _metricTasksEnabled;
        public int MetricTasksEnabled
        {
            get => _metricTasksEnabled;
            set => SetProperty(ref _metricTasksEnabled, value);
        }

        private int _metricTasksDisabled;
        public int MetricTasksDisabled
        {
            get => _metricTasksDisabled;
            set => SetProperty(ref _metricTasksDisabled, value);
        }

        private int _metricTasksNonMicrosoft;
        public int MetricTasksNonMicrosoft
        {
            get => _metricTasksNonMicrosoft;
            set => SetProperty(ref _metricTasksNonMicrosoft, value);
        }

        private int _metricTasksSuspicious;
        public int MetricTasksSuspicious
        {
            get => _metricTasksSuspicious;
            set => SetProperty(ref _metricTasksSuspicious, value);
        }

        private string _taskCountSummary = "0 tarefas agendadas";
        public string TaskCountSummary
        {
            get => _taskCountSummary;
            set
            {
                if (SetProperty(ref _taskCountSummary, value))
                {
                    OnPropertyChanged(nameof(CurrentTabSummary));
                }
            }
        }

        // Metrics & Counters - Tab 3: Itens de Inicialização
        private int _metricStartupTotal;
        public int MetricStartupTotal
        {
            get => _metricStartupTotal;
            set => SetProperty(ref _metricStartupTotal, value);
        }

        private int _metricStartupRegistry;
        public int MetricStartupRegistry
        {
            get => _metricStartupRegistry;
            set => SetProperty(ref _metricStartupRegistry, value);
        }

        private int _metricStartupFolder;
        public int MetricStartupFolder
        {
            get => _metricStartupFolder;
            set => SetProperty(ref _metricStartupFolder, value);
        }

        private int _metricStartupSuspicious;
        public int MetricStartupSuspicious
        {
            get => _metricStartupSuspicious;
            set => SetProperty(ref _metricStartupSuspicious, value);
        }

        private string _startupCountSummary = "0 itens de inicialização";
        public string StartupCountSummary
        {
            get => _startupCountSummary;
            set
            {
                if (SetProperty(ref _startupCountSummary, value))
                {
                    OnPropertyChanged(nameof(CurrentTabSummary));
                }
            }
        }

        // Threat intelligence counters
        private int _threatCountProxy;
        public int ThreatCountProxy
        {
            get => _threatCountProxy;
            set => SetProperty(ref _threatCountProxy, value);
        }

        private int _threatCountMiner;
        public int ThreatCountMiner
        {
            get => _threatCountMiner;
            set => SetProperty(ref _threatCountMiner, value);
        }

        private int _threatCountSpikes;
        public int ThreatCountSpikes
        {
            get => _threatCountSpikes;
            set => SetProperty(ref _threatCountSpikes, value);
        }

        private int _threatCountTasks;
        public int ThreatCountTasks
        {
            get => _threatCountTasks;
            set => SetProperty(ref _threatCountTasks, value);
        }

        private int _threatCountStartup;
        public int ThreatCountStartup
        {
            get => _threatCountStartup;
            set => SetProperty(ref _threatCountStartup, value);
        }

        public int TotalThreatsCount => ThreatCountProxy + ThreatCountMiner + ThreatCountSpikes + ThreatCountTasks + ThreatCountStartup;

        private string _connectionCountSummary = "0 conexões encontradas";
        public string ConnectionCountSummary
        {
            get => _connectionCountSummary;
            set
            {
                if (SetProperty(ref _connectionCountSummary, value))
                {
                    OnPropertyChanged(nameof(CurrentTabSummary));
                }
            }
        }

        private bool _isEmptyState;
        public bool IsEmptyState
        {
            get => _isEmptyState;
            set => SetProperty(ref _isEmptyState, value);
        }

        public string AppVersion { get; }

        public ICommand ReloadCommand { get; }
        public ICommand RefreshCurrentTabCommand { get; }
        public ICommand RefreshAllTabsCommand { get; }
        public ICommand ClearFiltersCommand { get; }

        // Windows Tools Commands
        public ICommand OpenTaskSchedulerCommand { get; }
        public ICommand OpenStartupSettingsCommand { get; }
        public ICommand OpenTaskManagerCommand { get; }
        public ICommand OpenResourceMonitorCommand { get; }
        public ICommand OpenStartupFolderCommand { get; }
        public ICommand OpenRegistryEditorCommand { get; }

        public ManageAllowlistUseCase AllowlistUseCase => _allowlistUseCase;

        public MainViewModel(
            ScanConnectionsUseCase scanUseCase,
            ManageAllowlistUseCase allowlistUseCase,
            ExportConnectionsUseCase exportUseCase,
            ScanProcessMetricsUseCase? metricsUseCase = null,
            ScanScheduledTasksUseCase? tasksUseCase = null,
            ScanStartupItemsUseCase? startupUseCase = null)
        {
            _scanUseCase = scanUseCase;
            _allowlistUseCase = allowlistUseCase;
            _exportUseCase = exportUseCase;
            _metricsUseCase = metricsUseCase ?? new ScanProcessMetricsUseCase(new Win32ProcessMetricsProvider());
            _tasksUseCase = tasksUseCase ?? new ScanScheduledTasksUseCase(new WindowsScheduledTaskScanner());
            _startupUseCase = startupUseCase ?? new ScanStartupItemsUseCase(new WindowsStartupScanner());

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            AppVersion = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.3.0";

            ReloadCommand = new AsyncRelayCommand(RefreshCurrentTabAsync);
            RefreshCurrentTabCommand = new AsyncRelayCommand(RefreshCurrentTabAsync);
            RefreshAllTabsCommand = new AsyncRelayCommand(LoadAllDataAsync);
            ClearFiltersCommand = new RelayCommand(ClearFilters);

            OpenTaskSchedulerCommand = new RelayCommand(() => WindowsToolsLauncher.OpenTaskScheduler());
            OpenStartupSettingsCommand = new RelayCommand(() => WindowsToolsLauncher.OpenStartupAppsSettings());
            OpenTaskManagerCommand = new RelayCommand(() => WindowsToolsLauncher.OpenTaskManager());
            OpenResourceMonitorCommand = new RelayCommand(() => WindowsToolsLauncher.OpenResourceMonitor());
            OpenStartupFolderCommand = new RelayCommand(() => WindowsToolsLauncher.OpenStartupFolder());
            OpenRegistryEditorCommand = new RelayCommand(() => WindowsToolsLauncher.OpenRegistryEditor());

            _allowlistUseCase.RulesChanged += (s, e) =>
            {
                ReevaluateTrustedStatus();
                UpdateMetrics();
                ApplyFilters();
            };
        }

        public async Task RefreshCurrentTabAsync()
        {
            switch (SelectedTabIndex)
            {
                case 0:
                    await RefreshConnectionsAsync();
                    break;
                case 1:
                    await RefreshProcessesAsync();
                    break;
                case 2:
                    await RefreshTasksAsync();
                    break;
                case 3:
                    await RefreshStartupItemsAsync();
                    break;
                default:
                    await RefreshConnectionsAsync();
                    break;
            }
        }

        public async Task RefreshConnectionsAsync(bool isSilent = false)
        {
            if (IsLoading) return;
            try
            {
                if (!isSilent)
                {
                    IsLoading = true;
                    StatusMessage = "Lendo conexões de rede ativas (netstat)...";
                }
                await LoadConnectionsInternalAsync();
                UpdateMetrics();
                if (!isSilent)
                {
                    StatusMessage = $"Pronto - Conexões lidas via netstat. Exibindo {FilteredEntries.Count} conexões.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro ao carregar conexões: {ex.Message}";
            }
            finally
            {
                if (!isSilent)
                {
                    IsLoading = false;
                }
            }
        }

        public async Task RefreshProcessesAsync(bool isSilent = false)
        {
            if (IsLoading) return;
            try
            {
                if (!isSilent)
                {
                    IsLoading = true;
                    StatusMessage = "Coletando amostra de consumo de CPU e banda dos processos...";
                }
                await LoadConnectionsInternalAsync();
                UpdateProcessMetrics();
                if (!isSilent)
                {
                    StatusMessage = $"Pronto - Amostra de processos e consumo atualizada. Exibindo {FilteredProcesses.Count} processos.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro ao atualizar processos: {ex.Message}";
            }
            finally
            {
                if (!isSilent)
                {
                    IsLoading = false;
                }
            }
        }

        public async Task RefreshTasksAsync()
        {
            if (IsLoading) return;
            try
            {
                IsLoading = true;
                StatusMessage = "Consultando tarefas agendadas do Windows...";
                await LoadScheduledTasksInternalAsync();
                UpdateTaskMetrics();
                StatusMessage = $"Pronto - Tarefas lidas do Agendador do Windows. Exibindo {FilteredTasks.Count} tarefas.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro ao carregar tarefas: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task RefreshStartupItemsAsync()
        {
            if (IsLoading) return;
            try
            {
                IsLoading = true;
                StatusMessage = "Examinando chaves de inicialização no Registro e pastas...";
                await LoadStartupItemsInternalAsync();
                UpdateStartupMetrics();
                StatusMessage = $"Pronto - Programas de inicialização lidos do Registro. Exibindo {FilteredStartupItems.Count} itens.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro ao carregar itens de inicialização: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadAllDataAsync()
        {
            if (IsLoading) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Atualizando conexões, métricas de CPU/Banda, tarefas e inicialização...";

                await LoadConnectionsInternalAsync();
                await LoadScheduledTasksInternalAsync();
                await LoadStartupItemsInternalAsync();

                UpdateThreatIntelligenceMetrics();
                UpdateAllTabMetrics();
                StatusMessage = "Pronto - Dados de rede, processos, tarefas e inicialização atualizados.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro ao carregar dados: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadConnectionsAsync()
        {
            if (IsLoading) return;
            try
            {
                IsLoading = true;
                StatusMessage = "Carregando conexões de rede e métricas...";
                await LoadConnectionsInternalAsync();
                StatusMessage = "Pronto";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro ao carregar conexões: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadConnectionsInternalAsync()
        {
            var rules = _allowlistUseCase.GetAllRules();
            var domainConnections = await _scanUseCase.ExecuteAsync(rules);

            // Extract unique PIDs
            var pids = domainConnections.Select(c => c.PID).Distinct().ToList();
            var metrics = await _metricsUseCase.ExecuteAsync(pids);

            // Count connections per PID for anomaly detection
            var connCountByPid = domainConnections.GroupBy(c => c.PID).ToDictionary(g => g.Key, g => g.Count());

            // Enrich domain connections with metrics and threat evaluation
            foreach (var conn in domainConnections)
            {
                if (metrics.TryGetValue(conn.PID, out var m))
                {
                    conn.CpuPercent = m.CpuPercent;
                    conn.BandwidthBytesPerSec = m.TotalBytesPerSec;
                    int count = connCountByPid.TryGetValue(conn.PID, out int c) ? c : 1;

                    var eval = ThreatDetectionPolicy.EvaluateProcess(
                        conn.ProcessName,
                        conn.ProcessPath,
                        m.CpuPercent,
                        m.TotalBytesPerSec,
                        count);

                    conn.ThreatLevel = eval.Level;
                    conn.ThreatReason = eval.Reason;
                }
            }

            _allEntries.Clear();
            foreach (var conn in domainConnections)
            {
                _allEntries.Add(new ConnectionItemViewModel(conn));
            }

            // Build aggregated processes view
            _allProcesses.Clear();
            var groupedByPid = domainConnections.GroupBy(c => c.PID);
            foreach (var group in groupedByPid)
            {
                var first = group.First();
                metrics.TryGetValue(first.PID, out var m);

                double cpu = m?.CpuPercent ?? first.CpuPercent;
                double read = m?.ReadBytesPerSec ?? 0.0;
                double write = m?.WriteBytesPerSec ?? 0.0;
                int count = group.Count();

                var eval = ThreatDetectionPolicy.EvaluateProcess(
                    first.ProcessName,
                    first.ProcessPath,
                    cpu,
                    read + write,
                    count);

                _allProcesses.Add(new ProcessAggregateItemViewModel
                {
                    PID = first.PID,
                    ProcessName = first.ProcessName,
                    ProcessPath = first.ProcessPath,
                    CpuPercent = cpu,
                    ReadBytesPerSec = read,
                    WriteBytesPerSec = write,
                    ActiveConnectionsCount = count,
                    ThreatLevel = eval.Level,
                    ThreatReason = eval.Reason
                });
            }

            UpdateStateFilterOptions();
            UpdateMetrics();
            UpdateProcessMetrics();
            ApplyFilters();
            ApplyProcessFilters();
        }

        private async Task LoadScheduledTasksInternalAsync()
        {
            var tasks = await _tasksUseCase.ExecuteAsync();
            _allTasks.Clear();
            foreach (var t in tasks)
            {
                _allTasks.Add(new ScheduledTaskItemViewModel(t));
            }
            UpdateTaskMetrics();
            ApplyTaskFilters();
        }

        private async Task LoadStartupItemsInternalAsync()
        {
            var items = await _startupUseCase.ExecuteAsync();
            _allStartupItems.Clear();
            foreach (var i in items)
            {
                _allStartupItems.Add(new StartupItemViewModel(i));
            }
            UpdateStartupMetrics();
            ApplyStartupFilters();
        }

        private void UpdateThreatIntelligenceMetrics()
        {
            ThreatCountProxy = _allProcesses.Count(p => p.ThreatReason.Contains("Proxy")) +
                               _allTasks.Count(t => t.ThreatReason.Contains("Proxy")) +
                               _allStartupItems.Count(s => s.ThreatReason.Contains("Proxy"));

            ThreatCountMiner = _allProcesses.Count(p => p.ThreatReason.Contains("Mineração") || p.ThreatReason.Contains("distribuída")) +
                               _allTasks.Count(t => t.ThreatReason.Contains("minerador") || t.ThreatReason.Contains("distribuída")) +
                               _allStartupItems.Count(s => s.ThreatReason.Contains("Mineração"));

            ThreatCountSpikes = _allProcesses.Count(p => p.ThreatLevel >= ThreatLevel.Medium &&
                                                         (p.ThreatReason.Contains("Pico") || p.ThreatReason.Contains("conexões")));

            ThreatCountTasks = _allTasks.Count(t => t.IsSuspicious);
            ThreatCountStartup = _allStartupItems.Count(s => s.IsSuspicious);

            OnPropertyChanged(nameof(TotalThreatsCount));
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
            MetricThreatConnections = _allEntries.Count(e => e.ThreatLevel > ThreatLevel.Normal);
        }

        private void UpdateProcessMetrics()
        {
            MetricProcessesTotal = _allProcesses.Count;
            MetricProcessesHighCpu = _allProcesses.Count(p => p.CpuPercent >= 5.0);
            MetricProcessesHighNetwork = _allProcesses.Count(p => p.TotalBytesPerSec >= 50_000);
            MetricProcessesThreats = _allProcesses.Count(p => p.ThreatLevel > ThreatLevel.Normal);
            double totalBandwidth = _allProcesses.Sum(p => p.TotalBytesPerSec);
            MetricProcessesBandwidthSum = ProcessMetricInfo.FormatBytesPerSec(totalBandwidth);
        }

        private void UpdateTaskMetrics()
        {
            MetricTasksTotal = _allTasks.Count;
            MetricTasksEnabled = _allTasks.Count(t => !string.Equals(t.State, "Desabilitado", StringComparison.OrdinalIgnoreCase));
            MetricTasksDisabled = _allTasks.Count(t => string.Equals(t.State, "Desabilitado", StringComparison.OrdinalIgnoreCase));
            MetricTasksNonMicrosoft = _allTasks.Count(t => !t.IsMicrosoftTask);
            MetricTasksSuspicious = _allTasks.Count(t => t.IsSuspicious);
        }

        private void UpdateStartupMetrics()
        {
            MetricStartupTotal = _allStartupItems.Count;
            MetricStartupRegistry = _allStartupItems.Count(s => s.SourceType == "Registro");
            MetricStartupFolder = _allStartupItems.Count(s => s.SourceType == "Pasta Inicializar");
            MetricStartupSuspicious = _allStartupItems.Count(s => s.IsSuspicious);
        }

        public void UpdateAllTabMetrics()
        {
            UpdateMetrics();
            UpdateProcessMetrics();
            UpdateTaskMetrics();
            UpdateStartupMetrics();
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

        public void ApplyAllFilters()
        {
            ApplyFilters();
            ApplyProcessFilters();
            ApplyTaskFilters();
            ApplyStartupFilters();
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

                if ((SelectedTrustFilter == "Apenas Confiáveis" || SelectedTrustFilter == "Apenas Confiáveis (Allowlist)") && !entry.IsAllowed)
                {
                    return false;
                }
                if ((SelectedTrustFilter == "Não Cadastradas" || SelectedTrustFilter == "Não Cadastradas (Monitoradas)" || SelectedTrustFilter == "Ocultar Confiáveis") && entry.IsAllowed)
                {
                    return false;
                }

                // CPU Cutoff filter
                if (CpuCutoffThreshold > 0.0 && entry.CpuPercent < CpuCutoffThreshold)
                {
                    return false;
                }

                // Only Anomalies filter
                if (OnlyAnomalies && entry.ThreatLevel == ThreatLevel.Normal)
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
                    bool matchesThreat = !string.IsNullOrEmpty(entry.ThreatReason) && entry.ThreatReason.ToLowerInvariant().Contains(query);

                    if (!matchesName && !matchesPid && !matchesLocal && !matchesForeign && !matchesProtocol && !matchesState && !matchesAllowed && !matchesThreat)
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

        public void ApplyProcessFilters()
        {
            string query = SearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;

            var filtered = _allProcesses.Where(p =>
            {
                // CPU cutoff filter
                if (CpuCutoffThreshold > 0.0 && p.CpuPercent < CpuCutoffThreshold)
                {
                    return false;
                }

                // Bandwidth cutoff filter (Download/Read or Total)
                if (BandwidthCutoffThreshold > 0.0 && p.TotalBytesPerSec < BandwidthCutoffThreshold)
                {
                    return false;
                }

                // Only Anomalies filter
                if (OnlyAnomalies && p.ThreatLevel == ThreatLevel.Normal)
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(query))
                {
                    bool matchesName = p.ProcessName.ToLowerInvariant().Contains(query);
                    bool matchesPid = p.PID.ToString().Contains(query);
                    bool matchesThreat = !string.IsNullOrEmpty(p.ThreatReason) && p.ThreatReason.ToLowerInvariant().Contains(query);

                    if (!matchesName && !matchesPid && !matchesThreat)
                    {
                        return false;
                    }
                }

                return true;
            })
            .OrderByDescending(p => p.CpuPercent)
            .ThenByDescending(p => p.TotalBytesPerSec)
            .ToList();

            FilteredProcesses.Clear();
            foreach (var item in filtered)
            {
                FilteredProcesses.Add(item);
            }
            ProcessCountSummary = $"Exibindo {FilteredProcesses.Count} de {_allProcesses.Count} processos";
        }

        public void ApplyTaskFilters()
        {
            string query = SearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;

            var filtered = _allTasks.Where(t =>
            {
                if (SelectedTaskFilter.Contains("Suspeitas") && (t.IsMicrosoftTask && !t.IsSuspicious))
                {
                    return false;
                }
                if (SelectedTaskFilter.Contains("Habilitadas") && t.State.Equals("Desabilitado", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(query))
                {
                    bool matchesName = t.TaskName.ToLowerInvariant().Contains(query);
                    bool matchesPath = t.TaskPath.ToLowerInvariant().Contains(query);
                    bool matchesCmd = t.TaskToRun.ToLowerInvariant().Contains(query);
                    bool matchesAuthor = t.Author.ToLowerInvariant().Contains(query);
                    bool matchesThreat = !string.IsNullOrEmpty(t.ThreatReason) && t.ThreatReason.ToLowerInvariant().Contains(query);

                    if (!matchesName && !matchesPath && !matchesCmd && !matchesAuthor && !matchesThreat)
                    {
                        return false;
                    }
                }

                return true;
            })
            .OrderByDescending(t => t.ThreatLevel)
            .ThenBy(t => t.TaskName)
            .ToList();

            FilteredTasks.Clear();
            foreach (var item in filtered)
            {
                FilteredTasks.Add(item);
            }
            TaskCountSummary = $"Exibindo {FilteredTasks.Count} de {_allTasks.Count} tarefas";
        }

        public void ApplyStartupFilters()
        {
            string query = SearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;

            var filtered = _allStartupItems.Where(s =>
            {
                if (SelectedStartupFilter.Contains("Suspeitos") && !s.IsSuspicious)
                {
                    return false;
                }
                if (SelectedStartupFilter.Contains("Registro") && s.SourceType != "Registro")
                {
                    return false;
                }
                if ((SelectedStartupFilter.Contains("Pasta") || SelectedStartupFilter.Contains("Pastas")) && s.SourceType != "Pasta Inicializar")
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(query))
                {
                    bool matchesName = s.Name.ToLowerInvariant().Contains(query);
                    bool matchesCmd = s.Command.ToLowerInvariant().Contains(query);
                    bool matchesLoc = s.Location.ToLowerInvariant().Contains(query);
                    bool matchesThreat = !string.IsNullOrEmpty(s.ThreatReason) && s.ThreatReason.ToLowerInvariant().Contains(query);

                    if (!matchesName && !matchesCmd && !matchesLoc && !matchesThreat)
                    {
                        return false;
                    }
                }

                return true;
            })
            .OrderByDescending(s => s.ThreatLevel)
            .ThenBy(s => s.Name)
            .ToList();

            FilteredStartupItems.Clear();
            foreach (var item in filtered)
            {
                FilteredStartupItems.Add(item);
            }
            StartupCountSummary = $"Exibindo {FilteredStartupItems.Count} de {_allStartupItems.Count} itens";
        }

        public void ClearConnectionFilters()
        {
            SearchQuery = string.Empty;
            SelectedProtocol = "Todos";
            SelectedState = "Todos os Estados";
            SelectedTrustFilter = "Todas";
            CpuCutoffThreshold = 0.0;
            OnlyAnomalies = false;
            ApplyFilters();
        }

        public void ClearProcessFilters()
        {
            SearchQuery = string.Empty;
            CpuCutoffThreshold = 0.0;
            BandwidthCutoffThreshold = 0.0;
            OnlyAnomalies = false;
            ApplyProcessFilters();
        }

        public void ClearTaskFilters()
        {
            SearchQuery = string.Empty;
            SelectedTaskFilter = "Todas";
            ApplyTaskFilters();
        }

        public void ClearStartupFilters()
        {
            SearchQuery = string.Empty;
            SelectedStartupFilter = "Todos";
            ApplyStartupFilters();
        }

        public void ClearFilters()
        {
            SearchQuery = string.Empty;
            SelectedProtocol = "Todos";
            SelectedState = "Todos os Estados";
            SelectedTrustFilter = "Todas";
            CpuCutoffThreshold = 0.0;
            BandwidthCutoffThreshold = 0.0;
            OnlyAnomalies = false;
            SelectedTaskFilter = "Todas";
            SelectedStartupFilter = "Todos";
            ApplyAllFilters();
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
