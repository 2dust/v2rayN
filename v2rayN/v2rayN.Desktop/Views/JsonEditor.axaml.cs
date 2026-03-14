using System.Text.Json;
using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace v2rayN.Desktop.Views;

public partial class JsonEditor : UserControl
{
    private static readonly JsonSerializerOptions SIndentedOptions = new() { WriteIndented = true };

    private static readonly Lazy<IHighlightingDefinition> SHighlightingDark =
        new(() => BuildHighlighting(dark: true), isThreadSafe: true);

    private static readonly Lazy<IHighlightingDefinition> SHighlightingLight =
        new(() => BuildHighlighting(dark: false), isThreadSafe: true);

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<JsonEditor, string>(nameof(Text), defaultValue: string.Empty);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public JsonEditor()
    {
        InitializeComponent();
        var isDark = Application.Current?.ActualThemeVariant != ThemeVariant.Light;
        Editor.SyntaxHighlighting = isDark ? SHighlightingDark.Value : SHighlightingLight.Value;
        Editor.TextArea.TextView.Options.EnableHyperlinks = false;

        Editor.TextChanged += (_, _) =>
        {
            if (Text != Editor.Text)
            {
                SetCurrentValue(TextProperty, Editor.Text);
            }
        };

        this.GetObservable(TextProperty).Subscribe(text =>
        {
            if (Editor.Text != text)
            {
                Editor.Text = text ?? string.Empty;
            }
        });
    }

    private static IHighlightingDefinition BuildHighlighting(bool dark)
    {
        var keyColor = dark ? "#9CDCFE" : "#0451A5";
        var strColor = dark ? "#CE9178" : "#A31515";
        var numColor = dark ? "#B5CEA8" : "#098658";
        var kwColor  = dark ? "#569CD6" : "#0000FF";
        var xshd = $"""
            <?xml version="1.0"?>
            <SyntaxDefinition name="JSON" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
              <Color name="Key" foreground="{keyColor}" />
              <Color name="String" foreground="{strColor}" />
              <Color name="Number" foreground="{numColor}" />
              <Color name="Keyword" foreground="{kwColor}" fontWeight="bold" />
              <RuleSet>
                <Rule color="Key">"([^"\\]|\\.)*"(?=\s*:)</Rule>
                <Rule color="String">"([^"\\]|\\.)*"</Rule>
                <Rule color="Number">-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?</Rule>
                <Keywords color="Keyword">
                  <Word>true</Word>
                  <Word>false</Word>
                  <Word>null</Word>
                </Keywords>
              </RuleSet>
            </SyntaxDefinition>
            """;
        using var reader = XmlReader.Create(new StringReader(xshd));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    private void FormatJson_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var obj = JsonUtils.ParseJson(Editor.Text);
            Editor.Text = JsonUtils.Serialize(obj, SIndentedOptions);
        }
        catch
        {
            // ignored
        }
    }

    private void Copy_Click(object? sender, RoutedEventArgs e) => Editor.Copy();
    private void Paste_Click(object? sender, RoutedEventArgs e) => Editor.Paste();
    private void SelectAll_Click(object? sender, RoutedEventArgs e) => Editor.SelectAll();
}
