using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NetStatAnalyzer.Application.UseCases;

namespace NetStatAnalyzer.Presentation.Windows;

public sealed class EditTagWindow : Window
{
    private readonly TextBox _inputBox;
    private readonly TextBlock _validationText;
    private readonly WrapPanel _appliedPanel;
    private readonly TextBlock _emptyAppliedText;
    private readonly StackPanel _suggestionsSection;
    private readonly WrapPanel _suggestionsPanel;
    private readonly Button _saveButton;

    private readonly List<string> _allSavedTags;
    private readonly HashSet<string> _initialTags;
    private readonly List<string> _currentTags;
    private readonly HashSet<string> _addedTags = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _removedTags = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string>? _onDeleteSavedTag;

    public IReadOnlyList<string> FinalTags => _currentTags.ToArray();
    public IReadOnlyList<string> AddedTags => _addedTags.ToArray();
    public IReadOnlyList<string> RemovedTags => _removedTags.ToArray();
    public string SelectedTag => string.Join(", ", _currentTags);

    public EditTagWindow(
        IEnumerable<string> savedTags,
        IEnumerable<string> currentTags,
        string title = "Editar tags",
        int selectedCount = 1,
        Action<string>? onDeleteSavedTag = null)
    {
        _onDeleteSavedTag = onDeleteSavedTag;
        _allSavedTags = savedTags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _currentTags = currentTags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _initialTags = new HashSet<string>(_currentTags, StringComparer.OrdinalIgnoreCase);

        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        MinHeight = 290;
        MinWidth = 420;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)FindResource("BgPrimary");
        Foreground = (Brush)FindResource("TextPrimary");
        FontFamily = new FontFamily("Segoe UI, Arial, sans-serif");

        bool isApplying = title.StartsWith("Aplicar", StringComparison.OrdinalIgnoreCase);

        var root = new StackPanel { Margin = new Thickness(20) };

        // Header
        root.Children.Add(new TextBlock
        {
            Text = selectedCount > 1
                ? $"{(isApplying ? "Aplicar" : "Editar")} tags ({selectedCount} itens selecionados)"
                : (isApplying ? "Aplicar tags no item" : "Editar tags do item"),
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextPrimary"),
            Margin = new Thickness(0, 0, 0, 12)
        });

