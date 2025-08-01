using ServiceLib.Services.CoreConfig.Minimal;

namespace ServiceLib.Handler;

/// <summary>
/// Core configuration file processing class
/// </summary>
public class CoreConfigHandler
{
    private static readonly string _tag = "CoreConfigHandler";

    public static async Task<RetResult> GenerateClientConfig(CoreLaunchContext context, string? fileName)
    {
        var result = new RetResult();

        if (context.ConfigType == EConfigType.Custom)
        {
            result = await GetCoreConfigServiceForCustom(context.CoreType).GenerateClientCustomConfig(context.Node, fileName);
        }
        else
        {
            try
            {
                result = await GetCoreConfigServiceForClientConfig(context.CoreType).GenerateClientConfigContent(context.Node);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
                result.Msg = ResUI.FailedGenDefaultConfiguration;
                return result;
            }
        }
        if (result.Success != true)
        {
            return result;
        }
        if (fileName.IsNotEmpty() && result.Data != null)
        {
            await File.WriteAllTextAsync(fileName, result.Data.ToString());
        }

        return result;
    }

    public static async Task<RetResult> GeneratePassthroughConfig(CoreLaunchContext context, string? fileName)
    {
        var result = new RetResult();

        try
        {
            result = await GetCoreConfigServiceForPassthrough(context.CoreType).GeneratePassthroughConfig(context.Node);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            result.Msg = ResUI.FailedGenDefaultConfiguration;
            return result;
        }

        if (result.Success != true)
        {
            return result;
        }
        if (fileName.IsNotEmpty() && result.Data != null)
        {
            await File.WriteAllTextAsync(fileName, result.Data.ToString());
        }
        return result;
    }

    public static async Task<RetResult> GenerateClientSpeedtestConfig(Config config, string fileName, List<ServerTestItem> selecteds, ECoreType coreType)
    {
        var result = new RetResult();
        try
        {
            result = await GetCoreConfigServiceForMultipleSpeedtest(coreType).GenerateClientSpeedtestConfig(selecteds);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            result.Msg = ResUI.FailedGenDefaultConfiguration;
            return result;
        }
        if (result.Success != true)
        {
            return result;
        }
        await File.WriteAllTextAsync(fileName, result.Data.ToString());
        return result;
    }

    public static async Task<RetResult> GenerateClientSpeedtestConfig(Config config, CoreLaunchContext context, ServerTestItem testItem, string fileName)
    {
        var result = new RetResult();
        var initPort = AppHandler.Instance.GetLocalPort(EInboundProtocol.speedtest);
        var port = Utils.GetFreePort(initPort + testItem.QueueNum);
        testItem.Port = port;

        try
        {
            result = await GetCoreConfigServiceForSpeedtest(context.CoreType).GenerateClientSpeedtestConfig(context.Node, port);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            result.Msg = ResUI.FailedGenDefaultConfiguration;
            return result;
        }

        if (result.Success != true)
        {
            return result;
        }

        await File.WriteAllTextAsync(fileName, result.Data.ToString());
        return result;
    }

    public static async Task<RetResult> GenerateClientMultipleLoadConfig(Config config, string fileName, List<ProfileItem> selecteds, ECoreType coreType, EMultipleLoad multipleLoad)
    {
        var result = new RetResult();
        if (coreType == ECoreType.sing_box)
        {
            result = await new CoreConfigSingboxService(config).GenerateClientMultipleLoadConfig(selecteds);
        }
        else if (coreType == ECoreType.Xray)
        {
            result = await new CoreConfigV2rayService(config).GenerateClientMultipleLoadConfig(selecteds, multipleLoad);
        }

        if (result.Success != true)
        {
            return result;
        }
        await File.WriteAllTextAsync(fileName, result.Data.ToString());
        return result;
    }

    private static CoreConfigServiceMinimalBase GetCoreConfigServiceForPassthrough(ECoreType coreType)
    {
        switch (coreType)
        {
            case ECoreType.sing_box:
                return new CoreConfigSingboxService(AppHandler.Instance.Config);
            case ECoreType.Xray:
                return new CoreConfigV2rayService(AppHandler.Instance.Config);
            case ECoreType.hysteria2:
                return new CoreConfigHy2Service(AppHandler.Instance.Config);
            case ECoreType.naiveproxy:
                return new CoreConfigNaiveService(AppHandler.Instance.Config);
            case ECoreType.tuic:
                return new CoreConfigTuicService(AppHandler.Instance.Config);
            case ECoreType.juicity:
                return new CoreConfigJuicityService(AppHandler.Instance.Config);
            case ECoreType.brook:
                return new CoreConfigBrookService(AppHandler.Instance.Config);
            case ECoreType.shadowquic:
                return new CoreConfigShadowquicService(AppHandler.Instance.Config);
            default:
                throw new NotImplementedException($"Core type {coreType} is not implemented for passthrough configuration.");
        }
    }

    private static CoreConfigServiceMinimalBase GetCoreConfigServiceForSpeedtest(ECoreType coreType)
    {
        switch (coreType)
        {
            case ECoreType.sing_box:
                return new CoreConfigSingboxService(AppHandler.Instance.Config);
            case ECoreType.Xray:
                return new CoreConfigV2rayService(AppHandler.Instance.Config);
            case ECoreType.hysteria2:
                return new CoreConfigHy2Service(AppHandler.Instance.Config);
            case ECoreType.naiveproxy:
                return new CoreConfigNaiveService(AppHandler.Instance.Config);
            case ECoreType.tuic:
                return new CoreConfigTuicService(AppHandler.Instance.Config);
            case ECoreType.juicity:
                return new CoreConfigJuicityService(AppHandler.Instance.Config);
            case ECoreType.brook:
                return new CoreConfigBrookService(AppHandler.Instance.Config);
            case ECoreType.shadowquic:
                return new CoreConfigShadowquicService(AppHandler.Instance.Config);
            default:
                throw new NotImplementedException($"Core type {coreType} is not implemented for passthrough configuration.");
        }
    }

    private static CoreConfigServiceBase GetCoreConfigServiceForMultipleSpeedtest(ECoreType coreType)
    {
        switch (coreType)
        {
            case ECoreType.sing_box:
                return new CoreConfigSingboxService(AppHandler.Instance.Config);
            case ECoreType.Xray:
                return new CoreConfigV2rayService(AppHandler.Instance.Config);
            default:
                throw new NotImplementedException($"Core type {coreType} is not implemented for passthrough configuration.");
        }
    }

    private static CoreConfigServiceMinimalBase GetCoreConfigServiceForCustom(ECoreType coreType)
    {
        switch (coreType)
        {
            case ECoreType.mihomo:
                return new CoreConfigClashService(AppHandler.Instance.Config);
            case ECoreType.sing_box:
                return new CoreConfigSingboxService(AppHandler.Instance.Config);
            case ECoreType.hysteria2:
                return new CoreConfigHy2Service(AppHandler.Instance.Config);
            default:
                // CoreConfigServiceMinimalBase
                return new CoreConfigV2rayService(AppHandler.Instance.Config);
        }
    }

    private static CoreConfigServiceBase GetCoreConfigServiceForClientConfig(ECoreType coreType)
    {
        switch (coreType)
        {
            case ECoreType.sing_box:
                return new CoreConfigSingboxService(AppHandler.Instance.Config);
            case ECoreType.Xray:
                return new CoreConfigV2rayService(AppHandler.Instance.Config);
            default:
                throw new NotImplementedException($"Core type {coreType} is not implemented for client configuration.");
        }
    }
}
