namespace NetStatAnalyzer.Domain.Enums;

public enum WindowsServiceState
{
    Unknown = 0,
    NotInstalled = 1,
    Stopped = 2,
    StartPending = 3,
    StopPending = 4,
    Running = 5,
    ContinuePending = 6,
    PausePending = 7,
    Paused = 8
}

public enum WindowsServiceStartType
{
    Unknown = 0,
    Boot = 1,
    System = 2,
    Automatic = 3,
    AutomaticDelayed = 4,
    Manual = 5,
    Disabled = 6
}

public enum WindowsServiceAction
{
    Start = 1,
    Stop = 2,
    Pause = 3,
    Continue = 4,
    Restart = 5
}
