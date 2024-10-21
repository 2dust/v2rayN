namespace ServiceLib.Handler
{
    /// <summary>
    /// Core configuration file processing class
    /// </summary>
    public class CoreConfigHandler
    {
        public static async Task<RetResult> GenerateClientConfig(ProfileItem node, string? fileName)
        {
            var config = AppHandler.Instance.Config;
            var result = new RetResult();

            if (node.configType == EConfigType.Custom)
            {
                if (node.coreType is ECoreType.mihomo)
                {
                    result = await new CoreConfigClashService(config).GenerateClientCustomConfig(node, fileName);
                }
                if (node.coreType is ECoreType.sing_box)
                {
                    result = await new CoreConfigSingboxService(config).GenerateClientCustomConfig(node, fileName);
                }
                else
                {
                    result = await GenerateClientCustomConfig(node, fileName);
                }
            }
            else if (AppHandler.Instance.GetCoreType(node, node.configType) == ECoreType.sing_box)
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
            if (Utils.IsNotEmpty(fileName) && result.Data != null)
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

                string addressFileName = node.address;
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
                return ret;
            }
            catch (Exception ex)
            {
                Logging.SaveLog("GenerateClientCustomConfig", ex);
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

        public static async Task<RetResult> GenerateClientMultipleLoadConfig(Config config, string fileName, List<ProfileItem> selecteds, ECoreType coreType)
        {
            var result = new RetResult();
            if (coreType == ECoreType.sing_box)
            {
                result = await new CoreConfigSingboxService(config).GenerateClientMultipleLoadConfig(selecteds);
            }
            else if (coreType == ECoreType.Xray)
            {
                result = await new CoreConfigV2rayService(config).GenerateClientMultipleLoadConfig(selecteds);
            }

            if (result.Success != true)
            {
                return result;
            }
            await File.WriteAllTextAsync(fileName, result.Data.ToString());
            return result;
        }
    }
}