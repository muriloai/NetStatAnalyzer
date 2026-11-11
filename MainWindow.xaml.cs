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
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Policies;
using NetStatAnalyzer.Presentation.Extensions;
using NetStatAnalyzer.Presentation.ViewModels;

namespace NetStatAnalyzer
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }
        private readonly DispatcherTimer _liveReadingTimer;
        private readonly IExecutablePathResolver _executablePathResolver;
        private readonly Func<AllowlistManagerViewModel> _allowlistManagerViewModelFactory;

        public MainWindow(
            MainViewModel viewModel,
            IExecutablePathResolver executablePathResolver,
            Func<AllowlistManagerViewModel> allowlistManagerViewModelFactory)
        {
            InitializeComponent();
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _executablePathResolver = executablePathResolver ?? throw new ArgumentNullException(nameof(executablePathResolver));
            _allowlistManagerViewModelFactory = allowlistManagerViewModelFactory ?? throw new ArgumentNullException(nameof(allowlistManagerViewModelFactory));

            DataContext = ViewModel;

            _liveReadingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _liveReadingTimer.Tick += async (s, e) =>
            {
                if (ViewModel.IsLoading || ContinuousReadingToggleBtn.IsChecked != true) return;
                if (ViewModel.SelectedTabIndex == 0)
                {
                    await ViewModel.RefreshConnectionsAsync(isSilent: true);
                    DataGrid.AutoFitColumns();
                }
                else if (ViewModel.SelectedTabIndex == 1)
                {
                    await ViewModel.RefreshProcessesAsync(isSilent: true);
                    ProcessesDataGrid.AutoFitColumns();
                }
            };

            VersionTextBlock.Text = ViewModel.AppVersion;
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => OnViewModelPropertyChanged(e.PropertyName)));
                    return;
                }
                OnViewModelPropertyChanged(e.PropertyName);
            };

            DataGrid.ItemsSource = ViewModel.FilteredEntries;
            ProcessesDataGrid.ItemsSource = ViewModel.FilteredProcesses;
            TasksDataGrid.ItemsSource = ViewModel.FilteredTasks;
            StartupDataGrid.ItemsSource = ViewModel.FilteredStartupItems;

            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(ViewModel.FilteredEntries, ViewModel.EntriesLock);
            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(ViewModel.FilteredProcesses, ViewModel.ProcessesLock);
            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(ViewModel.FilteredTasks, ViewModel.TasksLock);
            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(ViewModel.FilteredStartupItems, ViewModel.StartupLock);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateLoadingVisualState();
            await ViewModel.LoadAllDataAsync();
            AutoFitActiveTabGrid();
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

            ContinuousReadingToggleBtn.Visibility = index < 2 ? Visibility.Visible : Visibility.Collapsed;
            if (index >= 2 && ContinuousReadingToggleBtn.IsChecked == true)
            {
                ContinuousReadingToggleBtn.IsChecked = false;
                _liveReadingTimer.Stop();
                ViewModel.IsLiveReading = false;
                CurrentTabReloadButton.IsEnabled = !ViewModel.IsLoading;
                ViewModel.StatusMessage = "Leitura contínua pausada; disponível nas abas Conexões e Processos.";
            }

            ConnectionCountText.Text = ViewModel.CurrentTabSummary;

            // Clear row selection details on tab switch
            SelectedSeparator.Visibility = Visibility.Collapsed;
            SelectedProcessText.Visibility = Visibility.Collapsed;
            SelectedProcessText.Text = string.Empty;

            AutoFitActiveTabGrid();
        }

        
        private void OnViewModelPropertyChanged(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return;

            // Tab 0: Conexões metrics
            if (propertyName == nameof(MainViewModel.MetricTotal))
                MetricTotalText.Text = ViewModel.MetricTotal.ToString();
            else if (propertyName == nameof(MainViewModel.MetricEstablished))
                MetricEstablishedText.Text = ViewModel.MetricEstablished.ToString();
            else if (propertyName == nameof(MainViewModel.MetricListening))
                MetricListeningText.Text = ViewModel.MetricListening.ToString();
            else if (propertyName == nameof(MainViewModel.MetricAllowed))
                MetricAllowedText.Text = ViewModel.MetricAllowed.ToString();
            else if (propertyName == nameof(MainViewModel.MetricThreatConnections) ||
                     propertyName == nameof(MainViewModel.TotalThreatsCount))
            {
                MetricThreatText.Text = ViewModel.MetricThreatConnections.ToString();
            }

            // Tab 1: Processos metrics
            else if (propertyName == nameof(MainViewModel.MetricProcessesTotal))
                MetricProcessesTotalText.Text = ViewModel.MetricProcessesTotal.ToString();
            else if (propertyName == nameof(MainViewModel.MetricProcessesHighCpu))
                MetricProcessesHighCpuText.Text = ViewModel.MetricProcessesHighCpu.ToString();
            else if (propertyName == nameof(MainViewModel.MetricProcessesHighNetwork))
                MetricProcessesHighNetworkText.Text = ViewModel.MetricProcessesHighNetwork.ToString();
            else if (propertyName == nameof(MainViewModel.MetricProcessesBandwidthSum))
                MetricProcessesBandwidthSumText.Text = ViewModel.MetricProcessesBandwidthSum;
            else if (propertyName == nameof(MainViewModel.MetricProcessesThreats))
                MetricProcessesThreatsText.Text = ViewModel.MetricProcessesThreats.ToString();

            // Tab 2: Tarefas Agendadas metrics
            else if (propertyName == nameof(MainViewModel.MetricTasksTotal))
                MetricTasksTotalText.Text = ViewModel.MetricTasksTotal.ToString();
            else if (propertyName == nameof(MainViewModel.MetricTasksEnabled))
                MetricTasksEnabledText.Text = ViewModel.MetricTasksEnabled.ToString();
            else if (propertyName == nameof(MainViewModel.MetricTasksDisabled))
                MetricTasksDisabledText.Text = ViewModel.MetricTasksDisabled.ToString();
            else if (propertyName == nameof(MainViewModel.MetricTasksNonMicrosoft))
                MetricTasksNonMicrosoftText.Text = ViewModel.MetricTasksNonMicrosoft.ToString();
            else if (propertyName == nameof(MainViewModel.MetricTasksSuspicious))
                MetricTasksSuspiciousText.Text = ViewModel.MetricTasksSuspicious.ToString();

            // Tab 3: Itens de Inicialização metrics
            else if (propertyName == nameof(MainViewModel.MetricStartupTotal))
                MetricStartupTotalText.Text = ViewModel.MetricStartupTotal.ToString();
            else if (propertyName == nameof(MainViewModel.MetricStartupRegistry))
                MetricStartupRegistryText.Text = ViewModel.MetricStartupRegistry.ToString();
            else if (propertyName == nameof(MainViewModel.MetricStartupFolder))
                MetricStartupFolderText.Text = ViewModel.MetricStartupFolder.ToString();
            else if (propertyName == nameof(MainViewModel.MetricStartupSuspicious))
                MetricStartupSuspiciousText.Text = ViewModel.MetricStartupSuspicious.ToString();

            // Status & Contextual Summaries
            else if (propertyName == nameof(MainViewModel.CurrentTabSummary) ||
                     propertyName == nameof(MainViewModel.ConnectionCountSummary) ||
                     propertyName == nameof(MainViewModel.ProcessCountSummary) ||
                     propertyName == nameof(MainViewModel.TaskCountSummary) ||
                     propertyName == nameof(MainViewModel.StartupCountSummary))
            {
                ConnectionCountText.Text = ViewModel.CurrentTabSummary;
            }
            else if (propertyName == nameof(MainViewModel.StatusMessage))
                StatusTextBlock.Text = ViewModel.StatusMessage;
            else if (propertyName == nameof(MainViewModel.IsLoading) || propertyName == nameof(MainViewModel.IsLiveReading))
            {
                UpdateLoadingVisualState();

            }
        }

        private void UpdateLoadingVisualState()
        {
            bool isProcessing = ViewModel.IsLoading;
            bool isLive = ViewModel.IsLiveReading;
            bool showSpinner = isProcessing || isLive;

            StatusSpinner.Visibility = showSpinner ? Visibility.Visible : Visibility.Collapsed;
            LoadingProgressBar.Visibility = isProcessing ? Visibility.Visible : Visibility.Collapsed;

            if (isProcessing)
            {
                CurrentTabReloadButton.Style = (Style)FindResource("ModernReloadProcessingButton");
                CurrentTabReloadButton.IsEnabled = false;
            }
            else
            {
                CurrentTabReloadButton.Style = (Style)FindResource("ModernPrimaryButton");
                CurrentTabReloadButton.IsEnabled = !isLive;
            }
        }

        private void AutoFitActiveTabGrid()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(AutoFitActiveTabGrid));
                return;
            }

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
                btn.ContextMenu.Placement = PlacementMode.Left;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void TabActionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = PlacementMode.Left;
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
                vm.SelectedTaskFilter = item.Content?.ToString() ?? "Todas as tarefas";
                TasksDataGrid.AutoFitColumns();
            }
        }

        private void StartupFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm && StartupFilterComboBox?.SelectedItem is ComboBoxItem item)
            {
                vm.SelectedStartupFilter = item.Content?.ToString() ?? "Todos os itens";
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

        // Tab 0 Clear
        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = string.Empty;
            if (ProtocolComboBox != null) ProtocolComboBox.SelectedIndex = 0;
            if (StateComboBox != null) StateComboBox.SelectedIndex = 0;
            if (AllowFilterComboBox != null) AllowFilterComboBox.SelectedIndex = 0;
            if (CpuCutoffComboBox != null) CpuCutoffComboBox.SelectedIndex = 0;
            if (OnlyAnomaliesCheckBox != null) OnlyAnomaliesCheckBox.IsChecked = false;

            ViewModel.ClearConnectionFilters();
            DataGrid.AutoFitColumns();
        }

        // Tab 1 Clear
        private void ClearProcessFilters_Click(object sender, RoutedEventArgs e)
        {
            ProcessSearchTextBox.Text = string.Empty;
            if (ProcessCpuCutoffComboBox != null) ProcessCpuCutoffComboBox.SelectedIndex = 0;
            if (ProcessBandwidthCutoffComboBox != null) ProcessBandwidthCutoffComboBox.SelectedIndex = 0;
            if (ProcessOnlyAnomaliesCheckBox != null) ProcessOnlyAnomaliesCheckBox.IsChecked = false;

            ViewModel.ClearProcessFilters();
            ProcessesDataGrid.AutoFitColumns();
        }

        // Tab 2 Clear
        private void ClearTaskFilters_Click(object sender, RoutedEventArgs e)
        {
            TaskSearchTextBox.Text = string.Empty;
            if (TaskFilterComboBox != null) TaskFilterComboBox.SelectedIndex = 0;

            ViewModel.ClearTaskFilters();
            TasksDataGrid.AutoFitColumns();
        }

        // Tab 3 Clear
        private void ClearStartupFilters_Click(object sender, RoutedEventArgs e)
        {
            StartupSearchTextBox.Text = string.Empty;
            if (StartupFilterComboBox != null) StartupFilterComboBox.SelectedIndex = 0;

            ViewModel.ClearStartupFilters();
            StartupDataGrid.AutoFitColumns();
        }

        // Interactive Metric Card Filters (Tab 0)
        private void MetricCardTotal_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ClearFilters_Click(sender, e);
        }

        private void MetricCardEstablished_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (StateComboBox != null)
            {
                for (int i = 0; i < StateComboBox.Items.Count; i++)
                {
                    if (string.Equals(StateComboBox.Items[i]?.ToString(), "ESTABLISHED", StringComparison.OrdinalIgnoreCase))
                    {
                        StateComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void MetricCardListening_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (StateComboBox != null)
            {
                for (int i = 0; i < StateComboBox.Items.Count; i++)
                {
                    if (string.Equals(StateComboBox.Items[i]?.ToString(), "LISTENING", StringComparison.OrdinalIgnoreCase))
                    {
                        StateComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void MetricCardAllowed_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (AllowFilterComboBox != null)
            {
                AllowFilterComboBox.SelectedIndex = 1;
            }
        }

        private void MetricCardThreat_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (OnlyAnomaliesCheckBox != null)
            {
                OnlyAnomaliesCheckBox.IsChecked = !(OnlyAnomaliesCheckBox.IsChecked ?? false);
                OnlyAnomaliesCheckBox_Click(sender, e);
            }
        }

        // Continuous reading follows the selected table on the two supported tabs.
        private void ContinuousReadingToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ContinuousReadingToggleBtn.IsChecked == true)
            {
                _liveReadingTimer.Start();
                ViewModel.IsLiveReading = true;
                CurrentTabReloadButton.IsEnabled = false;
                ContinuousReadingToggleBtn.Content = "Pausar leitura";
                ViewModel.StatusMessage = "Leitura contínua da tabela atual ativada, com amostras a cada 2 segundos.";
            }
            else
            {
                _liveReadingTimer.Stop();
                ViewModel.IsLiveReading = false;
                CurrentTabReloadButton.IsEnabled = !ViewModel.IsLoading;
                ContinuousReadingToggleBtn.Content = "Leitura contínua (2 s)";
                ViewModel.StatusMessage = "Leitura contínua pausada.";
            }
        }

        // Dynamic Refresh Handlers
        private async void Reload_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.RefreshCurrentTabAsync();
            AutoFitActiveTabGrid();
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
            var managerWindow = new AllowlistManagerWindow(_allowlistManagerViewModelFactory())
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
        private void OpenFileInExplorer(string? rawPath, string itemName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    MessageBox.Show($"Caminho não disponível para '{itemName}'.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string cleanPath = _executablePathResolver.Resolve(rawPath);
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
                string details = $"Processo: {entry.ProcessName} (PID: {entry.PID})\nCPU: {entry.DisplayCpu}\nBanda: {entry.DisplayBandwidth}\nProtocolo: {entry.Protocol}\nLocal: {entry.LocalAddress}\nRemoto: {entry.ForeignAddress}\nEstado: {entry.State}\nNa lista de confiança: {(entry.IsAllowed ? "Sim" : "Não")}\nAlerta: {entry.ThreatReason}\nCaminho: {entry.ProcessPath ?? "N/A"}";
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
                string details = $"Processo: {entry.ProcessName} (PID: {entry.PID})\nCPU: {entry.DisplayCpu}\nBanda: {entry.DisplayBandwidth}\nProtocolo: {entry.Protocol}\nLocal: {entry.LocalAddress}\nRemoto: {entry.ForeignAddress}\nEstado: {entry.State}\nNa lista de confiança: {(entry.IsAllowed ? "Sim" : "Não")}\nAlerta: {entry.ThreatReason}\nCaminho: {entry.ProcessPath ?? "N/A"}";
                Clipboard.SetText(details);
            }
            else
            {
                var lines = selected.Select(entry =>
                    $"{entry.ProcessName}\tPID: {entry.PID}\tCPU: {entry.DisplayCpu}\tBanda: {entry.DisplayBandwidth}\t{entry.Protocol}\t{entry.LocalAddress}\t{entry.ForeignAddress}\t{entry.State}\tNa lista de confiança: {(entry.IsAllowed ? "Sim" : "Não")}");
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
                string allowStatus = entry.IsAllowed ? " [Confiável]" : " [Não verificada]";
                SelectedProcessText.Text = $"Selecionado: {entry.ProcessName} (PID: {entry.PID}){allowStatus} • CPU: {entry.DisplayCpu} • Banda: {entry.DisplayBandwidth}";
            }
            else if (selectedCount > 1)
            {
                SelectedSeparator.Visibility = Visibility.Visible;
                SelectedProcessText.Visibility = Visibility.Visible;
                int allowCount = DataGrid.SelectedItems.OfType<ConnectionItemViewModel>().Count(i => i.IsAllowed);
                SelectedProcessText.Text = $"Selecionados: {selectedCount} itens ({allowCount} confiáveis)";
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
