using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Media;

namespace v2rayN.Desktop.Common;

public static class AppBuilderExtension
{
    public static AppBuilder WithFontByDefault(this AppBuilder appBuilder)
    {
        if (!OperatingSystem.IsLinux())
        {
            var uri = Path.Combine(Global.AvaAssets, "Fonts#Noto Sans SC");
            return appBuilder.With(new FontManagerOptions
            {
                FontFallbacks = new[]
                {
                    new FontFallback
                    {
                        FontFamily = new FontFamily(uri)
                    }
                }
            });
        }

        var fallbacks = new List<FontFallback>();

        var emojiFamily = DetectFontFamily("emoji");

        if (!string.IsNullOrWhiteSpace(emojiFamily) &&
            emojiFamily.Contains("Noto Color Emoji", StringComparison.OrdinalIgnoreCase))
        {
            fallbacks.Add(new FontFallback
            {
                FontFamily = new FontFamily(emojiFamily)
            });
        }

        var notoScUri = Path.Combine(Global.AvaAssets, "Fonts#Noto Sans SC");
        fallbacks.Add(new FontFallback
        {
            FontFamily = new FontFamily(notoScUri)
        });

        return appBuilder.With(new FontManagerOptions
        {
            FontFallbacks = fallbacks.ToArray()
        });
    }

    private static string? DetectFontFamily(string pattern)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                ArgumentList =
                {
                    "-c",
                    $"fc-match -f \"%{{family}}\\n\" \"{pattern}\" | head -n 1"
                },
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false
            };

            using var p = Process.Start(psi);
            if (p == null)
                return null;

            p.WaitForExit(2000);

            var output = p.StandardOutput.ReadToEnd();
            var family = output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
                ?.Trim();

            return string.IsNullOrWhiteSpace(family) ? null : family;
        }
        catch
        {
            return null;
        }
    }
}
