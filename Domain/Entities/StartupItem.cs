using NetStatAnalyzer.Domain.Enums;

namespace NetStatAnalyzer.Domain.Entities
{
    public class StartupItem
    {
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string SourceType { get; set; } = "Registro";
        public bool IsEnabled { get; set; } = true;
        public ThreatLevel ThreatLevel { get; set; } = ThreatLevel.Normal;
        public string ThreatReason { get; set; } = string.Empty;
    }
}
