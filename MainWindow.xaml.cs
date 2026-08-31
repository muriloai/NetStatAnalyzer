using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using NetStatAnalyzer.Application.UseCases;
using NetStatAnalyzer.Domain.Policies;
using NetStatAnalyzer.Infrastructure.Exporting;
using NetStatAnalyzer.Infrastructure.Persistence;
using NetStatAnalyzer.Infrastructure.Processes;
using NetStatAnalyzer.Infrastructure.Scanning;
using NetStatAnalyzer.Presentation.Extensions;
using NetStatAnalyzer.Presentation.ViewModels;

namespace NetStatAnalyzer
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();

            var networkScanner = new NetStatCliScanner();
            var processResolver = new Win32ProcessResolver();
            var allowlistRepo = new JsonFileAllowlistRepository();
            var connectionExporter = new FileConnectionExporter();

            var scanUseCase = new ScanConnectionsUseCase(networkScanner, processResolver);
            var allowlistUseCase = new ManageAllowlistUseCase(allowlistRepo);
            var exportUseCase = new ExportConnectionsUseCase(connectionExporter);

            ViewModel = new MainViewModel(scanUseCase, allowlistUseCase, exportUseCase);
            DataContext = ViewModel;

            VersionTextBlock.Text = ViewModel.AppVersion;
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.MetricTotal))
                    MetricTotalText.Text = ViewModel.MetricTotal.ToString();
                else if (e.PropertyName == nameof(MainViewModel.MetricEstablished))
                    MetricEstablishedText.Text = ViewModel.MetricEstablished.ToString();
                else if (e.PropertyName == nameof(MainViewModel.MetricListening))
                    MetricListeningText.Text = ViewModel.MetricListening.ToString();
                else if (e.PropertyName == nameof(MainViewModel.MetricAllowed))
                    MetricAllowedText.Text = ViewModel.MetricAllowed.ToString();
                else if (e.PropertyName == nameof(MainViewModel.ConnectionCountSummary))
                    ConnectionCountText.Text = ViewModel.ConnectionCountSummary;
                else if (e.PropertyName == nameof(MainViewModel.StatusMessage))
                    StatusTextBlock.Text = ViewModel.StatusMessage;
                else if (e.PropertyName == nameof(MainViewModel.IsLoading))
                {
                    LoadingProgressBar.Visibility = ViewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;
                    ReloadButton.IsEnabled = !ViewModel.IsLoading;
                }
                else if (e.PropertyName == nameof(MainViewModel.IsEmptyState))
                    EmptyStatePanel.Visibility = ViewModel.IsEmptyState ? Visibility.Visible : Visibility.Collapsed;
            };

            DataGrid.ItemsSource = ViewModel.FilteredEntries;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadConnectionsAsync();
            DataGrid.AutoFitColumns();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SearchQuery = SearchTextBox.Text;
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
            }
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = string.Empty;
            if (ProtocolComboBox != null) ProtocolComboBox.SelectedIndex = 0;
            if (StateComboBox != null) StateComboBox.SelectedIndex = 0;
            if (AllowFilterComboBox != null) AllowFilterComboBox.SelectedIndex = 0;

            ViewModel.ClearFilters();
        }

        private async void Reload_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadConnectionsAsync();
            DataGrid.AutoFitColumns();
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
        }

        private void ExportConnections_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModel.FilteredEntries.Count == 0)
                {
                    MessageBox.Show("Não há conexões na lista para exportar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void RowOpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ConnectionItemViewModel entry)
            {
                OpenFileLocation(entry);
            }
        }

        private void RowCopyDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ConnectionItemViewModel entry)
            {
                string details = $"Processo: {entry.ProcessName} (PID: {entry.PID})\nProtocolo: {entry.Protocol}\nLocal: {entry.LocalAddress}\nRemoto: {entry.ForeignAddress}\nEstado: {entry.State}\nConfiável: {(entry.IsAllowed ? "Sim" : "Não")}\nCaminho: {entry.ProcessPath ?? "N/A"}";
                Clipboard.SetText(details);
                ViewModel.StatusMessage = $"Detalhes de '{entry.ProcessName}' (PID {entry.PID}) copiados!";
            }
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid.SelectedItem is ConnectionItemViewModel entry)
            {
                OpenFileLocation(entry);
            }
            else
            {
                MessageBox.Show("Selecione um processo na tabela para abrir o local do arquivo.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private static void OpenFileLocation(ConnectionItemViewModel entry)
        {
            try
            {
                string? path = entry.ProcessPath;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
                else
                {
                    MessageBox.Show($"Não foi possível obter o local do arquivo para o processo '{entry.ProcessName}' (PID {entry.PID}). Pode requerer execução como Administrador.", "Informação", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir local do arquivo: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyDetails_Click(object sender, RoutedEventArgs e)
        {
            var selected = DataGrid.SelectedItems.OfType<ConnectionItemViewModel>().ToList();
            if (selected.Count == 0) return;

            if (selected.Count == 1)
            {
                var entry = selected[0];
                string details = $"Processo: {entry.ProcessName} (PID: {entry.PID})\nProtocolo: {entry.Protocol}\nLocal: {entry.LocalAddress}\nRemoto: {entry.ForeignAddress}\nEstado: {entry.State}\nConfiável: {(entry.IsAllowed ? "Sim" : "Não")}\nCaminho: {entry.ProcessPath ?? "N/A"}";
                Clipboard.SetText(details);
            }
            else
            {
                var lines = selected.Select(entry =>
                    $"{entry.ProcessName}\tPID: {entry.PID}\t{entry.Protocol}\t{entry.LocalAddress}\t{entry.ForeignAddress}\t{entry.State}\tConfiável: {(entry.IsAllowed ? "Sim" : "Não")}");
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

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int selectedCount = DataGrid.SelectedItems.Count;

            if (selectedCount == 1 && DataGrid.SelectedItem is ConnectionItemViewModel entry)
            {
                SelectedSeparator.Visibility = Visibility.Visible;
                SelectedProcessText.Visibility = Visibility.Visible;
                string allowStatus = entry.IsAllowed ? " [Confiável]" : "";
                SelectedProcessText.Text = $"Selecionado: {entry.ProcessName} (PID: {entry.PID}){allowStatus}";
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

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                DependencyObject? current = dep;
                while (current != null && current != DataGrid)
                {
                    if (current is DataGridColumnHeader || current is ScrollBar || current is Button)
                    {
                        return;
                    }

                    if (current is DataGridRow row && row.Item is ConnectionItemViewModel clickedEntry)
                    {
                        OpenFileLocation(clickedEntry);
                        e.Handled = true;
                        return;
                    }

                    current = VisualTreeHelper.GetParent(current);
                }
            }
        }
    }
}