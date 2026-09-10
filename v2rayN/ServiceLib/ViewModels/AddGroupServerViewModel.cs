using System.Collections.Generic;
using ServiceLib.Manager;
using ServiceLib.Models.Dto;
using ServiceLib.Models.Entities;

namespace ServiceLib.ViewModels;

public partial class AddGroupServerViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;

    [Reactive]
    public partial ProfileItem SelectedSource { get; set; }

    [Reactive]
    public partial ProfileItemModel SelectedChild { get; set; }

    [Reactive]
    public partial IList<ProfileItemModel> SelectedChildren { get; set; }

    [Reactive]
    public partial string? CoreType { get; set; }

    [Reactive]
    public partial string? PolicyGroupType { get; set; }

    [Reactive]
    public partial SubItem? SelectedSubItem { get; set; }

    [Reactive]
    public partial string? Filter { get; set; }

    [Reactive]
    public partial string? FilterMaxDelayText { get; set; }

    [Reactive]
    public partial string? FilterMinSpeedText { get; set; }

    [Reactive]
    public partial int SelectedTabIndex { get; set; }

    [Reactive]
    public partial string? ActionButtonText { get; set; }

    public BulkObservableCollection<SubItem> SubItems { get; } = [];

    public BulkObservableCollection<ProfileItemModel> ChildItemsObs { get; } = [];

    public BulkObservableCollection<ProfileItemModel> AllProfilePreviewItemsObs { get; } = [];

    public ReactiveCommand<RxVoid, RxVoid> AddCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> RemoveCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> MoveTopCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> MoveUpCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> MoveDownCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> MoveBottomCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> SaveCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> ActionCmd { get; }

    public AddGroupServerViewModel(ProfileItem profileItem)
    {
        _config = AppManager.Instance.Config;

        var canEditRemove = this.WhenAnyValue(
            x => x.SelectedChild,
            selectedChild => selectedChild != null && !selectedChild.Remarks.IsNullOrEmpty());

        AddCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddChildAsync();
        });
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
        ActionCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await OnActionAsync();
        });

        this.WhenAnyValue(x => x.SelectedTabIndex)
            .Subscribe(idx =>
            {
                ActionButtonText = idx == 0 ? ResUI.LvAdd : ResUI.LvRefresh;
            });
        ActionButtonText = SelectedTabIndex == 0 ? ResUI.LvAdd : ResUI.LvRefresh;

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
        subs.Insert(0, new SubItem { Id = Global.SubItemAllId, Remarks = ResUI.AllGroupServers });
        subs.Add(new SubItem());
        SubItems.AddRange(subs);
        SelectedSubItem = (protocolExtra?.SubChildItems.IsNotEmpty() == true
                            ? SubItems.FirstOrDefault(s => s.Id == protocolExtra.SubChildItems)
                            : null) ?? SubItems.FirstOrDefault();
        Filter = protocolExtra?.Filter;

        var childIndexIds = Utils.String2List(protocolExtra?.ChildItems) ?? [];
        var childItemList = await AppManager.Instance.GetProfileItemsOrderedByIndexIds(childIndexIds);
        ChildItemsObs.AddRange(await ToProfileItemModels(childItemList));

        FilterMaxDelayText = protocolExtra?.FilterMaxDelay > 0 ? protocolExtra.FilterMaxDelay.ToString() : null;
        FilterMinSpeedText = protocolExtra?.FilterMinSpeed > 0 ? protocolExtra.FilterMinSpeed.ToString() : null;

        await UpdatePreviewList();
    }

    public async Task AddChildAsync()
    {
        var profileSelectViewModel = new ProfilesSelectViewModel();
        profileSelectViewModel.SetConfigTypeFilter([EConfigType.Custom], exclude: true);
        profileSelectViewModel.MultiSelect = true;
        var result = await AppManager.Instance.WindowDialog.ShowDialogAsync(profileSelectViewModel);
        if (result != true)
        {
            return;
        }
        var profiles = await profileSelectViewModel.GetProfileItems() ?? [];
        ChildItemsObs.AddRange(await ToProfileItemModels(profiles));
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
        var selectedChild = SelectedChild;
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

    private ProtocolExtraItem GetUpdatedProtocolExtra()
    {
        return SelectedSource.GetProtocolExtra() with
        {
            ChildItems =
            Utils.List2String(ChildItemsObs.Where(s => !s.IndexId.IsNullOrEmpty()).Select(s => s.IndexId).ToList()),
            MultipleLoad = PolicyGroupType switch
            {
                var t when t == ResUI.TbLeastPing => EMultipleLoad.LeastPing,
                var t when t == ResUI.TbFallback => EMultipleLoad.Fallback,
                var t when t == ResUI.TbRandom => EMultipleLoad.Random,
                var t when t == ResUI.TbRoundRobin => EMultipleLoad.RoundRobin,
                var t when t == ResUI.TbLeastLoad => EMultipleLoad.LeastLoad,
                _ => EMultipleLoad.LeastPing,
            },
            SubChildItems = SelectedSubItem?.Id,
            Filter = Filter,
            FilterMaxDelay = int.TryParse(FilterMaxDelayText, out var delay) ? delay : 0,
            FilterMinSpeed = decimal.TryParse(FilterMinSpeedText, out var speed) ? speed : 0,
        };
    }

    public async Task UpdatePreviewList()
    {
        var extra = GetUpdatedProtocolExtra();
        var filterItems = await GroupProfileManager.GetSubChildProfileItems(extra);
        var selectedItems = await GroupProfileManager.GetSelectedChildProfileItems(extra);

        var filterModels = await ToProfileItemModels(filterItems);
        filterModels.ForEach(m => m.SourceTag = ResUI.LvSourceFilter);
        var selectedModels = await ToProfileItemModels(selectedItems);
        selectedModels.ForEach(m => m.SourceTag = ResUI.LvSourceManual);

        AllProfilePreviewItemsObs.ReplaceRange(filterModels.Concat(selectedModels).ToList());
    }

    private async Task OnActionAsync()
    {
        if (SelectedTabIndex == 0)
        {
            await UpdatePreviewList();
            SelectedTabIndex = 2;
            if (AllProfilePreviewItemsObs.Count == 0)
            {
                NoticeManager.Instance.Enqueue(ResUI.LvPreviewEmptyHint);
            }
        }
        else
        {
            await RefreshStatsAsync();
        }
    }

    private async Task<List<ProfileItemModel>> ToProfileItemModels(List<ProfileItem> items)
    {
        var lstProfileExs = await ProfileExManager.Instance.GetProfileExs();
        return (from t in items
                join t3 in lstProfileExs on t.IndexId equals t3.IndexId into t3b
                from t33 in t3b.DefaultIfEmpty()
                select new ProfileItemModel
                {
                    IndexId = t.IndexId,
                    ConfigType = t.ConfigType,
                    Remarks = t.Remarks,
                    Address = t.Address,
                    Port = t.Port,
                    Network = t.Network,
                    StreamSecurity = t.StreamSecurity,
                    Subid = t.Subid,
                    Delay = t33?.Delay ?? 0,
                    Speed = t33?.Speed ?? 0,
                    DelayVal = t33?.Delay != 0 ? $"{t33?.Delay}" : string.Empty,
                    SpeedVal = t33?.Speed > 0 ? $"{t33?.Speed}" : t33?.Message ?? string.Empty,
                }).ToList();
    }

    public async Task RefreshStatsAsync()
    {
        var lstProfileExs = await ProfileExManager.Instance.GetProfileExs();
        var exMap = lstProfileExs?
            .Where(x => x != null)
            .GroupBy(x => x.IndexId)
            .ToDictionary(g => g.Key, g => g.First())
            ?? new Dictionary<string, ProfileExItem>();

        RefreshStats(ChildItemsObs, exMap);
        RefreshStats(AllProfilePreviewItemsObs, exMap);
    }

    private static void RefreshStats(BulkObservableCollection<ProfileItemModel> items, Dictionary<string, ProfileExItem> exMap)
    {
        foreach (var m in items)
        {
            if (exMap.TryGetValue(m.IndexId, out var ex))
            {
                m.Delay = ex.Delay;
                m.Speed = ex.Speed;
                m.DelayVal = ex.Delay != 0 ? $"{ex.Delay}" : string.Empty;
                m.SpeedVal = ex.Speed > 0 ? $"{ex.Speed}" : ex.Message ?? string.Empty;
            }
            else
            {
                m.Delay = 0;
                m.Speed = 0;
                m.DelayVal = string.Empty;
                m.SpeedVal = string.Empty;
            }
        }
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
        SelectedSource.CoreType = CoreType.IsNullOrEmpty() ? ECoreType.Xray : Enum.Parse<ECoreType>(CoreType);
        if (SelectedSource.CoreType is not (ECoreType.Xray or ECoreType.sing_box) ||
            SelectedSource.ConfigType is not (EConfigType.ProxyChain or EConfigType.PolicyGroup))
        {
            return;
        }

        var protocolExtra = GetUpdatedProtocolExtra();

        SelectedSource.SetProtocolExtra(protocolExtra);

        if (await ConfigHandler.AddServerCommon(_config, SelectedSource) == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }
}
