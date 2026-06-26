namespace ServiceLib.ViewModels;

public class RoutingRuleSettingViewModel : MyReactiveObject
{
    public Interaction<Unit, Unit> CloseWindowInteraction { get; } = new();
    public Interaction<string, bool> ShowYesNoInteraction { get; } = new();
    public Interaction<string, Unit> SetClipboardDataInteraction { get; } = new();
    public Interaction<Unit, string?> ReadTextFromClipboardInteraction { get; } = new();
    public Interaction<Unit, string?> BrowseRulesFileInteraction { get; } = new();

    private List<RulesItem> _rules;

    [Reactive]
    public RoutingItem SelectedRouting { get; set; }

    public IObservableCollection<RulesItemModel> RulesItems { get; } = new ObservableCollectionExtended<RulesItemModel>();

    [Reactive]
    public RulesItemModel SelectedSource { get; set; }

    public IList<RulesItemModel> SelectedSources { get; set; }

    public ReactiveCommand<Unit, Unit> RuleAddCmd { get; }
    public ReactiveCommand<Unit, Unit> ImportRulesFromFileCmd { get; }
    public ReactiveCommand<Unit, Unit> ImportRulesFromClipboardCmd { get; }
    public ReactiveCommand<Unit, Unit> ImportRulesFromUrlCmd { get; }
    public ReactiveCommand<Unit, Unit> RuleRemoveCmd { get; }
    public ReactiveCommand<Unit, Unit> RuleExportSelectedCmd { get; }
    public ReactiveCommand<Unit, Unit> MoveTopCmd { get; }
    public ReactiveCommand<Unit, Unit> MoveUpCmd { get; }
    public ReactiveCommand<Unit, Unit> MoveDownCmd { get; }
    public ReactiveCommand<Unit, Unit> MoveBottomCmd { get; }

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }

    public RoutingRuleSettingViewModel(RoutingItem routingItem)
    {
        _config = AppManager.Instance.Config;

        var canEditRemove = this.WhenAnyValue(
            x => x.SelectedSource,
            selectedSource => selectedSource != null && !selectedSource.OutboundTag.IsNullOrEmpty());

        RuleAddCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await RuleEditAsync(true);
        });
        ImportRulesFromFileCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            var fileName = await BrowseRulesFileInteraction.Handle(Unit.Default);
            await ImportRulesFromFileAsync(fileName);
        });
        ImportRulesFromClipboardCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ImportRulesFromClipboardAsync(null);
        });
        ImportRulesFromUrlCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ImportRulesFromUrl();
        });

        RuleRemoveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await RuleRemoveAsync();
        }, canEditRemove);
        RuleExportSelectedCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await RuleExportSelectedAsync();
        }, canEditRemove);

        MoveTopCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveRule(EMove.Top);
        }, canEditRemove);
        MoveUpCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveRule(EMove.Up);
        }, canEditRemove);
        MoveDownCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveRule(EMove.Down);
        }, canEditRemove);
        MoveBottomCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveRule(EMove.Bottom);
        }, canEditRemove);

        SaveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveRoutingAsync();
        });

        SelectedSource = new();
        SelectedRouting = routingItem;
        _rules = routingItem.Id.IsNullOrEmpty() ? [] : JsonUtils.Deserialize<List<RulesItem>>(SelectedRouting.RuleSet);

        RefreshRulesItems();
    }

    public void RefreshRulesItems()
    {
        RulesItems.Clear();

        var models = new List<RulesItemModel>();
        foreach (var item in _rules)
        {
            var it = new RulesItemModel()
            {
                Id = item.Id,
                RuleTypeName = item.RuleType?.ToString(),
                OutboundTag = item.OutboundTag,
                Port = item.Port,
                Network = item.Network,
                Protocols = Utils.List2String(item.Protocol),
                InboundTags = Utils.List2String(item.InboundTag),
                Domains = Utils.List2String((item.Domain ?? []).Concat(item.Ip ?? []).ToList().Concat(item.Process ?? []).ToList()),
                Enabled = item.Enabled,
                Remarks = item.Remarks,
            };
            models.Add(it);
        }
        RulesItems.AddRange(models);
    }

    public async Task RuleEditAsync(bool blNew)
    {
        RulesItem? item;
        if (blNew)
        {
            item = new();
        }
        else
        {
            item = _rules.FirstOrDefault(t => t.Id == SelectedSource?.Id);
            if (item is null)
            {
                return;
            }
        }
        var routingRuleDetailsViewModel = new RoutingRuleDetailsViewModel(item);
        if (await AppManager.Instance.WindowDialog.ShowDialogAsync(routingRuleDetailsViewModel) == true)
        {
            if (blNew)
            {
                _rules.Insert(0, item);
            }
            RefreshRulesItems();
        }
    }

    public async Task RuleRemoveAsync()
    {
        if (SelectedSource is null || SelectedSource.OutboundTag.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectRules);
            return;
        }
        if (await ShowYesNoInteraction.Handle(ResUI.RemoveServer) == false)
        {
            return;
        }
        foreach (var it in SelectedSources ?? [SelectedSource])
        {
            var item = _rules.FirstOrDefault(t => t.Id == it?.Id);
            if (item != null)
            {
                _rules.Remove(item);
            }
        }

        RefreshRulesItems();
    }

    public async Task RuleExportSelectedAsync()
    {
        if (SelectedSource is null || SelectedSource.OutboundTag.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectRules);
            return;
        }

        var lst = new List<RulesItem>();
        var sources = SelectedSources ?? [SelectedSource];
        foreach (var it in _rules)
        {
            if (sources.Any(t => t.Id == it?.Id))
            {
                var item2 = JsonUtils.DeepCopy(it);
                item2.Id = null;
                lst.Add(item2 ?? new());
            }
        }
        if (lst.Count > 0)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            await SetClipboardDataInteraction.Handle(JsonUtils.Serialize(lst, options));
        }
    }

    public async Task MoveRule(EMove eMove)
    {
        if (SelectedSource is null || SelectedSource.OutboundTag.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectRules);
            return;
        }

        var item = _rules.FirstOrDefault(t => t.Id == SelectedSource?.Id);
        if (item == null)
        {
            return;
        }
        var index = _rules.IndexOf(item);
        if (await ConfigHandler.MoveRoutingRule(_rules, index, eMove) == 0)
        {
            RefreshRulesItems();
        }
    }

    private async Task SaveRoutingAsync()
    {
        var remarks = SelectedRouting.Remarks;
        if (remarks.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseFillRemarks);
            return;
        }
        var item = SelectedRouting;
        foreach (var it in _rules)
        {
            it.Id = Utils.GetGuid(false);
        }
        item.RuleNum = _rules.Count;
        item.RuleSet = JsonUtils.Serialize(_rules, false);

        if (await ConfigHandler.SaveRoutingItem(_config, item) == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
            await CloseWindowInteraction.Handle(Unit.Default);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }

    #region Import rules

    public async Task ImportRulesFromFileAsync(string fileName)
    {
        if (fileName.IsNullOrEmpty())
        {
            return;
        }

        var result = EmbedUtils.LoadResource(fileName);
        if (result.IsNullOrEmpty())
        {
            return;
        }
        var ret = await AddBatchRoutingRulesAsync(SelectedRouting, result);
        if (ret == 0)
        {
            RefreshRulesItems();
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        }
    }

    public async Task ImportRulesFromClipboardAsync(string? clipboardData)
    {
        var stringData = clipboardData;
        if (clipboardData == null)
        {
            var result = await ReadTextFromClipboardInteraction.Handle(Unit.Default);
            if (result.IsNullOrEmpty())
            {
                NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
                return;
            }
            stringData = result;
        }
        var ret = await AddBatchRoutingRulesAsync(SelectedRouting, stringData);
        if (ret == 0)
        {
            RefreshRulesItems();
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        }
    }

    private async Task ImportRulesFromUrl()
    {
        var url = SelectedRouting.Url;
        if (url.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.MsgNeedUrl);
            return;
        }

        var downloadHandle = new DownloadService();
        var result = await downloadHandle.TryDownloadString(url, true, "");
        var ret = await AddBatchRoutingRulesAsync(SelectedRouting, result);
        if (ret == 0)
        {
            RefreshRulesItems();
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        }
    }

    private async Task<int> AddBatchRoutingRulesAsync(RoutingItem routingItem, string? clipboardData)
    {
        var blReplace = false;
        if (await ShowYesNoInteraction.Handle(ResUI.AddBatchRoutingRulesYesNo) == false)
        {
            blReplace = true;
        }
        if (clipboardData.IsNullOrEmpty())
        {
            return -1;
        }
        var lstRules = JsonUtils.Deserialize<List<RulesItem>>(clipboardData);
        if (lstRules == null)
        {
            return -1;
        }
        foreach (var rule in lstRules)
        {
            rule.Id = Utils.GetGuid(false);
        }

        if (blReplace)
        {
            _rules = lstRules;
        }
        else
        {
            _rules.AddRange(lstRules);
        }
        return 0;
    }

    #endregion Import rules
}
