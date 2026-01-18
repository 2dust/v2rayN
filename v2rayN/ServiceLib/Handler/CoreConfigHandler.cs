namespace ServiceLib.Handler;

/// <summary>
/// Core configuration file processing class
/// </summary>
public static class CoreConfigHandler
{
    private static readonly string _tag = "CoreConfigHandler";

    private static readonly CompositeFormat _successfulConfigFormat =
        CompositeFormat.Parse(ResUI.SuccessfulConfiguration);

    public static async Task<RetResult> GenerateClientConfig(ProfileItem node, string? fileName)
    {
        var config = AppManager.Instance.Config;
        RetResult result;

        if (node.ConfigType == EConfigType.Custom)
        {
            result = node.CoreType switch
            {
                ECoreType.mihomo => await new CoreConfigClashService(config).GenerateClientCustomConfig(node, fileName),
                ECoreType.sing_box => await new CoreConfigSingboxService(config).GenerateClientCustomConfig(node, fileName),
                _ => await GenerateClientCustomConfig(node, fileName)
            };
        }
        else if (AppManager.Instance.GetCoreType(node, node.ConfigType) == ECoreType.sing_box)
        {
            result = await new CoreConfigSingboxService(config).GenerateClientConfigContent(node);
        }
        else
        {
            result = await new CoreConfigV2rayService(config).GenerateClientConfigContent(node);
        }
        if (result.Success != true)
        {
            return result;
        }
        if (fileName.IsNotEmpty() && result.Data is not null)
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
            if (node is null || fileName is null)
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            if (File.Exists(fileName))
            {
                File.SetAttributes(fileName, FileAttributes.Normal); //If the file has a read-only attribute, direct deletion will fail
                File.Delete(fileName);
            }

            var addressFileName = node.Address;
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

            ret.Msg = string.Format(CultureInfo.CurrentCulture, _successfulConfigFormat, "");
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
        RetResult result;
        var initPort = AppManager.Instance.GetLocalPort(EInboundProtocol.speedtest);
        var port = Utils.GetFreePort(initPort + testItem.QueueNum);
        testItem.Port = port;

        if (AppManager.Instance.GetCoreType(node, node.ConfigType) == ECoreType.sing_box)
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
}
