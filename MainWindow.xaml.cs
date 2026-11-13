using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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
using NetStatAnalyzer.Presentation.Windows;
using NetStatAnalyzer.Presentation.Common;

namespace NetStatAnalyzer
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }
        private readonly DispatcherTimer _liveReadingTimer;
        private bool _isContinuousSampleRunning;
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
            ApplyTheme(ViewModel.Theme);

            _liveReadingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _liveReadingTimer.Tick += async (s, e) =>
            {
                if (ViewModel.IsLoading || !ViewModel.IsLiveReading || _isContinuousSampleRunning ||
                    ContinuousReadingToggleBtn.IsChecked != true || ViewModel.SelectedTabIndex is not (0 or 1))
                    return;

                _isContinuousSampleRunning = true;
                UpdateLoadingVisualState();
                try
                {
                    if (ViewModel.SelectedTabIndex == 0)
                    {
                        await ViewModel.RefreshConnectionsAsync(isSilent: true);
                        DataGrid.AutoFitColumns();
                    }
                    else
                    {
                        await ViewModel.RefreshProcessesAsync(isSilent: true);
                        ProcessesDataGrid.AutoFitColumns();
                    }
                }
                catch (Exception ex)
                {
                    ViewModel.StatusMessage = $"Falha na leitura contínua: {ex.Message}";
                }
                finally
                {
                    _isContinuousSampleRunning = false;
                    UpdateLoadingVisualState();
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
                ViewModel.StatusMessage = "Leitura contínua pausada; disponível nas abas Conexões e Processos.";
            }

            ConnectionCountText.Text = ViewModel.CurrentTabSummary;
            UpdateSelectionSummary();

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
            bool isSampleRunning = _isContinuousSampleRunning;
            bool showSpinner = isProcessing || isLive || isSampleRunning;

            StatusSpinner.Visibility = showSpinner ? Visibility.Visible : Visibility.Collapsed;
            StatusIdleDot.Visibility = showSpinner ? Visibility.Collapsed : Visibility.Visible;
            RefreshLoadingProgressBar.Visibility = isProcessing ? Visibility.Visible : Visibility.Collapsed;
            ContinuousLoadingProgressBar.Visibility = isLive || isSampleRunning
                ? Visibility.Visible
                : Visibility.Collapsed;
            RefreshDataButton.IsEnabled = !isProcessing && !isLive && !isSampleRunning;
            ContinuousReadingToggleBtn.IsEnabled = !isProcessing;
            ContinuousReadingToggleBtn.Content = isLive ? "Pausar leitura" : "Leitura contínua";
            ContinuousReadingToggleBtn.ToolTip = isLive
                ? "Leitura contínua ativa. Clique para pausar."
                : "Ativar a leitura contínua nas abas Conexões e Processos";

            if (isProcessing)
            {
                RefreshDataButton.Style = (Style)FindResource("ModernReloadProcessingButton");
            }
            else
            {
                RefreshDataButton.Style = (Style)FindResource("ModernPrimaryButton");
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

        private void DataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGrid grid || e.OriginalSource is not DependencyObject source) return;
            if (ItemsControl.ContainerFromElement(grid, source) is not DataGridRow row) return;

            if (!row.IsSelected)
            {
                grid.SelectedItems.Clear();
                row.IsSelected = true;
            }
            row.Focus();
        }

        private static readonly IReadOnlyDictionary<string, string> LightPalette = new Dictionary<string, string>
        {
            ["BgPrimary"] = "#D8E0E8", ["BgSecondary"] = "#E1E8EE", ["BgTertiary"] = "#C9D4DE",
            ["BgCard"] = "#E7EDF2", ["SurfaceInset"] = "#D2DCE4", ["SurfaceRaised"] = "#E2E9EF",
            ["BorderBrush"] = "#91A1AF", ["BorderLightBrush"] = "#718392",
            ["TextPrimary"] = "#162633", ["TextSecondary"] = "#2E4556", ["TextMuted"] = "#455D6E",
            ["AccentColor"] = "#145C83", ["AccentHover"] = "#104B6A", ["AccentHoverText"] = "#FFFFFF", ["AccentPressed"] = "#0C3D57",
            ["PrimaryButtonColor"] = "#145C83", ["PrimaryButtonHover"] = "#104B6A",
            ["PrimaryButtonPressed"] = "#0C3D57", ["PrimaryButtonTextColor"] = "#FFFFFF",
            ["AccentBorder"] = "#145C83", ["AccentSurface"] = "#C7DCE8",
            ["SuccessColor"] = "#155B40", ["SuccessSurface"] = "#CBE3D7", ["SuccessBorder"] = "#32805E",
            ["SuccessHoverBackground"] = "#155B40", ["SuccessHoverText"] = "#FFFFFF",
            ["WarningColor"] = "#C2410C", ["WarningSurface"] = "#FFEDD5", ["WarningBorder"] = "#F97316",
            ["DangerColor"] = "#8E202C", ["DangerSurface"] = "#EED2D5", ["DangerBorder"] = "#A83D47",
            ["DangerHoverBackground"] = "#8E202C", ["DangerHoverText"] = "#FFFFFF",
            ["DataGridSurface"] = "#F0F3F6", ["DataGridAlternateSurface"] = "#E2E9EE",
            ["DataGridHeaderSurface"] = "#C9D5DE", ["DataGridHoverSurface"] = "#D5E0E7",
            ["DataGridSelectionSurface"] = "#A9CDDF", ["DataGridSelectionText"] = "#162633",
            ["DataGridSelectionBorder"] = "#286580", ["DataGridGridline"] = "#B4C2CC"
        };

        private static readonly IReadOnlyDictionary<string, string> DarkPalette = new Dictionary<string, string>
        {
            ["BgPrimary"] = "#1B1B1B", ["BgSecondary"] = "#242424", ["BgTertiary"] = "#323232",
            ["BgCard"] = "#292929", ["SurfaceInset"] = "#1E1E1E", ["SurfaceRaised"] = "#282828",
            ["BorderBrush"] = "#464646", ["BorderLightBrush"] = "#5A5A5A",
            ["TextPrimary"] = "#F1F1F1", ["TextSecondary"] = "#C5C5C5", ["TextMuted"] = "#999999",
            ["AccentColor"] = "#9A9A9A", ["AccentHover"] = "#B2B2B2", ["AccentHoverText"] = "#1B1B1B", ["AccentPressed"] = "#787878",
            ["PrimaryButtonColor"] = "#565656", ["PrimaryButtonHover"] = "#6B6B6B",
            ["PrimaryButtonPressed"] = "#414141", ["PrimaryButtonTextColor"] = "#FFFFFF",
            ["AccentBorder"] = "#D0D0D0", ["AccentSurface"] = "#333333",
            ["SuccessColor"] = "#83C9A6", ["SuccessSurface"] = "#1F4036", ["SuccessBorder"] = "#4C9E78",
            ["SuccessHoverBackground"] = "#25523F", ["SuccessHoverText"] = "#F1F1F1",
            ["WarningColor"] = "#FB923C", ["WarningSurface"] = "#2A180E", ["WarningBorder"] = "#EA580C",
            ["DangerColor"] = "#D98A8A", ["DangerSurface"] = "#432727", ["DangerBorder"] = "#AB6262",
            ["DangerHoverBackground"] = "#5A2E2E", ["DangerHoverText"] = "#F1F1F1",
            ["DataGridSurface"] = "#202020", ["DataGridAlternateSurface"] = "#292929",
            ["DataGridHeaderSurface"] = "#303030", ["DataGridHoverSurface"] = "#363636",
            ["DataGridSelectionSurface"] = "#484848", ["DataGridSelectionText"] = "#F1F1F1",
            ["DataGridSelectionBorder"] = "#BEBEBE", ["DataGridGridline"] = "#3A3A3A"
        };

        private void ApplyTheme(string theme)
        {
            var palette = theme == "Light" ? LightPalette : DarkPalette;
            foreach (var (key, hex) in palette)
            {
                System.Windows.Application.Current.Resources[key] =
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            }
            System.Windows.Application.Current.Resources[SystemColors.HighlightBrushKey] =
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette["DataGridSelectionSurface"]));
            System.Windows.Application.Current.Resources[SystemColors.HighlightTextBrushKey] =
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette["DataGridSelectionText"]));
            System.Windows.Application.Current.Resources[SystemColors.InactiveSelectionHighlightBrushKey] =
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette["DataGridAlternateSurface"]));
            System.Windows.Application.Current.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] =
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette["DataGridSelectionText"]));
        }

        private void LightTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme("Light");
            ViewModel.SaveTheme("Light");
        }

        private void DarkTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme("Dark");
            ViewModel.SaveTheme("Dark");
        }

        private async void RefreshAllData_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsLoading || ViewModel.IsLiveReading || _isContinuousSampleRunning) return;
            await ViewModel.LoadAllDataAsync();
            AutoFitActiveTabGrid();
        }

        private void ClearGlobalFilters_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearGlobalFilters();
            AutoFitActiveTabGrid();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.ConnectionSearchQuery = SearchTextBox.Text;
                DataGrid.AutoFitColumns();
            }
        }

        private void ProcessSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.ProcessSearchQuery = ProcessSearchTextBox.Text;
                ProcessesDataGrid.AutoFitColumns();
            }
        }

        private void TaskSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.TaskSearchQuery = TaskSearchTextBox.Text;
                TasksDataGrid.AutoFitColumns();
            }
        }

        private void StartupSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.StartupSearchQuery = StartupSearchTextBox.Text;
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
                vm.ProcessCpuCutoffThreshold = threshold;
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

        // Tab 0 Clear
        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = string.Empty;
            if (ProtocolComboBox != null) ProtocolComboBox.SelectedIndex = 0;
            if (StateComboBox != null) StateComboBox.SelectedIndex = 0;
            if (CpuCutoffComboBox != null) CpuCutoffComboBox.SelectedIndex = 0;

            ViewModel.ClearConnectionFilters();
            DataGrid.AutoFitColumns();
        }

        // Tab 1 Clear
        private void ClearProcessFilters_Click(object sender, RoutedEventArgs e)
        {
            ProcessSearchTextBox.Text = string.Empty;
            if (ProcessCpuCutoffComboBox != null) ProcessCpuCutoffComboBox.SelectedIndex = 0;
            if (ProcessBandwidthCutoffComboBox != null) ProcessBandwidthCutoffComboBox.SelectedIndex = 0;

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
            ViewModel.GlobalTrustFilter = "Apenas confiáveis";
        }

        private void MetricCardThreat_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ViewModel.GlobalOnlyAlerts = !ViewModel.GlobalOnlyAlerts;
        }

        // Continuous reading follows the selected table on the two supported tabs.
        private void ContinuousReadingToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsLoading)
            {
                ContinuousReadingToggleBtn.IsChecked = ViewModel.IsLiveReading;
                return;
            }

            if (ContinuousReadingToggleBtn.IsChecked == true)
            {
                _liveReadingTimer.Start();
                ViewModel.IsLiveReading = true;
                ViewModel.StatusMessage = "Leitura contínua ativa. Nova amostra a cada 2 segundos.";
            }
            else
            {
                _liveReadingTimer.Stop();
                ViewModel.IsLiveReading = false;
                ViewModel.StatusMessage = "Leitura contínua pausada.";
            }
        }

        // Dynamic Refresh Handlers
        private ViewModelBase[] GetActiveSelection() => (ViewModel.SelectedTabIndex switch
        {
            0 => DataGrid.SelectedItems.Cast<object>(),
            1 => ProcessesDataGrid.SelectedItems.Cast<object>(),
            2 => TasksDataGrid.SelectedItems.Cast<object>(),
            _ => StartupDataGrid.SelectedItems.Cast<object>()
        }).OfType<ViewModelBase>().ToArray();

        private void EditTagSelection_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetActiveSelection();
            if (selected.Length == 0)
            {
                MessageBox.Show(this, "Selecione um ou mais registros na tabela.", "Editar tag",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new EditTagWindow(ViewModel.SavedTags,
                selected.Length == 1 ? selected[0].TagText : string.Empty) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            try
            {
                ViewModel.SetItemTags(selected, dialog.SelectedTag);
                AutoFitActiveTabGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Não foi possível salvar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TrustActionMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu) return;
            var trustAction = menu.Items.OfType<MenuItem>()
                .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), "TrustToggle", StringComparison.Ordinal));
            if (trustAction is null) return;

            var selected = GetActiveSelection();
            bool willTrust = selected.Length == 0 || selected.All(item => !item.AnnotationTrusted);
            trustAction.Header = willTrust ? "Marcar como confiável" : "Remover confiança";
            trustAction.IsEnabled = selected.Length > 0;
            trustAction.Foreground = selected.Length == 0
                ? (Brush)FindResource("TextMuted")
                : (Brush)FindResource(willTrust ? "SuccessColor" : "DangerColor");
            trustAction.FontWeight = FontWeights.SemiBold;
        }

        private void ToggleSelectedTrust_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetActiveSelection();
            if (selected.Length == 0) return;

            bool trusted = selected.All(item => !item.AnnotationTrusted);
            try
            {
                ViewModel.SetItemsTrust(selected, trusted);
                AutoFitActiveTabGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Não foi possível atualizar a confiança",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void ExportCurrentTabJson_Click(object sender, RoutedEventArgs e)
        {
            var (label, filePrefix, count) = ViewModel.SelectedTabIndex switch
            {
                1 => ("processos", "processos", ViewModel.FilteredProcesses.Count),
                2 => ("tarefas agendadas", "tarefas_agendadas", ViewModel.FilteredTasks.Count),
                3 => ("itens de inicialização", "itens_inicializacao", ViewModel.FilteredStartupItems.Count),
                _ => ("conexões", "conexoes", ViewModel.FilteredEntries.Count)
            };

            if (count == 0)
            {
                MessageBox.Show($"Não há {label} visíveis para exportar com os filtros atuais.",
                    "Sem dados para exportar", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = $"Exportar {label} em JSON",
                Filter = "Arquivo JSON (*.json)|*.json",
                FileName = $"{filePrefix}_netstat_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog(this) != true) return;

            try
            {
                int exportedCount = ViewModel.ExportCurrentTabJson(dialog.FileName);
                MessageBox.Show($"{exportedCount} {label} exportados para:\n{dialog.FileName}",
                    "Exportação concluída", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao exportar {label}: {ex.Message}",
                    "Erro de exportação", MessageBoxButton.OK, MessageBoxImage.Error);
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
            UpdateSelectionSummary();
        }

        private void UpdateSelectionSummary()
        {
            IEnumerable<object> selection = ViewModel.SelectedTabIndex switch
            {
                0 => DataGrid.SelectedItems.Cast<object>(),
                1 => ProcessesDataGrid.SelectedItems.Cast<object>(),
                2 => TasksDataGrid.SelectedItems.Cast<object>(),
                _ => StartupDataGrid.SelectedItems.Cast<object>()
            };
            var selected = selection.OfType<ViewModelBase>().ToArray();
            if (selected.Length == 0)
            {
                SelectedSummaryText.Text = "Nenhum registro selecionado";
                SelectedSummaryText.ToolTip = SelectedSummaryText.Text;
                return;
            }

            if (selected.Length == 1)
            {
                SelectedSummaryText.Text = selected[0] switch
                {
                    ConnectionItemViewModel connection =>
                        $"{connection.ProcessName} · PID {connection.PID} · {connection.State} · CPU {connection.DisplayCpu} · Banda {connection.DisplayBandwidth}",
                    ProcessAggregateItemViewModel process =>
                        $"{process.ProcessName} · PID {process.PID} · CPU {process.FormattedCpu} · Banda {process.FormattedTotalBandwidth} · {process.ActiveConnectionsCount} conexões",
                    ScheduledTaskItemViewModel task =>
                        $"{task.TaskName} · {task.State} · {(task.IsSuspicious ? "Alerta" : "Sem alerta")}",
                    StartupItemViewModel startup =>
                        $"{startup.Name} · {startup.SourceType} · {(startup.IsEnabled ? "Ativo" : "Desativado")}",
                    _ => "1 registro selecionado"
                };
                if (!string.IsNullOrWhiteSpace(selected[0].TagText))
                    SelectedSummaryText.Text += $" · Tag: {selected[0].TagText}";
                SelectedSummaryText.Text += selected[0].AnnotationTrusted ? " · Confiável" : " · Não verificado";
                SelectedSummaryText.ToolTip = SelectedSummaryText.Text;
                return;
            }

            string itemType = selected[0] switch
            {
                ConnectionItemViewModel => "conexões",
                ProcessAggregateItemViewModel => "processos",
                ScheduledTaskItemViewModel => "tarefas",
                StartupItemViewModel => "itens de inicialização",
                _ => "registros"
            };
            int trustedCount = selected.Count(item => item.AnnotationTrusted);
            int taggedCount = selected.Count(item => !string.IsNullOrWhiteSpace(item.TagText));
            SelectedSummaryText.Text = $"{selected.Length} {itemType} selecionados · {trustedCount} confiáveis · {taggedCount} com tag";
            SelectedSummaryText.ToolTip = SelectedSummaryText.Text;
        }
    }
}
