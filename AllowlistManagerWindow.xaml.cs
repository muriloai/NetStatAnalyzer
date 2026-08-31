using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NetStatAnalyzer.Application.UseCases;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Policies;
using NetStatAnalyzer.Infrastructure.Persistence;
using NetStatAnalyzer.Presentation.Extensions;
using NetStatAnalyzer.Presentation.ViewModels;

namespace NetStatAnalyzer
{
    public partial class AllowlistManagerWindow : Window
    {
        public AllowlistManagerViewModel ViewModel { get; }

        public AllowlistManagerWindow(ManageAllowlistUseCase? useCase = null)
        {
            InitializeComponent();

            var allowlistUseCase = useCase ?? new ManageAllowlistUseCase(new JsonFileAllowlistRepository());
            ViewModel = new AllowlistManagerViewModel(allowlistUseCase);
            DataContext = ViewModel;

            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AllowlistManagerViewModel.RuleCountSummary))
                {
                    RuleCountTextBlock.Text = ViewModel.RuleCountSummary;
                }
            };

            RulesDataGrid.ItemsSource = ViewModel.DisplayRules;
            RuleCountTextBlock.Text = ViewModel.RuleCountSummary;
            RulesDataGrid.AutoFitColumns();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.SearchQuery = SearchTextBox.Text;
            RulesDataGrid.AutoFitColumns();
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = string.Empty;
            ViewModel.SearchQuery = string.Empty;
            RulesDataGrid.AutoFitColumns();
        }

        private void DeleteRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is AllowlistRule rule)
            {
                string targetDesc = !string.IsNullOrEmpty(rule.LocalAddress) && TrustEvaluationPolicy.IsWildcardOrListeningAddress(rule.ForeignAddress)
                    ? rule.LocalAddress
                    : (rule.ForeignAddress ?? rule.IP);

                var result = MessageBox.Show(
                    $"Deseja remover a conexão confiável para '{rule.ProcessName}' ({targetDesc})?",
                    "Confirmar Exclusão",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    ViewModel.DeleteRule(rule);
                    RulesDataGrid.AutoFitColumns();
                }
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.TotalRulesCount == 0)
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
                ViewModel.ClearAll();
                RulesDataGrid.AutoFitColumns();
            }
        }

        private void ExportJson_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModel.TotalRulesCount == 0)
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
                    string json = ViewModel.ExportToJson();
                    File.WriteAllText(dialog.FileName, json);
                    MessageBox.Show($"Conexões confiáveis exportadas com sucesso!\nArquivo: {dialog.FileName}\nTotal de conexões: {ViewModel.TotalRulesCount}",
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
                    var result = ViewModel.ImportFromJson(json);

                    if (result.Success)
                    {
                        RulesDataGrid.AutoFitColumns();
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
    }
}
