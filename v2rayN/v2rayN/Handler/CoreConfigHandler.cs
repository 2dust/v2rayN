using System.IO;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Handler
{
    /// <summary>
    /// Core configuration file processing class
    /// </summary>
    internal class CoreConfigHandler
    {
        public static int GenerateClientConfig(ProfileItem node, string? fileName, out string msg, out string content)
        {
            content = string.Empty;
            try
            {
                if (node == null)
                {
                    msg = ResUI.CheckServerSettings;
                    return -1;
                }
                var config = LazyConfig.Instance.GetConfig();

                msg = ResUI.InitialConfiguration;
                if (node.configType == EConfigType.Custom)
                {
                    return GenerateClientCustomConfig(node, fileName, out msg);
                }
                else if (config.tunModeItem.enableTun || LazyConfig.Instance.GetCoreType(node, node.configType) == ECoreType.sing_box)
                {
                    var configGenSingbox = new CoreConfigSingbox(config);
                    if (configGenSingbox.GenerateClientConfigContent(node, out SingboxConfig? singboxConfig, out msg) != 0)
                    {
                        return -1;
                    }
                    if (Utils.IsNullOrEmpty(fileName))
                    {
                        content = Utils.ToJson(singboxConfig);
                    }
                    else
                    {
                        Utils.ToJsonFile(singboxConfig, fileName, false);
                    }
                }
                else
                {
                    var coreConfigV2ray = new CoreConfigV2ray(config);
                    if (coreConfigV2ray.GenerateClientConfigContent(node, out V2rayConfig? v2rayConfig, out msg) != 0)
                    {
                        return -1;
                    }
                    if (Utils.IsNullOrEmpty(fileName))
                    {
                        content = Utils.ToJson(v2rayConfig);
                    }
                    else
                    {
                        Utils.ToJsonFile(v2rayConfig, fileName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog("GenerateClientConfig", ex);
                msg = ResUI.FailedGenDefaultConfiguration;
                return -1;
            }
            return 0;
        }

        private static int GenerateClientCustomConfig(ProfileItem node, string? fileName, out string msg)
        {
            try
            {
                if (node == null || fileName is null)
                {
                    msg = ResUI.CheckServerSettings;
                    return -1;
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
                    msg = ResUI.FailedGenDefaultConfiguration;
                    return -1;
                }
                File.Copy(addressFileName, fileName);
                File.SetAttributes(fileName, FileAttributes.Normal); //Copy will keep the attributes of addressFileName, so we need to add write permissions to fileName just in case of addressFileName is a read-only file.

                //check again
                if (!File.Exists(fileName))
                {
                    msg = ResUI.FailedGenDefaultConfiguration;
                    return -1;
                }

                //overwrite port
                if (node.preSocksPort <= 0)
                {
                    var fileContent = File.ReadAllLines(fileName).ToList();
                    var coreType = LazyConfig.Instance.GetCoreType(node, node.configType);
                    switch (coreType)
                    {
                        case ECoreType.v2fly:
                        case ECoreType.SagerNet:
                        case ECoreType.Xray:
                        case ECoreType.v2fly_v5:
                            break;

                        case ECoreType.clash:
                        case ECoreType.clash_meta:
                        case ECoreType.mihomo:
                            //remove the original
                            var indexPort = fileContent.FindIndex(t => t.Contains("port:"));
                            if (indexPort >= 0)
                            {
                                fileContent.RemoveAt(indexPort);
                            }
                            indexPort = fileContent.FindIndex(t => t.Contains("socks-port:"));
                            if (indexPort >= 0)
                            {
                                fileContent.RemoveAt(indexPort);
                            }

                            fileContent.Add($"port: {LazyConfig.Instance.GetLocalPort(Global.InboundHttp)}");
                            fileContent.Add($"socks-port: {LazyConfig.Instance.GetLocalPort(Global.InboundSocks)}");
                            break;
                    }
                    File.WriteAllLines(fileName, fileContent);
                }

                msg = string.Format(ResUI.SuccessfulConfiguration, "");
            }
            catch (Exception ex)
            {
                Utils.SaveLog("GenerateClientCustomConfig", ex);
                msg = ResUI.FailedGenDefaultConfiguration;
                return -1;
            }
            return 0;
        }

        public static string GenerateClientSpeedtestConfigString(Config config, List<ServerTestItem> selecteds, out string msg)
        {
            var coreConfigV2ray = new CoreConfigV2ray(config);
            return coreConfigV2ray.GenerateClientSpeedtestConfigString(selecteds, out msg);
        }
    }
}