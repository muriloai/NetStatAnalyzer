using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NetStatAnalyzer.Domain.Entities
{
    public class AllowlistRule
    {
        [JsonPropertyName("processName")]
        public string ProcessName { get; set; } = string.Empty;

        [JsonPropertyName("ip")]
        public string IP { get; set; } = string.Empty;

        [JsonPropertyName("foreignAddress")]
        public string? ForeignAddress { get; set; }

        [JsonPropertyName("localAddress")]
        public string? LocalAddress { get; set; }

        [JsonPropertyName("protocol")]
        public string? Protocol { get; set; }

        [JsonPropertyName("addedAt")]
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;
    }

    public class AllowlistDocument
    {
        [JsonPropertyName("app")]
        public string App { get; set; } = "NetStatAnalyzer";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.2.0";

        [JsonPropertyName("exportedAt")]
        public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("rulesCount")]
        public int RulesCount => Rules?.Count ?? 0;

        [JsonPropertyName("rules")]
        public List<AllowlistRule> Rules { get; set; } = new();
    }
}
