namespace ServiceLib.ViewModels;

public class NetBridgeViewModel : MyReactiveObject
{
    [Reactive]
    public bool EnableNetBridge { get; set; }

    [Reactive]
    public string RuleProcess { get; set; }

    public ReactiveCommand<Unit, Unit> SaveRulesCmd { get; }

    public NetBridgeViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;

        SaveRulesCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveRulesAsync();
        });

        this.WhenAnyValue(x => x.EnableNetBridge)
            .Skip(1)
            .Subscribe(async enabled =>
            {
                await ToggleNetBridgeAsync(enabled);
            });

        _ = Init();
    }

    private async Task Init()
    {
        _config.NetBridgeItem ??= new()
        {
            RuleProcess = string.Empty
        };

        EnableNetBridge = false;
        RuleProcess = _config.NetBridgeItem.RuleProcess;
        if (RuleProcess.IsNullOrEmpty())
        {
            RuleProcess = "Chrome.exe";
        }

        await Task.CompletedTask;
    }

    private async Task ToggleNetBridgeAsync(bool enabled)
    {
        await NetBridgeManager.Instance.Init(UpdateViewHandler);

        if (enabled)
        {
            var succeed = await NetBridgeManager.Instance.Start();
            if (succeed)
            {
                await NetBridgeManager.Instance.UpdateProxyConfig(Global.Loopback, AppManager.Instance.GetLocalPort(EInboundProtocol.socks));
                await NetBridgeManager.Instance.UpdateRoutes(RuleProcess);
            }
            NoticeManager.Instance.Enqueue(succeed ? ResUI.OperationSuccess : ResUI.OperationFailed);
        }
        else
        {
            var succeed = await NetBridgeManager.Instance.Stop();
            NoticeManager.Instance.Enqueue(succeed ? ResUI.OperationSuccess : ResUI.OperationFailed);
        }
    }

    /// </summary>
    private async Task SaveRulesAsync()
    {
        _config.NetBridgeItem ??= new();

        var normalizedRuleProcess = RuleProcess;
        _config.NetBridgeItem.RuleProcess = normalizedRuleProcess;
        RuleProcess = normalizedRuleProcess;

        if (await ConfigHandler.SaveConfig(_config) != 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            return;
        }

        await NetBridgeManager.Instance.Init(UpdateViewHandler);
        if (EnableNetBridge)
        {
            var routesUpdated = await NetBridgeManager.Instance.UpdateRoutes(normalizedRuleProcess);
            NoticeManager.Instance.Enqueue(routesUpdated ? ResUI.OperationSuccess : ResUI.OperationFailed);
        }
    }

    private async Task<bool> UpdateViewHandler(bool isError, string msg)
    {
        NoticeManager.Instance.SendMessageEx(msg);

        return await Task.FromResult(true);
    }
}
