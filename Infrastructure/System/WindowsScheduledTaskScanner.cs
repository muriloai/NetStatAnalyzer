using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetStatAnalyzer.Application.Contracts;
using NetStatAnalyzer.Domain.Entities;
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

                    for (int i = 1; i < lines.Count; i++)
                    {
                        var cols = ParseCsvLine(lines[i]);
                        if (cols.Count <= taskNameIdx) continue;

                        string taskFullName = GetCol(cols, taskNameIdx);
                        if (string.IsNullOrWhiteSpace(taskFullName)) continue;

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
                            IsMicrosoftTask = evaluation.IsMicrosoft,
                            ThreatLevel = evaluation.Level,
                            ThreatReason = evaluation.Reason
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Erro ao consultar tarefas agendadas: {ex.Message}");
                }

                return (IReadOnlyList<ScheduledTaskItem>)tasks;
            });
        }

        private static List<string> RunSchTasks()
        {
            var lines = new List<string>();
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = "/query /fo CSV /v",
                        StandardOutputEncoding = Encoding.Default,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

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
