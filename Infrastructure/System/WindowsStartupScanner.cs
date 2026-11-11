using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;
using NetStatAnalyzer.Domain.Policies;

namespace NetStatAnalyzer.Infrastructure.System
{
    public class WindowsStartupScanner : IStartupItemScanner
    {
        public async Task<IReadOnlyList<StartupItem>> ScanStartupItemsAsync()
        {
            return await Task.Run(() =>
            {
                var items = new List<StartupItem>();

                // 1. HKCU Run
                ScanRegistryKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU Run", items);
                // 2. HKCU RunOnce
                ScanRegistryKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "HKCU RunOnce", items);

                // 3. HKLM Run
                ScanRegistryKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM Run", items);
                // 4. HKLM RunOnce
                ScanRegistryKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "HKLM RunOnce", items);

                // 5. HKLM Wow6432Node Run
                ScanRegistryKey(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM 32-bit Run", items);

                // 6. User Startup Folder
                ScanStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Pasta Inicializar (Usuário)", items);

                // 7. Common Startup Folder
                ScanStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Pasta Inicializar (Todos)", items);

                return (IReadOnlyList<StartupItem>)items;
            });
        }

        private static void ScanRegistryKey(RegistryKey root, string subKeyPath, string locationName, List<StartupItem> items)
        {
            try
            {
                using var key = root.OpenSubKey(subKeyPath);
                if (key == null) return;

                foreach (string valName in key.GetValueNames())
                {
                    string command = key.GetValue(valName)?.ToString() ?? string.Empty;
                    var (level, reason) = ThreatDetectionPolicy.EvaluateStartupItem(valName, command, locationName);
                    var sec = FileSecurityInspector.Inspect(command);

                    if (level == ThreatLevel.Normal && !string.IsNullOrWhiteSpace(sec.ResolvedPath))
                    {
                        if (!sec.Exists)
                        {
                            level = ThreatLevel.Medium;
                            reason = "Arquivo executável referenciado não existe no disco (Registro Órfão)";
                        }
                        else if (!sec.IsSigned)
                        {
                            level = ThreatLevel.Low;
                            reason = "Executável sem assinatura digital válida (Não Homologado)";
                        }
                        else
                        {
                            reason = $"Assinado: {sec.SignerName ?? sec.CompanyName ?? "Fabricante Verificado"}";
                        }
                    }

                    items.Add(new StartupItem
                    {
                        Name = valName,
                        Command = command,
                        Location = locationName,
                        SourceType = "Registro",
                        IsEnabled = true,
                        ThreatLevel = level,
                        ThreatReason = reason,
                        ResolvedPath = sec.ResolvedPath,
                        FileExists = sec.Exists,
                        IsSigned = sec.IsSigned,
                        SignerName = sec.SignerName,
                        IsMicrosoft = sec.IsMicrosoft,
                        CompanyName = sec.CompanyName
                    });
                }
            }
            catch
            {
                // Access issues or missing key
            }
        }

        private static void ScanStartupFolder(string folderPath, string locationName, List<StartupItem> items)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) return;

                foreach (string filePath in Directory.GetFiles(folderPath))
                {
                    string fileName = Path.GetFileName(filePath);
                    if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;

                    var (level, reason) = ThreatDetectionPolicy.EvaluateStartupItem(fileName, filePath, locationName);
                    var sec = FileSecurityInspector.Inspect(filePath);

                    if (level == ThreatLevel.Normal && !string.IsNullOrWhiteSpace(sec.ResolvedPath))
                    {
                        if (!sec.Exists)
                        {
                            level = ThreatLevel.Medium;
                            reason = "Arquivo de atalho ou executável inexistente";
                        }
                        else if (!sec.IsSigned)
                        {
                            level = ThreatLevel.Low;
                            reason = "Arquivo sem assinatura digital válida";
                        }
                        else
                        {
                            reason = $"Assinado: {sec.SignerName ?? sec.CompanyName ?? "Fabricante Verificado"}";
                        }
                    }

                    items.Add(new StartupItem
                    {
                        Name = Path.GetFileNameWithoutExtension(fileName),
                        Command = filePath,
                        Location = locationName,
                        SourceType = "Pasta Inicializar",
                        IsEnabled = true,
                        ThreatLevel = level,
                        ThreatReason = reason,
                        ResolvedPath = sec.ResolvedPath,
                        FileExists = sec.Exists,
                        IsSigned = sec.IsSigned,
                        SignerName = sec.SignerName,
                        IsMicrosoft = sec.IsMicrosoft,
                        CompanyName = sec.CompanyName
                    });
                }
            }
            catch
            {
            }
        }
    }
}
