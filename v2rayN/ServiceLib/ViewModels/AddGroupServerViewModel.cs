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
    public partial string? UserAgent { get; set; }

    [Reactive]
    public partial string? PolicyGroupType { get; set; }

    [Reactive]
    public partial SubItem? SelectedSubItem { get; set; }

    [Reactive]
    public partial string? Filter { get; set; }

    /// <summary>
    /// The subscription groups, used as the content source of the group (written to SubChildItems).
    /// </summary>
    public BulkObservableCollection<SubItem> SubItems { get; } = [];

    /// <summary>
    /// Subscription groups the node itself can belong to, same entries as the main window
    /// ("All" first, then every subscription). The selection is written to ProfileItem.Subid.
    /// Kept apart from <see cref="SubItems"/> so the two dropdowns stay independent.
    /// </summary>
    public BulkObservableCollection<SubItem> SelfSubItems { get; } = [];

    [Reactive]
    public partial SubItem? SelectedSelfSubItem { get; set; }

    public BulkObservableCollection<ProfileItemModel> ChildItemsObs { get; } = [];

    public BulkObservableCollection<ProfileItemModel> AllProfilePreviewItemsObs { get; } = [];

    /// <summary>
    /// Mirrors ChildItemsObs as entities, so duplicates can be detected with the same rule the
    /// main window's "remove duplicates" action uses (ConfigHandler.CompareProfileItem).
    /// </summary>
    private readonly List<ProfileItem> _childSourceItems = [];

    [Reactive]
    public partial ProfileItemModel SelectedPreviewChild { get; set; }

    [Reactive]
    public partial IList<ProfileItemModel> SelectedPreviewChildren { get; set; }

    public ReactiveCommand<RxVoid, RxVoid> AddCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> RemoveCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> PreviewRemoveCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> PreviewMixedTestCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> PreviewSpeedTestCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> MoveTopCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> MoveUpCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> MoveDownCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> MoveBottomCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> SaveCmd { get; }

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

        var canPreviewEdit = this.WhenAnyValue(
            x => x.SelectedPreviewChild,
            selectedChild => selectedChild != null && !selectedChild.Remarks.IsNullOrEmpty());
        PreviewRemoveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await PreviewRemoveAsync();
        }, canPreviewEdit);
        PreviewMixedTestCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await PreviewSpeedtest(ESpeedActionType.Mixedtest);
        });
        PreviewSpeedTestCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await PreviewSpeedtest(ESpeedActionType.Speedtest);
        }, canPreviewEdit);
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
        // Prefill the built-in chrome UA for a new group; show the stored value when editing one.
        UserAgent = SelectedSource.GetProtocolExtra().UserAgent.NullIfEmpty()
                    ?? Global.RawHttpUserAgentTexts.GetValueOrDefault("chrome", string.Empty);

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

        // The subscription group this node itself belongs to (ProfileItem.Subid). The entries are
        // exactly the main window's list, i.e. "All" first plus every subscription. When the node
        // is new its Subid is preset to the subscription currently filtered in the main window,
        // so the dropdown shows that group (the main window defaults to "All", i.e. an empty id).
        var selfSubs = new List<SubItem> { new() { Remarks = ResUI.AllGroupServers } };
        selfSubs.AddRange(subs ?? []);
        SelfSubItems.AddRange(selfSubs);
        SelectedSelfSubItem = SelfSubItems.FirstOrDefault(s => s.Id.IsNotEmpty() && s.Id == SelectedSource.Subid)
                              ?? SelfSubItems.FirstOrDefault();

        subs.Add(new SubItem());
        SubItems.AddRange(subs);
        SelectedSubItem = SubItems.FirstOrDefault(s => s.Id == protocolExtra?.SubChildItems);
        Filter = protocolExtra?.Filter;

        var childIndexIds = Utils.String2List(protocolExtra?.ChildItems) ?? [];
        var childItemList = await AppManager.Instance.GetProfileItemsOrderedByIndexIds(childIndexIds);
        _childSourceItems.AddRange(childItemList);
        ChildItemsObs.AddRange(await ToProfileItemModels(childItemList));
    }

    public async Task AddChildAsync()
    {
        var profileSelectViewModel = new ProfilesSelectViewModel();
        // Policy groups are kept out of the picker: nesting one into another (or into itself,
        // which is possible because the group being edited is listed as well) can only produce
        // a cyclic dependency, which is rejected later when the core config is built.
        profileSelectViewModel.SetConfigTypeFilter([EConfigType.Custom, EConfigType.PolicyGroup], exclude: true);
        profileSelectViewModel.MultiSelect = true;
        var result = await AppManager.Instance.WindowDialog.ShowDialogAsync(profileSelectViewModel);
        if (result != true)
        {
            return;
        }
        var profiles = await profileSelectViewModel.GetProfileItems() ?? [];

        // Skip the nodes already in the child list. The identity rule is exactly the one used by
        // the main window's "remove duplicates" action, so one server that a subscription merged
        // several times is not added again and again. Complex entries (nested groups etc.) are
        // always kept, same as the main window does.
        var lstAdd = new List<ProfileItem>();
        foreach (var item in profiles.Where(t => t != null))
        {
            if (!item.IsComplex() && _childSourceItems.Exists(t => ConfigHandler.CompareProfileItem(t, item, false)))
            {
                continue;
            }
            _childSourceItems.Add(item);
            lstAdd.Add(item);
        }

        if (lstAdd.Count > 0)
        {
            ChildItemsObs.AddRange(await ToProfileItemModels(lstAdd));
        }
        if (lstAdd.Count < profiles.Count)
        {
            NoticeManager.Instance.Enqueue(string.Format(ResUI.RemoveDuplicateServerResult, profiles.Count, lstAdd.Count));
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
                _childSourceItems.RemoveAll(t => t.IndexId == it.IndexId);
            }
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Removes the selected preview nodes from the group. The preview list is a flat
    /// projection of ChildItems (direct) + SubChildItems (filtered), so a node may appear
    /// that is not in ChildItems — those are simply skipped with a notice.
    /// </summary>
    public async Task PreviewRemoveAsync()
    {
        var toRemove = (SelectedPreviewChildren ?? [SelectedPreviewChild])
            .Where(t => t != null && t.IndexId.IsNotEmpty())
            .ToList();
        if (toRemove.Count == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }
        var removed = 0;
        foreach (var it in toRemove)
        {
            var child = ChildItemsObs.FirstOrDefault(c => c.IndexId == it.IndexId);
            if (child != null)
            {
                ChildItemsObs.Remove(child);
                _childSourceItems.RemoveAll(t => t.IndexId == it.IndexId);
                removed++;
            }
        }
        await UpdatePreviewList();
        if (removed == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }

    /// <summary>
    /// Runs a speed test on the preview nodes. Uses the same SpeedtestService the main
    /// window uses, with a callback that updates the preview grid in place.
    /// </summary>
    public async Task PreviewSpeedtest(ESpeedActionType actionType)
    {
        var items = AllProfilePreviewItemsObs
            .Where(t => t != null && t.IndexId.IsNotEmpty())
            .Select(t => new ProfileItem { IndexId = t.IndexId, ConfigType = t.ConfigType, Remarks = t.Remarks, Address = t.Address, Port = t.Port, Network = t.Network, StreamSecurity = t.StreamSecurity, Subid = t.Subid })
            .ToList();
        if (items.Count == 0)
        {
            return;
        }
        NoticeManager.Instance.SendMessage(ResUI.Speedtesting);

        var speedtestService = new SpeedtestService(_config, async (SpeedTestResult result) =>
        {
            RxSchedulers.MainThreadScheduler.Schedule(() =>
            {
                if (result.IndexId.IsNullOrEmpty())
                {
                    NoticeManager.Instance.SendMessageEx(result.Delay);
                    return;
                }
                var item = AllProfilePreviewItemsObs.FirstOrDefault(t => t.IndexId == result.IndexId);
                if (item != null)
                {
                    if (result.Delay.IsNotEmpty())
                    {
                        item.Delay = result.Delay.ToInt();
                        item.DelayVal = result.Delay ?? string.Empty;
                    }
                    if (result.Speed.IsNotEmpty())
                    {
                        item.SpeedVal = result.Speed ?? string.Empty;
                    }
                }
            });
            await Task.CompletedTask;
        });
        speedtestService.RunLoop(actionType, items);
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

    /// <summary>
    /// Maps child profiles to the display model and fills delay/speed from ProfileExItem.
    /// The value expressions are kept identical to ProfilesSelectViewModel.GetProfileItemsEx
    /// so this grid renders exactly like the profile select window and the main window:
    /// delay 0 (untested) shows blank, delay -1 (failed) shows "-1", and speed falls back
    /// to the message text when no speed result is available.
    /// </summary>
    private static async Task<List<ProfileItemModel>> ToProfileItemModels(IEnumerable<ProfileItem> items)
    {
        var lstProfileExs = await ProfileExManager.Instance.GetProfileExs();
        var dicProfileEx = lstProfileExs.Where(t => t != null).ToDictionary(t => t.IndexId, t => t);

        return items.Where(t => t != null)
            .Select(t =>
            {
                var ex = dicProfileEx.GetValueOrDefault(t.IndexId);
                return new ProfileItemModel
                {
                    IndexId = t.IndexId,
                    ConfigType = t.ConfigType,
                    Remarks = t.Remarks,
                    Address = t.Address,
                    Port = t.Port,
                    Network = t.Network,
                    StreamSecurity = t.StreamSecurity,
                    Subid = t.Subid,
                    Delay = ex?.Delay ?? 0,
                    Speed = ex?.Speed ?? 0,
                    DelayVal = ex?.Delay != 0 ? $"{ex?.Delay}" : string.Empty,
                    SpeedVal = ex?.Speed > 0 ? $"{ex?.Speed}" : ex?.Message ?? string.Empty,
                };
            })
            .ToList();
    }

    private ProtocolExtraItem GetUpdatedProtocolExtra()
    {
        return SelectedSource.GetProtocolExtra() with
        {
            ChildItems =
            Utils.List2String(ChildItemsObs.Where(s => !s.IndexId.IsNullOrEmpty()).Select(s => s.IndexId).ToList()),
            MultipleLoad = PolicyGroupType switch
            {
                var s when s == ResUI.TbLeastPing => EMultipleLoad.LeastPing,
                var s when s == ResUI.TbFallback => EMultipleLoad.Fallback,
                var s when s == ResUI.TbRandom => EMultipleLoad.Random,
                var s when s == ResUI.TbRoundRobin => EMultipleLoad.RoundRobin,
                var s when s == ResUI.TbLeastLoad => EMultipleLoad.LeastLoad,
                _ => EMultipleLoad.LeastPing,
            },
            SubChildItems = SelectedSubItem?.Id,
            Filter = Filter,
            UserAgent = UserAgent.NullIfEmpty(),
        };
    }

    public async Task UpdatePreviewList()
    {
        var items = await GroupProfileManager.GetChildProfileItemsByProtocolExtra(GetUpdatedProtocolExtra());
        AllProfilePreviewItemsObs.ReplaceRange(await ToProfileItemModels(items));
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
        SelectedSource.Subid = SelectedSelfSubItem?.Id ?? string.Empty;
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
