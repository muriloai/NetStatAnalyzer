using System.Windows;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;
using NetStatAnalyzer.Domain.Policies;
using NetStatAnalyzer.Presentation.Common;

namespace NetStatAnalyzer.Presentation.ViewModels;

public class WindowsServiceItemViewModel : ViewModelBase
{
    public WindowsServiceItem Model { get; }
    private readonly Action<WindowsServiceItemViewModel, bool>? _onPinnedChanged;

    public string ServiceName => Model.ServiceName;
    public string DisplayName => Model.DisplayName;
    public string Description => Model.Description;
    public WindowsServiceState State => Model.State;
    public WindowsServiceStartType StartType => Model.StartType;
    public int ProcessId => Model.ProcessId;
    public string BinaryPathName => Model.BinaryPathName;
    public string ResolvedPath => Model.ResolvedPath;
    public string ServiceAccount => Model.ServiceAccount;
    public bool AcceptsStop => Model.AcceptsStop;
    public bool AcceptsPauseAndContinue => Model.AcceptsPauseAndContinue;
    public bool FileExists => Model.FileExists;
    public bool IsSigned => Model.IsSigned;
    public bool IsMicrosoft => Model.IsMicrosoft;
    public string? SignerName => Model.SignerName;
    public string? CompanyName => Model.CompanyName;
    public ThreatLevel ThreatLevel => Model.ThreatLevel;
    public string ThreatReason => Model.ThreatReason;

