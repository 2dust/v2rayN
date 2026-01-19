namespace ServiceLib.ViewModels;

public class AddGroupServerViewModel : MyReactiveObject
{
    [Reactive]
    public ProfileItem SelectedSource { get; set; }

    [Reactive]
    public ProfileItem SelectedChild { get; set; }

    [Reactive]
    public IList<ProfileItem> SelectedChildren { get; set; }

    [Reactive]
    public string? CoreType { get; set; }

    [Reactive]
    public string? PolicyGroupType { get; set; }

    [Reactive]
    public SubItem? SelectedSubItem { get; set; }

    [Reactive]
    public string? Filter { get; set; }

    public IObservableCollection<SubItem> SubItems { get; } = new ObservableCollectionExtended<SubItem>();

    public IObservableCollection<ProfileItem> ChildItemsObs { get; } = new ObservableCollectionExtended<ProfileItem>();

    //public ReactiveCommand<Unit, Unit> AddCmd { get; }
    public ReactiveCommand<Unit, Unit> RemoveCmd { get; }

    public ReactiveCommand<Unit, Unit> MoveTopCmd { get; }
    public ReactiveCommand<Unit, Unit> MoveUpCmd { get; }
    public ReactiveCommand<Unit, Unit> MoveDownCmd { get; }
    public ReactiveCommand<Unit, Unit> MoveBottomCmd { get; }

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }

    public AddGroupServerViewModel(ProfileItem profileItem, Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;

        var canEditRemove = this.WhenAnyValue(
            x => x.SelectedChild,
            SelectedChild => SelectedChild != null && !SelectedChild.Remarks.IsNullOrEmpty());

        RemoveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ChildRemoveAsync();
        }, canEditRemove);
        MoveTopCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Top);
        }, canEditRemove);
        MoveUpCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Up);
        }, canEditRemove);
        MoveDownCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Down);
        }, canEditRemove);
        MoveBottomCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Bottom);
        }, canEditRemove);
        SaveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveServerAsync();
        });

        SelectedSource = profileItem.IndexId.IsNullOrEmpty() ? profileItem : JsonUtils.DeepCopy(profileItem);
        CoreType = (SelectedSource?.CoreType ?? ECoreType.Xray).ToString();

        _ = Init();
    }

    public async Task Init()
    {
        var protocolExtra = SelectedSource.GetProtocolExtra();
        PolicyGroupType = (protocolExtra?.MultipleLoad ?? EMultipleLoad.LeastPing) switch
        {
            EMultipleLoad.LeastPing => ResUI.TbLeastPing,
            EMultipleLoad.Fallback => ResUI.TbFallback,
            EMultipleLoad.Random => ResUI.TbRandom,
            EMultipleLoad.RoundRobin => ResUI.TbRoundRobin,
            EMultipleLoad.LeastLoad => ResUI.TbLeastLoad,
            _ => ResUI.TbLeastPing,
        };

        var subs = await AppManager.Instance.SubItems();
        subs.Add(new SubItem());
        SubItems.AddRange(subs);
        SelectedSubItem = SubItems.FirstOrDefault(s => s.Id == protocolExtra?.SubChildItems);
        Filter = protocolExtra?.Filter;

        var childIndexIds = Utils.String2List(protocolExtra?.ChildItems) ?? [];
        foreach (var item in childIndexIds)
        {
            var child = await AppManager.Instance.GetProfileItem(item);
            if (child == null)
            {
                continue;
            }
            ChildItemsObs.Add(child);
        }
    }

    public async Task ChildRemoveAsync()
    {
        if (SelectedChild == null || SelectedChild.IndexId.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }
        foreach (var it in SelectedChildren ?? [SelectedChild])
        {
            if (it != null)
            {
                ChildItemsObs.Remove(it);
            }
        }
        await Task.CompletedTask;
    }

    public async Task MoveServer(EMove eMove)
    {
        if (SelectedChild == null || SelectedChild.IndexId.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }
        var index = ChildItemsObs.IndexOf(SelectedChild);
        if (index < 0)
        {
            return;
        }
        var selectedChild = JsonUtils.DeepCopy(SelectedChild);
        switch (eMove)
        {
            case EMove.Top:
                if (index == 0)
                {
                    return;
                }
                ChildItemsObs.RemoveAt(index);
                ChildItemsObs.Insert(0, selectedChild);
                break;

            case EMove.Up:
                if (index == 0)
                {
                    return;
                }
                ChildItemsObs.RemoveAt(index);
                ChildItemsObs.Insert(index - 1, selectedChild);
                break;

            case EMove.Down:
                if (index == ChildItemsObs.Count - 1)
                {
                    return;
                }
                ChildItemsObs.RemoveAt(index);
                ChildItemsObs.Insert(index + 1, selectedChild);
                break;

            case EMove.Bottom:
                if (index == ChildItemsObs.Count - 1)
                {
                    return;
                }
                ChildItemsObs.RemoveAt(index);
                ChildItemsObs.Add(selectedChild);
                break;

            default:
                break;
        }
        await Task.CompletedTask;
    }

    private async Task SaveServerAsync()
    {
        var remarks = SelectedSource.Remarks;
        if (remarks.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseFillRemarks);
            return;
        }
        if (ChildItemsObs.Count == 0 && SelectedSubItem?.Id.IsNullOrEmpty() == true)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseAddAtLeastOneServer);
            return;
        }
        SelectedSource.CoreType = CoreType.IsNullOrEmpty() ? ECoreType.Xray : (ECoreType)Enum.Parse(typeof(ECoreType), CoreType);
        if (SelectedSource.CoreType is not (ECoreType.Xray or ECoreType.sing_box) ||
            SelectedSource.ConfigType is not (EConfigType.ProxyChain or EConfigType.PolicyGroup))
        {
            return;
        }

        var protocolExtra = SelectedSource.GetProtocolExtra();
        protocolExtra.ChildItems =
            Utils.List2String(ChildItemsObs.Where(s => !s.IndexId.IsNullOrEmpty()).Select(s => s.IndexId).ToList());
        protocolExtra.MultipleLoad = PolicyGroupType switch
        {
            var s when s == ResUI.TbLeastPing => EMultipleLoad.LeastPing,
            var s when s == ResUI.TbFallback => EMultipleLoad.Fallback,
            var s when s == ResUI.TbRandom => EMultipleLoad.Random,
            var s when s == ResUI.TbRoundRobin => EMultipleLoad.RoundRobin,
            var s when s == ResUI.TbLeastLoad => EMultipleLoad.LeastLoad,
            _ => EMultipleLoad.LeastPing,
        };

        protocolExtra.SubChildItems = SelectedSubItem?.Id;
        protocolExtra.Filter = Filter;

        var hasCycle = await GroupProfileManager.HasCycle(SelectedSource.IndexId, protocolExtra);
        if (hasCycle)
        {
            NoticeManager.Instance.Enqueue(string.Format(ResUI.GroupSelfReference, remarks));
            return;
        }

        SelectedSource.SetProtocolExtra(protocolExtra);

        if (await ConfigHandler.AddServerCommon(_config, SelectedSource) == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
            _updateView?.Invoke(EViewAction.CloseWindow, null);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }
}
