using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace NetStatAnalyzer.Tests
{
    public class ScreenshotGeneratorTests
    {
        [Fact]
        public void GenerateScreenshots_ShouldProduceImages()
        {
            var outputDir = @"C:\GITHUB\NetStatAnalyzer\docs\images";
            Directory.CreateDirectory(outputDir);

            Exception? error = null;

            var thread = new Thread(() =>
            {
                try
                {
                    if (System.Windows.Application.Current == null)
                    {
                        new System.Windows.Application();
                    }

                    var window = new MainWindow();
                    window.Width = 1240;
                    window.Height = 780;
                    window.Show();

                    window.ViewModel.LoadAllDataAsync().GetAwaiter().GetResult();

                    Capture(window, 0, Path.Combine(outputDir, "preview-main.png"));
                    Capture(window, 1, Path.Combine(outputDir, "preview-processes.png"));
                    Capture(window, 2, Path.Combine(outputDir, "preview-scheduled-tasks.png"));
                    Capture(window, 3, Path.Combine(outputDir, "preview-startup-items.png"));
                    Capture(window, 4, Path.Combine(outputDir, "preview-threat-insights.png"));

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
        }

        private static void Capture(MainWindow window, int tabIndex, string filePath)
        {
            window.SwitchTab(tabIndex);

            var size = new Size(1240, 780);
            window.Measure(size);
            window.Arrange(new Rect(new Point(0, 0), size));
            window.UpdateLayout();

            var rtb = new RenderTargetBitmap(1240, 780, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(window);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            encoder.Save(fs);
        }
    }
}
