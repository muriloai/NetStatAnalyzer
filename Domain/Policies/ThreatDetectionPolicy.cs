using System;
using System.IO;
using System.Linq;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;

namespace NetStatAnalyzer.Domain.Policies
{
    public static class ThreatDetectionPolicy
    {
        // Known proxy monetization / residential bandwidth sharing tools
        private static readonly string[] ProxyMonetizationKeywords =
        {
            "worldproxypool", "packetstream", "honeygain", "earnapp",
            "peer2profit", "traffmonetizer", "pawns", "nanomid",
            "mysterium", "iproyal", "repocket", "proxygen", "proxypool"
        };

        // Known distributed computing / crypto mining payloads
        private static readonly string[] DistributedComputingKeywords =
        {
            "charityengine", "boinc", "xmrig", "minerd", "ethminer",
            "cryptonight", "cpuminer", "nicehash", "stratum", "silentminer"
        };

        // Known PUPs, executors, activators, auto-clickers
        private static readonly string[] PupKeywords =
        {
            "advanced windows manager", "autoclicker", "auto-clicker",
            "robloxexecutor", "synapse", "krnl", "fluxus", "kmspico", "kmsauto"
        };

        public static (ThreatLevel Level, string Reason) EvaluateProcess(
            string processName,
            string? processPath,
            double cpuPercent,
            double bandwidthBytesPerSec,
            int activeConnectionsCount)
        {
            string nameLower = processName.ToLowerInvariant();
            string pathLower = (processPath ?? string.Empty).ToLowerInvariant();

            // 1. Check known residential proxy / bandwidth monetization
            if (ProxyMonetizationKeywords.Any(k => nameLower.Contains(k) || pathLower.Contains(k)))
            {
                return (ThreatLevel.Critical, "Compartilhamento não autorizado de banda (Proxy Residencial detectado)");
            }

            // 2. Check known distributed computing / crypto miner
            if (DistributedComputingKeywords.Any(k => nameLower.Contains(k) || pathLower.Contains(k)))
            {
                return (ThreatLevel.Critical, "Computação distribuída / Mineração em segundo plano detectada");
            }

            // 3. Check known PUPs / unwanted tools
            if (PupKeywords.Any(k => nameLower.Contains(k) || pathLower.Contains(k)))
            {
                return (ThreatLevel.High, "Utilitário indesejado / Injetor de scripts ou ativador não autorizado");
            }

            // 4. Abnormal network bandwidth spike (> 2 MB/s)
            if (bandwidthBytesPerSec > 2.0 * 1024 * 1024)
            {
                return (ThreatLevel.Medium, $"Pico anormal de uso de rede ({ProcessMetricInfo.FormatBytesPerSec(bandwidthBytesPerSec)})");
            }

            // 5. Unusually high number of connections (proxy / scanner behavior)
            if (activeConnectionsCount >= 20)
            {
                return (ThreatLevel.Medium, $"Alto volume de conexões ativas simultâneas ({activeConnectionsCount} conexões)");
            }

            // 6. Abnormally high CPU usage (> 35%)
            if (cpuPercent >= 35.0)
            {
                return (ThreatLevel.Low, $"Carga elevada de CPU ({cpuPercent:F1}%)");
            }

            return (ThreatLevel.Normal, string.Empty);
        }

        public static (ThreatLevel Level, string Reason, bool IsMicrosoft) EvaluateScheduledTask(
            string taskName,
            string taskPath,
            string taskToRun,
            string author,
            string scheduleType)
        {
            string lowerName = taskName.ToLowerInvariant();
            string lowerPath = taskPath.ToLowerInvariant();
            string lowerCmd = (taskToRun ?? string.Empty).ToLowerInvariant();
            string lowerAuthor = (author ?? string.Empty).ToLowerInvariant();

            bool isMicrosoft = lowerPath.StartsWith(@"\microsoft\windows") ||
                               lowerAuthor.Contains("microsoft") ||
                               lowerCmd.Contains(@"\system32\");

            // 1. Check Proxy / Miner / PUP keywords
            if (ProxyMonetizationKeywords.Any(k => lowerName.Contains(k) || lowerCmd.Contains(k)))
            {
                return (ThreatLevel.Critical, "Tarefa agendada associada a compartilhamento de banda (proxy)", false);
            }

            if (DistributedComputingKeywords.Any(k => lowerName.Contains(k) || lowerCmd.Contains(k)))
            {
                return (ThreatLevel.Critical, "Tarefa agendada associada a computação distribuída/mineração", false);
            }

            if (PupKeywords.Any(k => lowerName.Contains(k) || lowerCmd.Contains(k)))
            {
                return (ThreatLevel.High, "Tarefa vinculada a utilitário suspeito/indesejado", false);
            }

            // 2. Hidden PowerShell script execution (only flag if non-Microsoft)
            if (!isMicrosoft && lowerCmd.Contains("powershell") && (lowerCmd.Contains("-w hidden") || lowerCmd.Contains("-windowstyle hidden") || lowerCmd.Contains("-enc")))
            {
                return (ThreatLevel.Critical, "Monitor PowerShell oculto em execução furtiva", false);
            }

            // 3. Running executables/scripts from Temp or User AppData
            if (lowerCmd.Contains(@"\appdata\local\temp") || lowerCmd.Contains(@"\temp\"))
            {
                return (ThreatLevel.High, "Executável ou script sendo disparado a partir da pasta temporária (Temp)", false);
            }

            // 4. Non-Microsoft task in root directory
            if (!isMicrosoft && (lowerPath == @"\" || string.IsNullOrWhiteSpace(lowerPath)))
            {
                return (ThreatLevel.Medium, "Tarefa de terceiro criada na raiz do Agendador do Windows", false);
            }

            return (ThreatLevel.Normal, string.Empty, isMicrosoft);
        }

        public static (ThreatLevel Level, string Reason) EvaluateStartupItem(string name, string command, string location)
        {
            string lowerName = name.ToLowerInvariant();
            string lowerCmd = (command ?? string.Empty).ToLowerInvariant();

            // 1. Check known threats
            if (ProxyMonetizationKeywords.Any(k => lowerName.Contains(k) || lowerCmd.Contains(k)))
            {
                return (ThreatLevel.Critical, "Inicialização automática de Proxy Residencial detectada");
            }

            if (DistributedComputingKeywords.Any(k => lowerName.Contains(k) || lowerCmd.Contains(k)))
            {
                return (ThreatLevel.Critical, "Inicialização automática de Mineração / Computação Externa detectada");
            }

            if (PupKeywords.Any(k => lowerName.Contains(k) || lowerCmd.Contains(k)))
            {
                return (ThreatLevel.High, "Inicialização automática de aplicativo potencialmente indesejado (PUP)");
            }

            // 2. Suspicious scripts in startup
            if (lowerCmd.EndsWith(".vbs") || lowerCmd.EndsWith(".bat") || lowerCmd.EndsWith(".cmd") || lowerCmd.EndsWith(".ps1") ||
                lowerCmd.Contains("powershell.exe -w hidden") || lowerCmd.Contains("wscript.exe"))
            {
                return (ThreatLevel.High, "Script de inicialização furtivo (VBS/PowerShell/Batch)");
            }

            // 3. Executable in Temp or roaming AppData
            if (lowerCmd.Contains(@"\appdata\local\temp") || lowerCmd.Contains(@"\temp\"))
            {
                return (ThreatLevel.High, "Item de inicialização localizado em diretório Temporário");
            }

            return (ThreatLevel.Normal, string.Empty);
        }
    }
}
