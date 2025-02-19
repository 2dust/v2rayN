using Avalonia.Input;
using Avalonia.Interactivity;

namespace v2rayN.Desktop.Controls;

public class AutoCompleteBox : Avalonia.Controls.AutoCompleteBox
{
    static AutoCompleteBox()
    {
        MinimumPrefixLengthProperty.OverrideDefaultValue<AutoCompleteBox>(0);
    }

    public AutoCompleteBox()
    {
        AddHandler(PointerPressedEvent, OnBoxPointerPressed, RoutingStrategies.Tunnel);
    }

    private void OnBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Equals(sender, this) && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            SetCurrentValue(IsDropDownOpenProperty, true);
        }
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        if (IsDropDownOpen)
            return;
        SetCurrentValue(IsDropDownOpenProperty, true);
    }

    public void Clear()
    {
        SetCurrentValue(SelectedItemProperty, null);
    }
}
