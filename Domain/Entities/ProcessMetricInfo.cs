using System;
using System.Globalization;

namespace NetStatAnalyzer.Domain.Entities
{
    public class ProcessMetricInfo
    {
        public int PID { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string? ProcessPath { get; set; }
        public double CpuPercent { get; set; }
        public double ReadBytesPerSec { get; set; }
        public double WriteBytesPerSec { get; set; }
        public double TotalBytesPerSec => ReadBytesPerSec + WriteBytesPerSec;

        public string FormattedCpu => $"{CpuPercent.ToString("F1", CultureInfo.InvariantCulture)}%";
        public string FormattedTotalBandwidth => FormatBytesPerSec(TotalBytesPerSec);
        public string FormattedReadBandwidth => FormatBytesPerSec(ReadBytesPerSec);
        public string FormattedWriteBandwidth => FormatBytesPerSec(WriteBytesPerSec);

        public static string FormatBytesPerSec(double bytesPerSec)
        {
            if (bytesPerSec <= 0) return "0 B/s";
            if (bytesPerSec < 1024) return $"{bytesPerSec.ToString("F0", CultureInfo.InvariantCulture)} B/s";
            if (bytesPerSec < 1024 * 1024) return $"{(bytesPerSec / 1024.0).ToString("F1", CultureInfo.InvariantCulture)} KB/s";
            if (bytesPerSec < 1024 * 1024 * 1024) return $"{(bytesPerSec / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture)} MB/s";
            return $"{(bytesPerSec / (1024.0 * 1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture)} GB/s";
        }
    }
}