        // Input Row
        var inputGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _inputBox = new TextBox
        {
            Height = 32,
            MaxLength = 160,
            Style = (Style)FindResource("ModernTextBox"),
            ToolTip = $"Digite uma tag (máx. {ManageItemAnnotationsUseCase.MaxTagLength} caracteres) e pressione Enter"
        };
        _inputBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                TryAddInputTags();
                e.Handled = true;
            }
        };
        _inputBox.TextChanged += (_, _) => ClearValidationError();
        Grid.SetColumn(_inputBox, 0);
        inputGrid.Children.Add(_inputBox);

        var addButton = new Button
        {
            Content = "Adicionar",
            Style = (Style)FindResource("ModernSecondaryButton"),
            Height = 32,
            Padding = new Thickness(12, 0, 12, 0),
            Margin = new Thickness(8, 0, 0, 0)
        };
        addButton.Click += (_, _) => TryAddInputTags();
        Grid.SetColumn(addButton, 1);
        inputGrid.Children.Add(addButton);
        root.Children.Add(inputGrid);

        // Validation message
        _validationText = new TextBlock
        {
            Foreground = (Brush)FindResource("DangerColor"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 0, 0, 6),
            Visibility = Visibility.Collapsed
        };
        root.Children.Add(_validationText);

        // Rules hint
        root.Children.Add(new TextBlock
        {
            Text = $"Use letras, números, '-', '_' ou '.'. Máx. {ManageItemAnnotationsUseCase.MaxTagLength} caracteres por tag e até {ManageItemAnnotationsUseCase.MaxTagsPerItem} tags por item.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("TextMuted"),
            FontSize = 10,
            Margin = new Thickness(2, 0, 0, 12)
        });

        // Applied tags section
        root.Children.Add(new TextBlock
        {
            Text = "Tags aplicadas",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextSecondary"),
            Margin = new Thickness(0, 0, 0, 6)
        });

        var appliedBorder = new Border
        {
            Background = (Brush)FindResource("BgSecondary"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            MinHeight = 46,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var appliedContainer = new Grid();
        _appliedPanel = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _emptyAppliedText = new TextBlock
        {
            Text = "Nenhuma tag aplicada. Digite acima ou clique em uma sugestão abaixo.",
            FontSize = 11,
            Foreground = (Brush)FindResource("TextMuted"),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        appliedContainer.Children.Add(_appliedPanel);
        appliedContainer.Children.Add(_emptyAppliedText);
        appliedBorder.Child = appliedContainer;
        root.Children.Add(appliedBorder);

        // Suggestions section
        _suggestionsSection = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        _suggestionsSection.Children.Add(new TextBlock
        {
            Text = _onDeleteSavedTag != null
                ? "Tags cadastradas (clique para adicionar ou × para excluir do sistema)"
                : "Tags já cadastradas (clique para adicionar)",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextSecondary"),
            Margin = new Thickness(0, 0, 0, 6)
        });
        _suggestionsPanel = new WrapPanel { Orientation = Orientation.Horizontal };
        _suggestionsSection.Children.Add(_suggestionsPanel);
        root.Children.Add(_suggestionsSection);

        // Action buttons
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0)
        };

        var cancel = new Button
        {
            Content = "Cancelar",
            Style = (Style)FindResource("ModernSecondaryButton"),
            Height = 32,
            Padding = new Thickness(16, 0, 16, 0)
        };
        cancel.Click += (_, _) => DialogResult = false;
        actions.Children.Add(cancel);

        _saveButton = new Button
        {
            Content = isApplying ? "Aplicar" : "Salvar",
            Style = (Style)FindResource("ModernPrimaryButton"),
            Height = 32,
            Padding = new Thickness(18, 0, 18, 0),
            Margin = new Thickness(8, 0, 0, 0)
        };
        _saveButton.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_inputBox.Text) && !TryAddInputTags())
            {
                return;
            }
            DialogResult = true;
        };
        actions.Children.Add(_saveButton);
        root.Children.Add(actions);

        Content = root;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
        };

        Loaded += (_, _) => Keyboard.Focus(_inputBox);
        RebuildChips();
    }

    public EditTagWindow(IEnumerable<string> savedTags, string currentTag, string title = "Editar tag")
        : this(savedTags, ManageItemAnnotationsUseCase.ParseTags(currentTag), title, 1)
    {
    }

    private bool TryAddInputTags()
    {
        string raw = _inputBox.Text.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            ShowValidationError("Digite uma tag para adicionar.");
            return false;
        }

        var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            ShowValidationError("Digite uma tag válida.");
            return false;
        }

        foreach (string part in parts)
        {
            if (!ManageItemAnnotationsUseCase.TryValidateSingleTag(part, out string validTag, out string? error))
            {
                ShowValidationError(error ?? "Tag inválida.");
                return false;
            }

            if (_currentTags.Contains(validTag, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (_currentTags.Count >= ManageItemAnnotationsUseCase.MaxTagsPerItem)
            {
                ShowValidationError($"Limite máximo de {ManageItemAnnotationsUseCase.MaxTagsPerItem} tags por item atingido.");
                return false;
            }

            AddTagInternal(validTag);
        }

        _inputBox.Clear();
        ClearValidationError();
        RebuildChips();
        return true;
    }

    private void AddTagInternal(string tag)
    {
        if (!_currentTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            _currentTags.Add(tag);
        }
        _removedTags.Remove(tag);
        _addedTags.Add(tag);
    }

    private void RemoveTagInternal(string tag)
    {
        _currentTags.RemoveAll(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
        _addedTags.Remove(tag);
        if (_initialTags.Contains(tag))
        {
            _removedTags.Add(tag);
        }
        ClearValidationError();
        RebuildChips();
    }

    private void RebuildChips()
    {
        _appliedPanel.Children.Clear();
        foreach (string tag in _currentTags)
        {
            _appliedPanel.Children.Add(CreateAppliedChip(tag));
        }
        _emptyAppliedText.Visibility = _currentTags.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        _suggestionsPanel.Children.Clear();
        var availableSuggestions = _allSavedTags
            .Where(t => !_currentTags.Contains(t, StringComparer.OrdinalIgnoreCase))
            .ToList();

        foreach (string suggestion in availableSuggestions)
        {
            _suggestionsPanel.Children.Add(CreateSuggestionChip(suggestion));
        }
        _suggestionsSection.Visibility = availableSuggestions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Border CreateAppliedChip(string tag)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("SuccessSurface"),
            BorderBrush = (Brush)FindResource("SuccessBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 4, 3),
            Margin = new Thickness(0, 0, 6, 6)
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock
        {
            Text = tag,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("SuccessColor"),
            VerticalAlignment = VerticalAlignment.Center
        });

        var removeBtn = new Button
        {
            Content = "×",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextSecondary"),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(5, 0, 3, 1),
            Margin = new Thickness(2, 0, 0, 0),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = $"Remover tag '{tag}'"
        };
        removeBtn.Click += (_, _) => RemoveTagInternal(tag);
        panel.Children.Add(removeBtn);

        border.Child = panel;
        return border;
    }

    private Border CreateSuggestionChip(string tag)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("SurfaceInset"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 4, 2),
            Margin = new Thickness(0, 0, 6, 6)
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var addBtn = new Button
        {
            Content = $"+ {tag}",
            FontSize = 11,
            Foreground = (Brush)FindResource("TextPrimary"),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(2, 1, 4, 1),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = $"Adicionar tag '{tag}' na seleção"
        };
        addBtn.Click += (_, _) =>
        {
            if (_currentTags.Count >= ManageItemAnnotationsUseCase.MaxTagsPerItem)
            {
                ShowValidationError($"Limite máximo de {ManageItemAnnotationsUseCase.MaxTagsPerItem} tags por item atingido.");
                return;
            }
            AddTagInternal(tag);
            ClearValidationError();
            RebuildChips();
        };
        panel.Children.Add(addBtn);

        if (_onDeleteSavedTag != null)
        {
            var deleteBtn = new Button
            {
                Content = "×",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("DangerColor"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 0, 2, 1),
                Margin = new Thickness(2, 0, 0, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = $"Excluir a tag '{tag}' de todos os registros do sistema"
            };
            deleteBtn.Click += (_, _) =>
            {
                var confirm = MessageBox.Show(
                    this,
                    $"Deseja excluir a tag '{tag}' permanentemente de todos os registros do sistema?",
                    "Excluir tag",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes) return;

                _onDeleteSavedTag(tag);
                _allSavedTags.RemoveAll(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
                _currentTags.RemoveAll(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
                _addedTags.Remove(tag);
                _removedTags.Remove(tag);
                _initialTags.Remove(tag);
                RebuildChips();
            };
            panel.Children.Add(deleteBtn);
        }

        border.Child = panel;
        return border;
    }

    private void ShowValidationError(string message)
    {
        _validationText.Text = message;
        _validationText.Visibility = Visibility.Visible;
    }

    private void ClearValidationError()
    {
        _validationText.Text = string.Empty;
        _validationText.Visibility = Visibility.Collapsed;
    }
}
