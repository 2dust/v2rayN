namespace ServiceLib.ViewModels;

public partial class AddGroupServerViewModel : MyReactiveObject
{
    [Reactive]
    private ProfileItem _selectedSource;

    [Reactive]
    private ProfileItem _selectedChild;

    // [Reactive]
    public IList<ProfileItem> SelectedChildren { get; set; }

    [Reactive]
    private string? _coreType;

    [Reactive]
    private string? _policyGroupType;

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

        ProfileGroupItemManager.Instance.TryGet(profileItem.IndexId, out var profileGroup);
        PolicyGroupType = (profileGroup?.MultipleLoad ?? EMultipleLoad.LeastPing) switch
        {
            EMultipleLoad.LeastPing => ResUI.TbLeastPing,
            EMultipleLoad.Fallback => ResUI.TbFallback,
            EMultipleLoad.Random => ResUI.TbRandom,
            EMultipleLoad.RoundRobin => ResUI.TbRoundRobin,
            EMultipleLoad.LeastLoad => ResUI.TbLeastLoad,
            _ => ResUI.TbLeastPing,
        };

        _ = Init();
    }

    public async Task Init()
    {
        var childItemMulti = ProfileGroupItemManager.Instance.GetOrCreateAndMarkDirty(SelectedSource?.IndexId);
        if (childItemMulti != null)
        {
            var childIndexIds = childItemMulti.ChildItems.IsNullOrEmpty() ? new List<string>() : Utils.String2List(childItemMulti.ChildItems);
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
        if (ChildItemsObs.Count == 0)
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
        var childIndexIds = new List<string>();
        foreach (var item in ChildItemsObs)
        {
            if (item.IndexId.IsNullOrEmpty())
            {
                continue;
            }
            childIndexIds.Add(item.IndexId);
        }
        var profileGroup = ProfileGroupItemManager.Instance.GetOrCreateAndMarkDirty(SelectedSource.IndexId);
        profileGroup.ChildItems = Utils.List2String(childIndexIds);
        profileGroup.MultipleLoad = PolicyGroupType switch
        {
            var s when s == ResUI.TbLeastPing => EMultipleLoad.LeastPing,
            var s when s == ResUI.TbFallback => EMultipleLoad.Fallback,
            var s when s == ResUI.TbRandom => EMultipleLoad.Random,
            var s when s == ResUI.TbRoundRobin => EMultipleLoad.RoundRobin,
            var s when s == ResUI.TbLeastLoad => EMultipleLoad.LeastLoad,
            _ => EMultipleLoad.LeastPing,
        };

        var hasCycle = ProfileGroupItemManager.HasCycle(profileGroup.IndexId);
        if (hasCycle)
        {
            NoticeManager.Instance.Enqueue(string.Format(ResUI.GroupSelfReference, remarks));
            return;
        }

        if (await ConfigHandler.AddGroupServerCommon(_config, SelectedSource, profileGroup, true) == 0)
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
