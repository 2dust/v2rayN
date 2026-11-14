namespace ServiceLib.Manager.Data;

public class ConfigTextManager
{
    private static readonly Lazy<ConfigTextManager> _instance = new(() => new());
    public static ConfigTextManager Instance => _instance.Value;

    private static readonly string _configRes = Global.ConfigFileName;
    private static readonly string _tag = "ConfigTextManager";

    public Config Config { get; private set; }

    #region Init

    public Config? Init()
    {
        Config = LoadConfig();
        return Config;
    }

    /// <summary>
    /// Load the application configuration file
    /// If the file exists, deserialize it from JSON
    /// If not found, create a new Config object with default settings
    /// Initialize default values for missing configuration sections
    /// </summary>
    /// <returns>Config object containing application settings or null if there's an error</returns>
    private Config? LoadConfig()
    {
        Config? config = null;
        var result = EmbedUtils.LoadResource(Utils.GetConfigPath(_configRes));
        if (result.IsNotEmpty())
        {
            config = JsonUtils.Deserialize<Config>(result);
        }
        else
        {
            if (File.Exists(Utils.GetConfigPath(_configRes)))
            {
                Logging.SaveLog("LoadConfig Exception");
                return null;
            }
        }

        config ??= new Config();

        config.CoreBasicItem ??= new()
        {
            LogEnabled = false,
            Loglevel = "warning",
        };

        if (config.Inbound == null)
        {
            config.Inbound = [];
            InItem inItem = new()
            {
                Protocol = nameof(EInboundProtocol.socks),
                LocalPort = 10808,
                UdpEnabled = true,
                SniffingEnabled = true,
                RouteOnly = false,
            };

            config.Inbound.Add(inItem);
        }
        else
        {
            if (config.Inbound.Count > 0)
            {
                config.Inbound.First().Protocol = nameof(EInboundProtocol.socks);
            }
        }

        config.RoutingBasicItem ??= new();
        if (config.RoutingBasicItem.DomainStrategy.IsNullOrEmpty())
        {
            config.RoutingBasicItem.DomainStrategy = Global.DomainStrategies.First();
        }

        config.KcpItem ??= new KcpItem
        {
            Mtu = 1350,
            Tti = 50,
            UplinkCapacity = 12,
            DownlinkCapacity = 100,
            CwndMultiplier = 1,
            MaxSendingWindow = 2 * 1024 * 1024,
        };
        config.KcpItem.CwndMultiplier = config.KcpItem.CwndMultiplier <= 0 ? 1 : config.KcpItem.CwndMultiplier;
        config.KcpItem.MaxSendingWindow = config.KcpItem.MaxSendingWindow <= 0 ? (2 * 1024 * 1024) : config.KcpItem.MaxSendingWindow;
        config.GrpcItem ??= new GrpcItem
        {
            IdleTimeout = 60,
            HealthCheckTimeout = 20,
            PermitWithoutStream = false,
            InitialWindowsSize = 0,
        };
        config.TunModeItem ??= new TunModeItem
        {
            EnableTun = false,
            Mtu = 9000,
            IcmpRouting = Global.TunIcmpRoutingPolicies.First(),
            EnableLegacyProtect = true,
        };
        config.GuiItem ??= new();
        if (!Global.RootCertProviders.Contains(config.GuiItem.RootCertProvider))
        {
            config.GuiItem.RootCertProvider = Global.RootCertProviders.First();
        }
        config.MsgUIItem ??= new();

        config.UiItem ??= new();
        config.UiItem.MainColumnItem ??= [];
        config.UiItem.WindowSizeItem ??= [];
        if (config.UiItem.FrozenColumnCount == null)
        {
            var lvColumnItem = config.UiItem.MainColumnItem.OrderBy(t => t.Index).ToList();
            if (lvColumnItem.Count == 0
                || (lvColumnItem.Count >= 2
                    && lvColumnItem[0].Name.Equals("ConfigType")
                    && lvColumnItem[1].Name.Equals("Remarks")))
            {
                config.UiItem.FrozenColumnCount = 2;
            }
            else
            {
                config.UiItem.FrozenColumnCount = 0;
            }
        }

        if (config.UiItem.CurrentLanguage.IsNullOrEmpty())
        {
            config.UiItem.CurrentLanguage = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.CurrentCultureIgnoreCase)
                ? Global.Languages.First()
                : Global.Languages[2];
        }

        config.ConstItem ??= new ConstItem();

        config.SimpleDNSItem ??= ConfigDbManager.InitBuiltinSimpleDNS();
        config.SimpleDNSItem.BlockAAAAQuery ??= false;
        config.SimpleDNSItem.FakeIPRange ??= Global.FakeIPRanges.FirstOrDefault();
        config.SimpleDNSItem.FakeIPv6 ??= false;
        config.SimpleDNSItem.FakeIPv6Range ??= Global.FakeIPv6Ranges.First();
        config.SimpleDNSItem.GlobalFakeIp ??= true;
        config.SimpleDNSItem.BootstrapDNS ??= Global.DomainPureIPDNSAddress.FirstOrDefault();
        config.SimpleDNSItem.ServeStale ??= false;
        config.SimpleDNSItem.ParallelQuery ??= false;
        config.SimpleDNSItem.EnableHappyEyeballs ??= false;

