namespace v2rayN.Desktop.Common;

/// <summary>
/// Linux workaround for https://github.com/2dust/v2rayN/issues/10112
///
/// v2rayN deterministically crashes (SIGSEGV inside libfontconfig) on some Linux setups
/// (Deepin 25 + fontconfig 2.17.1 reproduced; likely any distro rule set that routes CJK text
/// through styled match/prepare paths such as synthetic oblique/embolden). The bundled
/// "Noto Sans SC" default font covers most text, but glyph fallback for characters/emoji not
/// present in the bundle still goes through the system fontconfig, where the crash occurs.
///
/// Experiment matrix on the reporter's machine (v2rayN 7.24.9, Deepin 25, fontconfig 2.17.1):
///   system fontconfig (all distro rules) + CJK fonts     -> crash ~13-14 s after startup
///   same font dirs, no rules                             -> stable
///   minimal rules + Noto Sans CJK SC as default family   -> stable (>3 min, then hours)
///
/// This helper writes a minimal fontconfig profile (all standard font dirs, no distro rule
/// files, CJK generic families pinned to Noto CJK which ships with real Bold/Italic faces so
/// synthetic styles are not needed) and points FONTCONFIG_FILE at it. It only activates on
/// Linux and only when the user did not already set FONTCONFIG_FILE.
/// </summary>
public static class LinuxFontconfig
{
    private const string ConfigFileName = "fonts.conf";

    private const string ConfigContent = """
        <?xml version="1.0"?>
        <!DOCTYPE fontconfig SYSTEM "fonts.dtd">
        <fontconfig>
          <dir>/usr/share/fonts</dir>
          <dir>/usr/local/share/fonts</dir>
          <dir prefix="xdg">fonts</dir>
          <cachedir>/var/cache/fontconfig</cachedir>
          <cachedir prefix="xdg">fontconfig</cachedir>
          <alias><family>sans-serif</family><prefer><family>Noto Sans CJK SC</family></prefer></alias>
          <alias><family>system-ui</family><prefer><family>Noto Sans CJK SC</family></prefer></alias>
          <alias><family>serif</family><prefer><family>Noto Serif CJK SC</family></prefer></alias>
          <alias><family>monospace</family><prefer><family>Noto Sans Mono</family></prefer></alias>
        </fontconfig>
        """;

    /// <summary>
    /// Set FONTCONFIG_FILE to a safe, minimal fontconfig profile. Best effort: any failure is
    /// swallowed so the app still starts with the system fontconfig.
    /// Must run before Avalonia creates its FontManager (i.e. before BuildAvaloniaApp).
    /// </summary>
    public static void ApplyIfNeeded()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // User/distro override wins.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FONTCONFIG_FILE")))
        {
            return;
        }

        try
        {
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "v2rayN");
            var targetDir = Path.Combine(dataDir, "guiTemps");
            Directory.CreateDirectory(targetDir);
            var configFile = Path.Combine(targetDir, ConfigFileName);

            if (!File.Exists(configFile)
                || File.ReadAllText(configFile) != ConfigContent)
            {
                File.WriteAllText(configFile, ConfigContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }

            Environment.SetEnvironmentVariable("FONTCONFIG_FILE", configFile);
        }
        catch
        {
            // Best effort only; fall back to the system fontconfig.
        }
    }
}
