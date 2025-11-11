using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace v2rayN.Desktop.Common;

public class KeywordColorizer : DocumentColorizingTransformer
{
    private readonly string[] _keywords;
    private readonly Dictionary<string, IBrush> _brushMap;

    public KeywordColorizer(IDictionary<string, IBrush> keywordBrushMap)
    {
        if (keywordBrushMap == null || keywordBrushMap.Count == 0)
        {
            throw new ArgumentException("keywordBrushMap must not be null or empty", nameof(keywordBrushMap));
        }

        _brushMap = new Dictionary<string, IBrush>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in keywordBrushMap)
        {
            if (string.IsNullOrEmpty(kvp.Key) || kvp.Value == null)
            {
                continue;
            }

            if (!_brushMap.ContainsKey(kvp.Key))
            {
                _brushMap[kvp.Key] = kvp.Value;
            }
        }

        if (_brushMap.Count == 0)
        {
            throw new ArgumentException("keywordBrushMap must contain at least one non-empty key with a non-null brush", nameof(keywordBrushMap));
        }

        _keywords = _brushMap.Keys.ToArray();
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        var text = CurrentContext.Document.GetText(line);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (var kw in _keywords)
        {
            if (string.IsNullOrEmpty(kw))
            {
                continue;
            }

            var searchStart = 0;
            while (true)
            {
                var idx = text.IndexOf(kw, searchStart, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                {
                    break;
                }

                var kwEndIndex = idx + kw.Length;
                if (IsWordCharBefore(text, idx) || IsWordCharAfter(text, kwEndIndex))
                {
                    searchStart = idx + Math.Max(1, kw.Length);
                    continue;
                }

                var start = line.Offset + idx;
                var end = start + kw.Length;

                if (_brushMap.TryGetValue(kw, out var brush) && brush != null)
                {
                    ChangeLinePart(start, end, element => element.TextRunProperties.SetForegroundBrush(brush));
                }

                searchStart = idx + Math.Max(1, kw.Length);
            }
        }
    }

    private static bool IsWordCharBefore(string text, int idx)
    {
        if (idx <= 0)
        {
            return false;
        }

        var c = text[idx - 1];
        return char.IsLetterOrDigit(c) || c == '_';
    }

    private static bool IsWordCharAfter(string text, int idx)
    {
        if (idx >= text.Length)
        {
            return false;
        }

        var c = text[idx];
        return char.IsLetterOrDigit(c) || c == '_';
    }
}

public static class TextEditorKeywordHighlighter
{
    public static void Attach(TextEditor editor, IDictionary<string, IBrush> keywordBrushMap)
    {
        ArgumentNullException.ThrowIfNull(editor);

        if (keywordBrushMap == null || keywordBrushMap.Count == 0)
        {
            return;
        }

        if (editor.TextArea?.TextView?.LineTransformers?.OfType<KeywordColorizer>().Any() == true)
        {
            return;
        }

        var colorizer = new KeywordColorizer(keywordBrushMap);
        editor.TextArea.TextView.LineTransformers.Add(colorizer);
        editor.TextArea.TextView.InvalidateVisual();
    }
}
