using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Media;
using System.Collections.Generic;

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
                    new FontFallback { FontFamily = new FontFamily(uri) }
                }
            });
        }

        List<FontFallback> fallbacks = new();

        string? zhFamily = RunFcMatchFamily("sans:lang=zh-cn");
        string? emojiFamily = RunFcMatchFamily("emoji");

        if (!string.IsNullOrWhiteSpace(zhFamily))
        {
            fallbacks.Add(new FontFallback
            {
                FontFamily = new FontFamily(zhFamily)
            });
        }

        if (!string.IsNullOrWhiteSpace(emojiFamily))
        {
            fallbacks.Add(new FontFallback
            {
                FontFamily = new FontFamily(emojiFamily)
            });
        }

        var localFont = Path.Combine(Global.AvaAssets, "Fonts#Noto Sans SC");
        fallbacks.Add(new FontFallback
        {
            FontFamily = new FontFamily(localFont)
        });

        return appBuilder.With(new FontManagerOptions
        {
            FontFallbacks = fallbacks.ToArray()
        });
    }

    private static string? RunFcMatchFamily(string pattern)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                ArgumentList = { "-c", $"fc-match -f \"%{{family}}\\n\" \"{pattern}\" | head -n 1" },
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using var p = Process.Start(psi);
            if (p == null) return null;

            string output = p.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }
}
