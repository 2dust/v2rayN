using System;
using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;

namespace v2rayN.Helper;

public static class FlagImageHelper
{
    private static readonly ConcurrentDictionary<string, BitmapImage> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string _flagsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Flags");

    public static BitmapImage? GetFlagImage(string? countryCode)
    {
        if (string.IsNullOrEmpty(countryCode) || countryCode.Length != 2)
        {
            return null;
        }

        var key = countryCode.ToUpperInvariant();
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var pngPath = Path.Combine(_flagsDir, $"{key}.png");
        if (!File.Exists(pngPath))
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(pngPath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            _cache[key] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
