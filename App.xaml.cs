using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace NetStatAnalyzer
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                LogAndShowError("Unhandled AppDomain Exception", args.ExceptionObject as Exception);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                LogAndShowError("Unhandled Dispatcher Exception", args.Exception);
                args.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                LogAndShowError("Unobserved Task Exception", args.Exception);
                args.SetObserved();
            };
        }

        private static void LogAndShowError(string title, Exception? ex)
        {
            var sb = new System.Text.StringBuilder();
            var current = ex;
            while (current != null)
            {
                sb.AppendLine($"[{current.GetType().Name}] {current.Message}");
                if (!string.IsNullOrEmpty(current.StackTrace))
                {
                    sb.AppendLine(current.StackTrace);
                }
                sb.AppendLine();
                current = current.InnerException;
            }

            string fullMessage = sb.ToString();

            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] {title}:\n{fullMessage}\n--------------------\n");
            }
            catch { }

            MessageBox.Show(fullMessage, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
