using Avalonia.Controls.Documents;

namespace v2rayN.Desktop.Controls;

/// <summary>
/// Represents a single line in the application log view with an accessible ToString value.
/// </summary>
public sealed class LogLineItem(string text)
{
    public string Text { get; } = text;

    public override string ToString() => Text;
}

/// <summary>
/// TextBlock that highlights log level keywords (debug, info, warning, error) in-line.
/// </summary>
public class LogLineTextBlock : TextBlock
{
    private static readonly Dictionary<string, IBrush> SBrushMap =
        Global.LogLevelColors.ToDictionary(
            kv => kv.Key,
            kv => (IBrush)new SolidColorBrush(Color.Parse(kv.Value)),
            StringComparer.OrdinalIgnoreCase);

    private static readonly string[] SKeywords = [.. SBrushMap.Keys];

    public static readonly StyledProperty<string?> LogTextProperty =
        AvaloniaProperty.Register<LogLineTextBlock, string?>(nameof(LogText));

    static LogLineTextBlock()
    {
        LogTextProperty.Changed.AddClassHandler<LogLineTextBlock>((control, _) => control.UpdateInlines());
    }

    public LogLineTextBlock()
    {
        TextWrapping = TextWrapping.Wrap;
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    public string? LogText
    {
        get => GetValue(LogTextProperty);
        set => SetValue(LogTextProperty, value);
    }

    private void UpdateInlines()
    {
        Inlines?.Clear();
        var text = LogText;
        if (string.IsNullOrEmpty(text) || Inlines is null)
        {
            return;
        }

        var pos = 0;
        while (pos < text.Length)
        {
            var bestIdx = -1;
            string? bestKw = null;

            foreach (var kw in SKeywords)
            {
                var searchFrom = pos;
                while (searchFrom < text.Length)
                {
                    var idx = text.IndexOf(kw, searchFrom, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0)
                    {
                        break;
                    }

                    var endIdx = idx + kw.Length;
                    if (!IsWordChar(text, idx - 1) && !IsWordChar(text, endIdx))
                    {
                        if (bestIdx < 0 || idx < bestIdx || (idx == bestIdx && kw.Length > (bestKw?.Length ?? 0)))
                        {
                            bestIdx = idx;
                            bestKw = kw;
                        }
                        break;
                    }

                    searchFrom = idx + Math.Max(1, kw.Length);
                }
            }

            if (bestIdx < 0 || bestKw is null)
            {
                Inlines.Add(new Run(text[pos..]));
                break;
            }

            if (bestIdx > pos)
            {
                Inlines.Add(new Run(text[pos..bestIdx]));
            }

            var kwRun = new Run(text.Substring(bestIdx, bestKw.Length));
            if (SBrushMap.TryGetValue(bestKw, out var brush))
            {
                kwRun.Foreground = brush;
            }
            Inlines.Add(kwRun);

            pos = bestIdx + bestKw.Length;
        }
    }

    private static bool IsWordChar(string text, int index)
    {
        if (index < 0 || index >= text.Length)
        {
            return false;
        }

        var c = text[index];
        return char.IsLetterOrDigit(c) || c == '_';
    }
}
