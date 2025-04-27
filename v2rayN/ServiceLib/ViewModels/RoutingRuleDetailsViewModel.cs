using System.Reactive;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace ServiceLib.ViewModels;

public class RoutingRuleDetailsViewModel : MyReactiveObject
{
    public IList<string> ProtocolItems { get; set; }
    public IList<string> InboundTagItems { get; set; }

    [Reactive]
    public RulesItem SelectedSource { get; set; }

    [Reactive]
    public string Domain { get; set; }

    [Reactive]
    public string IP { get; set; }

    [Reactive]
    public string Process { get; set; }

    [Reactive]
    public bool AutoSort { get; set; }

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }

    public RoutingRuleDetailsViewModel(RulesItem rulesItem, Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppHandler.Instance.Config;
        _updateView = updateView;

        SaveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveRulesAsync();
        });

        if (rulesItem.Id.IsNullOrEmpty())
        {
            rulesItem.Id = Utils.GetGuid(false);
            rulesItem.OutboundTag = Global.ProxyTag;
            rulesItem.Enabled = true;
            SelectedSource = rulesItem;
        }
        else
        {
            SelectedSource = rulesItem;
        }

        Domain = Utils.List2String(SelectedSource.Domain, true);
        IP = Utils.List2String(SelectedSource.Ip, true);
        Process = Utils.List2String(SelectedSource.Process, true);
    }

    private async Task SaveRulesAsync()
    {
        Domain = Utils.Convert2Comma(Domain);
        IP = Utils.Convert2Comma(IP);
        Process = Utils.Convert2Comma(Process);

        if (AutoSort)
        {
            SelectedSource.Domain = Utils.String2ListSorted(Domain);
            SelectedSource.Ip = Utils.String2ListSorted(IP);
            SelectedSource.Process = Utils.String2ListSorted(Process);
        }
        else
        {
            SelectedSource.Domain = Utils.String2List(Domain);
            SelectedSource.Ip = Utils.String2List(IP);
            SelectedSource.Process = Utils.String2List(Process);
        }
        SelectedSource.Protocol = ProtocolItems?.ToList();
        SelectedSource.InboundTag = InboundTagItems?.ToList();

        var hasRule = SelectedSource.Domain?.Count > 0
          || SelectedSource.Ip?.Count > 0
          || SelectedSource.Protocol?.Count > 0
          || SelectedSource.Process?.Count > 0
          || SelectedSource.Port.IsNotEmpty()
          || SelectedSource.Network.IsNotEmpty();

        if (!hasRule)
        {
            NoticeHandler.Instance.Enqueue(string.Format(ResUI.RoutingRuleDetailRequiredTips, "Network/Port/Protocol/Domain/IP/Process"));
            return;
        }
        //NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
        await _updateView?.Invoke(EViewAction.CloseWindow, null);
    }
}
