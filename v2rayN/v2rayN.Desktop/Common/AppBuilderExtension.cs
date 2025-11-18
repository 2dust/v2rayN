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
        var notoSansSc = new FontFamily(Path.Combine(Global.AvaAssets, "Fonts#Noto Sans SC"));

        var fallbacks = new List<FontFallback>
        {
            new() { FontFamily = notoSansSc }
        };

        if (OperatingSystem.IsLinux())
        {
            var emojiFamily = DetectLinuxEmojiFamily();
            if (!string.IsNullOrWhiteSpace(emojiFamily))
            {
                fallbacks.Add(new FontFallback
                {
                    FontFamily = new FontFamily(emojiFamily)
                });
            }
        }

        return appBuilder.With(new FontManagerOptions
        {
            FontFallbacks = fallbacks.ToArray()
        });
    }

    private static string? DetectLinuxEmojiFamily()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                ArgumentList =
                {
                    "-c",
                    "fc-match -f \"%{family[0]}\\n\" \"emoji\" | head -n 1"
                },
                RedirectStandardOutput = true,
                RedirectStandardError = false,
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
