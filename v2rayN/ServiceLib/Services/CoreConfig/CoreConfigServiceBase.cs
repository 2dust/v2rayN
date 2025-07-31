using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core.Tokens;

namespace ServiceLib.Services.CoreConfig;

public abstract class CoreConfigServiceMinimalBase(Config config)
{
    public virtual string _tag => GetType().Name;
    protected Config _config = config;

    public virtual Task<RetResult> GeneratePassthroughConfig(ProfileItem node)
    {
        return GeneratePassthroughConfig(node, AppHandler.Instance.GetLocalPort(EInboundProtocol.split));
    }
    public virtual Task<RetResult> GenerateClientSpeedtestConfig(ProfileItem node, int port)
    {
        return GeneratePassthroughConfig(node, port);
    }
    protected abstract Task<RetResult> GeneratePassthroughConfig(ProfileItem node, int port);
    public virtual async Task<RetResult> GenerateClientCustomConfig(ProfileItem node, string? fileName)
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
}

public abstract class CoreConfigServiceBase(Config config) : CoreConfigServiceMinimalBase(config)
{
    public abstract Task<RetResult> GenerateClientConfigContent(ProfileItem node);
    public abstract Task<RetResult> GenerateClientMultipleLoadConfig(List<ProfileItem> selecteds, EMultipleLoad multipleLoad);
    public virtual Task<RetResult> GenerateClientMultipleLoadConfig(List<ProfileItem> selecteds)
    {
        return GenerateClientMultipleLoadConfig(selecteds, EMultipleLoad.LeastPing);
    }
    public abstract Task<RetResult> GenerateClientSpeedtestConfig(List<ServerTestItem> selecteds);
}
