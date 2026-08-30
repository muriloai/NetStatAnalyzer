using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace NetStatAnalyzer
{
    public class NetStatEntry
    {
        public string Protocol { get; set; } = string.Empty;
        public string LocalAddress { get; set; } = string.Empty;
        public string ForeignAddress { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public int PID { get; set; }
        public string ProcessName { get; set; } = "Unknown";
        public string? ProcessPath { get; set; }
        public BitmapImage? ProcessIcon { get; set; }
        public bool IsAllowed { get; set; }

        public string AllowBadgeBackground => IsAllowed ? "#064E3B" : "Transparent";
        public string AllowBadgeForeground => IsAllowed ? "#34D399" : "Transparent";
        public string AllowBadgeBorder => IsAllowed ? "#059669" : "Transparent";
        public Visibility AllowBadgeVisibility => IsAllowed ? Visibility.Visible : Visibility.Collapsed;

        public string StateBadgeBackground => State?.ToUpperInvariant() switch
        {
            "ESTABLISHED" => "#14532D",
            "LISTENING" => "#1E3A8A",
            "TIME_WAIT" => "#713F12",
            "CLOSE_WAIT" => "#7C2D12",
            "SYN_SENT" or "SYN_RECEIVED" => "#581C87",
            _ => "#334155"
        };

        public string StateBadgeForeground => State?.ToUpperInvariant() switch
        {
            "ESTABLISHED" => "#4ADE80",
            "LISTENING" => "#60A5FA",
            "TIME_WAIT" => "#FDE047",
            "CLOSE_WAIT" => "#FB923C",
            "SYN_SENT" or "SYN_RECEIVED" => "#C084FC",
            _ => "#CBD5E1"
        };

        public string ProtocolBadgeBackground => Protocol?.ToUpperInvariant() switch
        {
            "TCP" => "#0369A1",
            "UDP" => "#4338CA",
            _ => "#334155"
        };

        public string ProtocolBadgeForeground => "#F0F9FF";
    }

    public partial class MainWindow : Window
    {
        private readonly List<NetStatEntry> _allEntries = new();
        public ObservableCollection<NetStatEntry> FilteredEntries { get; set; } = new();
        private readonly AllowlistService _allowlistService = AllowlistService.Instance;

        private bool _isLoading = false;
        private bool _isInitialized = false;

        public MainWindow()
        {
            InitializeComponent();
            _isInitialized = true;
            DataContext = this;
            LoadVersion();

            _allowlistService.RulesChanged += (s, e) => Dispatcher.Invoke(() =>
            {
                ReevaluateAllowed();
                UpdateMetrics();
                ApplyFilters();
            });
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadNetStatDataAsync();
        }

        private async Task LoadNetStatDataAsync()
        {
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                LoadingProgressBar.Visibility = Visibility.Visible;
                StatusTextBlock.Text = "Carregando conexões de rede...";
                ReloadButton.IsEnabled = false;

                var entries = await Task.Run(() =>
                {
                    var resultList = new List<NetStatEntry>();
                    var outputLines = RunCommand("netstat", "-ano");

                    foreach (var line in outputLines)
                    {
                        var entry = ParseNetStatLine(line);
                        if (entry != null)
                        {
                            entry.ProcessName = GetProcessName(entry.PID);
                            entry.ProcessPath = GetProcessPath(entry.PID);
                            entry.ProcessIcon = GetProcessIcon(entry.ProcessPath);
                            entry.IsAllowed = _allowlistService.IsAllowed(entry.ProcessName, entry.ForeignAddress, entry.LocalAddress);
                            resultList.Add(entry);
                        }
                    }

                    return resultList;
                });

                _allEntries.Clear();
                _allEntries.AddRange(entries);

                UpdateStateFilterOptions();
                UpdateMetrics();
                ApplyFilters();
                AutoFitColumns();

                StatusTextBlock.Text = "Pronto";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar dados do NetStat: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Erro ao carregar conexões.";
            }
            finally
            {
                _isLoading = false;
                LoadingProgressBar.Visibility = Visibility.Collapsed;
                ReloadButton.IsEnabled = true;
            }
        }

        private void ReevaluateAllowed()
        {
            foreach (var entry in _allEntries)
            {
                entry.IsAllowed = _allowlistService.IsAllowed(entry.ProcessName, entry.ForeignAddress, entry.LocalAddress, entry.Protocol);
            }
        }

        private void UpdateMetrics()
        {
            int total = _allEntries.Count;
            int established = _allEntries.Count(e => string.Equals(e.State, "ESTABLISHED", StringComparison.OrdinalIgnoreCase));
            int listening = _allEntries.Count(e => string.Equals(e.State, "LISTENING", StringComparison.OrdinalIgnoreCase));
            int allowed = _allEntries.Count(e => e.IsAllowed);

            MetricTotalText.Text = total.ToString();
            MetricEstablishedText.Text = established.ToString();
            MetricListeningText.Text = listening.ToString();
            MetricAllowedText.Text = allowed.ToString();
        }

        private void UpdateStateFilterOptions()
        {
            string? currentSelected = StateComboBox.SelectedItem as string;

            var states = _allEntries
                .Select(e => string.IsNullOrWhiteSpace(e.State) ? "N/A" : e.State)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            states.Insert(0, "Todos os Estados");

            StateComboBox.ItemsSource = states;

            if (!string.IsNullOrEmpty(currentSelected) && states.Contains(currentSelected))
            {
                StateComboBox.SelectedItem = currentSelected;
            }
            else
            {
                StateComboBox.SelectedIndex = 0;
            }

            if (ProtocolComboBox.SelectedIndex < 0)
            {
                ProtocolComboBox.SelectedIndex = 0;
            }
        }

        private void ApplyFilters()
        {
            if (!_isInitialized || SearchTextBox == null || ProtocolComboBox == null || StateComboBox == null || AllowFilterComboBox == null || ConnectionCountText == null || EmptyStatePanel == null)
            {
                return;
            }

            string searchQuery = SearchTextBox.Text?.Trim().ToLowerInvariant() ?? string.Empty;
            string selectedProtocol = (ProtocolComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todos";
            string selectedState = StateComboBox.SelectedItem as string ?? "Todos os Estados";
            string selectedPermission = (AllowFilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todas";

            var filtered = _allEntries.Where(entry =>
            {
                // Protocol Filter
                if (selectedProtocol != "Todos" && !entry.Protocol.Equals(selectedProtocol, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // State Filter
                if (selectedState != "Todos os Estados" && !entry.State.Equals(selectedState, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Confidence Filter
                if (selectedPermission == "Apenas Confiáveis" && !entry.IsAllowed)
                {
                    return false;
                }
                if (selectedPermission == "Ocultar Confiáveis" && entry.IsAllowed)
                {
                    return false;
                }

                // Global Search Filter (Process name, PID, IP local/remote, Port)
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    bool matchesName = entry.ProcessName.ToLowerInvariant().Contains(searchQuery);
                    bool matchesPid = entry.PID.ToString().Contains(searchQuery);
                    bool matchesLocal = entry.LocalAddress.ToLowerInvariant().Contains(searchQuery);
                    bool matchesForeign = entry.ForeignAddress.ToLowerInvariant().Contains(searchQuery);
                    bool matchesProtocol = entry.Protocol.ToLowerInvariant().Contains(searchQuery);
                    bool matchesState = entry.State.ToLowerInvariant().Contains(searchQuery);
                    bool matchesAllowed = entry.IsAllowed && "confiavel".Contains(searchQuery);

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

            ConnectionCountText.Text = $"Exibindo {FilteredEntries.Count} de {_allEntries.Count} conexões";
            EmptyStatePanel.Visibility = FilteredEntries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private string[] RunCommand(string command, string args)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao executar comando '{command}': {ex.Message}");
                return Array.Empty<string>();
            }
        }

        private NetStatEntry? ParseNetStatLine(string line)
        {
            try
            {
                string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) return null;

                string protocol = parts[0].ToUpperInvariant();
                if (protocol != "TCP" && protocol != "UDP") return null;

                if (protocol == "TCP" && parts.Length >= 5)
                {
                    if (int.TryParse(parts[^1], out int pid))
                    {
                        return new NetStatEntry
                        {
                            Protocol = protocol,
                            LocalAddress = parts[1],
                            ForeignAddress = parts[2],
                            State = parts[3],
                            PID = pid
                        };
                    }
                }
                else if (protocol == "UDP" && parts.Length >= 4)
                {
                    if (int.TryParse(parts[^1], out int pid))
                    {
                        return new NetStatEntry
                        {
                            Protocol = protocol,
                            LocalAddress = parts[1],
                            ForeignAddress = parts[2],
                            State = "N/A",
                            PID = pid
                        };
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao processar linha '{line}': {ex.Message}");
                return null;
            }
        }

        private string GetProcessName(int pid)
        {
            if (pid == 0) return "Sistema Ocioso";
            if (pid == 4) return "System";

            try
            {
                using var process = Process.GetProcessById(pid);
                return process.ProcessName;
            }
            catch
            {
                return "Desconhecido";
            }
        }

        private string? GetProcessPath(int pid)
        {
            if (pid <= 4) return null;

            try
            {
                using var process = Process.GetProcessById(pid);
                return process.MainModule?.FileName;
            }
            catch
            {
                return null;
            }
        }

        private BitmapImage? GetProcessIcon(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
                if (icon != null)
                {
                    using var bitmap = icon.ToBitmap();
                    using var memoryStream = new MemoryStream();
                    bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                    memoryStream.Seek(0, SeekOrigin.Begin);

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memoryStream;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze(); // Permite uso seguro entre threads WPF
                    return bitmapImage;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao extrair ícone '{filePath}': {ex.Message}");
            }

            return null;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = string.Empty;
            ProtocolComboBox.SelectedIndex = 0;
            StateComboBox.SelectedIndex = 0;
            if (AllowFilterComboBox != null) AllowFilterComboBox.SelectedIndex = 0;
            ApplyFilters();
        }

        private async void Reload_Click(object sender, RoutedEventArgs e)
        {
            await LoadNetStatDataAsync();
        }

        private void AddToAllowlist_Click(object sender, RoutedEventArgs e)
        {
            var selected = DataGrid.SelectedItems.OfType<NetStatEntry>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Selecione uma ou mais conexões na lista para marcar como confiável.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int added = _allowlistService.AddRules(selected.Select(entry =>
                (entry.ProcessName, entry.ForeignAddress, entry.Protocol, (string?)entry.LocalAddress)));

            ReevaluateAllowed();
            UpdateMetrics();
            ApplyFilters();

            StatusTextBlock.Text = added > 0 
                ? $"{added} conexão(ões) marcada(s) como confiável(is)." 
                : "Itens selecionados já estão marcados como confiáveis.";
        }

        private void RemoveFromAllowlist_Click(object sender, RoutedEventArgs e)
        {
            var selected = DataGrid.SelectedItems.OfType<NetStatEntry>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Selecione uma ou mais conexões para remover da lista de confiáveis.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int removed = _allowlistService.RemoveRules(selected.Select(entry =>
                (entry.ProcessName, entry.ForeignAddress, (string?)entry.LocalAddress)));

            ReevaluateAllowed();
            UpdateMetrics();
            ApplyFilters();

            StatusTextBlock.Text = removed > 0 
                ? $"{removed} conexão(ões) removida(s) da lista de confiáveis." 
                : "Nenhuma conexão confiável correspondente encontrada.";
        }

        private void OpenAllowManager_Click(object sender, RoutedEventArgs e)
        {
            var managerWindow = new AllowlistManagerWindow
            {
                Owner = this
            };

            managerWindow.ShowDialog();
            ReevaluateAllowed();
            UpdateMetrics();
            ApplyFilters();
        }

        private void ExportConnections_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var entriesToExport = FilteredEntries.ToList();
                if (entriesToExport.Count == 0)
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
                    string ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();

                    if (ext == ".csv")
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("Processo;PID;Caminho;Protocolo;Endereço Local;Endereço Remoto;Estado;Confiável");
                        foreach (var entry in entriesToExport)
                        {
                            string path = (entry.ProcessPath ?? string.Empty).Replace("\"", "\"\"");
                            sb.AppendLine($"\"{entry.ProcessName}\";{entry.PID};\"{path}\";\"{entry.Protocol}\";\"{entry.LocalAddress}\";\"{entry.ForeignAddress}\";\"{entry.State}\";\"{(entry.IsAllowed ? "Sim" : "Não")}\"");
                        }
                        File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    }
                    else if (ext == ".txt")
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("=== NetStatAnalyzer - Relatório de Conexões de Rede ===");
                        sb.AppendLine($"Data/Hora: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                        sb.AppendLine($"Total de Conexões Exibidas: {entriesToExport.Count}");
                        sb.AppendLine(new string('-', 120));
                        sb.AppendLine($"{"PROCESSO",-24} {"PID",-8} {"PROTO",-7} {"ENDEREÇO LOCAL",-24} {"ENDEREÇO REMOTO",-24} {"ESTADO",-14} {"CONFIÁVEL",-10}");
                        sb.AppendLine(new string('-', 120));
                        foreach (var entry in entriesToExport)
                        {
                            sb.AppendLine($"{entry.ProcessName,-24} {entry.PID,-8} {entry.Protocol,-7} {entry.LocalAddress,-24} {entry.ForeignAddress,-24} {entry.State,-14} {(entry.IsAllowed ? "Sim" : "Não"),-10}");
                        }
                        File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    }
                    else
                    {
                        var version = Assembly.GetExecutingAssembly().GetName().Version;
                        string versionStr = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.1.0";

                        var exportData = new
                        {
                            app = "NetStatAnalyzer",
                            version = versionStr,
                            exportedAt = DateTime.Now,
                            totalConnections = entriesToExport.Count,
                            connections = entriesToExport.Select(entry => new
                            {
                                processName = entry.ProcessName,
                                pid = entry.PID,
                                processPath = entry.ProcessPath ?? string.Empty,
                                protocol = entry.Protocol,
                                localAddress = entry.LocalAddress,
                                foreignAddress = entry.ForeignAddress,
                                state = entry.State,
                                isTrusted = entry.IsAllowed
                            })
                        };

                        string json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(dialog.FileName, json, Encoding.UTF8);
                    }

                    StatusTextBlock.Text = $"{entriesToExport.Count} conexões exportadas para '{Path.GetFileName(dialog.FileName)}'!";
                    MessageBox.Show($"Conexões exportadas com sucesso!\nArquivo: {dialog.FileName}\nTotal de conexões: {entriesToExport.Count}",
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
            if (sender is Button btn && btn.DataContext is NetStatEntry entry)
            {
                OpenFileLocation(entry);
            }
        }

        private void RowCopyDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is NetStatEntry entry)
            {
                string details = $"Processo: {entry.ProcessName} (PID: {entry.PID})\nProtocolo: {entry.Protocol}\nLocal: {entry.LocalAddress}\nRemoto: {entry.ForeignAddress}\nEstado: {entry.State}\nConfiável: {(entry.IsAllowed ? "Sim" : "Não")}\nCaminho: {entry.ProcessPath ?? "N/A"}";
                Clipboard.SetText(details);
                StatusTextBlock.Text = $"Detalhes de '{entry.ProcessName}' (PID {entry.PID}) copiados!";
            }
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid.SelectedItem is NetStatEntry entry)
            {
                OpenFileLocation(entry);
            }
            else
            {
                MessageBox.Show("Selecione um processo na tabela para abrir o local do arquivo.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OpenFileLocation(NetStatEntry entry)
        {
            try
            {
                string? path = entry.ProcessPath;
                if (string.IsNullOrEmpty(path))
                {
                    path = GetProcessPath(entry.PID);
                }

                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
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
            var selected = DataGrid.SelectedItems.OfType<NetStatEntry>().ToList();
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

            StatusTextBlock.Text = $"{selected.Count} detalhe(s) copiado(s) para a área de transferência.";
        }

        private void CopyForeignIP_Click(object sender, RoutedEventArgs e)
        {
            var selected = DataGrid.SelectedItems.OfType<NetStatEntry>().ToList();
            if (selected.Count == 0) return;

            var ips = selected
                .Select(entry => AllowlistService.ExtractIP(entry.ForeignAddress))
                .Where(ip => !string.IsNullOrWhiteSpace(ip))
                .Distinct();

            string result = string.Join(Environment.NewLine, ips);
            if (!string.IsNullOrEmpty(result))
            {
                Clipboard.SetText(result);
                StatusTextBlock.Text = $"{selected.Count} IP(s) remoto(s) copiado(s).";
            }
        }

        private void CopyLocalIP_Click(object sender, RoutedEventArgs e)
        {
            var selected = DataGrid.SelectedItems.OfType<NetStatEntry>().ToList();
            if (selected.Count == 0) return;

            var ips = selected
                .Select(entry => AllowlistService.ExtractIP(entry.LocalAddress))
                .Where(ip => !string.IsNullOrWhiteSpace(ip))
                .Distinct();

            string result = string.Join(Environment.NewLine, ips);
            if (!string.IsNullOrEmpty(result))
            {
                Clipboard.SetText(result);
                StatusTextBlock.Text = $"{selected.Count} IP(s) local(is) copiado(s).";
            }
        }

        private void CopyPID_Click(object sender, RoutedEventArgs e)
        {
            var selected = DataGrid.SelectedItems.OfType<NetStatEntry>().ToList();
            if (selected.Count == 0) return;

            var pids = selected.Select(entry => entry.PID.ToString()).Distinct();
            Clipboard.SetText(string.Join(Environment.NewLine, pids));
            StatusTextBlock.Text = $"{selected.Count} PID(s) copiado(s).";
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int selectedCount = DataGrid.SelectedItems.Count;

            if (selectedCount == 1 && DataGrid.SelectedItem is NetStatEntry entry)
            {
                SelectedSeparator.Visibility = Visibility.Visible;
                SelectedProcessText.Visibility = Visibility.Visible;
                string allowStatus = entry.IsAllowed ? " [🛡️ Confiável]" : "";
                SelectedProcessText.Text = $"Selecionado: {entry.ProcessName} (PID: {entry.PID}){allowStatus}";
            }
            else if (selectedCount > 1)
            {
                SelectedSeparator.Visibility = Visibility.Visible;
                SelectedProcessText.Visibility = Visibility.Visible;
                int allowCount = DataGrid.SelectedItems.OfType<NetStatEntry>().Count(i => i.IsAllowed);
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
                    // If clicked on column header, scrollbar or button, ignore
                    if (current is DataGridColumnHeader || current is ScrollBar || current is Button)
                    {
                        return;
                    }

                    // Only trigger if double click occurred directly on a DataGridRow
                    if (current is DataGridRow row && row.Item is NetStatEntry clickedEntry)
                    {
                        OpenFileLocation(clickedEntry);
                        e.Handled = true;
                        return;
                    }

                    current = VisualTreeHelper.GetParent(current);
                }
            }
        }

        private void AutoFitColumns()
        {
            if (DataGrid == null || DataGrid.Columns.Count == 0) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var column in DataGrid.Columns)
                {
                    if (column.Header?.ToString() == "AÇÕES") continue;

                    // Automatically recalibrates column width based on headers and data contents
                    column.Width = new DataGridLength(0, DataGridLengthUnitType.Auto);
                    column.Width = DataGridLength.Auto;
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void LoadVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                VersionTextBlock.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
            }
            else
            {
                VersionTextBlock.Text = "v1.1.0";
            }
        }
    }
}