        config.SpeedTestItem ??= new();
        if (config.SpeedTestItem.SpeedTestTimeout < 10)
        {
            config.SpeedTestItem.SpeedTestTimeout = 10;
        }
        if (config.SpeedTestItem.SpeedTestUrl.IsNullOrEmpty())
        {
            config.SpeedTestItem.SpeedTestUrl = Global.SpeedTestUrls.First();
        }
        if (config.SpeedTestItem.SpeedPingTestUrl.IsNullOrEmpty())
        {
            config.SpeedTestItem.SpeedPingTestUrl = Global.SpeedPingTestUrls.First();
        }
        config.SpeedTestItem.MixedConcurrencyCount = Math.Max(config.SpeedTestItem.MixedConcurrencyCount, Global.SpeedTestConcurrencyCountMin);
        if (config.SpeedTestItem.UdpTestTarget.IsNullOrEmpty())
        {
            config.SpeedTestItem.UdpTestTarget = Global.UdpTestTargets.First();
        }

        config.Mux4RayItem ??= new()
        {
            Concurrency = 8,
            XudpConcurrency = 16,
            XudpProxyUDP443 = "reject"
        };

        config.Mux4SboxItem ??= new()
        {
            Protocol = Global.SingboxMuxs.First(),
            MaxConnections = 8
        };

        config.HysteriaItem ??= new()
        {
            UpMbps = 100,
            DownMbps = 100
        };
        config.ClashUIItem ??= new();
        config.ClashUIItem.ConnectionsColumnItem ??= [];
        config.SystemProxyItem ??= new();
        config.WebDavItem ??= new();
        config.CheckUpdateItem ??= new();
        config.Fragment4RayItem ??= new()
        {
            Packets = "tlshello",
        };
        config.Fragment4RayItem.MaxSplit ??= "0";

        config.HappyEyeballs4RayItem ??= new()
        {
            TryDelayMs = 250,
            PrioritizeIPv6 = false,
            Interleave = 1,
            MaxConcurrentTry = 4,
        };
        if ((config.Fragment4RayItem.Lengths ?? []).Count == 0)
        {
            config.Fragment4RayItem.Lengths = [config.Fragment4RayItem.Length ?? "50-100"];
        }
        if ((config.Fragment4RayItem.Delays ?? []).Count == 0)
        {
            config.Fragment4RayItem.Delays = [config.Fragment4RayItem.Interval ?? "10-20"];
        }
        config.GlobalHotkeys ??= [];

        if (config.SystemProxyItem.SystemProxyExceptions.IsNullOrEmpty())
        {
            config.SystemProxyItem.SystemProxyExceptions = Utils.IsWindows() ? Global.SystemProxyExceptionsWindows : Global.SystemProxyExceptionsLinux;
        }

        return config;
    }

    /// <summary>
    /// Save the configuration to a file
    /// First writes to a temporary file, then replaces the original file
    /// </summary>
    /// <param name="_config">Configuration object to be saved</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public async Task<int> SaveConfig()
    {
        try
        {
            //save temp file
            var resPath = Utils.GetConfigPath(_configRes);
            var tempPath = $"{resPath}_temp";

            var content = JsonUtils.Serialize(Config, true, true);
            if (content.IsNullOrEmpty())
            {
                return -1;
            }
            await File.WriteAllTextAsync(tempPath, content);

            //rename
            File.Move(tempPath, resPath, true);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return -1;
        }

        return 0;
    }

    #endregion Init

    #region UIItem

    public WindowSizeItem? GetWindowSizeItem(string typeName)
    {
        var sizeItem = Config?.UiItem?.WindowSizeItem?.FirstOrDefault(t => t.TypeName == typeName);
        if (sizeItem == null || sizeItem.Width <= 0 || sizeItem.Height <= 0)
        {
            return null;
        }

        return sizeItem;
    }

    public int SaveWindowSizeItem(string typeName, double width, double height)
    {
        var sizeItem = Config?.UiItem?.WindowSizeItem?.FirstOrDefault(t => t.TypeName == typeName);
        if (sizeItem == null)
        {
            sizeItem = new WindowSizeItem { TypeName = typeName };
            Config.UiItem.WindowSizeItem.Add(sizeItem);
        }

        sizeItem.Width = (int)width;
        sizeItem.Height = (int)height;

        return 0;
    }

    public int SaveMainGirdHeight(double height1, double height2)
    {
        var uiItem = Config.UiItem ?? new();

        uiItem.MainGirdHeight1 = (int)(height1 + 0.1);
        uiItem.MainGirdHeight2 = (int)(height2 + 0.1);

        return 0;
    }

    #endregion UIItem
}
