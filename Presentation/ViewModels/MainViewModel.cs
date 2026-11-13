using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Globalization;
using System.Windows.Input;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Application.DTOs;
using NetStatAnalyzer.Application.UseCases;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;
using NetStatAnalyzer.Domain.Policies;
using NetStatAnalyzer.Presentation.Common;

namespace NetStatAnalyzer.Presentation.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ScanConnectionsUseCase _scanUseCase;
        private readonly ManageAllowlistUseCase _allowlistUseCase;
        private readonly ManageItemAnnotationsUseCase _annotationUseCase;
        private readonly ExportConnectionsUseCase _exportUseCase;
        private readonly IAnalysisJsonExporter _analysisJsonExporter;
        private readonly ScanProcessMetricsUseCase _metricsUseCase;
        private readonly ScanScheduledTasksUseCase _tasksUseCase;
        private readonly ScanStartupItemsUseCase _startupUseCase;
        private readonly IWindowsToolsLauncher _windowsToolsLauncher;

        // 1. Network Connections Tab
        public object EntriesLock { get; } = new();
        private readonly List<ConnectionItemViewModel> _allEntries = new();
        public ObservableCollection<ConnectionItemViewModel> FilteredEntries { get; } = new();
        public ObservableCollection<string> StateOptions { get; } = new();

        // 2. Aggregated Processes Tab
        public object ProcessesLock { get; } = new();
        private readonly List<ProcessAggregateItemViewModel> _allProcesses = new();
        public ObservableCollection<ProcessAggregateItemViewModel> FilteredProcesses { get; } = new();

        // 3. Scheduled Tasks Tab
        public object TasksLock { get; } = new();
        private readonly List<ScheduledTaskItemViewModel> _allTasks = new();
        public ObservableCollection<ScheduledTaskItemViewModel> FilteredTasks { get; } = new();

        // 4. Startup Items Tab
        public object StartupLock { get; } = new();
        private readonly List<StartupItemViewModel> _allStartupItems = new();
        public ObservableCollection<StartupItemViewModel> FilteredStartupItems { get; } = new();

        public ObservableCollection<string> GlobalTagOptions { get; } = new();
        public IReadOnlyList<string> SavedTags => _annotationUseCase.Tags;
        private string _globalTagFilter = "Todas as tags";
        public string GlobalTagFilter
        {
            get => _globalTagFilter;
            set { if (SetProperty(ref _globalTagFilter, value)) ApplyAllFilters(); }
        }

        private string _globalTrustFilter = "Todas";
        public string GlobalTrustFilter
        {
            get => _globalTrustFilter;
            set { if (SetProperty(ref _globalTrustFilter, value)) ApplyAllFilters(); }
        }

        private bool _globalOnlyAlerts;
        public bool GlobalOnlyAlerts
        {
            get => _globalOnlyAlerts;
            set { if (SetProperty(ref _globalOnlyAlerts, value)) ApplyAllFilters(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private readonly string[] _tabStatusMessages =
        {
            "Aguardando leitura das conexões.",
            "Aguardando leitura dos processos.",
            "Aguardando leitura das tarefas.",
            "Aguardando leitura dos itens de inicialização."
        };

        public string StatusMessage
        {
            get => _tabStatusMessages[GetStatusTabIndex(SelectedTabIndex)];
            set => SetTabStatusMessage(SelectedTabIndex, value);
        }

        private static int GetStatusTabIndex(int tabIndex) => tabIndex is >= 0 and < 4 ? tabIndex : 0;

        private void SetTabStatusMessage(int tabIndex, string message)
        {
            int statusTabIndex = GetStatusTabIndex(tabIndex);
            if (_tabStatusMessages[statusTabIndex] == message)
            {
                return;
            }

            _tabStatusMessages[statusTabIndex] = message;
            if (_selectedTabIndex == statusTabIndex)
            {
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        private DateTime? _connectionsLastUpdatedAt;
        private DateTime? _processesLastUpdatedAt;
        private DateTime? _tasksLastUpdatedAt;
        private DateTime? _startupLastUpdatedAt;

        public string CurrentTabLastUpdatedText
        {
            get
            {
                var lastUpdatedAt = SelectedTabIndex switch
                {
                    0 => _connectionsLastUpdatedAt,
                    1 => _processesLastUpdatedAt,
                    2 => _tasksLastUpdatedAt,
                    3 => _startupLastUpdatedAt,
                    _ => _connectionsLastUpdatedAt
                };

                return lastUpdatedAt is DateTime timestamp
                    ? $"Atualizado: {timestamp.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture)}"
                    : "Dados desta aba ainda não foram carregados";
            }
        }

        private string _connectionSearchQuery = string.Empty;
        public string ConnectionSearchQuery
        {
            get => _connectionSearchQuery;
            set
            {
                if (SetProperty(ref _connectionSearchQuery, value)) ApplyFilters();
            }
        }

        private string _processSearchQuery = string.Empty;
        public string ProcessSearchQuery
        {
            get => _processSearchQuery;
            set { if (SetProperty(ref _processSearchQuery, value)) ApplyProcessFilters(); }
        }

        private string _taskSearchQuery = string.Empty;
        public string TaskSearchQuery
        {
            get => _taskSearchQuery;
            set { if (SetProperty(ref _taskSearchQuery, value)) ApplyTaskFilters(); }
        }

        private string _startupSearchQuery = string.Empty;
        public string StartupSearchQuery
        {
            get => _startupSearchQuery;
            set { if (SetProperty(ref _startupSearchQuery, value)) ApplyStartupFilters(); }
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

        // CPU (%) column filter
        private double _cpuCutoffThreshold = 0.0;
        public double CpuCutoffThreshold
        {
            get => _cpuCutoffThreshold;
            set
            {
                if (SetProperty(ref _cpuCutoffThreshold, value))
                {
                    ApplyFilters();
                }
            }
        }

        private double _processCpuCutoffThreshold;
        public double ProcessCpuCutoffThreshold
        {
            get => _processCpuCutoffThreshold;
            set { if (SetProperty(ref _processCpuCutoffThreshold, value)) ApplyProcessFilters(); }
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
                    OnPropertyChanged(nameof(CurrentTabLastUpdatedText));
                    OnPropertyChanged(nameof(StatusMessage));
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
        public ICommand ClearFiltersCommand { get; }

        // Windows Tools Commands
        public ICommand OpenTaskSchedulerCommand { get; }
        public ICommand OpenStartupSettingsCommand { get; }
        public ICommand OpenTaskManagerCommand { get; }
        public ICommand OpenResourceMonitorCommand { get; }
        public ICommand OpenStartupFolderCommand { get; }
        public ICommand OpenRegistryEditorCommand { get; }

        public MainViewModel(
            ScanConnectionsUseCase scanUseCase,
            ManageAllowlistUseCase allowlistUseCase,
            ManageItemAnnotationsUseCase annotationUseCase,
            ExportConnectionsUseCase exportUseCase,
            IAnalysisJsonExporter analysisJsonExporter,
            ScanProcessMetricsUseCase metricsUseCase,
            ScanScheduledTasksUseCase tasksUseCase,
            ScanStartupItemsUseCase startupUseCase,
            IWindowsToolsLauncher windowsToolsLauncher)
        {
            _scanUseCase = scanUseCase ?? throw new ArgumentNullException(nameof(scanUseCase));
            _allowlistUseCase = allowlistUseCase ?? throw new ArgumentNullException(nameof(allowlistUseCase));
            _annotationUseCase = annotationUseCase ?? throw new ArgumentNullException(nameof(annotationUseCase));
            _exportUseCase = exportUseCase ?? throw new ArgumentNullException(nameof(exportUseCase));
            _analysisJsonExporter = analysisJsonExporter ?? throw new ArgumentNullException(nameof(analysisJsonExporter));
            _metricsUseCase = metricsUseCase ?? throw new ArgumentNullException(nameof(metricsUseCase));
            _tasksUseCase = tasksUseCase ?? throw new ArgumentNullException(nameof(tasksUseCase));
            _startupUseCase = startupUseCase ?? throw new ArgumentNullException(nameof(startupUseCase));
            _windowsToolsLauncher = windowsToolsLauncher ?? throw new ArgumentNullException(nameof(windowsToolsLauncher));
            UpdateTagOptions();

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            AppVersion = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.4.1";

            ReloadCommand = new AsyncRelayCommand(RefreshCurrentTabAsync);
            RefreshCurrentTabCommand = new AsyncRelayCommand(RefreshCurrentTabAsync);
            ClearFiltersCommand = new RelayCommand(ClearFilters);

            OpenTaskSchedulerCommand = new RelayCommand(() => _windowsToolsLauncher.OpenTaskScheduler());
            OpenStartupSettingsCommand = new RelayCommand(() => _windowsToolsLauncher.OpenStartupAppsSettings());
            OpenTaskManagerCommand = new RelayCommand(() => _windowsToolsLauncher.OpenTaskManager());
            OpenResourceMonitorCommand = new RelayCommand(() => _windowsToolsLauncher.OpenResourceMonitor());
            OpenStartupFolderCommand = new RelayCommand(() => _windowsToolsLauncher.OpenStartupFolder());
            OpenRegistryEditorCommand = new RelayCommand(() => _windowsToolsLauncher.OpenRegistryEditor());

            _allowlistUseCase.RulesChanged += (s, e) =>
            {
                ReevaluateTrustedStatus();
                RefreshAnnotations();
                UpdateMetrics();
                ApplyFilters();
            };
        }

        public bool OpenRegistryEditor(string? keyPath = null) => _windowsToolsLauncher.OpenRegistryEditor(keyPath);

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
                    SetTabStatusMessage(0, "Lendo conexões de rede ativas (netstat)...");
                    SetTabStatusMessage(1, "Atualizando dados dos processos a partir das conexões de rede...");
                }
                await LoadConnectionsInternalAsync();
                UpdateMetrics();
                if (!isSilent)
                {
                    SetTabStatusMessage(0, "Conexões atualizadas.");
                    SetTabStatusMessage(1, "Amostra de processos atualizada.");
                }
            }
            catch (Exception ex)
            {
                SetTabStatusMessage(0, $"Erro ao carregar conexões: {ex.Message}");
                SetTabStatusMessage(1, $"Não foi possível atualizar os processos: {ex.Message}");
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
                    SetTabStatusMessage(0, "Atualizando conexões de rede...");
                    SetTabStatusMessage(1, "Coletando amostra de consumo de CPU e banda dos processos...");
                }
                await LoadConnectionsInternalAsync();
                UpdateProcessMetrics();
                if (!isSilent)
                {
                    SetTabStatusMessage(0, "Conexões atualizadas.");
                    SetTabStatusMessage(1, "Amostra de processos atualizada.");
                }
            }
            catch (Exception ex)
            {
                SetTabStatusMessage(0, $"Não foi possível atualizar as conexões: {ex.Message}");
                SetTabStatusMessage(1, $"Erro ao atualizar processos: {ex.Message}");
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
                SetTabStatusMessage(2, "Consultando tarefas agendadas do Windows...");
                await LoadScheduledTasksInternalAsync();
                UpdateTaskMetrics();
                SetTabStatusMessage(2, "Tarefas atualizadas.");
            }
            catch (Exception ex)
            {
                SetTabStatusMessage(2, $"Erro ao carregar tarefas: {ex.Message}");
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
                SetTabStatusMessage(3, "Examinando chaves de inicialização no Registro e pastas...");
                await LoadStartupItemsInternalAsync();
                UpdateStartupMetrics();
                SetTabStatusMessage(3, "Itens de inicialização atualizados.");
            }
            catch (Exception ex)
            {
                SetTabStatusMessage(3, $"Erro ao carregar itens de inicialização: {ex.Message}");
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
                SetTabStatusMessage(0, "Atualizando conexões de rede...");
                SetTabStatusMessage(1, "Atualizando métricas dos processos...");
                SetTabStatusMessage(2, "Consultando tarefas agendadas...");
                SetTabStatusMessage(3, "Examinando itens de inicialização...");

                try
                {
                    await LoadConnectionsInternalAsync();
                    SetTabStatusMessage(0, "Conexões atualizadas.");
                    SetTabStatusMessage(1, "Processos atualizados.");
                }
                catch (Exception ex)
                {
                    SetTabStatusMessage(0, $"Erro ao carregar conexões: {ex.Message}");
                    SetTabStatusMessage(1, $"Erro ao carregar processos: {ex.Message}");
                }

                try
                {
                    await LoadScheduledTasksInternalAsync();
                    SetTabStatusMessage(2, "Tarefas atualizadas.");
                }
                catch (Exception ex)
                {
                    SetTabStatusMessage(2, $"Erro ao carregar tarefas: {ex.Message}");
                }

                try
                {
                    await LoadStartupItemsInternalAsync();
                    SetTabStatusMessage(3, "Itens de inicialização atualizados.");
                }
                catch (Exception ex)
                {
                    SetTabStatusMessage(3, $"Erro ao carregar itens de inicialização: {ex.Message}");
                }

                UpdateThreatIntelligenceMetrics();
                UpdateAllTabMetrics();
            }
            catch (Exception ex)
            {
                SetTabStatusMessage(SelectedTabIndex, $"Erro ao carregar dados: {ex.Message}");
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
                SetTabStatusMessage(0, "Carregando conexões de rede e métricas...");
                SetTabStatusMessage(1, "Carregando métricas dos processos...");
                await LoadConnectionsInternalAsync();
                SetTabStatusMessage(0, "Conexões atualizadas.");
                SetTabStatusMessage(1, "Amostra de processos atualizada.");
            }
            catch (Exception ex)
            {
                SetTabStatusMessage(0, $"Erro ao carregar conexões: {ex.Message}");
                SetTabStatusMessage(1, $"Erro ao carregar processos: {ex.Message}");
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
            RefreshAnnotations();
            UpdateMetrics();
            UpdateProcessMetrics();
            ApplyFilters();
            ApplyProcessFilters();

            var updatedAt = DateTime.Now;
            _connectionsLastUpdatedAt = updatedAt;
            _processesLastUpdatedAt = updatedAt;
            OnPropertyChanged(nameof(CurrentTabLastUpdatedText));
        }

        private async Task LoadScheduledTasksInternalAsync()
        {
            var tasks = await _tasksUseCase.ExecuteAsync();
            _allTasks.Clear();
            foreach (var t in tasks)
            {
                _allTasks.Add(new ScheduledTaskItemViewModel(t));
            }
            RefreshAnnotations();
            UpdateTaskMetrics();
            ApplyTaskFilters();
            _tasksLastUpdatedAt = DateTime.Now;
            OnPropertyChanged(nameof(CurrentTabLastUpdatedText));
        }

        private async Task LoadStartupItemsInternalAsync()
        {
            var items = await _startupUseCase.ExecuteAsync();
            _allStartupItems.Clear();
            foreach (var i in items)
            {
                _allStartupItems.Add(new StartupItemViewModel(i));
            }
            RefreshAnnotations();
            UpdateStartupMetrics();
            ApplyStartupFilters();
            _startupLastUpdatedAt = DateTime.Now;
            OnPropertyChanged(nameof(CurrentTabLastUpdatedText));
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

        public string Theme => _annotationUseCase.Theme;

        public void SaveTheme(string theme) => _annotationUseCase.SaveTheme(theme);

        private static (string Type, string Key) Identity(ViewModelBase item) => item switch
        {
            ConnectionItemViewModel c => ("connection", $"{c.Protocol}|{c.ProcessPath ?? c.ProcessName}|{c.ForeignAddress}"),
            ProcessAggregateItemViewModel p => ("process", p.ProcessPath ?? p.ProcessName),
            ScheduledTaskItemViewModel t => ("task", $"{t.TaskPath}|{t.TaskName}"),
            StartupItemViewModel s => ("startup", $"{s.Location}|{s.Name}"),
            _ => throw new ArgumentException("Tipo de item desconhecido.", nameof(item))
        };

        private void UpdateTagOptions()
        {
            string selected = GlobalTagFilter;
            GlobalTagOptions.Clear();
            GlobalTagOptions.Add("Todas as tags");
            foreach (var tag in _annotationUseCase.Tags) GlobalTagOptions.Add(tag);
            GlobalTagFilter = GlobalTagOptions.Contains(selected) ? selected : "Todas as tags";
        }

        private void RefreshAnnotations()
        {
            foreach (ViewModelBase item in _allEntries.Cast<ViewModelBase>()
                .Concat(_allProcesses).Concat(_allTasks).Concat(_allStartupItems))
            {
                var (type, key) = Identity(item);
                var annotation = _annotationUseCase.Find(type, key);
                item.TagText = annotation?.Tag ?? string.Empty;
                item.AnnotationTrusted = item is ConnectionItemViewModel c ? c.IsAllowed : annotation?.IsTrusted ?? false;
                switch (item)
                {
                    case ConnectionItemViewModel connection: connection.Model.Tag = item.TagText; break;
                    case ScheduledTaskItemViewModel task:
                        task.Model.Tag = item.TagText;
                        task.Model.IsTrusted = item.AnnotationTrusted;
                        break;
                    case StartupItemViewModel startup:
                        startup.Model.Tag = item.TagText;
                        startup.Model.IsTrusted = item.AnnotationTrusted;
                        break;
                }
            }
        }

        public void SetItemTags(IEnumerable<ViewModelBase> items, string? tag)
        {
            var selected = items.ToArray();
            if (selected.Length == 0) return;
            _annotationUseCase.SetTags(selected.Select(Identity), tag);
            UpdateTagOptions();
            RefreshAnnotations();
            ApplyAllFilters();
        }

        public void SetItemsTrust(IEnumerable<ViewModelBase> items, bool trusted)
        {
            var selected = items.ToArray();
            if (selected.Length == 0) return;

            var connections = selected.OfType<ConnectionItemViewModel>().ToArray();
            if (connections.Length > 0)
            {
                var toTrust = connections.Where(item => trusted && !item.IsAllowed).ToArray();
                var toUntrust = connections.Where(item => !trusted && item.IsAllowed).ToArray();
                if (toTrust.Length > 0) AddSelectedToAllowlist(toTrust);
                if (toUntrust.Length > 0) RemoveSelectedFromAllowlist(toUntrust);
            }

            var annotatedItems = selected.Where(item => item is not ConnectionItemViewModel).ToArray();
            if (annotatedItems.Length > 0)
                _annotationUseCase.SetTrust(annotatedItems.Select(Identity), trusted);

            RefreshAnnotations();
            ApplyAllFilters();
        }

        private bool PassesGlobalFilters(ViewModelBase item, ThreatLevel level)
        {
            if (!string.IsNullOrWhiteSpace(GlobalTagFilter) && GlobalTagFilter != "Todas as tags" &&
                !item.TagText.Equals(GlobalTagFilter, StringComparison.OrdinalIgnoreCase)) return false;
            if (GlobalTrustFilter == "Apenas confiáveis" && !item.AnnotationTrusted) return false;
            if (GlobalTrustFilter == "Não verificados" && item.AnnotationTrusted) return false;
            return !GlobalOnlyAlerts || level != ThreatLevel.Normal;
        }

        public void ClearGlobalFilters()
        {
            GlobalTagFilter = "Todas as tags";
            GlobalTrustFilter = "Todas";
            GlobalOnlyAlerts = false;
            ApplyAllFilters();
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
            string query = ConnectionSearchQuery.Trim().ToLowerInvariant();

            var filtered = _allEntries.Where(entry =>
            {
                if (!PassesGlobalFilters(entry, entry.ThreatLevel)) return false;
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

                // Only keep connections that meet the selected CPU (%) level.
                if (CpuCutoffThreshold > 0.0 && entry.CpuPercent < CpuCutoffThreshold)
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

            lock (EntriesLock)
            {
                FilteredEntries.Clear();
                foreach (var item in filtered)
                {
                    FilteredEntries.Add(item);
                }
            }

            ConnectionCountSummary = $"Exibindo {FilteredEntries.Count} de {_allEntries.Count} conexões";
            IsEmptyState = FilteredEntries.Count == 0;
        }

        public void ApplyProcessFilters()
        {
            string query = ProcessSearchQuery.Trim().ToLowerInvariant();

            var filtered = _allProcesses.Where(p =>
            {
                if (!PassesGlobalFilters(p, p.ThreatLevel)) return false;
                // Only keep processes that meet the selected CPU (%) level.
                if (ProcessCpuCutoffThreshold > 0.0 && p.CpuPercent < ProcessCpuCutoffThreshold)
                {
                    return false;
                }

                // Bandwidth cutoff filter (Download/Read or Total)
                if (BandwidthCutoffThreshold > 0.0 && p.TotalBytesPerSec < BandwidthCutoffThreshold)
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

            lock (ProcessesLock)
            {
                FilteredProcesses.Clear();
                foreach (var item in filtered)
                {
                    FilteredProcesses.Add(item);
                }
            }
            ProcessCountSummary = $"Exibindo {FilteredProcesses.Count} de {_allProcesses.Count} processos";
        }

        public void ApplyTaskFilters()
        {
            string query = TaskSearchQuery.Trim().ToLowerInvariant();

            var filtered = _allTasks.Where(t =>
            {
                if (!PassesGlobalFilters(t, t.ThreatLevel)) return false;
                if (SelectedTaskFilter.Contains("suspeitas", StringComparison.OrdinalIgnoreCase) && (t.IsMicrosoftTask && !t.IsSuspicious))
                {
                    return false;
                }
                if (SelectedTaskFilter.Contains("habilitadas", StringComparison.OrdinalIgnoreCase) && t.State.Equals("Desabilitado", StringComparison.OrdinalIgnoreCase))
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

            lock (TasksLock)
            {
                FilteredTasks.Clear();
                foreach (var item in filtered)
                {
                    FilteredTasks.Add(item);
                }
            }
            TaskCountSummary = $"Exibindo {FilteredTasks.Count} de {_allTasks.Count} tarefas";
        }

        public void ApplyStartupFilters()
        {
            string query = StartupSearchQuery.Trim().ToLowerInvariant();

            var filtered = _allStartupItems.Where(s =>
            {
                if (!PassesGlobalFilters(s, s.ThreatLevel)) return false;
                if (SelectedStartupFilter.Contains("suspeitos", StringComparison.OrdinalIgnoreCase) && !s.IsSuspicious)
                {
                    return false;
                }
                if (SelectedStartupFilter.Contains("registro", StringComparison.OrdinalIgnoreCase) && s.SourceType != "Registro")
                {
                    return false;
                }
                if (SelectedStartupFilter.Contains("pasta", StringComparison.OrdinalIgnoreCase) && s.SourceType != "Pasta Inicializar")
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

            lock (StartupLock)
            {
                FilteredStartupItems.Clear();
                foreach (var item in filtered)
                {
                    FilteredStartupItems.Add(item);
                }
            }
            StartupCountSummary = $"Exibindo {FilteredStartupItems.Count} de {_allStartupItems.Count} itens";
        }

        public void ClearConnectionFilters()
        {
            ConnectionSearchQuery = string.Empty;
            SelectedProtocol = "Todos";
            SelectedState = "Todos os Estados";
            CpuCutoffThreshold = 0.0;
            ApplyFilters();
        }

        public void ClearProcessFilters()
        {
            ProcessSearchQuery = string.Empty;
            ProcessCpuCutoffThreshold = 0.0;
            BandwidthCutoffThreshold = 0.0;
            ApplyProcessFilters();
        }

        public void ClearTaskFilters()
        {
            TaskSearchQuery = string.Empty;
            SelectedTaskFilter = "Todas";
            ApplyTaskFilters();
        }

        public void ClearStartupFilters()
        {
            StartupSearchQuery = string.Empty;
            SelectedStartupFilter = "Todos";
            ApplyStartupFilters();
        }

        public void ClearFilters()
        {
            ConnectionSearchQuery = string.Empty;
            ProcessSearchQuery = string.Empty;
            TaskSearchQuery = string.Empty;
            StartupSearchQuery = string.Empty;
            SelectedProtocol = "Todos";
            SelectedState = "Todos os Estados";
            CpuCutoffThreshold = 0.0;
            ProcessCpuCutoffThreshold = 0.0;
            BandwidthCutoffThreshold = 0.0;
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
                _analysisJsonExporter.ExportConnections(filePath, connections, AppVersion, _connectionsLastUpdatedAt);
            }

            StatusMessage = $"{FilteredEntries.Count} conexões exportadas para '{Path.GetFileName(filePath)}'!";
        }

        public int ExportCurrentTabJson(string filePath)
        {
            int count;
            switch (SelectedTabIndex)
            {
                case 1:
                    var processes = FilteredProcesses.Select(item => new ProcessAnalysisItem(
                        item.PID, item.ProcessName, item.ProcessPath, item.CpuPercent,
                        item.ReadBytesPerSec, item.WriteBytesPerSec, item.ActiveConnectionsCount,
                        item.ThreatLevel, item.ThreatReason, item.TagText, item.AnnotationTrusted)).ToArray();
                    _analysisJsonExporter.ExportProcesses(filePath, processes, AppVersion, _processesLastUpdatedAt);
                    count = processes.Length;
                    break;
                case 2:
                    _analysisJsonExporter.ExportTasks(filePath, FilteredTasks.Select(item => item.Model), AppVersion, _tasksLastUpdatedAt);
                    count = FilteredTasks.Count;
                    break;
                case 3:
                    _analysisJsonExporter.ExportStartupItems(filePath, FilteredStartupItems.Select(item => item.Model), AppVersion, _startupLastUpdatedAt);
                    count = FilteredStartupItems.Count;
                    break;
                default:
                    _analysisJsonExporter.ExportConnections(filePath, FilteredEntries.Select(item => item.Model), AppVersion, _connectionsLastUpdatedAt);
                    count = FilteredEntries.Count;
                    break;
            }

            StatusMessage = $"{count} itens exportados para '{Path.GetFileName(filePath)}'.";
            return count;
        }
    }
}
