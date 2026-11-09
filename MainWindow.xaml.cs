using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using NetStatAnalyzer.Application.UseCases;
using NetStatAnalyzer.Domain.Policies;
using NetStatAnalyzer.Infrastructure.Exporting;
using NetStatAnalyzer.Infrastructure.Monitoring;
using NetStatAnalyzer.Infrastructure.Persistence;
using NetStatAnalyzer.Infrastructure.Processes;
using NetStatAnalyzer.Infrastructure.Scanning;
using NetStatAnalyzer.Infrastructure.System;
using NetStatAnalyzer.Presentation.Extensions;
using NetStatAnalyzer.Presentation.ViewModels;

namespace NetStatAnalyzer
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }
        private readonly DispatcherTimer _liveReadingTimer;

        public MainWindow()
        {
            InitializeComponent();

            var networkScanner = new NetStatCliScanner();
            var processResolver = new Win32ProcessResolver();
            var allowlistRepo = new JsonFileAllowlistRepository();
            var connectionExporter = new FileConnectionExporter();
            var metricsProvider = new Win32ProcessMetricsProvider();
            var taskScanner = new WindowsScheduledTaskScanner();
            var startupScanner = new WindowsStartupScanner();

            var scanUseCase = new ScanConnectionsUseCase(networkScanner, processResolver);
            var allowlistUseCase = new ManageAllowlistUseCase(allowlistRepo);
            var exportUseCase = new ExportConnectionsUseCase(connectionExporter);
            var metricsUseCase = new ScanProcessMetricsUseCase(metricsProvider);
            var tasksUseCase = new ScanScheduledTasksUseCase(taskScanner);
            var startupUseCase = new ScanStartupItemsUseCase(startupScanner);

            ViewModel = new MainViewModel(
                scanUseCase,
                allowlistUseCase,
                exportUseCase,
                metricsUseCase,
                tasksUseCase,
                startupUseCase);

            DataContext = ViewModel;

            _liveReadingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _liveReadingTimer.Tick += async (s, e) =>
            {
                if (ViewModel.IsLoading) return;
                if (ViewModel.SelectedTabIndex == 0 && ConnectionsLiveReadingToggleBtn.IsChecked == true)
                {
                    await ViewModel.RefreshConnectionsAsync();
                    DataGrid.AutoFitColumns();
                }
                else if (ViewModel.SelectedTabIndex == 1 && LiveReadingToggleBtn.IsChecked == true)
                {
                    await ViewModel.RefreshProcessesAsync();
                    ProcessesDataGrid.AutoFitColumns();
                }
            };

            VersionTextBlock.Text = ViewModel.AppVersion;
            ViewModel.PropertyChanged += (s, e) =>
            {
                // Tab 0: Conexões metrics
                if (e.PropertyName == nameof(MainViewModel.MetricTotal))
                    MetricTotalText.Text = ViewModel.MetricTotal.ToString();
                else if (e.PropertyName == nameof(MainViewModel.MetricEstablished))
                    MetricEstablishedText.Text = ViewModel.MetricEstablished.ToString();
                else if (e.PropertyName == nameof(MainViewModel.MetricListening))
                    MetricListeningText.Text = ViewModel.MetricListening.ToString();
                else if (e.PropertyName == nameof(MainViewModel.MetricAllowed))
                    MetricAllowedText.Text = ViewModel.MetricAllowed.ToString();
                else if (e.PropertyName == nameof(MainViewModel.MetricThreatConnections) ||
                         e.PropertyName == nameof(MainViewModel.TotalThreatsCount))
                {
                    MetricThreatText.Text = ViewModel.MetricThreatConnections.ToString();
                }

                // Tab 1: Processos metrics
                else if (e.PropertyName == nameof(MainViewModel.MetricProcessesTotal))
                    MetricProcessesTotalText.Text = ViewModel.MetricProcessesTotal.ToString();
                else if (e.PropertyName == nameof(MainViewModel.MetricProcessesHighCpu))
                    MetricProcessesHighCpuText.Text = ViewModel.MetricProcessesHighCpu.ToString();
                else if (e.PropertyName == nameof(MainViewModel.MetricProcessesHighNetwork))
                    MetricProcessesHighNetworkText.Text = ViewModel.MetricProcessesHighNetwork.ToString();
                else if (e.PropertyName == nameof(MainViewModel.MetricProcessesBandwidthSum))
                    MetricProcessesBandwidthSumText.Text = ViewModel.MetricProcessesBandwidthSum;
                else if (e.PropertyName == nameof(MainViewModel.MetricProcessesThreats))
                    MetricProcessesThreatsText.Text = ViewModel.MetricProcessesThreats.ToString();

                // Tab 2: Tarefas Agendadas metrics
                else if (e.PropertyName == nameof(MainViewModel.MetricTasksTotal))
                    MetricTasksTotalText.Text = ViewModel.MetricTasksTotal.ToString();
                else if (e.PropertyName == nameof(MainViewModel.MetricTasksEnabled))
                    MetricTasksEnabledText.Text = ViewModel.MetricTasksEnabled.ToString();
                else if (e.PropertyName == nameof(MainViewModel.MetricTasksDisabled))
                    MetricTasksDisabledText.Text = ViewModel.MetricTasksDisabled.ToString();
                else if (e.PropertyName == nameof(MainViewModel.MetricTasksNonMicrosoft))
                    MetricTasksNonMicrosoftText.Text = ViewModel.MetricTasksNonMicrosoft.ToString();
                else if (e.PropertyName == nameof(MainViewModel.MetricTasksSuspicious))
                    MetricTasksSuspiciousText.Text = ViewModel.MetricTasksSuspicious.ToString();

                // Tab 3: Itens de Inicialização metrics
                else if (e.PropertyName == nameof(MainViewModel.MetricStartupTotal))
                    MetricStartupTotalText.Text = ViewModel.MetricStartupTotal.ToString();
                else if (e.PropertyName == nameof(MainViewModel.MetricStartupRegistry))
                    MetricStartupRegistryText.Text = ViewModel.MetricStartupRegistry.ToString();
                else if (e.PropertyName == nameof(MainViewModel.MetricStartupFolder))
                    MetricStartupFolderText.Text = ViewModel.MetricStartupFolder.ToString();
                else if (e.PropertyName == nameof(MainViewModel.MetricStartupSuspicious))
                    MetricStartupSuspiciousText.Text = ViewModel.MetricStartupSuspicious.ToString();

                // Status & Contextual Summaries
                else if (e.PropertyName == nameof(MainViewModel.CurrentTabSummary) ||
                         e.PropertyName == nameof(MainViewModel.ConnectionCountSummary) ||
                         e.PropertyName == nameof(MainViewModel.ProcessCountSummary) ||
                         e.PropertyName == nameof(MainViewModel.TaskCountSummary) ||
                         e.PropertyName == nameof(MainViewModel.StartupCountSummary))
                {
                    ConnectionCountText.Text = ViewModel.CurrentTabSummary;
                }
                else if (e.PropertyName == nameof(MainViewModel.StatusMessage))
                    StatusTextBlock.Text = ViewModel.StatusMessage;
                else if (e.PropertyName == nameof(MainViewModel.IsLoading))
                {
                    LoadingProgressBar.Visibility = ViewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;
                    ReloadButton.IsEnabled = !ViewModel.IsLoading;
                }
            };

            DataGrid.ItemsSource = ViewModel.FilteredEntries;
            ProcessesDataGrid.ItemsSource = ViewModel.FilteredProcesses;
            TasksDataGrid.ItemsSource = ViewModel.FilteredTasks;
            StartupDataGrid.ItemsSource = ViewModel.FilteredStartupItems;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAllDataAsync();
            DataGrid.AutoFitColumns();
        }

        private void TabButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int tabIndex))
            {
                SwitchTab(tabIndex);
            }
        }

        public void SwitchTab(int index)
        {
            ViewModel.SelectedTabIndex = index;

            var activeStyle = (Style)FindResource("ModernActiveTabButton");
            var inactiveStyle = (Style)FindResource("ModernTabButton");

            TabConnectionsBtn.Style = index == 0 ? activeStyle : inactiveStyle;
            TabProcessesBtn.Style = index == 1 ? activeStyle : inactiveStyle;
            TabTasksBtn.Style = index == 2 ? activeStyle : inactiveStyle;
            TabStartupBtn.Style = index == 3 ? activeStyle : inactiveStyle;

            TabContentConnections.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            TabContentProcesses.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            TabContentTasks.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
            TabContentStartup.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;

            ConnectionsActionPanel.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;

            ConnectionCountText.Text = ViewModel.CurrentTabSummary;

            // Clear row selection details on tab switch
            SelectedSeparator.Visibility = Visibility.Collapsed;
            SelectedProcessText.Visibility = Visibility.Collapsed;
            SelectedProcessText.Text = string.Empty;

            AutoFitActiveTabGrid();
        }

        private void AutoFitActiveTabGrid()
        {
            switch (ViewModel.SelectedTabIndex)
            {
                case 0: DataGrid.AutoFitColumns(); break;
                case 1: ProcessesDataGrid.AutoFitColumns(); break;
                case 2: TasksDataGrid.AutoFitColumns(); break;
                case 3: StartupDataGrid.AutoFitColumns(); break;
            }
        }

        private void WindowsToolsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SearchQuery = SearchTextBox.Text;
                DataGrid.AutoFitColumns();
            }
        }

        private void ProcessSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SearchQuery = ProcessSearchTextBox.Text;
                ProcessesDataGrid.AutoFitColumns();
            }
        }

        private void TaskSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SearchQuery = TaskSearchTextBox.Text;
                TasksDataGrid.AutoFitColumns();
            }
        }

        private void StartupSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SearchQuery = StartupSearchTextBox.Text;
                StartupDataGrid.AutoFitColumns();
            }
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                if (ProtocolComboBox?.SelectedItem is ComboBoxItem protoItem)
                    vm.SelectedProtocol = protoItem.Content?.ToString() ?? "Todos";

                if (StateComboBox?.SelectedItem is string stateStr)
                    vm.SelectedState = stateStr;

                if (AllowFilterComboBox?.SelectedItem is ComboBoxItem allowItem)
                    vm.SelectedTrustFilter = allowItem.Content?.ToString() ?? "Todas";

                vm.ApplyFilters();
                DataGrid.AutoFitColumns();
            }
        }

        private void CpuCutoffComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm && CpuCutoffComboBox?.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && double.TryParse(tagStr, out double threshold))
            {
                vm.CpuCutoffThreshold = threshold;
                DataGrid.AutoFitColumns();
            }
        }

        private void ProcessCpuCutoffComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm && ProcessCpuCutoffComboBox?.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && double.TryParse(tagStr, out double threshold))
            {
                vm.CpuCutoffThreshold = threshold;
                ProcessesDataGrid.AutoFitColumns();
            }
        }

        private void ProcessBandwidthCutoffComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm && ProcessBandwidthCutoffComboBox?.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && double.TryParse(tagStr, out double threshold))
            {
                vm.BandwidthCutoffThreshold = threshold;
                ProcessesDataGrid.AutoFitColumns();
            }
        }

        private void TaskFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm && TaskFilterComboBox?.SelectedItem is ComboBoxItem item)
            {
                vm.SelectedTaskFilter = item.Content?.ToString() ?? "Todas as Tarefas";
                TasksDataGrid.AutoFitColumns();
            }
        }

        private void StartupFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm && StartupFilterComboBox?.SelectedItem is ComboBoxItem item)
            {
                vm.SelectedStartupFilter = item.Content?.ToString() ?? "Todos os Itens";
                StartupDataGrid.AutoFitColumns();
            }
        }

        private void OnlyAnomaliesCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.OnlyAnomalies = OnlyAnomaliesCheckBox.IsChecked ?? false;
                DataGrid.AutoFitColumns();
            }
        }

        private void ProcessOnlyAnomaliesCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.OnlyAnomalies = ProcessOnlyAnomaliesCheckBox.IsChecked ?? false;
                ProcessesDataGrid.AutoFitColumns();
            }
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = string.Empty;
            if (ProtocolComboBox != null) ProtocolComboBox.SelectedIndex = 0;
            if (StateComboBox != null) StateComboBox.SelectedIndex = 0;
            if (AllowFilterComboBox != null) AllowFilterComboBox.SelectedIndex = 0;
            if (CpuCutoffComboBox != null) CpuCutoffComboBox.SelectedIndex = 0;
            if (OnlyAnomaliesCheckBox != null) OnlyAnomaliesCheckBox.IsChecked = false;

            ViewModel.ClearFilters();
            DataGrid.AutoFitColumns();
        }

        private void ClearProcessFilters_Click(object sender, RoutedEventArgs e)
        {
            ProcessSearchTextBox.Text = string.Empty;
            if (ProcessCpuCutoffComboBox != null) ProcessCpuCutoffComboBox.SelectedIndex = 0;
            if (ProcessBandwidthCutoffComboBox != null) ProcessBandwidthCutoffComboBox.SelectedIndex = 0;
            if (ProcessOnlyAnomaliesCheckBox != null) ProcessOnlyAnomaliesCheckBox.IsChecked = false;

            ViewModel.ClearFilters();
            ProcessesDataGrid.AutoFitColumns();
        }

        // Live Reading Toggle (Conexões de Rede)
        private void ConnectionsLiveReadingToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ConnectionsLiveReadingToggleBtn.IsChecked == true)
            {
                _liveReadingTimer.Start();
                ViewModel.IsLiveReading = true;
                ConnectionsLiveReadingToggleBtn.Content = "⏸ Pausar";
                ViewModel.StatusMessage = "Leitura ao vivo de conexões ativada (a cada 2s).";
            }
            else
            {
                if (LiveReadingToggleBtn?.IsChecked != true)
                {
                    _liveReadingTimer.Stop();
                    ViewModel.IsLiveReading = false;
                }
                ConnectionsLiveReadingToggleBtn.Content = "▶ Ao Vivo (2s)";
                ViewModel.StatusMessage = "Leitura ao vivo pausada.";
            }
        }

        // Live Reading Toggle (Processos & Consumo)
        private void LiveReadingToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (LiveReadingToggleBtn.IsChecked == true)
            {
                _liveReadingTimer.Start();
                ViewModel.IsLiveReading = true;
                LiveReadingToggleBtn.Content = "⏸ Pausar";
                ViewModel.StatusMessage = "Leitura ao vivo de processos ativada (a cada 2s).";
            }
            else
            {
                if (ConnectionsLiveReadingToggleBtn?.IsChecked != true)
                {
                    _liveReadingTimer.Stop();
                    ViewModel.IsLiveReading = false;
                }
                LiveReadingToggleBtn.Content = "▶ Ao Vivo (2s)";
                ViewModel.StatusMessage = "Leitura ao vivo pausada.";
            }
        }

        // Dynamic Refresh Handlers
        private async void Reload_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.RefreshCurrentTabAsync();
            AutoFitActiveTabGrid();
        }

        private async void ReloadAll_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAllDataAsync();
            AutoFitActiveTabGrid();
        }

        private async void RefreshProcesses_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.RefreshProcessesAsync();
            ProcessesDataGrid.AutoFitColumns();
        }

        private async void RefreshTasks_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.RefreshTasksAsync();
            TasksDataGrid.AutoFitColumns();
        }

        private async void RefreshStartup_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.RefreshStartupItemsAsync();
            StartupDataGrid.AutoFitColumns();
        }

        private void AddToAllowlist_Click(object sender, RoutedEventArgs e)
        {
            var selected = DataGrid.SelectedItems.OfType<ConnectionItemViewModel>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Selecione uma ou mais conexões na lista para marcar como confiável.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ViewModel.AddSelectedToAllowlist(selected);
            DataGrid.AutoFitColumns();
        }

        private void RemoveFromAllowlist_Click(object sender, RoutedEventArgs e)
        {
            var selected = DataGrid.SelectedItems.OfType<ConnectionItemViewModel>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Selecione uma ou mais conexões para remover da lista de confiáveis.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ViewModel.RemoveSelectedFromAllowlist(selected);
            DataGrid.AutoFitColumns();
        }

        private void OpenAllowManager_Click(object sender, RoutedEventArgs e)
        {
            var managerWindow = new AllowlistManagerWindow(ViewModel.AllowlistUseCase)
            {
                Owner = this
            };

            managerWindow.ShowDialog();
            ViewModel.ReevaluateTrustedStatus();
            ViewModel.ApplyFilters();
            DataGrid.AutoFitColumns();
        }

        private void ExportConnections_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModel.FilteredEntries.Count == 0)
                {
                    MessageBox.Show("Não há conexões visíveis para exportar com os filtros atuais.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Title = "Exportar Conexões Vigentes",
                    Filter = "Arquivo JSON (*.json)|*.json|Arquivo CSV (*.csv)|*.csv|Arquivo de Texto (*.txt)|*.txt",
                    FileName = $"conexoes_netstat_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                    DefaultExt = ".json"
                };

                if (dialog.ShowDialog(this) == true)
                {
                    ViewModel.ExportCurrentConnections(dialog.FileName);
                    MessageBox.Show($"Conexões exportadas com sucesso!\nArquivo: {dialog.FileName}\nTotal de conexões: {ViewModel.FilteredEntries.Count}",
                        "Exportação Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao exportar conexões: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Windows Tools Launchers
        private void OpenResourceMonitor_Click(object sender, RoutedEventArgs e) => ViewModel.OpenResourceMonitorCommand.Execute(null);
        private void OpenTaskScheduler_Click(object sender, RoutedEventArgs e) => ViewModel.OpenTaskSchedulerCommand.Execute(null);
        private void OpenStartupSettings_Click(object sender, RoutedEventArgs e) => ViewModel.OpenStartupSettingsCommand.Execute(null);
        private void OpenTaskManager_Click(object sender, RoutedEventArgs e) => ViewModel.OpenTaskManagerCommand.Execute(null);
        private void OpenStartupFolder_Click(object sender, RoutedEventArgs e) => ViewModel.OpenStartupFolderCommand.Execute(null);
        private void OpenRegistryEditor_Click(object sender, RoutedEventArgs e) => ViewModel.OpenRegistryEditorCommand.Execute(null);

        // File Location Inspection (Shared Explorer Launcher)
        public static void OpenFileInExplorer(string? rawPath, string itemName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    MessageBox.Show($"Caminho não disponível para '{itemName}'.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string cleanPath = FileSecurityInspector.ExtractExecutablePath(rawPath);
                if (File.Exists(cleanPath))
                {
                    Process.Start("explorer.exe", $"/select,\"{cleanPath}\"");
                    return;
                }

                if (Directory.Exists(cleanPath))
                {
                    Process.Start("explorer.exe", $"\"{cleanPath}\"");
                    return;
                }

                MessageBox.Show($"O arquivo não foi encontrado no disco:\n{cleanPath}\nPode ser um executável órfão ou requerer elevação de privilégios.", "Arquivo Inexistente", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir o local do arquivo: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Tab 0: Conexões Row Actions
        private void RowOpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ConnectionItemViewModel entry)
            {
                OpenFileInExplorer(entry.ProcessPath, entry.ProcessName);
            }
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid.SelectedItem is ConnectionItemViewModel entry)
            {
                OpenFileInExplorer(entry.ProcessPath, entry.ProcessName);
            }
            else
            {
                MessageBox.Show("Selecione um processo na tabela para abrir o local do arquivo.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RowCopyDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ConnectionItemViewModel entry)
            {
                string details = $"Processo: {entry.ProcessName} (PID: {entry.PID})\nCPU: {entry.DisplayCpu}\nBanda: {entry.DisplayBandwidth}\nProtocolo: {entry.Protocol}\nLocal: {entry.LocalAddress}\nRemoto: {entry.ForeignAddress}\nEstado: {entry.State}\nConfiável: {(entry.IsAllowed ? "Sim" : "Não")}\nAlerta: {entry.ThreatReason}\nCaminho: {entry.ProcessPath ?? "N/A"}";
                Clipboard.SetText(details);
                ViewModel.StatusMessage = $"Detalhes de '{entry.ProcessName}' (PID {entry.PID}) copiados!";
            }
        }

        private void CopyDetails_Click(object sender, RoutedEventArgs e)
        {
            var selected = DataGrid.SelectedItems.OfType<ConnectionItemViewModel>().ToList();
            if (selected.Count == 0) return;

            if (selected.Count == 1)
            {
                var entry = selected[0];
                string details = $"Processo: {entry.ProcessName} (PID: {entry.PID})\nCPU: {entry.DisplayCpu}\nBanda: {entry.DisplayBandwidth}\nProtocolo: {entry.Protocol}\nLocal: {entry.LocalAddress}\nRemoto: {entry.ForeignAddress}\nEstado: {entry.State}\nConfiável: {(entry.IsAllowed ? "Sim" : "Não")}\nAlerta: {entry.ThreatReason}\nCaminho: {entry.ProcessPath ?? "N/A"}";
                Clipboard.SetText(details);
            }
            else
            {
                var lines = selected.Select(entry =>
                    $"{entry.ProcessName}\tPID: {entry.PID}\tCPU: {entry.DisplayCpu}\tBanda: {entry.DisplayBandwidth}\t{entry.Protocol}\t{entry.LocalAddress}\t{entry.ForeignAddress}\t{entry.State}\tConfiável: {(entry.IsAllowed ? "Sim" : "Não")}");
                Clipboard.SetText(string.Join(Environment.NewLine, lines));
            }

            ViewModel.StatusMessage = $"{selected.Count} detalhe(s) copiado(s) para a área de transferência.";
        }

        private void CopyForeignIP_Click(object sender, RoutedEventArgs e)
        {
            var selected = DataGrid.SelectedItems.OfType<ConnectionItemViewModel>().ToList();
            if (selected.Count == 0) return;

            var ips = selected
                .Select(entry => TrustEvaluationPolicy.ExtractIP(entry.ForeignAddress))
                .Where(ip => !string.IsNullOrWhiteSpace(ip))
                .Distinct();

            string result = string.Join(Environment.NewLine, ips);
            if (!string.IsNullOrEmpty(result))
            {
                Clipboard.SetText(result);
                ViewModel.StatusMessage = $"{selected.Count} IP(s) remoto(s) copiado(s).";
            }
        }

        private void CopyLocalIP_Click(object sender, RoutedEventArgs e)
        {
            var selected = DataGrid.SelectedItems.OfType<ConnectionItemViewModel>().ToList();
            if (selected.Count == 0) return;

            var ips = selected
                .Select(entry => TrustEvaluationPolicy.ExtractIP(entry.LocalAddress))
                .Where(ip => !string.IsNullOrWhiteSpace(ip))
                .Distinct();

            string result = string.Join(Environment.NewLine, ips);
            if (!string.IsNullOrEmpty(result))
            {
                Clipboard.SetText(result);
                ViewModel.StatusMessage = $"{selected.Count} IP(s) local(is) copiado(s).";
            }
        }

        private void CopyPID_Click(object sender, RoutedEventArgs e)
        {
            var selected = DataGrid.SelectedItems.OfType<ConnectionItemViewModel>().ToList();
            if (selected.Count == 0) return;

            var pids = selected.Select(entry => entry.PID.ToString()).Distinct();
            Clipboard.SetText(string.Join(Environment.NewLine, pids));
            ViewModel.StatusMessage = $"{selected.Count} PID(s) copiado(s).";
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                DependencyObject? current = dep;
                while (current != null && current != DataGrid)
                {
                    if (current is DataGridColumnHeader || current is ScrollBar || current is Button) return;
                    if (current is DataGridRow row && row.Item is ConnectionItemViewModel clickedEntry)
                    {
                        OpenFileInExplorer(clickedEntry.ProcessPath, clickedEntry.ProcessName);
                        e.Handled = true;
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }
        }

        // Tab 1: Processos Row Actions
        private void RowOpenProcessFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ProcessAggregateItemViewModel item)
            {
                OpenFileInExplorer(item.ProcessPath, item.ProcessName);
            }
        }

        private void ProcessOpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessesDataGrid.SelectedItem is ProcessAggregateItemViewModel item)
            {
                OpenFileInExplorer(item.ProcessPath, item.ProcessName);
            }
        }

        private void RowCopyProcessDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ProcessAggregateItemViewModel item)
            {
                string text = $"Processo: {item.ProcessName} (PID: {item.PID})\nCPU: {item.FormattedCpu}\nDownload: {item.FormattedReadBandwidth}\nUpload: {item.FormattedWriteBandwidth}\nBanda Total: {item.FormattedTotalBandwidth}\nConexões: {item.ActiveConnectionsCount}\nDiagnóstico: {item.ThreatBadgeText} - {item.ThreatReason}\nCaminho: {item.ProcessPath ?? "N/A"}";
                Clipboard.SetText(text);
                ViewModel.StatusMessage = $"Detalhes de '{item.ProcessName}' copiados!";
            }
        }

        private void CopyProcessDetails_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessesDataGrid.SelectedItem is ProcessAggregateItemViewModel item)
            {
                string text = $"Processo: {item.ProcessName} (PID: {item.PID})\nCPU: {item.FormattedCpu}\nDownload: {item.FormattedReadBandwidth}\nUpload: {item.FormattedWriteBandwidth}\nBanda Total: {item.FormattedTotalBandwidth}\nConexões: {item.ActiveConnectionsCount}\nDiagnóstico: {item.ThreatBadgeText} - {item.ThreatReason}\nCaminho: {item.ProcessPath ?? "N/A"}";
                Clipboard.SetText(text);
                ViewModel.StatusMessage = $"Detalhes de '{item.ProcessName}' copiados!";
            }
        }

        private void CopyProcessPID_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessesDataGrid.SelectedItem is ProcessAggregateItemViewModel item)
            {
                Clipboard.SetText(item.PID.ToString());
                ViewModel.StatusMessage = $"PID {item.PID} copiado!";
            }
        }

        private void ProcessesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                DependencyObject? current = dep;
                while (current != null && current != ProcessesDataGrid)
                {
                    if (current is DataGridColumnHeader || current is ScrollBar || current is Button) return;
                    if (current is DataGridRow row && row.Item is ProcessAggregateItemViewModel item)
                    {
                        OpenFileInExplorer(item.ProcessPath, item.ProcessName);
                        e.Handled = true;
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }
        }

        // Tab 2: Tarefas Agendadas Row Actions
        private void RowOpenTaskFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ScheduledTaskItemViewModel item)
            {
                OpenFileInExplorer(item.TaskToRun, item.TaskName);
            }
        }

        private void TaskOpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (TasksDataGrid.SelectedItem is ScheduledTaskItemViewModel item)
            {
                OpenFileInExplorer(item.TaskToRun, item.TaskName);
            }
        }

        private void CopyTaskDetails_Click(object sender, RoutedEventArgs e)
        {
            if (TasksDataGrid.SelectedItem is ScheduledTaskItemViewModel item)
            {
                string text = $"Tarefa: {item.TaskName}\nPasta: {item.TaskPath}\nStatus: {item.State}\nAção: {item.TaskToRun}\nAutor: {item.Author}\nPróxima: {item.NextRunTime}\nÚltima: {item.LastRunTime}\nAuditoria: {item.ThreatBadgeText} - {item.ThreatReason}";
                Clipboard.SetText(text);
                ViewModel.StatusMessage = $"Detalhes da tarefa '{item.TaskName}' copiados!";
            }
        }

        private void CopyTaskCommand_Click(object sender, RoutedEventArgs e)
        {
            if (TasksDataGrid.SelectedItem is ScheduledTaskItemViewModel item)
            {
                Clipboard.SetText(item.TaskToRun);
                ViewModel.StatusMessage = "Comando da tarefa copiado!";
            }
        }

        private void TasksDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                DependencyObject? current = dep;
                while (current != null && current != TasksDataGrid)
                {
                    if (current is DataGridColumnHeader || current is ScrollBar || current is Button) return;
                    if (current is DataGridRow row && row.Item is ScheduledTaskItemViewModel item)
                    {
                        OpenFileInExplorer(item.TaskToRun, item.TaskName);
                        e.Handled = true;
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }
        }

        // Tab 3: Itens de Inicialização Row Actions
        private void RowOpenStartupFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is StartupItemViewModel item)
            {
                OpenFileInExplorer(item.Command, item.Name);
            }
        }

        private void StartupOpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (StartupDataGrid.SelectedItem is StartupItemViewModel item)
            {
                OpenFileInExplorer(item.Command, item.Name);
            }
        }

        private void CopyStartupDetails_Click(object sender, RoutedEventArgs e)
        {
            if (StartupDataGrid.SelectedItem is StartupItemViewModel item)
            {
                string text = $"Item: {item.Name}\nOrigem: {item.SourceType}\nLocalização: {item.Location}\nComando: {item.Command}\nAuditoria: {item.ThreatBadgeText} - {item.ThreatReason}";
                Clipboard.SetText(text);
                ViewModel.StatusMessage = $"Detalhes do item '{item.Name}' copiados!";
            }
        }

        private void CopyStartupCommand_Click(object sender, RoutedEventArgs e)
        {
            if (StartupDataGrid.SelectedItem is StartupItemViewModel item)
            {
                Clipboard.SetText(item.Command);
                ViewModel.StatusMessage = "Comando copiado!";
            }
        }

        private void StartupDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                DependencyObject? current = dep;
                while (current != null && current != StartupDataGrid)
                {
                    if (current is DataGridColumnHeader || current is ScrollBar || current is Button) return;
                    if (current is DataGridRow row && row.Item is StartupItemViewModel item)
                    {
                        OpenFileInExplorer(item.Command, item.Name);
                        e.Handled = true;
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int selectedCount = DataGrid.SelectedItems.Count;

            if (selectedCount == 1 && DataGrid.SelectedItem is ConnectionItemViewModel entry)
            {
                SelectedSeparator.Visibility = Visibility.Visible;
                SelectedProcessText.Visibility = Visibility.Visible;
                string allowStatus = entry.IsAllowed ? " [Confiável]" : " [Monitorado]";
                SelectedProcessText.Text = $"Selecionado: {entry.ProcessName} (PID: {entry.PID}){allowStatus} • CPU: {entry.DisplayCpu} • Banda: {entry.DisplayBandwidth}";
            }
            else if (selectedCount > 1)
            {
                SelectedSeparator.Visibility = Visibility.Visible;
                SelectedProcessText.Visibility = Visibility.Visible;
                int allowCount = DataGrid.SelectedItems.OfType<ConnectionItemViewModel>().Count(i => i.IsAllowed);
                SelectedProcessText.Text = $"Selecionados: {selectedCount} itens ({allowCount} Confiáveis)";
            }
            else
            {
                SelectedSeparator.Visibility = Visibility.Collapsed;
                SelectedProcessText.Visibility = Visibility.Collapsed;
                SelectedProcessText.Text = string.Empty;
            }
        }
    }
}
