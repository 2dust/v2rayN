using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ServiceLib.Services;

public class UpdateService
{
    private Action<bool, string>? _updateFunc;
    private readonly int _timeout = 30;
    private static readonly string _tag = "UpdateService";

    public async Task CheckUpdateGuiN(Config config, Action<bool, string> updateFunc, bool preRelease)
    {
        _updateFunc = updateFunc;
        var url = string.Empty;
        var fileName = string.Empty;

        DownloadService downloadHandle = new();
        downloadHandle.UpdateCompleted += (sender2, args) =>
        {
            if (args.Success)
            {
                _updateFunc?.Invoke(false, ResUI.MsgDownloadV2rayCoreSuccessfully);
                _updateFunc?.Invoke(true, Utils.UrlEncode(fileName));
            }
            else
            {
                _updateFunc?.Invoke(false, args.Msg);
            }
        };
        downloadHandle.Error += (sender2, args) =>
        {
            _updateFunc?.Invoke(false, args.GetException().Message);
        };

        _updateFunc?.Invoke(false, string.Format(ResUI.MsgStartUpdating, ECoreType.v2rayN));
        var result = await CheckUpdateAsync(downloadHandle, ECoreType.v2rayN, preRelease);
        if (result.Success)
        {
            _updateFunc?.Invoke(false, string.Format(ResUI.MsgParsingSuccessfully, ECoreType.v2rayN));
            _updateFunc?.Invoke(false, result.Msg);

            url = result.Data?.ToString();
            fileName = Utils.GetTempPath(Utils.GetGuid());
            await downloadHandle.DownloadFileAsync(url, fileName, true, _timeout);
        }
        else
        {
            _updateFunc?.Invoke(false, result.Msg);
        }
    }

    public async Task CheckUpdateCore(ECoreType type, Config config, Action<bool, string> updateFunc, bool preRelease)
    {
        _updateFunc = updateFunc;
        var url = string.Empty;
        var fileName = string.Empty;

        DownloadService downloadHandle = new();
        downloadHandle.UpdateCompleted += (sender2, args) =>
        {
            if (args.Success)
            {
                _updateFunc?.Invoke(false, ResUI.MsgDownloadV2rayCoreSuccessfully);
                _updateFunc?.Invoke(false, ResUI.MsgUnpacking);

                try
                {
                    _updateFunc?.Invoke(true, fileName);
                }
                catch (Exception ex)
                {
                    _updateFunc?.Invoke(false, ex.Message);
                }
            }
            else
            {
                _updateFunc?.Invoke(false, args.Msg);
            }
        };
        downloadHandle.Error += (sender2, args) =>
        {
            _updateFunc?.Invoke(false, args.GetException().Message);
        };

        _updateFunc?.Invoke(false, string.Format(ResUI.MsgStartUpdating, type));
        var result = await CheckUpdateAsync(downloadHandle, type, preRelease);
        if (result.Success)
        {
            _updateFunc?.Invoke(false, string.Format(ResUI.MsgParsingSuccessfully, type));
            _updateFunc?.Invoke(false, result.Msg);

            url = result.Data?.ToString();
            var ext = url.Contains(".tar.gz") ? ".tar.gz" : Path.GetExtension(url);
            fileName = Utils.GetTempPath(Utils.GetGuid() + ext);
            await downloadHandle.DownloadFileAsync(url, fileName, true, _timeout);
        }
        else
        {
            if (!result.Msg.IsNullOrEmpty())
            {
                _updateFunc?.Invoke(false, result.Msg);
            }
        }
    }

    public async Task UpdateGeoFileAll(Config config, Action<bool, string> updateFunc)
    {
        await UpdateGeoFiles(config, updateFunc);
        await UpdateOtherFiles(config, updateFunc);
        await UpdateSrsFileAll(config, updateFunc);
        _updateFunc?.Invoke(true, string.Format(ResUI.MsgDownloadGeoFileSuccessfully, "geo"));
    }

    #region CheckUpdate private

    private async Task<RetResult> CheckUpdateAsync(DownloadService downloadHandle, ECoreType type, bool preRelease)
    {
        try
        {
            var result = await GetRemoteVersion(downloadHandle, type, preRelease);
            if (!result.Success || result.Data is null)
            {
                return result;
            }
            return await ParseDownloadUrl(type, (SemanticVersion)result.Data);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            _updateFunc?.Invoke(false, ex.Message);
            return new RetResult(false, ex.Message);
        }
    }

