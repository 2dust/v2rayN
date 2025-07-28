using ServiceLib.Services.CoreConfig.Minimal;

namespace ServiceLib.Handler;

/// <summary>
/// Core configuration file processing class
/// </summary>
public class CoreConfigHandler
{
    private static readonly string _tag = "CoreConfigHandler";

    public static async Task<RetResult> GenerateClientConfig(ProfileItem node, string? fileName)
    {
        var config = AppHandler.Instance.Config;
        var result = new RetResult();

        if (node.ConfigType == EConfigType.Custom)
        {
            result = node.CoreType switch
            {
                ECoreType.mihomo => await new CoreConfigClashService(config).GenerateClientCustomConfig(node, fileName),
                ECoreType.sing_box => await new CoreConfigSingboxService(config).GenerateClientCustomConfig(node, fileName),
                _ => await GenerateClientCustomConfig(node, fileName)
            };
        }
        else if (AppHandler.Instance.GetCoreType(node, node.ConfigType) == ECoreType.sing_box)
        {
            result = await new CoreConfigSingboxService(config).GenerateClientConfigContent(node);
        }
        else if (AppHandler.Instance.GetCoreType(node, node.ConfigType) == ECoreType.Xray)
        {
            result = await new CoreConfigV2rayService(config).GenerateClientConfigContent(node);
        }
        else
        {
            result.Msg = ResUI.OperationFailed;
            result.Success = false;
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

    public static async Task<RetResult> GeneratePureEndpointConfig(ProfileItem node, string? fileName)
    {
        var config = AppHandler.Instance.Config;
        var result = new RetResult();

        var coreType = AppHandler.Instance.GetSplitCoreType(node, node.ConfigType);

        result = coreType switch
        {
            ECoreType.sing_box => await new CoreConfigSingboxService(config).GeneratePureEndpointConfig(node),
            ECoreType.Xray => await new CoreConfigV2rayService(config).GeneratePureEndpointConfig(node),
            ECoreType.hysteria2 => await new CoreConfigHy2Service(config).GeneratePureEndpointConfig(node),
            ECoreType.naiveproxy => await new CoreConfigNaiveService(config).GeneratePureEndpointConfig(node),
            ECoreType.tuic => await new CoreConfigTuicService(config).GeneratePureEndpointConfig(node),
            ECoreType.juicity => await new CoreConfigJuicityService(config).GeneratePureEndpointConfig(node),
            ECoreType.brook => await new CoreConfigBrookService(config).GeneratePureEndpointConfig(node),
            ECoreType.shadowquic => await new CoreConfigShadowquicService(config).GeneratePureEndpointConfig(node),
            _ => throw new NotImplementedException(),
        };

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

    private static async Task<RetResult> GenerateClientCustomConfig(ProfileItem node, string? fileName)
    {
        var ret = new RetResult();
        try
        {
            if (node == null || fileName is null)
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            if (File.Exists(fileName))
            {
                File.SetAttributes(fileName, FileAttributes.Normal); //If the file has a read-only attribute, direct deletion will fail
                File.Delete(fileName);
            }

            string addressFileName = node.Address;
            if (!File.Exists(addressFileName))
            {
                addressFileName = Utils.GetConfigPath(addressFileName);
            }
            if (!File.Exists(addressFileName))
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }
            File.Copy(addressFileName, fileName);
            File.SetAttributes(fileName, FileAttributes.Normal); //Copy will keep the attributes of addressFileName, so we need to add write permissions to fileName just in case of addressFileName is a read-only file.

            //check again
            if (!File.Exists(fileName))
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;
            return await Task.FromResult(ret);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    public static async Task<RetResult> GenerateClientSpeedtestConfig(Config config, string fileName, List<ServerTestItem> selecteds, ECoreType coreType)
    {
        var result = new RetResult();
        if (coreType == ECoreType.sing_box)
        {
            result = await new CoreConfigSingboxService(config).GenerateClientSpeedtestConfig(selecteds);
        }
        else if (coreType == ECoreType.Xray)
        {
            result = await new CoreConfigV2rayService(config).GenerateClientSpeedtestConfig(selecteds);
        }
        if (result.Success != true)
        {
            return result;
        }
        await File.WriteAllTextAsync(fileName, result.Data.ToString());
        return result;
    }

    public static async Task<RetResult> GenerateClientSpeedtestConfig(Config config, ProfileItem node, ServerTestItem testItem, string fileName)
    {
        var result = new RetResult();
        var initPort = AppHandler.Instance.GetLocalPort(EInboundProtocol.speedtest);
        var port = Utils.GetFreePort(initPort + testItem.QueueNum);
        testItem.Port = port;

        if (AppHandler.Instance.GetCoreType(node, node.ConfigType) == ECoreType.sing_box)
        {
            result = await new CoreConfigSingboxService(config).GenerateClientSpeedtestConfig(node, port);
        }
        else
        {
            result = await new CoreConfigV2rayService(config).GenerateClientSpeedtestConfig(node, port);
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
        else
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
}
