using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace NetStatAnalyzer
{
    public partial class AllowlistManagerWindow : Window
    {
        private readonly AllowlistService _allowlistService = AllowlistService.Instance;
        public ObservableCollection<AllowlistRule> DisplayRules { get; set; } = new();

        public AllowlistManagerWindow()
        {
            InitializeComponent();
            RulesDataGrid.ItemsSource = DisplayRules;
            RefreshRules();

            _allowlistService.RulesChanged += (s, e) => Dispatcher.Invoke(RefreshRules);
        }

        private void RefreshRules()
        {
            string query = SearchTextBox?.Text?.Trim().ToLowerInvariant() ?? string.Empty;

            var filtered = _allowlistService.Rules.Where(r =>
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

            RuleCountTextBlock.Text = $"{_allowlistService.Rules.Count} Conexões Confiáveis";
            AutoFitColumns();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshRules();
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = string.Empty;
            RefreshRules();
        }

        private void DeleteRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is AllowlistRule rule)
            {
                string targetDesc = !string.IsNullOrEmpty(rule.LocalAddress) && AllowlistService.IsWildcardOrListeningAddress(rule.ForeignAddress)
                    ? rule.LocalAddress
                    : (rule.ForeignAddress ?? rule.IP);

                var result = MessageBox.Show(
                    $"Deseja remover a conexão confiável para '{rule.ProcessName}' ({targetDesc})?",
                    "Confirmar Exclusão",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _allowlistService.RemoveRule(rule);
                    RefreshRules();
                }
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_allowlistService.Rules.Count == 0)
            {
                MessageBox.Show("A lista de conexões confiáveis já está vazia.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "Tem certeza que deseja remover TODAS as conexões confiáveis? Esta ação não pode ser desfeita.",
                "Limpar Conexões Confiáveis",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _allowlistService.ClearAll();
                RefreshRules();
            }
        }

        private void ExportJson_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_allowlistService.Rules.Count == 0)
                {
                    MessageBox.Show("Não há conexões confiáveis para exportar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Title = "Exportar Conexões Confiáveis",
                    Filter = "Arquivo JSON (*.json)|*.json|Todos os Arquivos (*.*)|*.*",
                    FileName = $"conexoes_confiaveis_netstat_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                    DefaultExt = ".json"
                };

                if (dialog.ShowDialog(this) == true)
                {
                    string json = _allowlistService.ExportToJson();
                    File.WriteAllText(dialog.FileName, json);
                    MessageBox.Show($"Conexões confiáveis exportadas com sucesso!\nArquivo: {dialog.FileName}\nTotal de conexões: {_allowlistService.Rules.Count}",
                        "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao exportar conexões confiáveis: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportJson_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Importar Conexões Confiáveis",
                    Filter = "Arquivo JSON (*.json)|*.json|Todos os Arquivos (*.*)|*.*",
                    DefaultExt = ".json"
                };

                if (dialog.ShowDialog(this) == true)
                {
                    string json = File.ReadAllText(dialog.FileName);
                    var result = _allowlistService.ImportFromJson(json);

                    if (result.Success)
                    {
                        RefreshRules();
                        MessageBox.Show(result.Message, "Importação Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(result.Message, "Erro na Importação", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao importar arquivo: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AutoFitColumns()
        {
            if (RulesDataGrid == null || RulesDataGrid.Columns.Count == 0) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var column in RulesDataGrid.Columns)
                {
                    if (column.Header?.ToString() == "AÇÕES") continue;

                    column.Width = new DataGridLength(0, DataGridLengthUnitType.Auto);
                    column.Width = DataGridLength.Auto;
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }
}