    private async Task<RetResult> GetRemoteVersion(DownloadService downloadHandle, ECoreType type, bool preRelease)
    {
        var coreInfo = CoreInfoHandler.Instance.GetCoreInfo(type);
        var tagName = string.Empty;
        if (preRelease)
        {
            var url = coreInfo?.ReleaseApiUrl;
            var result = await downloadHandle.TryDownloadString(url, true, Global.AppName);
            if (result.IsNullOrEmpty())
            {
                return new RetResult(false, "");
            }

            var gitHubReleases = JsonUtils.Deserialize<List<GitHubRelease>>(result);
            var gitHubRelease = preRelease ? gitHubReleases?.First() : gitHubReleases?.First(r => r.Prerelease == false);
            tagName = gitHubRelease?.TagName;
            //var body = gitHubRelease?.Body;
        }
        else
        {
            var url = Path.Combine(coreInfo.Url, "latest");
            var lastUrl = await downloadHandle.UrlRedirectAsync(url, true);
            if (lastUrl == null)
            {
                return new RetResult(false, "");
            }

            tagName = lastUrl?.Split("/tag/").LastOrDefault();
        }
        return new RetResult(true, "", new SemanticVersion(tagName));
    }

    private async Task<SemanticVersion> GetCoreVersion(ECoreType type)
    {
        try
        {
            var coreInfo = CoreInfoHandler.Instance.GetCoreInfo(type);
            string filePath = string.Empty;
            foreach (var name in coreInfo.CoreExes)
            {
                var vName = Utils.GetBinPath(Utils.GetExeName(name), coreInfo.CoreType.ToString());
                if (File.Exists(vName))
                {
                    filePath = vName;
                    break;
                }
            }

            if (!File.Exists(filePath))
            {
                string msg = string.Format(ResUI.NotFoundCore, @"", "", "");
                //ShowMsg(true, msg);
                return new SemanticVersion("");
            }

            var result = await Utils.GetCliWrapOutput(filePath, coreInfo.VersionArg);
            var echo = result ?? "";
            string version = string.Empty;
            switch (type)
            {
                case ECoreType.v2fly:
                case ECoreType.Xray:
                case ECoreType.v2fly_v5:
                    version = Regex.Match(echo, $"{coreInfo.Match} ([0-9.]+) \\(").Groups[1].Value;
                    break;

                case ECoreType.mihomo:
                    version = Regex.Match(echo, $"v[0-9.]+").Groups[0].Value;
                    break;

                case ECoreType.sing_box:
                    version = Regex.Match(echo, $"([0-9.]+)").Groups[1].Value;
                    break;
            }
            return new SemanticVersion(version);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            _updateFunc?.Invoke(false, ex.Message);
            return new SemanticVersion("");
        }
    }

    private async Task<RetResult> ParseDownloadUrl(ECoreType type, SemanticVersion version)
    {
        try
        {
            var coreInfo = CoreInfoHandler.Instance.GetCoreInfo(type);
            var coreUrl = await GetUrlFromCore(coreInfo) ?? string.Empty;
            SemanticVersion curVersion;
            string message;
            string? url;
            switch (type)
            {
                case ECoreType.v2fly:
                case ECoreType.Xray:
                case ECoreType.v2fly_v5:
                    {
                        curVersion = await GetCoreVersion(type);
                        message = string.Format(ResUI.IsLatestCore, type, curVersion.ToVersionString("v"));
                        url = string.Format(coreUrl, version.ToVersionString("v"));
                        break;
                    }
                case ECoreType.mihomo:
                    {
                        curVersion = await GetCoreVersion(type);
                        message = string.Format(ResUI.IsLatestCore, type, curVersion);
                        url = string.Format(coreUrl, version.ToVersionString("v"));
                        break;
                    }
                case ECoreType.sing_box:
                    {
                        curVersion = await GetCoreVersion(type);
                        message = string.Format(ResUI.IsLatestCore, type, curVersion.ToVersionString("v"));
                        url = string.Format(coreUrl, version.ToVersionString("v"), version);
                        break;
                    }
                case ECoreType.v2rayN:
                    {
                        curVersion = new SemanticVersion(Utils.GetVersionInfo());
                        message = string.Format(ResUI.IsLatestN, type, curVersion);
                        url = string.Format(coreUrl, version);
                        break;
                    }
                default:
                    throw new ArgumentException("Type");
            }

            if (curVersion >= version && version != new SemanticVersion(0, 0, 0))
            {
                return new RetResult(false, message);
            }

            return new RetResult(true, "", url);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            _updateFunc?.Invoke(false, ex.Message);
            return new RetResult(false, ex.Message);
        }
    }

