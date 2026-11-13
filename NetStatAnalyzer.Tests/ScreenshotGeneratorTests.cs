using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Presentation.ViewModels;
using Xunit;

namespace NetStatAnalyzer.Tests
{
    public class ScreenshotGeneratorTests
    {
        [Fact]
        public void GenerateScreenshots_ShouldProduceImages()
        {
            var dirInfo = new DirectoryInfo(AppContext.BaseDirectory);
            while (dirInfo != null && !File.Exists(Path.Combine(dirInfo.FullName, "NetStatAnalyzer.sln")))
            {
                dirInfo = dirInfo.Parent;
            }
            var outputDir = dirInfo != null ? Path.Combine(dirInfo.FullName, "docs", "images") : @"C:\GITHUB\NetStatAnalyzer\docs\images";
            Directory.CreateDirectory(outputDir);

            // Clean previous allowlist to start fresh
            var allowlistPath = Path.Combine(AppContext.BaseDirectory, "allowlist.json");
            if (File.Exists(allowlistPath))
            {
                try { File.Delete(allowlistPath); } catch { }
            }

            Exception? error = null;

            var thread = new Thread(() =>
            {
                try
                {
                    if (System.Windows.Application.Current == null)
                    {
                        var app = new NetStatAnalyzer.App();
                        app.InitializeComponent();
                    }

                    var window = ApplicationCompositionRoot.CreateMainWindow();
                    window.WindowStyle = WindowStyle.None;
                    window.ResizeMode = ResizeMode.NoResize;
                    window.Width = 1260;
                    window.Height = 800;
                    window.Show();

                    // Wait for initial async load to finish
                    var timeout = DateTime.UtcNow.AddSeconds(15);
                    while (window.ViewModel.IsLoading && DateTime.UtcNow < timeout)
                    {
                        var frame = new System.Windows.Threading.DispatcherFrame();
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                            System.Windows.Threading.DispatcherPriority.Background,
                            new Action(() => frame.Continue = false));
                        System.Windows.Threading.Dispatcher.PushFrame(frame);
                        Thread.Sleep(50);
                    }

                    // Populate representative trust and tags across tabs for screenshot gallery showcase
                    var sampleConnections = window.ViewModel.FilteredEntries
                        .Where(c => c.ProcessName.Equals("svchost", StringComparison.OrdinalIgnoreCase) ||
                                    c.ProcessName.Equals("System", StringComparison.OrdinalIgnoreCase))
                        .Take(4)
                        .ToList();
                    if (sampleConnections.Count > 0)
                    {
                        window.ViewModel.SetItemsTrust(sampleConnections, true);
                        window.ViewModel.SetItemTags(sampleConnections, "Sistema");
                    }

                    var sampleTasks = window.ViewModel.FilteredTasks
                        .Where(t => t.IsMicrosoftTask)
                        .Take(2)
                        .ToList();
                    if (sampleTasks.Count > 0)
                    {
                        window.ViewModel.SetItemsTrust(sampleTasks, true);
                        window.ViewModel.SetItemTags(sampleTasks, "Sistema");
                    }

                    var sampleStartup = window.ViewModel.FilteredStartupItems
                        .Where(s => s.IsMicrosoft || s.IsSigned)
                        .Take(2)
                        .ToList();
                    if (sampleStartup.Count > 0)
                    {
                        window.ViewModel.SetItemsTrust(sampleStartup, true);
                        window.ViewModel.SetItemTags(sampleStartup, "Sistema");
                    }

                    // Exclude internal test runner process so docs screenshot is production-ready
                    var testHostItem = window.ViewModel.FilteredProcesses
                        .FirstOrDefault(p => p.ProcessName.Equals("testhost", StringComparison.OrdinalIgnoreCase));
                    if (testHostItem != null)
                    {
                        window.ViewModel.FilteredProcesses.Remove(testHostItem);
                    }

                    // Populate representative trust and tags for processes tab
                    var sampleProcesses = window.ViewModel.FilteredProcesses
                        .Where(p => p.ProcessName.Equals("svchost", StringComparison.OrdinalIgnoreCase) ||
                                    p.ProcessName.Equals("System", StringComparison.OrdinalIgnoreCase))
                        .Take(2)
                        .ToList();
                    if (sampleProcesses.Count > 0)
                    {
                        window.ViewModel.SetItemsTrust(sampleProcesses, true);
                        window.ViewModel.SetItemTags(sampleProcesses, "Sistema");
                    }

                    Capture(window, 0, Path.Combine(outputDir, "preview-main.png"));
                    Capture(window, 1, Path.Combine(outputDir, "preview-processes.png"));
                    Capture(window, 2, Path.Combine(outputDir, "preview-scheduled-tasks.png"));
                    Capture(window, 3, Path.Combine(outputDir, "preview-startup-items.png"));

                    window.Close();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (error != null)
            {
                throw new Exception("Capture error: " + error.ToString());
            }

            Assert.True(File.Exists(Path.Combine(outputDir, "preview-main.png")));
            Assert.True(File.Exists(Path.Combine(outputDir, "preview-processes.png")));
            Assert.True(File.Exists(Path.Combine(outputDir, "preview-scheduled-tasks.png")));
            Assert.True(File.Exists(Path.Combine(outputDir, "preview-startup-items.png")));
        }

        private static void Capture(MainWindow window, int tabIndex, string filePath)
        {
            window.SwitchTab(tabIndex);

            if (tabIndex == 1)
            {
                var field = typeof(MainViewModel).GetField("_allProcesses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(window.ViewModel) is List<ProcessAggregateItemViewModel> allProc)
                {
                    allProc.RemoveAll(p => p.ProcessName.Equals("testhost", StringComparison.OrdinalIgnoreCase));
                }
                window.ViewModel.ApplyProcessFilters();
            }

            // Allow UI layout and visual tree to settle
            var frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);

            var size = new Size(1260, 800);
            window.Width = 1260;
            window.Height = 800;
            window.Measure(size);
            window.Arrange(new Rect(new Point(0, 0), size));
            window.UpdateLayout();

            // Render on top of a dark solid background matching the theme to prevent any white edge
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                var bgBrush = (Brush)window.FindResource("BgPrimary");
                dc.DrawRectangle(bgBrush, null, new Rect(0, 0, 1260, 800));
                dc.DrawRectangle(new VisualBrush(window), null, new Rect(0, 0, 1260, 800));
            }

            var rtb = new RenderTargetBitmap(1260, 800, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            encoder.Save(fs);
        }
    }
}
