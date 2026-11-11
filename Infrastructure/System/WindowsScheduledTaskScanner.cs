using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;
using NetStatAnalyzer.Domain.Policies;

namespace NetStatAnalyzer.Infrastructure.System
{
    public class WindowsScheduledTaskScanner : IScheduledTaskScanner
    {
        public async Task<IReadOnlyList<ScheduledTaskItem>> ScanScheduledTasksAsync()
        {
            return await Task.Run(() =>
            {
                var tasks = new List<ScheduledTaskItem>();

                try
                {
                    var lines = RunSchTasks();
                    if (lines.Count < 2) return tasks;

                    var header = ParseCsvLine(lines[0]);
                    int taskNameIdx = FindColumnIndex(header, 1, "tarefa", "taskname", "task name");
                    int nextRunIdx = FindColumnIndex(header, 2, "próxima", "proxima", "next");
                    int statusIdx = FindColumnIndex(header, 3, "status", "estado");
                    int lastRunIdx = FindColumnIndex(header, 5, "última", "ultima", "last");
                    int authorIdx = FindColumnIndex(header, 7, "autor", "author");
                    int cmdIdx = FindColumnIndex(header, 8, "executada", "task to run", "action");
                    int schedTypeIdx = FindColumnIndex(header, 18, "tipo de agendamento", "schedule type");

                    var seenTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    for (int i = 1; i < lines.Count; i++)
                    {
                        var cols = ParseCsvLine(lines[i]);
                        if (cols.Count <= taskNameIdx) continue;

                        string taskFullName = GetCol(cols, taskNameIdx).Trim().Trim('"');
                        if (string.IsNullOrWhiteSpace(taskFullName)) continue;

                        string firstCol = GetCol(cols, 0).Trim().Trim('"');
                        if (firstCol.Equals("Nome do host", StringComparison.OrdinalIgnoreCase) ||
                            firstCol.Equals("HostName", StringComparison.OrdinalIgnoreCase) ||
                            taskFullName.Equals("Nome da tarefa", StringComparison.OrdinalIgnoreCase) ||
                            taskFullName.Equals("TaskName", StringComparison.OrdinalIgnoreCase) ||
                            taskFullName.Equals("Task Name", StringComparison.OrdinalIgnoreCase))
                        {
                            continue; // Skip repeated CSV headers from subfolders!
                        }

                        if (!seenTasks.Add(taskFullName))
                        {
                            continue; // Avoid duplicate rows for multi-trigger tasks
                        }

                        string nextRun = GetCol(cols, nextRunIdx);
                        string status = GetCol(cols, statusIdx);
                        string lastRun = GetCol(cols, lastRunIdx);
                        string author = GetCol(cols, authorIdx);
                        string cmd = GetCol(cols, cmdIdx);
                        string schedType = GetCol(cols, schedTypeIdx);

                        string taskPath = @"\";
                        string taskName = taskFullName;
                        int lastSlash = taskFullName.LastIndexOf('\\');
                        if (lastSlash >= 0)
                        {
                            taskPath = taskFullName.Substring(0, lastSlash + 1);
                            taskName = taskFullName.Substring(lastSlash + 1);
                        }

                        var evaluation = ThreatDetectionPolicy.EvaluateScheduledTask(
                            taskName,
                            taskPath,
                            cmd,
                            author,
                            schedType);

                        var sec = FileSecurityInspector.Inspect(cmd);
                        ThreatLevel threat = evaluation.Level;
                        string reason = evaluation.Reason;

                        if (threat == ThreatLevel.Normal && !evaluation.IsMicrosoft && !string.IsNullOrWhiteSpace(sec.ResolvedPath))
                        {
                            bool looksLikeFile = sec.ResolvedPath.Contains('\\') ||
                                                 sec.ResolvedPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                                                 sec.ResolvedPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                                                 sec.ResolvedPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                                                 sec.ResolvedPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);

                            if (looksLikeFile && !sec.Exists)
                            {
                                threat = ThreatLevel.Medium;
                                reason = "Arquivo executável da tarefa não foi encontrado no disco (Órfão)";
                            }
                            else if (sec.Exists && !sec.IsSigned)
                            {
                                threat = ThreatLevel.Low;
                                reason = "Executável de terceiro sem assinatura digital detectada";
                            }
                        }

                        tasks.Add(new ScheduledTaskItem
                        {
                            TaskName = taskName,
                            TaskPath = taskPath,
                            State = status,
                            NextRunTime = nextRun,
                            LastRunTime = lastRun,
                            Author = author,
                            TaskToRun = cmd,
                            ScheduleType = schedType,
                            IsMicrosoftTask = evaluation.IsMicrosoft || sec.IsMicrosoft,
                            ThreatLevel = threat,
                            ThreatReason = reason,
                            ResolvedPath = sec.ResolvedPath,
                            FileExists = sec.Exists,
                            IsSigned = sec.IsSigned,
                            SignerName = sec.SignerName,
                            CompanyName = sec.CompanyName
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Erro ao consultar tarefas agendadas: {ex.Message}");
                    throw new InvalidOperationException($"Não foi possível consultar as tarefas agendadas: {ex.Message}", ex);
                }

                return (IReadOnlyList<ScheduledTaskItem>)tasks;
            });
        }

        private static List<string> RunSchTasks()
        {
            var lines = new List<string>();
            try
            {
                Encoding encoding;
                try
                {
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    int oemCp = CultureInfo.CurrentCulture.TextInfo.OEMCodePage;
                    encoding = Encoding.GetEncoding(oemCp > 0 ? oemCp : 850);
                }
                catch
                {
                    encoding = Encoding.Default;
                }

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = "/query /fo CSV /v",
                        StandardOutputEncoding = encoding,
                        StandardErrorEncoding = encoding,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                string output = outputTask.GetAwaiter().GetResult();
                string error = errorTask.GetAwaiter().GetResult();

                if (process.ExitCode != 0)
                {
                    string detail = string.IsNullOrWhiteSpace(error)
                        ? $"schtasks.exe encerrou com código {process.ExitCode}."
                        : error.Trim();
                    throw new InvalidOperationException(detail);
                }

                using var reader = new StringReader(output);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        lines.Add(line);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao rodar schtasks: {ex.Message}");
                throw;
            }

            return lines;
        }

        public static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString().Trim());
            return result;
        }

        private static int FindColumnIndex(List<string> header, int fallback, params string[] keywords)
        {
            for (int i = 0; i < header.Count; i++)
            {
                string h = header[i].ToLowerInvariant();
                if (keywords.Any(k => h.Contains(k)))
                {
                    return i;
                }
            }
            return fallback < header.Count ? fallback : 0;
        }

        private static string GetCol(List<string> cols, int index)
        {
            if (index >= 0 && index < cols.Count)
            {
                return cols[index];
            }
            return string.Empty;
        }
    }
}
