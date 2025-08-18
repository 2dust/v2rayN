using System.Reactive.Disposables;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReactiveUI;
using v2rayN.Desktop.Base;
using v2rayN.Desktop.Manager;

namespace v2rayN.Desktop.Views;

public partial class GlobalHotkeySettingWindow : WindowBase<GlobalHotkeySettingViewModel>
{
    private readonly List<object> _textBoxKeyEventItem = new();

    public GlobalHotkeySettingWindow()
    {
        InitializeComponent();

        ViewModel = new GlobalHotkeySettingViewModel(UpdateViewHandler);

        btnReset.Click += btnReset_Click;

        HotkeyManager.Instance.IsPause = true;
        this.Closing += (s, e) => HotkeyManager.Instance.IsPause = false;
        btnCancel.Click += (s, e) => this.Close();

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });

        Init();
        BindingData();
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.CloseWindow:
                this.Close(true);
                break;
        }
        return await Task.FromResult(true);
    }

    private void Init()
    {
        _textBoxKeyEventItem.Add(txtGlobalHotkey0);
        _textBoxKeyEventItem.Add(txtGlobalHotkey1);
        _textBoxKeyEventItem.Add(txtGlobalHotkey2);
        _textBoxKeyEventItem.Add(txtGlobalHotkey3);
        _textBoxKeyEventItem.Add(txtGlobalHotkey4);

        for (var index = 0; index < _textBoxKeyEventItem.Count; index++)
        {
            var sender = _textBoxKeyEventItem[index];
            if (sender is not TextBox txtBox)
            {
                continue;
            }
            txtBox.Tag = (EGlobalHotkey)index;
            txtBox.KeyDown += TxtGlobalHotkey_PreviewKeyDown;
        }
    }

    private void TxtGlobalHotkey_PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (sender is not TextBox txtBox)
        {
            return;
        }

        var item = ViewModel?.GetKeyEventItem((EGlobalHotkey)txtBox.Tag);
        var modifierKeys = new Key[] { Key.LeftCtrl, Key.RightCtrl, Key.LeftShift, Key.RightShift, Key.LeftAlt, Key.RightAlt, Key.LWin, Key.RWin };

        item.KeyCode = (int)(e.Key == Key.System ? modifierKeys.Contains(Key.System) ? Key.None : Key.System : modifierKeys.Contains(e.Key) ? Key.None : e.Key);
        item.Alt = (e.KeyModifiers & KeyModifiers.Alt) == KeyModifiers.Alt;
        item.Control = (e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control;
        item.Shift = (e.KeyModifiers & KeyModifiers.Shift) == KeyModifiers.Shift;

        txtBox.Text = KeyEventItemToString(item);
    }

    private void BindingData()
    {
        foreach (var sender in _textBoxKeyEventItem)
        {
            if (sender is not TextBox txtBox)
            {
                continue;
            }

            var item = ViewModel?.GetKeyEventItem((EGlobalHotkey)txtBox.Tag);
            txtBox.Text = KeyEventItemToString(item);
        }
    }

    private void btnReset_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.ResetKeyEventItem();
        BindingData();
    }

    private string KeyEventItemToString(KeyEventItem? item)
    {
        if (item == null)
        {
            return string.Empty;
        }
        var res = new StringBuilder();

        if (item.Control)
        {
            res.Append($"{KeyModifiers.Control} +");
        }

        if (item.Shift)
        {
            res.Append($"{KeyModifiers.Shift} +");
        }

        if (item.Alt)
        {
            res.Append($"{KeyModifiers.Alt} +");
        }

        if (item.KeyCode != null && (Key)item.KeyCode != Key.None)
        {
            res.Append($"{(Key)item.KeyCode}");
        }

        return res.ToString();
    }
}
