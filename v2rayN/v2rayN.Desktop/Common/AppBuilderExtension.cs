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

        string? zhPath = RunFcMatch("sans:lang=zh-cn");
        if (zhPath != null)
        {
            var (dir, family) = ConvertPathToFontFamily(zhPath);
            if (dir != null && family != null)
            {
                fallbacks.Add(new FontFallback
                {
                    FontFamily = new FontFamily($"file://{dir}#{family}")
                });
            }
        }

        string? emojiPath = RunFcMatch("emoji");
        if (emojiPath != null)
        {
            var (dir, family) = ConvertPathToFontFamily(emojiPath);
            if (dir != null && family != null)
            {
                fallbacks.Add(new FontFallback
                {
                    FontFamily = new FontFamily($"file://{dir}#{family}")
                });
            }
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

    private static string? RunFcMatch(string pattern)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                ArgumentList = { "-c", $"fc-match -f \"%{{file}}\\n\" \"{pattern}\" | head -n 1" },
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using var p = Process.Start(psi);
            if (p == null) return null;

            var output = p.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }

    private static (string? Dir, string? Family) ConvertPathToFontFamily(string filePath)
    {
        try
        {
            string dir = Path.GetDirectoryName(filePath)!;
            string file = Path.GetFileNameWithoutExtension(filePath);

            if (file.StartsWith("NotoColorEmoji", StringComparison.OrdinalIgnoreCase))
                return (dir, "Noto Color Emoji");

            if (file.StartsWith("NotoEmoji", StringComparison.OrdinalIgnoreCase))
                return (dir, "Noto Emoji");

            if (file.StartsWith("NotoSansCJK", StringComparison.OrdinalIgnoreCase))
                return (dir, "Noto Sans CJK SC");

            if (file.StartsWith("DroidSansFallbackFull", StringComparison.OrdinalIgnoreCase))
                return (dir, "Droid Sans Fallback");

            return (dir, file);
        }
        catch
        {
            return (null, null);
        }
    }
}
