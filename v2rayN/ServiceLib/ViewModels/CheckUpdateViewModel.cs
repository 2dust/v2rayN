using System;                    // +++
using System.IO;                 // +++
using System.Linq;               // +++
using System.Threading.Tasks;    // +++
using System.Collections.Generic;// +++
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;

namespace ServiceLib.ViewModels;

public class CheckUpdateViewModel : MyReactiveObject
{
    private const string _geo = "GeoFiles";
    private readonly string _v2rayN = ECoreType.v2rayN.ToString();
    private List<CheckUpdateModel> _lstUpdated = [];
    private static readonly string _tag = "CheckUpdateViewModel";

    public IObservableCollection<CheckUpdateModel> CheckUpdateModels { get; } = new ObservableCollectionExtended<CheckUpdateModel>();
    public ReactiveCommand<Unit, Unit> CheckUpdateCmd { get; }
    [Reactive] public bool EnableCheckPreReleaseUpdate { get; set; }

    public CheckUpdateViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;

        CheckUpdateCmd = ReactiveCommand.CreateFromTask(CheckUpdate);
        CheckUpdateCmd.ThrownExceptions.Subscribe(ex =>
        {
            Logging.SaveLog(_tag, ex);
            _ = UpdateView(_v2rayN, ex.Message);
        });

        EnableCheckPreReleaseUpdate = _config.CheckUpdateItem.CheckPreReleaseUpdate;

        this.WhenAnyValue(
        x => x.EnableCheckPreReleaseUpdate,
        y => y == true)
            .Subscribe(c => { _config.CheckUpdateItem.CheckPreReleaseUpdate = EnableCheckPreReleaseUpdate; });

        RefreshCheckUpdateItems();
    }

    private void RefreshCheckUpdateItems()
    {
        CheckUpdateModels.Clear();

        if (RuntimeInformation.ProcessArchitecture != Architecture.X86)
        {
            CheckUpdateModels.Add(GetCheckUpdateModel(_v2rayN));
            //Not Windows and under Win10
            if (!(Utils.IsWindows() && Environment.OSVersion.Version.Major < 10))
            {
                CheckUpdateModels.Add(GetCheckUpdateModel(ECoreType.Xray.ToString()));
                CheckUpdateModels.Add(GetCheckUpdateModel(ECoreType.mihomo.ToString()));
                CheckUpdateModels.Add(GetCheckUpdateModel(ECoreType.sing_box.ToString()));
            }
        }
        CheckUpdateModels.Add(GetCheckUpdateModel(_geo));
    }

    // ---- packaged env detection: AppImage or runs from /opt/v2rayN ----
    private static bool IsPackagedInstall()
    {
        try
        {
            if (Utils.IsWindows())
                return false;

            // AppImage: APPIMAGE var exists
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPIMAGE")))
                return true;

            // deb/rpm: current startup path is under /opt/v2rayN
            var sp = Utils.StartupPath();
            if (!string.IsNullOrEmpty(sp))
            {
                sp = sp.Replace('\\', '/');
                if (sp.Equals("/opt/v2rayN", StringComparison.OrdinalIgnoreCase) ||
                    sp.StartsWith("/opt/v2rayN/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
        }
        return false;
    }

    private CheckUpdateModel GetCheckUpdateModel(string coreType)
    {
        // packaged env: disable v2rayN self-update
        if (coreType == _v2rayN && IsPackagedInstall())
        {
            return new()
            {
                IsSelected = false,
                CoreType = coreType,
                Remarks = ResUI.menuCheckUpdate + " (Not Support)",
            };
        }

        return new()
        {
            IsSelected = _config.CheckUpdateItem.SelectedCoreTypes?.Contains(coreType) ?? true,
            CoreType = coreType,
            Remarks = ResUI.menuCheckUpdate,
        };
    }

    private async Task SaveSelectedCoreTypes()
    {
        // packaged env: do not persist v2rayN toggle
        var list = CheckUpdateModels
            .Where(t => t.IsSelected == true && !(IsPackagedInstall() && t.CoreType == _v2rayN))
            .Select(t => t.CoreType ?? "")
            .ToList();

        _config.CheckUpdateItem.SelectedCoreTypes = list;
        await ConfigHandler.SaveConfig(_config);
    }

    private async Task CheckUpdate()
    {
        await Task.Run(CheckUpdateTask);
    }

    private async Task CheckUpdateTask()
    {
        _lstUpdated.Clear();
        _lstUpdated = CheckUpdateModels.Where(x => x.IsSelected == true)
                .Select(x => new CheckUpdateModel() { CoreType = x.CoreType }).ToList();
        await SaveSelectedCoreTypes();

        for (var k = CheckUpdateModels.Count - 1; k >= 0; k--)
        {
            var item = CheckUpdateModels[k];
            if (item.IsSelected != true)
            {
                continue;
            }

            await UpdateView(item.CoreType, "...");
            if (item.CoreType == _geo)
            {
                await CheckUpdateGeo();
            }
            else if (item.CoreType == _v2rayN)
            {
                if (IsPackagedInstall())
                {
                    await UpdateView(_v2rayN, "Not Support");
                    continue;
                }
                await CheckUpdateN(EnableCheckPreReleaseUpdate);
            }
            else if (item.CoreType == ECoreType.Xray.ToString())
            {
                await CheckUpdateCore(item, EnableCheckPreReleaseUpdate);
            }
            else
            {
                await CheckUpdateCore(item, false);
            }
        }

        await UpdateFinished();
    }

    private void UpdatedPlusPlus(string coreType, string fileName)
    {
        var item = _lstUpdated.FirstOrDefault(x => x.CoreType == coreType);
        if (item == null)
        {
            return;
        }
        item.IsFinished = true;
        if (!fileName.IsNullOrEmpty())
        {
            item.FileName = fileName;
        }
    }

    private async Task CheckUpdateGeo()
    {
        async Task _updateUI(bool success, string msg)
        {
            await UpdateView(_geo, msg);
            if (success)
            {
                Updated
