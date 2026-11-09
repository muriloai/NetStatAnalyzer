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

        // File and Authenticode Audit Info
        public string ResolvedPath { get; set; } = string.Empty;
        public bool FileExists { get; set; } = true;
        public bool IsSigned { get; set; }
        public string? SignerName { get; set; }
        public bool IsMicrosoft { get; set; }
        public string? CompanyName { get; set; }
    }
}
