using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace NetStatAnalyzer.Presentation.Services
{
    public class ProcessIconCache
    {
        private static readonly Lazy<ProcessIconCache> _instance = new(() => new ProcessIconCache());
        public static ProcessIconCache Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, BitmapImage?> _cache = new(StringComparer.OrdinalIgnoreCase);

        public BitmapImage? GetIcon(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            return _cache.GetOrAdd(filePath, path =>
            {
                try
                {
                    using var icon = Icon.ExtractAssociatedIcon(path);
                    if (icon == null) return null;

                    using var bitmap = icon.ToBitmap();
                    using var memoryStream = new MemoryStream();
                    bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                    memoryStream.Seek(0, SeekOrigin.Begin);

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memoryStream;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    return bitmapImage;
                }
                catch
                {
                    return null;
                }
            });
        }
    }
}
