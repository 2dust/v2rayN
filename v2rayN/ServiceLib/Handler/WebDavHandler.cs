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
        private string _webFileName = "backup.zip";
        private string _logTitle = "WebDav--";

        public WebDavHandler()
        {
            _config = LazyConfig.Instance.Config;
        }

        private async Task<bool> GetClient()
        {
            try
            {
                if (_config.webDavItem.url.IsNullOrEmpty()
                || _config.webDavItem.userName.IsNullOrEmpty()
                || _config.webDavItem.password.IsNullOrEmpty())
                {
                    throw new ArgumentException("webdav parameter error or null");
                }
                if (_client != null)
                {
                    _client?.Dispose();
                    _client = null;
                }

                var clientParams = new WebDavClientParams
                {
                    BaseAddress = new Uri(_config.webDavItem.url),
                    Credentials = new NetworkCredential(_config.webDavItem.userName, _config.webDavItem.password)
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
            Logging.SaveLog(_logTitle + desc);
        }

        private void SaveLog(Exception ex)
        {
            _lastDescription = ex.Message;
            Logging.SaveLog(_logTitle, ex);
        }

        public async Task<bool> CheckConnection()
        {
            if (await GetClient() == false)
            {
                return false;
            }
            await TryCreateDir();

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
                return false;
            }
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
                using var fs = File.OpenRead(fileName);
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
                }
                using var outputFileStream = new FileStream(fileName, FileMode.Create);
                response.Stream.CopyTo(outputFileStream);
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