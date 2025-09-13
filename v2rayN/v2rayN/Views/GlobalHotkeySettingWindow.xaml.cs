using System.Reactive.Disposables;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ReactiveUI;
using v2rayN.Manager;

namespace v2rayN.Views;

public partial class GlobalHotkeySettingWindow
{
    private readonly List<object> _textBoxKeyEventItem = new();

    public GlobalHotkeySettingWindow()
    {
        InitializeComponent();

        this.Owner = Application.Current.MainWindow;

        ViewModel = new GlobalHotkeySettingViewModel(UpdateViewHandler);

        btnReset.Click += btnReset_Click;

        HotkeyManager.Instance.IsPause = true;
        this.Closing += (s, e) => HotkeyManager.Instance.IsPause = false;

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });
        WindowsUtils.SetDarkBorder(this, AppManager.Instance.Config.UiItem.CurrentTheme);

        Init();
        BindingData();
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.CloseWindow:
                this.DialogResult = true;
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
            txtBox.PreviewKeyDown += TxtGlobalHotkey_PreviewKeyDown;
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

        item.KeyCode = (int)(e.Key == Key.System ? (modifierKeys.Contains(e.SystemKey) ? Key.None : e.SystemKey) : (modifierKeys.Contains(e.Key) ? Key.None : e.Key));
        item.Alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
        item.Control = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        item.Shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

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
            res.Append($"{ModifierKeys.Control} +");
        }

        if (item.Shift)
        {
            res.Append($"{ModifierKeys.Shift} +");
        }

        if (item.Alt)
        {
            res.Append($"{ModifierKeys.Alt} +");
        }

        if (item.KeyCode != null && (Key)item.KeyCode != Key.None)
        {
            res.Append($"{(Key)item.KeyCode}");
        }

        return res.ToString();
    }
}
