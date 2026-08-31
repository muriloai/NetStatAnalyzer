namespace NetStatAnalyzer.Application.DTOs
{
    public class RawSocketInfo
    {
        public string Protocol { get; set; } = string.Empty;
        public string LocalAddress { get; set; } = string.Empty;
        public string ForeignAddress { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public int PID { get; set; }
    }
}