    private async Task<string?> GetUrlFromCore(CoreInfo? coreInfo)
    {
        if (Utils.IsWindows())
        {
            var url = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => coreInfo?.DownloadUrlWinArm64,
                Architecture.X64 => coreInfo?.DownloadUrlWin64,
                _ => null,
            };

            if (coreInfo?.CoreType != ECoreType.v2rayN)
            {
                return url;
            }

            //Check for standalone windows .Net version
            if (File.Exists(Path.Combine(Utils.GetBaseDirectory(), "wpfgfx_cor3.dll"))
                && File.Exists(Path.Combine(Utils.GetBaseDirectory(), "D3DCompiler_47_cor3.dll")))
            {
                return url?.Replace(".zip", "-SelfContained.zip");
            }

            //Check for avalonia desktop windows version
            if (File.Exists(Path.Combine(Utils.GetBaseDirectory(), "libHarfBuzzSharp.dll")))
            {
                return url?.Replace(".zip", "-desktop.zip");
            }

            return url;
        }
        else if (Utils.IsLinux())
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => coreInfo?.DownloadUrlLinuxArm64,
                Architecture.X64 => coreInfo?.DownloadUrlLinux64,
                _ => null,
            };
        }
        else if (Utils.IsOSX())
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => coreInfo?.DownloadUrlOSXArm64,
                Architecture.X64 => coreInfo?.DownloadUrlOSX64,
                _ => null,
            };
        }
        return await Task.FromResult("");
    }

    #endregion CheckUpdate private

    #region Geo private

    private async Task UpdateGeoFiles(Config config, Action<bool, string> updateFunc)
    {
        _updateFunc = updateFunc;

        var geoUrl = string.IsNullOrEmpty(config?.ConstItem.GeoSourceUrl)
            ? Global.GeoUrl
            : config.ConstItem.GeoSourceUrl;

        List<string> files = ["geosite", "geoip"];
        foreach (var geoName in files)
        {
            var fileName = $"{geoName}.dat";
            var targetPath = Utils.GetBinPath($"{fileName}");
            var url = string.Format(geoUrl, geoName);

            await DownloadGeoFile(url, fileName, targetPath, updateFunc);
        }
    }

    private async Task UpdateOtherFiles(Config config, Action<bool, string> updateFunc)
    {
        //If it is not in China area, no update is required
        if (config.ConstItem.GeoSourceUrl.IsNotEmpty())
        {
            return;
        }

        _updateFunc = updateFunc;

        foreach (var url in Global.OtherGeoUrls)
        {
            var fileName = Path.GetFileName(url);
            var targetPath = Utils.GetBinPath($"{fileName}");

            await DownloadGeoFile(url, fileName, targetPath, updateFunc);
        }
    }

    private async Task UpdateSrsFileAll(Config config, Action<bool, string> updateFunc)
    {
        _updateFunc = updateFunc;

        var geoipFiles = new List<string>();
        var geoSiteFiles = new List<string>();

        //Collect used files list
        var routingItems = await AppHandler.Instance.RoutingItems();
        foreach (var routing in routingItems)
        {
            var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet);
            foreach (var item in rules ?? [])
            {
                foreach (var ip in item.Ip ?? [])
                {
                    var prefix = "geoip:";
                    if (ip.StartsWith(prefix))
                    {
                        geoipFiles.Add(ip.Substring(prefix.Length));
                    }
                }

                foreach (var domain in item.Domain ?? [])
                {
                    var prefix = "geosite:";
                    if (domain.StartsWith(prefix))
                    {
                        geoSiteFiles.Add(domain.Substring(prefix.Length));
                    }
                }
            }
        }

        //append dns items TODO
        geoSiteFiles.Add("cn");
        geoSiteFiles.Add("geolocation-cn");
        geoSiteFiles.Add("category-ads-all");

        var path = Utils.GetBinPath("srss");
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        foreach (var item in geoipFiles.Distinct())
        {
            await UpdateSrsFile("geoip", item, config, updateFunc);
        }

        foreach (var item in geoSiteFiles.Distinct())
        {
            await UpdateSrsFile("geosite", item, config, updateFunc);
        }
    }

    private async Task UpdateSrsFile(string type, string srsName, Config config, Action<bool, string> updateFunc)
    {
        var srsUrl = string.IsNullOrEmpty(config.ConstItem.SrsSourceUrl)
                        ? Global.SingboxRulesetUrl
                        : config.ConstItem.SrsSourceUrl;

        var fileName = $"{type}-{srsName}.srs";
        var targetPath = Path.Combine(Utils.GetBinPath("srss"), fileName);
        var url = string.Format(srsUrl, type, $"{type}-{srsName}");

        await DownloadGeoFile(url, fileName, targetPath, updateFunc);
    }

    private async Task DownloadGeoFile(string url, string fileName, string targetPath, Action<bool, string> updateFunc)
    {
        var tmpFileName = Utils.GetTempPath(Utils.GetGuid());

        DownloadService downloadHandle = new();
        downloadHandle.UpdateCompleted += (sender2, args) =>
        {
            if (args.Success)
            {
                _updateFunc?.Invoke(false, string.Format(ResUI.MsgDownloadGeoFileSuccessfully, fileName));

                try
                {
                    if (File.Exists(tmpFileName))
                    {
                        File.Copy(tmpFileName, targetPath, true);

                        File.Delete(tmpFileName);
                        //_updateFunc?.Invoke(true, "");
                    }
                }
                catch (Exception ex)
                {
                    _updateFunc?.Invoke(false, ex.Message);
                }
            }
            else
            {
                _updateFunc?.Invoke(false, args.Msg);
            }
        };
        downloadHandle.Error += (sender2, args) =>
        {
            _updateFunc?.Invoke(false, args.GetException().Message);
        };

        await downloadHandle.DownloadFileAsync(url, tmpFileName, true, _timeout);
    }

    #endregion Geo private
}
