using System.Text.Json;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace v2rayN.Desktop.Views;

public partial class JsonEditor : UserControl
{
    private static readonly JsonSerializerOptions SIndentedOptions = new() { WriteIndented = true };

    public JsonEditor()
    {
        InitializeComponent();
        var currentTheme = Application.Current?.ActualThemeVariant;
        var themeName = currentTheme == ThemeVariant.Dark ? ThemeName.DarkPlus :
            currentTheme == ThemeVariant.Light ? ThemeName.LightPlus :
            ThemeName.DarkPlus;
        var jsonRegistryOptions = new RegistryOptions(themeName);
        var grammarScopeName = jsonRegistryOptions.GetScopeByLanguageId(jsonRegistryOptions.GetLanguageByExtension(".json").Id);
        Editor.InstallTextMate(jsonRegistryOptions).SetGrammar(grammarScopeName);
        Editor.TextArea.TextView.Options.EnableHyperlinks = false;
    }

    public string Text
    {
        get => Editor.Text;
        set => Editor.Text = value;
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
