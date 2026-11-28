using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using v2rayN.Desktop.Views;

namespace v2rayN.Desktop.Controls;

/// <summary>
/// AvaloniaEdit TextEditor subclass exposing UI Automation metadata on its inner TextArea
/// and allowing Tab/Shift+Tab to move focus to neighboring controls.
/// </summary>
public class AccessibleTextEditor : TextEditor
{
    private string _lastValue = string.Empty;

    public AccessibleTextEditor() : base(new AccessibleTextArea())
    {
        // Avoid Shift+Tab bouncing back into TextEditor and re-focusing TextArea.
        IsTabStop = false;

        if (TextArea is AccessibleTextArea area)
        {
            area.OwnerEditor = this;
        }

        RemoveTabKeyBindings(TextArea.DefaultInputHandler);
        RemoveTabKeyBindings(TextArea.DefaultInputHandler.Editing);
        RemoveTabKeyBindings(TextArea.DefaultInputHandler.CaretNavigation);

        this.GetObservable(AutomationProperties.NameProperty).Subscribe(name =>
        {
            AutomationProperties.SetName(TextArea, name);
        });

        this.GetObservable(AutomationProperties.HelpTextProperty).Subscribe(helpText =>
        {
            AutomationProperties.SetHelpText(TextArea, helpText);
        });

        TextChanged += (_, _) =>
        {
            var current = Text ?? string.Empty;
            if (current != _lastValue)
            {
                var previous = _lastValue;
                _lastValue = current;
                ControlAutomationPeer.FromElement(TextArea)?.RaisePropertyChangedEvent(
                    ValuePatternIdentifiers.ValueProperty, previous, current);
            }
        };
    }

    protected override Type StyleKeyOverride => typeof(TextEditor);

    private static void RemoveTabKeyBindings(TextAreaInputHandler? handler)
    {
        if (handler is null)
        {
            return;
        }

        var tabBindings = handler.KeyBindings.Where(kb => kb.Gesture.Key == Key.Tab).ToList();
        foreach (var binding in tabBindings)
        {
            handler.KeyBindings.Remove(binding);
        }
    }
}

/// <summary>
/// TextArea subclass providing an accessible Edit automation peer and non-trapping Tab navigation.
/// </summary>
public class AccessibleTextArea : TextArea
{
    internal AccessibleTextEditor? OwnerEditor { get; set; }

    protected override Type StyleKeyOverride => typeof(TextArea);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Tab && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            return;
        }

        base.OnKeyDown(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessibleTextAreaAutomationPeer(this);
}

/// <summary>
/// AutomationPeer for AccessibleTextArea exposing Edit role, inherited label/help metadata, and ValuePattern.
/// </summary>
public class AccessibleTextAreaAutomationPeer(AccessibleTextArea owner) : ControlAutomationPeer(owner), IValueProvider
{
    private readonly AccessibleTextArea _area = owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override string? GetNameCore()
    {
        var baseName = base.GetNameCore();
        if (!string.IsNullOrEmpty(baseName))
        {
            return baseName;
        }

        var editorName = GetNameFromControl(_area.OwnerEditor);
        if (!string.IsNullOrEmpty(editorName))
        {
            return editorName;
        }

        var jsonEditor = _area.FindAncestorOfType<JsonEditor>();
        return GetNameFromControl(jsonEditor);
    }

    protected override string? GetHelpTextCore()
    {
        var baseHelp = base.GetHelpTextCore();
        if (!string.IsNullOrEmpty(baseHelp))
        {
            return baseHelp;
        }

        if (_area.OwnerEditor is { } editor)
        {
            var editorHelp = AutomationProperties.GetHelpText(editor);
            if (!string.IsNullOrEmpty(editorHelp))
            {
                return editorHelp;
            }
        }

        var jsonEditor = _area.FindAncestorOfType<JsonEditor>();
        return jsonEditor is not null ? AutomationProperties.GetHelpText(jsonEditor) : null;
    }

    public bool IsReadOnly => _area.OwnerEditor?.IsReadOnly ?? true;

    public string Value => _area.OwnerEditor?.Text ?? string.Empty;

    public void SetValue(string? value)
    {
        if (_area.OwnerEditor is { IsReadOnly: false } editor)
        {
            editor.Text = value ?? string.Empty;
        }
    }

    private static string? GetNameFromControl(Control? control)
    {
        if (control is null)
        {
            return null;
        }

        var name = AutomationProperties.GetName(control);
        if (!string.IsNullOrEmpty(name))
        {
            return name;
        }

        var labeledBy = AutomationProperties.GetLabeledBy(control);
        if (labeledBy is not null)
        {
            var peer = CreatePeerForElement(labeledBy);
            var labelName = peer.GetName();
            if (!string.IsNullOrEmpty(labelName))
            {
                return labelName;
            }
        }

        return null;
    }
}
