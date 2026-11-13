using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NetStatAnalyzer.Presentation.Windows;

public sealed class EditTagWindow : Window
{
    private readonly ComboBox _tagBox;

    public string SelectedTag => _tagBox.Text.Trim();

    public EditTagWindow(IEnumerable<string> savedTags, string currentTag)
    {
        Title = "Editar tags";
        Width = 390;
        SizeToContent = SizeToContent.Height;
        MinHeight = 215;
        MinWidth = 350;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)FindResource("BgPrimary");
        Foreground = (Brush)FindResource("TextPrimary");
        FontFamily = new FontFamily("Segoe UI");

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = "Tag da seleção",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        });

        _tagBox = new ComboBox
        {
            IsEditable = true,
            IsTextSearchEnabled = true,
            Height = 32,
            ItemsSource = savedTags,
            Text = currentTag,
            Style = (Style)FindResource("ModernComboBox")
        };
        panel.Children.Add(_tagBox);
        var tips = new StackPanel { Margin = new Thickness(2, 6, 0, 0) };
        tips.Children.Add(new TextBlock
        {
            Text = "•  Selecione uma tag salva ou digite uma nova.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("TextMuted"),
            FontSize = 10
        });
        tips.Children.Add(new TextBlock
        {
            Text = "•  Deixe vazio para remover a tag dos itens selecionados.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("TextMuted"),
            FontSize = 10,
            Margin = new Thickness(0, 2, 0, 0)
        });
        panel.Children.Add(tips);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 22, 0, 0)
        };
        var cancel = new Button { Content = "Cancelar", Style = (Style)FindResource("ModernSecondaryButton") };
        cancel.Click += (_, _) => DialogResult = false;
        actions.Children.Add(cancel);
        var save = new Button
        {
            Content = "Aplicar",
            Style = (Style)FindResource("ModernPrimaryButton"),
            Margin = new Thickness(8, 0, 0, 0)
        };
        save.Click += (_, _) => DialogResult = true;
        actions.Children.Add(save);
        panel.Children.Add(actions);
        Content = panel;

        Loaded += (_, _) => Keyboard.Focus(_tagBox);
    }
}
