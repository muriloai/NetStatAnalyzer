using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

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

        private bool _isLoading = false;
        private bool _isInitialized = false;

        public MainWindow()
        {
            InitializeComponent();
            _isInitialized = true;
            DataContext = this;
            LoadVersion();
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

        private void UpdateMetrics()
        {
            int total = _allEntries.Count;
            int established = _allEntries.Count(e => string.Equals(e.State, "ESTABLISHED", StringComparison.OrdinalIgnoreCase));
            int listening = _allEntries.Count(e => string.Equals(e.State, "LISTENING", StringComparison.OrdinalIgnoreCase));

            MetricTotalText.Text = total.ToString();
            MetricEstablishedText.Text = established.ToString();
            MetricListeningText.Text = listening.ToString();
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
            if (!_isInitialized || SearchTextBox == null || ProtocolComboBox == null || StateComboBox == null || ConnectionCountText == null || EmptyStatePanel == null)
            {
                return;
            }

            string searchQuery = SearchTextBox.Text?.Trim().ToLowerInvariant() ?? string.Empty;
            string selectedProtocol = (ProtocolComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todos";
            string selectedState = StateComboBox.SelectedItem as string ?? "Todos os Estados";

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

                // Global Search Filter (Process name, PID, IP local/remote, Port)
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    bool matchesName = entry.ProcessName.ToLowerInvariant().Contains(searchQuery);
                    bool matchesPid = entry.PID.ToString().Contains(searchQuery);
                    bool matchesLocal = entry.LocalAddress.ToLowerInvariant().Contains(searchQuery);
                    bool matchesForeign = entry.ForeignAddress.ToLowerInvariant().Contains(searchQuery);
                    bool matchesProtocol = entry.Protocol.ToLowerInvariant().Contains(searchQuery);
                    bool matchesState = entry.State.ToLowerInvariant().Contains(searchQuery);

                    if (!matchesName && !matchesPid && !matchesLocal && !matchesForeign && !matchesProtocol && !matchesState)
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
            ApplyFilters();
        }

        private async void Reload_Click(object sender, RoutedEventArgs e)
        {
            await LoadNetStatDataAsync();
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
            if (DataGrid.SelectedItem is NetStatEntry entry)
            {
                string details = $"Processo: {entry.ProcessName} (PID: {entry.PID})\nProtocolo: {entry.Protocol}\nLocal: {entry.LocalAddress}\nRemoto: {entry.ForeignAddress}\nEstado: {entry.State}\nCaminho: {entry.ProcessPath ?? "N/A"}";
                Clipboard.SetText(details);
            }
        }

        private void CopyIP_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid.SelectedItem is NetStatEntry entry)
            {
                Clipboard.SetText(entry.LocalAddress);
            }
        }

        private void CopyPID_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid.SelectedItem is NetStatEntry entry)
            {
                Clipboard.SetText(entry.PID.ToString());
            }
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGrid.SelectedItem is NetStatEntry entry)
            {
                SelectedSeparator.Visibility = Visibility.Visible;
                SelectedProcessText.Visibility = Visibility.Visible;
                SelectedProcessText.Text = $"Selecionado: {entry.ProcessName} (PID: {entry.PID})";
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
            if (DataGrid.SelectedItem is NetStatEntry entry)
            {
                OpenFileLocation(entry);
            }
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
                VersionTextBlock.Text = "v1.0.3";
            }
        }
    }
}