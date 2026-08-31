using System;
using System.Collections.Generic;
using System.Linq;
using NetStatAnalyzer.Domain.Entities;

namespace NetStatAnalyzer.Domain.Policies
{
    public static class TrustEvaluationPolicy
    {
        public static string ExtractIP(string? address)
        {
            if (string.IsNullOrWhiteSpace(address)) return string.Empty;
            address = address.Trim();

            if (address == "*:*" || address == "*.*" || address == "0.0.0.0:0" || address == "[::]:0")
            {
                return address;
            }

            if (address.StartsWith("[") && address.Contains("]"))
            {
                int endBracket = address.IndexOf(']');
                return address.Substring(1, endBracket - 1);
            }

            int colonIndex = address.LastIndexOf(':');
            if (colonIndex > 0 && !address.Contains(":::"))
            {
                return address.Substring(0, colonIndex);
            }

            return address;
        }

        public static bool IsWildcardOrListeningAddress(string? address)
        {
            if (string.IsNullOrWhiteSpace(address)) return true;
            string trimmed = address.Trim();
            return trimmed == "0.0.0.0:0" ||
                   trimmed == "*:*" ||
                   trimmed == "*.*" ||
                   trimmed == "0.0.0.0" ||
                   trimmed == "[::]:0" ||
                   trimmed == "[::]" ||
                   trimmed == "::";
        }

        public static bool IsTrusted(
            string processName,
            string foreignAddress,
            string localAddress,
            IEnumerable<AllowlistRule> rules,
            string protocol = "")
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;

            bool isListeningOrWildcard = IsWildcardOrListeningAddress(foreignAddress);
            string foreignIP = ExtractIP(foreignAddress);

            return rules.Any(r =>
            {
                if (!r.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (isListeningOrWildcard)
                {
                    return !string.IsNullOrEmpty(r.LocalAddress) &&
                           r.LocalAddress.Equals(localAddress, StringComparison.OrdinalIgnoreCase);
                }

                if (!string.IsNullOrEmpty(r.ForeignAddress) && !IsWildcardOrListeningAddress(r.ForeignAddress))
                {
                    if (r.ForeignAddress.Equals(foreignAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                if (!string.IsNullOrEmpty(foreignIP) && !IsWildcardOrListeningAddress(foreignIP))
                {
                    if (!string.IsNullOrEmpty(r.IP) && !IsWildcardOrListeningAddress(r.IP) &&
                        r.IP.Equals(foreignIP, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            });
        }
    }
}
