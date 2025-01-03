using System.Net;
using WebDav;

namespace ServiceLib.Handler
{
    public sealed class WebDavHandler
    {
        private static readonly Lazy<WebDavHandler> _instance = new(() => new());
        public static WebDavHandler Instance => _instance.Value;

        private Config? _config;
        private WebDavClient? _client;
        private string? _lastDescription;
        private string _webDir = Global.AppName + "_backup";
        private readonly string _webFileName = "backup.zip";
        private readonly string _tag = "WebDav--";
         

        public WebDavHandler()
        {
            _config = AppHandler.Instance.Config;
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

                var clientParams = new WebDavClientParams
                {
                    BaseAddress = new Uri(_config.WebDavItem.Url),
                    Credentials = new NetworkCredential(_config.WebDavItem.UserName, _config.WebDavItem.Password)
                };
                _client = new WebDavClient(clientParams);
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
            if (_client is null) return false;
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

        public async Task<bool> CheckConnection()
        {
            if (await GetClient() == false)
            {
                return false;
            }
            await TryCreateDir();

            try
            {
                var testName = "readme_test";
                var myContent = new StringContent(testName);
                var result = await _client.PutFile($"{_webDir}/{testName}", myContent);
                if (result.IsSuccessful)
                {
                    await _client.Delete($"{_webDir}/{testName}");
                    return true;
                }
                else
                {
                    SaveLog(result.Description);
                }
            }
            catch (Exception ex)
            {
                SaveLog(ex);
            }
            return false;
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
            await TryCreateDir();

            try
            {
                var response = await _client.GetRawFile($"{_webDir}/{_webFileName}");
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
}