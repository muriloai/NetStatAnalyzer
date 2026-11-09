using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace NetStatAnalyzer.Infrastructure.System
{
    public class FileSecurityInfo
    {
        public string ResolvedPath { get; set; } = string.Empty;
        public bool Exists { get; set; }
        public bool IsSigned { get; set; }
        public string? SignerName { get; set; }
        public bool IsMicrosoft { get; set; }
        public string? CompanyName { get; set; }
    }

    public static class FileSecurityInspector
    {
        public static string ExtractExecutablePath(string rawCommand)
        {
            if (string.IsNullOrWhiteSpace(rawCommand)) return string.Empty;

            string trimmed = rawCommand.Trim();
            if (trimmed.StartsWith("Manipulador COM", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("COM handler", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("COM", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            string expanded = Environment.ExpandEnvironmentVariables(trimmed);

            // 1. Quoted string: "C:\Program Files\App\app.exe" -arg
            if (expanded.StartsWith("\""))
            {
                int endQuote = expanded.IndexOf('"', 1);
                if (endQuote > 1)
                {
                    string quotedPath = expanded.Substring(1, endQuote - 1);
                    return quotedPath;
                }
            }

            // 2. If entire string is an existing file
            if (File.Exists(expanded))
            {
                return expanded;
            }

            // 3. Search for .exe extension
            int exeIdx = expanded.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIdx > 0)
            {
                string candidate = expanded.Substring(0, exeIdx + 4).Trim('"', ' ');
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            // 4. Split by space
            int firstSpace = expanded.IndexOf(' ');
            if (firstSpace > 0)
            {
                string firstToken = expanded.Substring(0, firstSpace).Trim('"', ' ');
                if (File.Exists(firstToken))
                {
                    return firstToken;
                }

                // If just "cmd.exe" or "powershell.exe" without full path
                string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                string candidateSys = Path.Combine(systemDir, firstToken);
                if (File.Exists(candidateSys))
                {
                    return candidateSys;
                }

                return firstToken;
            }

            return expanded.Trim('"', ' ');
        }

        public static FileSecurityInfo Inspect(string rawCommand)
        {
            string path = ExtractExecutablePath(rawCommand);
            var info = new FileSecurityInfo { ResolvedPath = path };

            if (string.IsNullOrWhiteSpace(path))
            {
                info.Exists = false;
                return info;
            }

            // Check if relative system binary like notepad.exe or cmd.exe
            if (!File.Exists(path) && !path.Contains(Path.DirectorySeparatorChar.ToString()))
            {
                string sysPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), path);
                if (File.Exists(sysPath)) path = sysPath;
                else
                {
                    string winPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), path);
                    if (File.Exists(winPath)) path = winPath;
                }
            }

            info.ResolvedPath = path;
            info.Exists = File.Exists(path);

            if (!info.Exists)
            {
                return info;
            }

            // Read FileVersionInfo
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(path);
                info.CompanyName = vi.CompanyName;
            }
            catch
            {
                // Ignore version info errors
            }

            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            bool isUnderWindowsDir = path.StartsWith(winDir, StringComparison.OrdinalIgnoreCase);

            // 1. Try embedded Authenticode digital signature
            try
            {
                using var cert = X509Certificate.CreateFromSignedFile(path);
                using var cert2 = new X509Certificate2(cert);

                string subject = cert2.GetNameInfo(X509NameType.SimpleName, false);
                info.IsSigned = true;
                info.SignerName = !string.IsNullOrWhiteSpace(subject) ? subject : cert2.Subject;

                if ((info.SignerName != null && info.SignerName.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)) ||
                    (info.CompanyName != null && info.CompanyName.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)))
                {
                    info.IsMicrosoft = true;
                }
                return info;
            }
            catch
            {
                // No embedded Authenticode signature
            }

            // 2. Check catalog-signed Windows OS binary (in System32 / Windows)
            if (isUnderWindowsDir && info.CompanyName != null && info.CompanyName.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
            {
                info.IsSigned = true;
                info.IsMicrosoft = true;
                info.SignerName = "Microsoft Windows";
                return info;
            }

            info.IsSigned = false;
            info.SignerName = null;
            return info;
        }
    }
}
