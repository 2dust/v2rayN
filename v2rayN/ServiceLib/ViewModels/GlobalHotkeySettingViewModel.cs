using System.Reactive;
using ReactiveUI;

namespace ServiceLib.ViewModels;

public class GlobalHotkeySettingViewModel : MyReactiveObject
{
    private readonly List<KeyEventItem> _globalHotkeys;

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }

    public GlobalHotkeySettingViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppHandler.Instance.Config;
        _updateView = updateView;

        _globalHotkeys = JsonUtils.DeepCopy(_config.GlobalHotkeys);

        SaveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveSettingAsync();
        });
    }

    public KeyEventItem GetKeyEventItem(EGlobalHotkey eg)
    {
        var item = _globalHotkeys.FirstOrDefault((it) => it.EGlobalHotkey == eg);
        if (item != null)
        {
            return item;
        }

        item = new()
        {
            EGlobalHotkey = eg,
            Control = false,
            Alt = false,
            Shift = false,
            KeyCode = null
        };
        _globalHotkeys.Add(item);

        return item;
    }

    public void ResetKeyEventItem()
    {
        _globalHotkeys.Clear();
    }

    private async Task SaveSettingAsync()
    {
        _config.GlobalHotkeys = _globalHotkeys;

        if (await ConfigHandler.SaveConfig(_config) == 0)
        {
            _updateView?.Invoke(EViewAction.CloseWindow, null);
        }
        else
        {
            NoticeHandler.Instance.Enqueue(ResUI.OperationFailed);
        }
    }
}
