using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NetStatAnalyzer.Presentation.Windows;

public sealed class SettingsWindow : Window
{
    private readonly CheckBox _chkVerifyHash;
    private readonly ComboBox _themeBox;
    private readonly Button _btnSave;
    private readonly Button _btnCancel;

    private readonly bool _initialVerifyHash;
    private readonly string _initialTheme;

    public bool VerifyHashResult => _chkVerifyHash.IsChecked == true;
    public string ThemeResult => _themeBox.SelectedIndex == 1 ? "Light" : "Dark";

    public SettingsWindow(bool currentVerifyHash, string currentTheme)
    {
        _initialVerifyHash = currentVerifyHash;
        _initialTheme = string.Equals(currentTheme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";

        Title = "Configurações";
        Width = 470;
        SizeToContent = SizeToContent.Height;
        MinHeight = 320;
        MinWidth = 420;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)FindResource("BgPrimary");
        Foreground = (Brush)FindResource("TextPrimary");
        FontFamily = new FontFamily("Segoe UI, Arial, sans-serif");

        var root = new StackPanel { Margin = new Thickness(22) };

        // Header
        root.Children.Add(new TextBlock
        {
            Text = "⚙ Configurações",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimary"),
            Margin = new Thickness(0, 0, 0, 4)
        });

        root.Children.Add(new TextBlock
        {
            Text = "Defina as preferências de integridade e aparência do NetStatAnalyzer.",
            FontSize = 12,
            Foreground = (Brush)FindResource("TextMuted"),
            Margin = new Thickness(0, 0, 0, 16)
        });

        // Grupo 1: Segurança e Integridade
        var secBorder = new Border
        {
            Background = (Brush)FindResource("BgSecondary"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var secPanel = new StackPanel();
        secPanel.Children.Add(new TextBlock
        {
            Text = "Segurança e Confiança",
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = (Brush)FindResource("TextPrimary"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        _chkVerifyHash = new CheckBox
        {
            Content = "Exigir verificação de hash SHA-256 ao aprovar conexões e processos",
            IsChecked = _initialVerifyHash,
            FontWeight = FontWeights.Medium,
            Foreground = (Brush)FindResource("TextPrimary"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        _chkVerifyHash.Checked += (_, _) => UpdateDirtyState();
        _chkVerifyHash.Unchecked += (_, _) => UpdateDirtyState();
        secPanel.Children.Add(_chkVerifyHash);

        var hashTips = new StackPanel { Margin = new Thickness(20, 0, 0, 0) };
        hashTips.Children.Add(new TextBlock
        {
            Text = "Valida a integridade do executável via hash SHA-256. Se o arquivo for alterado, substituído ou atualizado em disco, a confiança é suspensa automaticamente para nova validação.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("TextMuted"),
            FontSize = 11,
            LineHeight = 16
        });
        secPanel.Children.Add(hashTips);
        secBorder.Child = secPanel;
        root.Children.Add(secBorder);

        // Grupo 2: Aparência
        var appBorder = new Border
        {
            Background = (Brush)FindResource("BgSecondary"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 18)
        };

        var appPanel = new StackPanel();
        appPanel.Children.Add(new TextBlock
        {
            Text = "Aparência e Tema",
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = (Brush)FindResource("TextPrimary"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        _themeBox = new ComboBox
        {
            Height = 32,
            Style = (Style)FindResource("ModernComboBox")
        };
        _themeBox.Items.Add(new ComboBoxItem { Content = "Tema escuro (Dark)" });
        _themeBox.Items.Add(new ComboBoxItem { Content = "Tema claro (Light)" });
        _themeBox.SelectedIndex = _initialTheme == "Light" ? 1 : 0;
        _themeBox.SelectionChanged += (_, _) => UpdateDirtyState();
        appPanel.Children.Add(_themeBox);
        appBorder.Child = appPanel;
        root.Children.Add(appBorder);

        // Ações (Botões que ficam disponíveis somente quando há mudanças)
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0)
        };

        _btnCancel = new Button
        {
            Content = "Cancelar",
            Style = (Style)FindResource("ModernSecondaryButton"),
            IsEnabled = false,
            Padding = new Thickness(16, 0, 16, 0),
            Height = 32
        };
        _btnCancel.Click += (_, _) => DialogResult = false;
        actions.Children.Add(_btnCancel);

        _btnSave = new Button
        {
            Content = "Salvar",
            Style = (Style)FindResource("ModernPrimaryButton"),
            IsEnabled = false,
            Padding = new Thickness(18, 0, 18, 0),
            Height = 32,
            Margin = new Thickness(8, 0, 0, 0)
        };
        _btnSave.Click += (_, _) => DialogResult = true;
        actions.Children.Add(_btnSave);

        root.Children.Add(actions);
        Content = root;

        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
        };

        UpdateDirtyState();
    }

    private void UpdateDirtyState()
    {
        if (_chkVerifyHash == null || _themeBox == null || _btnSave == null || _btnCancel == null) return;

        bool hashChanged = (_chkVerifyHash.IsChecked == true) != _initialVerifyHash;
        bool themeChanged = (_themeBox.SelectedIndex == 1 ? "Light" : "Dark") != _initialTheme;
        bool hasChanges = hashChanged || themeChanged;

        _btnSave.IsEnabled = hasChanges;
        _btnCancel.IsEnabled = hasChanges;
    }
}
