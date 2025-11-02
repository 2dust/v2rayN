namespace ServiceLib.ViewModels;

public class AddServerViewModel : MyReactiveObject
{
    [Reactive]
    public ProfileItem SelectedSource { get; set; }

    [Reactive]
    public string? CoreType { get; set; }

    [Reactive]
    public string Cert { get; set; }

    public ReactiveCommand<Unit, Unit> FetchCertCmd { get; }
    public ReactiveCommand<Unit, Unit> FetchCertChainCmd { get; }
    public ReactiveCommand<Unit, Unit> SaveCmd { get; }

    public AddServerViewModel(ProfileItem profileItem, Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;

        FetchCertCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await FetchCert();
        });
        FetchCertChainCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await FetchCertChain();
        });
        SaveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveServerAsync();
        });

        if (profileItem.IndexId.IsNullOrEmpty())
        {
            profileItem.Network = Global.DefaultNetwork;
            profileItem.HeaderType = Global.None;
            profileItem.RequestHost = "";
            profileItem.StreamSecurity = "";
            SelectedSource = profileItem;
        }
        else
        {
            SelectedSource = JsonUtils.DeepCopy(profileItem);
        }
        CoreType = SelectedSource?.CoreType?.ToString();
        Cert = SelectedSource?.Cert?.ToString() ?? string.Empty;
    }

    private async Task SaveServerAsync()
    {
        if (SelectedSource.Remarks.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseFillRemarks);
            return;
        }

        if (SelectedSource.Address.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.FillServerAddress);
            return;
        }
        var port = SelectedSource.Port.ToString();
        if (port.IsNullOrEmpty() || !Utils.IsNumeric(port)
            || SelectedSource.Port <= 0 || SelectedSource.Port >= Global.MaxPort)
        {
            NoticeManager.Instance.Enqueue(ResUI.FillCorrectServerPort);
            return;
        }
        if (SelectedSource.ConfigType == EConfigType.Shadowsocks)
        {
            if (SelectedSource.Id.IsNullOrEmpty())
            {
                NoticeManager.Instance.Enqueue(ResUI.FillPassword);
                return;
            }
            if (SelectedSource.Security.IsNullOrEmpty())
            {
                NoticeManager.Instance.Enqueue(ResUI.PleaseSelectEncryption);
                return;
            }
        }
        if (SelectedSource.ConfigType is not EConfigType.SOCKS and not EConfigType.HTTP)
        {
            if (SelectedSource.Id.IsNullOrEmpty())
            {
                NoticeManager.Instance.Enqueue(ResUI.FillUUID);
                return;
            }
        }
        SelectedSource.CoreType = CoreType.IsNullOrEmpty() ? null : (ECoreType)Enum.Parse(typeof(ECoreType), CoreType);
        SelectedSource.Cert = Cert.IsNullOrEmpty() ? null : Cert;

        if (await ConfigHandler.AddServer(_config, SelectedSource) == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
            _updateView?.Invoke(EViewAction.CloseWindow, null);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }

    private async Task FetchCert()
    {
        if (SelectedSource.StreamSecurity != Global.StreamSecurity)
        {
            return;
        }
        var domain = SelectedSource.Address;
        var serverName = SelectedSource.Sni;
        if (serverName.IsNullOrEmpty())
        {
            serverName = SelectedSource.RequestHost;
        }
        if (serverName.IsNullOrEmpty())
        {
            serverName = SelectedSource.Address;
        }
        if (!Utils.IsDomain(serverName))
        {
            NoticeManager.Instance.Enqueue(ResUI.ServerNameMustBeValidDomain);
            return;
        }
        if (SelectedSource.Port > 0)
        {
            domain += $":{SelectedSource.Port}";
        }
        Cert = await CertPemManager.Instance.GetCertPemAsync(domain, serverName);
    }

    private async Task FetchCertChain()
    {
        if (SelectedSource.StreamSecurity != Global.StreamSecurity)
        {
            return;
        }
        var domain = SelectedSource.Address;
        var serverName = SelectedSource.Sni;
        if (serverName.IsNullOrEmpty())
        {
            serverName = SelectedSource.RequestHost;
        }
        if (serverName.IsNullOrEmpty())
        {
            serverName = SelectedSource.Address;
        }
        if (!Utils.IsDomain(serverName))
        {
            NoticeManager.Instance.Enqueue(ResUI.ServerNameMustBeValidDomain);
            return;
        }
        if (SelectedSource.Port > 0)
        {
            domain += $":{SelectedSource.Port}";
        }
        var certs = await CertPemManager.Instance.GetCertChainPemAsync(domain, serverName);
        Cert = string.Join("\n", certs);
    }
}
