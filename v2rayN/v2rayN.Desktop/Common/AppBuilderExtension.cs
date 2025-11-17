using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Avalonia;
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
                    new FontFallback { FontFamily = new FontFamily(uri) }
                }
            });
        }

        var fallbacks = new List<FontFallback>();

        var zhFamily = RunFcFamily("sans:lang=zh-cn");
        if (!string.IsNullOrWhiteSpace(zhFamily))
        {
            fallbacks.Add(new FontFallback
            {
                FontFamily = new FontFamily(zhFamily)
            });
        }

        var emojiFamily = RunFcFamily("emoji");
        if (!string.IsNullOrWhiteSpace(emojiFamily))
        {
            fallbacks.Add(new FontFallback
            {
                FontFamily = new FontFamily(emojiFamily)
            });
        }

        var notoUri = Path.Combine(Global.AvaAssets, "Fonts#Noto Sans SC");
        fallbacks.Add(new FontFallback
        {
            FontFamily = new FontFamily(notoUri)
        });

        return appBuilder.With(new FontManagerOptions
        {
            FontFallbacks = fallbacks.ToArray()
        });
    }

    private static string? RunFcFamily(string pattern)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                ArgumentList = { "-c", $"fc-match -f \"%{{family}}\\n\" \"{pattern}\" | head -n 1" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var p = Process.Start(psi);
            if (p == null)
                return null;

            var output = p.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }
}
