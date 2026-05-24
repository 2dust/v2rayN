namespace ServiceLib.ViewModels;

public class RoutingRuleSettingViewModel : MyReactiveObject
{
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

    public RoutingRuleSettingViewModel(RoutingItem routingItem, Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;

        var canEditRemove = this.WhenAnyValue(
            x => x.SelectedSource,
            selectedSource => selectedSource != null && !selectedSource.OutboundTag.IsNullOrEmpty());

        RuleAddCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await RuleEditAsync(true);
        });
        ImportRulesFromFileCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await _updateView?.Invoke(EViewAction.ImportRulesFromFile, null);
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
        _rules = routingItem.Id.IsNullOrEmpty() ? new() : JsonUtils.Deserialize<List<RulesItem>>(SelectedRouting.RuleSet);

        RefreshRulesItems();
    }

    public void RefreshRulesItems()
    {
        RulesItems.Clear();

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
            RulesItems.Add(it);
        }
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
        if (await _updateView?.Invoke(EViewAction.RoutingRuleDetailsWindow, item) == true)
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
        if (await _updateView?.Invoke(EViewAction.ShowYesNo, null) == false)
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
            await _updateView?.Invoke(EViewAction.SetClipboardData, JsonUtils.Serialize(lst, options));
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
            _updateView?.Invoke(EViewAction.CloseWindow, null);
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
        if (clipboardData == null)
        {
            await _updateView?.Invoke(EViewAction.ImportRulesFromClipboard, null);
            return;
        }
        var ret = await AddBatchRoutingRulesAsync(SelectedRouting, clipboardData);
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
        if (await _updateView?.Invoke(EViewAction.AddBatchRoutingRulesYesNo, null) == false)
        {
            blReplace = true;
        }
        if (clipboardData.IsNullOrEmpty())
        {
            await ShowImportErrorAsync($"{ResUI.OperationFailed}: {ResUI.FailedReadConfiguration}");
            return -1;
        }

        if (!TryDeserializeRoutingRules(clipboardData, out var lstRules, out var errorMsg))
        {
            await ShowImportErrorAsync($"{ResUI.OperationFailed}: {errorMsg}");
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

    private async Task ShowImportErrorAsync(string message)
    {
        if (_updateView != null)
        {
            await _updateView.Invoke(EViewAction.ShowMessage, message);
            return;
        }

        NoticeManager.Instance.Enqueue(message);
    }

    private static bool TryDeserializeRoutingRules(string clipboardData, out List<RulesItem> rules, out string errorMsg)
    {
        rules = [];
        errorMsg = string.Empty;

        var trimmed = clipboardData.Trim();
        if (trimmed.IsNullOrEmpty())
        {
            errorMsg = ResUI.FailedReadConfiguration;
            return false;
        }

        if (trimmed.StartsWith('{'))
        {
            if (trimmed.Contains("\"routing\"", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("\"route\"", StringComparison.OrdinalIgnoreCase))
            {
                errorMsg = "你粘贴的是完整配置文件，不是路由规则数组。请只粘贴 routing.rules 或 route.rules 的 JSON 数组。";
            }
            else
            {
                errorMsg = "当前内容是 JSON 对象。路由规则导入需要顶层 JSON 数组，例如 [ { \"outboundTag\": \"proxy\" } ]。";
            }
            return false;
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
            };

            rules = JsonSerializer.Deserialize<List<RulesItem>>(trimmed, options) ?? [];
            if (rules.Count == 0)
            {
                errorMsg = "未解析到任何路由规则，请确认内容是非空 JSON 数组。";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            var location = ex.LineNumber is not null && ex.BytePositionInLine is not null
                ? $"第 {ex.LineNumber + 1} 行，第 {ex.BytePositionInLine + 1} 列"
                : "未知位置";
            errorMsg = $"JSON 解析错误，{location}。{ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            errorMsg = ex.Message;
            return false;
        }
    }

    #endregion Import rules
}