    public bool IsPinned
    {
        get => Model.IsPinned;
        set
        {
            if (Model.IsPinned != value)
            {
                Model.IsPinned = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PinnedStatusText));
                OnPropertyChanged(nameof(PinnedToolTip));
                _onPinnedChanged?.Invoke(this, value);
            }
        }
    }

    public string PinnedStatusText => IsPinned ? "Fixado" : "Não fixado";
    public string PinnedToolTip => IsPinned
        ? "Serviço marcado: mantido de forma fixa na aba 'Meus Serviços Fixados'."
        : "Clique para fixar este serviço na aba 'Meus Serviços Fixados'.";

    public string StateDisplay => State switch
    {
        WindowsServiceState.Running => "Em execução",
        WindowsServiceState.Stopped => "Parado",
        WindowsServiceState.Paused => "Pausado",
        WindowsServiceState.StartPending => "Iniciando...",
        WindowsServiceState.StopPending => "Parando...",
        WindowsServiceState.PausePending => "Pausando...",
        WindowsServiceState.ContinuePending => "Continuando...",
        WindowsServiceState.NotInstalled => "Não encontrado",
        _ => "Desconhecido"
    };

    public string StateBadgeBackground => State switch
    {
        WindowsServiceState.Running => "#1F4036", // SuccessSurface
        WindowsServiceState.Stopped => "#2B2B2B", // Muted surface
        WindowsServiceState.Paused => "#2A180E",  // WarningSurface
        WindowsServiceState.StartPending or WindowsServiceState.ContinuePending => "#14374A",
        WindowsServiceState.StopPending or WindowsServiceState.PausePending => "#432727",
        WindowsServiceState.NotInstalled => "#432727",
        _ => "#242424"
    };

    public string StateBadgeForeground => State switch
    {
        WindowsServiceState.Running => "#83C9A6", // SuccessColor
        WindowsServiceState.Stopped => "#9CA3AF", // TextMuted
        WindowsServiceState.Paused => "#FB923C",  // WarningColor
        WindowsServiceState.StartPending or WindowsServiceState.ContinuePending => "#7DD3FC",
        WindowsServiceState.StopPending or WindowsServiceState.PausePending => "#FCA5A5",
        WindowsServiceState.NotInstalled => "#D98A8A",
        _ => "#999999"
    };

    public string StateBadgeBorder => State switch
    {
        WindowsServiceState.Running => "#4C9E78",
        WindowsServiceState.Stopped => "#464646",
        WindowsServiceState.Paused => "#EA580C",
        WindowsServiceState.NotInstalled => "#AB6262",
        _ => "#464646"
    };

    public string StartTypeDisplay => StartType switch
    {
        WindowsServiceStartType.Automatic => "Automático",
        WindowsServiceStartType.AutomaticDelayed => "Automático (Atraso)",
        WindowsServiceStartType.Manual => "Manual",
        WindowsServiceStartType.Disabled => "Desativado",
        WindowsServiceStartType.Boot => "Inicialização (Boot)",
        WindowsServiceStartType.System => "Sistema (Kernel)",
        _ => "Desconhecido"
    };

    public string DisplayPid => ProcessId > 0 ? ProcessId.ToString() : "—";

    public bool CanStart => WindowsServiceActionPolicy.CanExecute(Model, WindowsServiceAction.Start);
    public bool CanStopAction => WindowsServiceActionPolicy.CanExecute(Model, WindowsServiceAction.Stop);
    public bool CanPauseAction => WindowsServiceActionPolicy.CanExecute(Model, WindowsServiceAction.Pause);
    public bool CanContinueAction => WindowsServiceActionPolicy.CanExecute(Model, WindowsServiceAction.Continue);
    public bool CanRestartAction => WindowsServiceActionPolicy.CanExecute(Model, WindowsServiceAction.Restart);

    public string ThreatBadgeText => ThreatLevel switch
    {
        ThreatLevel.Critical => "🚨 CRÍTICO",
        ThreatLevel.High => "⚠️ ALTO RISCO",
        ThreatLevel.Medium when !FileExists && State != WindowsServiceState.NotInstalled => "❓ ÓRFÃO",
        ThreatLevel.Medium => "⚠️ SUSPEITO",
        ThreatLevel.Low when !IsSigned && !IsMicrosoft => "⚠️ NÃO ASSINADO",
        ThreatLevel.Low => "ℹ️ ATENÇÃO",
        _ when IsMicrosoft => "🛡️ SISTEMA",
        _ when IsSigned => "🛡️ ASSINADO",
        _ => "ℹ️ NORMAL"
    };

    public string ThreatBadgeBackground => ThreatLevel switch
    {
        ThreatLevel.Critical => "#7F1D1D",
        ThreatLevel.High => "#991B1B",
        ThreatLevel.Medium when !FileExists && State != WindowsServiceState.NotInstalled => "#9A3412",
        ThreatLevel.Medium => "#C2410C",
        ThreatLevel.Low when !IsSigned && !IsMicrosoft => "#B45309",
        ThreatLevel.Low => "#1E3A8A",
        _ when IsMicrosoft => "#064E3B",
        _ when IsSigned => "#0C3B5E",
        _ => "#1F2937"
    };

    public string ThreatBadgeForeground => ThreatLevel switch
    {
        ThreatLevel.Critical or ThreatLevel.High => "#FCA5A5",
        ThreatLevel.Medium when !FileExists && State != WindowsServiceState.NotInstalled => "#FED7AA",
        ThreatLevel.Medium => "#FED7AA",
        ThreatLevel.Low when !IsSigned && !IsMicrosoft => "#FDE68A",
        _ when IsMicrosoft => "#6EE7B7",
        _ when IsSigned => "#7DD3FC",
        _ => "#9CA3AF"
    };

    public string ThreatBadgeToolTip => ThreatLevel switch
    {
        ThreatLevel.Critical or ThreatLevel.High => ThreatReason,
        ThreatLevel.Medium when !FileExists && State != WindowsServiceState.NotInstalled => "Arquivo executável do serviço não encontrado no disco (Órfão)",
        ThreatLevel.Medium => ThreatReason,
        ThreatLevel.Low when !IsSigned && !IsMicrosoft => "Executável de terceiro sem assinatura digital detectada",
        ThreatLevel.Low => ThreatReason,
        _ when IsMicrosoft => "Componente oficial do sistema operacional Windows (Microsoft)",
        _ when IsSigned => !string.IsNullOrWhiteSpace(SignerName) ? $"Assinado digitalmente por: {SignerName}" : "Executável de terceiro com assinatura digital válida",
        _ => "Serviço em conformidade"
    };

    public Visibility ThreatBadgeVisibility => Visibility.Visible;
    public Visibility ThreatReasonVisibility => string.IsNullOrWhiteSpace(ThreatReason) ? Visibility.Collapsed : Visibility.Visible;

    public WindowsServiceItemViewModel(WindowsServiceItem model, Action<WindowsServiceItemViewModel, bool>? onPinnedChanged = null)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _onPinnedChanged = onPinnedChanged;
        TagText = model.Tag;
        AnnotationTrusted = model.IsTrusted;
    }

    public void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(StateDisplay));
        OnPropertyChanged(nameof(StateBadgeBackground));
        OnPropertyChanged(nameof(StateBadgeForeground));
        OnPropertyChanged(nameof(StateBadgeBorder));
        OnPropertyChanged(nameof(ProcessId));
        OnPropertyChanged(nameof(DisplayPid));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStopAction));
        OnPropertyChanged(nameof(CanPauseAction));
        OnPropertyChanged(nameof(CanContinueAction));
        OnPropertyChanged(nameof(CanRestartAction));
    }
}
