using Avalonia;
using Avalonia.ReactiveUI;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop;

internal class Program
{
    public static EventWaitHandle ProgramStarted;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        OnStartup(args);

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static void OnStartup(string[]? Args)
    {
        if (Utils.IsWindows())
        {
            var exePathKey = Utils.GetMd5(Utils.GetExePath());
            var rebootas = (Args ?? Array.Empty<string>()).Any(t => t == Global.RebootAs);
            ProgramStarted = new EventWaitHandle(false, EventResetMode.AutoReset, exePathKey, out bool bCreatedNew);
            if (!rebootas && !bCreatedNew)
            {
                ProgramStarted.Set();
                Environment.Exit(0);
                return;
            }
        }
        else
        {
            _ = new Mutex(true, "v2rayN", out var bOnlyOneInstance);
            if (!bOnlyOneInstance)
            {
                Environment.Exit(0);
                return;
            }
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        //.WithInterFont()
        .WithFontByDefault()
        .LogToTrace()
        .UseReactiveUI()
        .With(new MacOSPlatformOptions { ShowInDock = false});
}