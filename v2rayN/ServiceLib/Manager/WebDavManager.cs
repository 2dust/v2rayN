using WebDav;

namespace ServiceLib.Manager;

public sealed class WebDavManager
{
    private static readonly Lazy<WebDavManager> _instance = new(() => new());
    public static WebDavManager Instance => _instance.Value;

    private readonly Config _config;
    private readonly Func<WebDavClientParams, WebDavClient> _clientFactory;
    private WebDavClient? _client;
    private string? _lastDescription;
    private string _webDir = Global.AppName + "_backup";
    private readonly string _webFileName = "backup.zip";
    private readonly string _tag = "WebDav--";

    public WebDavManager() : this(AppManager.Instance.Config, parameters => new WebDavClient(parameters))
    {
    }

    internal WebDavManager(Config config, Func<WebDavClientParams, WebDavClient> clientFactory)
    {
        _config = config;
        _clientFactory = clientFactory;
    }

    private async Task<bool> GetClient()
    {
        try
        {
            if (_config.WebDavItem.Url.IsNullOrEmpty()
            || _config.WebDavItem.UserName.IsNullOrEmpty()
            || _config.WebDavItem.Password.IsNullOrEmpty())
            {
                throw new ArgumentException("webdav parameter error or null");
            }
            if (_client != null)
            {
                _client?.Dispose();
                _client = null;
            }
            if (_config.WebDavItem.DirName.IsNullOrEmpty())
            {
                _webDir = Global.AppName + "_backup";
            }
            else
            {
                _webDir = _config.WebDavItem.DirName.TrimEx();
            }

            // Ensure BaseAddress URL ends with a trailing slash
            var baseUrl = _config.WebDavItem.Url.Trim().TrimEnd('/') + "/";

            var clientParams = new WebDavClientParams
            {
                BaseAddress = new Uri(baseUrl),
                Credentials = new NetworkCredential(_config.WebDavItem.UserName, _config.WebDavItem.Password)
            };
            _client = _clientFactory(clientParams);
        }
        catch (Exception ex)
        {
            SaveLog(ex);
            return false;
        }
        return await Task.FromResult(true);
    }

    private async Task<bool> TryCreateDir()
    {
        if (_client is null)
        {
            return false;
        }
        try
        {
            var result2 = await _client.Mkcol(_webDir);
            if (result2.IsSuccessful)
            {
                return true;
            }
            SaveLog(result2.Description);
        }
        catch (Exception ex)
        {
            SaveLog(ex);
        }
        return false;
    }

    private void SaveLog(string desc)
    {
        _lastDescription = desc;
        Logging.SaveLog(_tag + desc);
    }

    private void SaveLog(Exception ex)
    {
        _lastDescription = ex.Message;
        Logging.SaveLog(_tag, ex);
    }

    public async Task<WebDavCheckStatus> CheckConnection()
    {
        _lastDescription = null;
        if (await GetClient() == false)
        {
            return WebDavCheckStatus.Failed;
        }

        var canRead = false;
        var backupMissing = false;
        var readForbidden = false;
        try
        {
            using var response = await _client!.GetRawFile($"{_webDir}/{_webFileName}");
            if (response.IsSuccessful)
            {
                canRead = true;
            }
            else if (response.StatusCode == 404)
            {
                backupMissing = true;
            }
            else if (response.StatusCode == 401)
            {
                return WebDavCheckStatus.Unauthorized;
            }
            else if (response.StatusCode == 403)
            {
                readForbidden = true;
            }
            else
            {
                SaveLog(response.Description);
                return WebDavCheckStatus.Failed;
            }
        }
        catch (Exception ex)
        {
            SaveLog(ex);
            return WebDavCheckStatus.Failed;
        }

        var testPath = $"{_webDir}/v2rayN_check_{Guid.NewGuid():N}";
        var uploaded = false;
        var writeUnauthorized = false;
        try
        {
            using var content = new StringContent("v2rayN WebDAV check");
            var result = await _client!.PutFile(testPath, content);
            if (result.StatusCode == 404 && await TryCreateDir())
            {
                using var retryContent = new StringContent("v2rayN WebDAV check");
                result = await _client.PutFile(testPath, retryContent);
            }

            uploaded = result.IsSuccessful;
            if (!uploaded)
            {
                writeUnauthorized = result.StatusCode == 401;
                if (!writeUnauthorized)
                {
                    SaveLog(result.Description);
                }
            }
        }
        catch (Exception ex)
        {
            SaveLog(ex);
        }

        if (uploaded)
        {
            try
            {
                var deleted = await _client!.Delete(testPath);
                if (!deleted.IsSuccessful)
                {
                    SaveLog(deleted.Description);
                    return WebDavCheckStatus.CleanupFailed;
                }
            }
            catch (Exception ex)
            {
                SaveLog(ex);
                return WebDavCheckStatus.CleanupFailed;
            }
        }

        if (writeUnauthorized && !canRead)
        {
            return WebDavCheckStatus.Unauthorized;
        }

        if (canRead)
        {
            return uploaded ? WebDavCheckStatus.ReadWrite : WebDavCheckStatus.ReadOnly;
        }
        if (backupMissing)
        {
            return uploaded ? WebDavCheckStatus.BackupMissingWritable : WebDavCheckStatus.BackupMissingNoWrite;
        }
        return uploaded ? WebDavCheckStatus.WriteOnly : readForbidden ? WebDavCheckStatus.ReadForbidden : WebDavCheckStatus.Failed;
    }

    public async Task<bool> PutFile(string fileName)
    {
        if (await GetClient() == false)
        {
            return false;
        }
        await TryCreateDir();

        try
        {
            await using var fs = File.OpenRead(fileName);
            var result = await _client.PutFile($"{_webDir}/{_webFileName}", fs); // upload a resource
            if (result.IsSuccessful)
            {
                return true;
            }

            SaveLog(result.Description);
        }
        catch (Exception ex)
        {
            SaveLog(ex);
        }
        return false;
    }

    public async Task<bool> GetRawFile(string fileName)
    {
        if (await GetClient() == false)
        {
            return false;
        }
        try
        {
            using var response = await _client!.GetRawFile($"{_webDir}/{_webFileName}");
            if (!response.IsSuccessful)
            {
                SaveLog(response.Description);
                return false;
            }

            await using var outputFileStream = new FileStream(fileName, FileMode.Create);
            await response.Stream.CopyToAsync(outputFileStream);
            return true;
        }
        catch (Exception ex)
        {
            SaveLog(ex);
        }
        return false;
    }

    public string GetLastError() => _lastDescription ?? string.Empty;
}

public enum WebDavCheckStatus
{
    ReadWrite,
    ReadOnly,
    BackupMissingWritable,
    BackupMissingNoWrite,
    WriteOnly,
    ReadForbidden,
    Unauthorized,
    CleanupFailed,
    Failed,
}
