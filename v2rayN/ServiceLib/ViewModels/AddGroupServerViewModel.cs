using System.Reactive;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

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

        RemoveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ChildRemoveAsync();
        });
        MoveTopCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Top);
        });
        MoveUpCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Up);
        });
        MoveDownCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Down);
        });
        MoveBottomCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Bottom);
        });
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
        ChildItemsObs.Remove(SelectedChild);
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
        switch (eMove)
        {
            case EMove.Top:
                if (index == 0)
                {
                    return;
                }
                ChildItemsObs.RemoveAt(index);
                ChildItemsObs.Insert(0, SelectedChild);
                break;
            case EMove.Up:
                if (index == 0)
                {
                    return;
                }
                ChildItemsObs.RemoveAt(index);
                ChildItemsObs.Insert(index - 1, SelectedChild);
                break;
            case EMove.Down:
                if (index == ChildItemsObs.Count - 1)
                {
                    return;
                }
                ChildItemsObs.RemoveAt(index);
                ChildItemsObs.Insert(index + 1, SelectedChild);
                break;
            case EMove.Bottom:
                if (index == ChildItemsObs.Count - 1)
                {
                    return;
                }
                ChildItemsObs.RemoveAt(index);
                ChildItemsObs.Add(SelectedChild);
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
            if (item.IndexId.IsNotEmpty())
            {
                childIndexIds.Add(item.IndexId);
            }
        }
        SelectedSource.Address = Utils.List2String(childIndexIds);
        var profileGroup = ProfileGroupItemManager.Instance.GetOrCreateAndMarkDirty(SelectedSource.IndexId);
        profileGroup.ChildItems = Utils.List2String(childIndexIds);
        profileGroup.MultipleLoad = PolicyGroupType switch
        {
            var s when s == ResUI.TbLeastPing => EMultipleLoad.LeastPing,
            var s when s == ResUI.TbRandom => EMultipleLoad.Random,
            var s when s == ResUI.TbRoundRobin => EMultipleLoad.RoundRobin,
            var s when s == ResUI.TbLeastLoad => EMultipleLoad.LeastLoad,
            _ => EMultipleLoad.LeastPing,
        };
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
