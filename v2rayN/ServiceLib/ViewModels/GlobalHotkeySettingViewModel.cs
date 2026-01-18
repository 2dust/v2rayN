namespace ServiceLib.ViewModels;

public class GlobalHotkeySettingViewModel : MyReactiveObject
{
    private readonly List<KeyEventItem> _globalHotkeys;

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }

    public GlobalHotkeySettingViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        Config = AppManager.Instance.Config;
        UpdateView = updateView;

        _globalHotkeys = JsonUtils.DeepCopy(Config.GlobalHotkeys);

        SaveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveSettingAsync();
        });
    }

    public KeyEventItem GetKeyEventItem(EGlobalHotkey eg)
    {
        var item = _globalHotkeys.FirstOrDefault((it) => it.EGlobalHotkey == eg);
        if (item is not null)
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
        Config.GlobalHotkeys = _globalHotkeys;

        if (await ConfigHandler.SaveConfig(Config) == 0)
        {
            UpdateView?.Invoke(EViewAction.CloseWindow, null);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }
}
