namespace NetStatAnalyzer.Domain.Enums
{
    public enum ConnectionState
    {
        Unknown,
        Established,
        Listening,
        TimeWait,
        CloseWait,
        SynSent,
        SynReceived,
        FinWait1,
        FinWait2,
        Closing,
        LastAck,
        NotApplicable
    }
}
