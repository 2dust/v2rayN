using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;

namespace ServiceLib.Common
{
    /// <summary>
    /// </summary>
    public class HttpClientHelper
    {
        private static readonly Lazy<HttpClientHelper> _instance = new(() =>
        {
            HttpClientHandler handler = new() { UseCookies = false };
            HttpClientHelper helper = new(new HttpClient(handler));
            return helper;
        });

        public static HttpClientHelper Instance => _instance.Value;
        private readonly HttpClient httpClient;

        private HttpClientHelper(HttpClient httpClient) => this.httpClient = httpClient;

        public async Task<string?> TryGetAsync(string url)
        {
            if (Utils.IsNullOrEmpty(url))
                return null;

            try
            {
                var response = await httpClient.GetAsync(url);
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> GetAsync(string url)
        {
            if (Utils.IsNullOrEmpty(url)) return null;
            return await httpClient.GetStringAsync(url);
        }

        public async Task<string?> GetAsync(HttpClient client, string url, CancellationToken token = default)
        {
            if (Utils.IsNullOrEmpty(url)) return null;
            return await client.GetStringAsync(url, token);
        }

        public async Task PutAsync(string url, Dictionary<string, string> headers)
        {
            var jsonContent = JsonUtils.Serialize(headers);
            var content = new StringContent(jsonContent, Encoding.UTF8, MediaTypeNames.Application.Json);

            var result = await httpClient.PutAsync(url, content);
        }

        public async Task PatchAsync(string url, Dictionary<string, string> headers)
        {
            var myContent = JsonUtils.Serialize(headers);
            var buffer = System.Text.Encoding.UTF8.GetBytes(myContent);
            var byteContent = new ByteArrayContent(buffer);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            await httpClient.PatchAsync(url, byteContent);
        }

        public async Task DeleteAsync(string url)
        {
            await httpClient.DeleteAsync(url);
        }

        public static async Task DownloadFileAsync(HttpClient client, string url, string fileName, IProgress<double>? progress, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(url);
            ArgumentNullException.ThrowIfNull(fileName);
            if (File.Exists(fileName)) File.Delete(fileName);

            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);

            if (!response.IsSuccessStatusCode) throw new Exception(response.StatusCode.ToString());

            var total = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = total != -1 && progress != null;

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            await using var file = File.Create(fileName);
            var totalRead = 0L;
            var buffer = new byte[1024 * 1024];
            var progressPercentage = 0;

            while (true)
            {
                token.ThrowIfCancellationRequested();

                var read = await stream.ReadAsync(buffer, token);
                totalRead += read;

                if (read == 0) break;
                await file.WriteAsync(buffer, 0, read, token);

                if (canReportProgress)
                {
                    var percent = (int)(100.0 * totalRead / total);
                    //if (progressPercentage != percent && percent % 10 == 0)
                    {
                        progressPercentage = percent;
                        progress?.Report(percent);
                    }
                }
            }
            if (canReportProgress)
            {
                progress?.Report(101);
            }
        }

        public async Task DownloadDataAsync4Speed(HttpClient client, string url, IProgress<string> progress, CancellationToken token = default)
        {
            if (Utils.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException(nameof(url));
            }

            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(response.StatusCode.ToString());
            }

            //var total = response.Content.Headers.ContentLength.HasValue ? response.Content.Headers.ContentLength.Value : -1L;
            //var canReportProgress = total != -1 && progress != null;

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var totalRead = 0L;
            var buffer = new byte[1024 * 64];
            var isMoreToRead = true;
            var progressSpeed = string.Empty;
            var totalDatetime = DateTime.Now;
            var totalSecond = 0;

            do
            {
                if (token.IsCancellationRequested)
                {
                    if (totalRead > 0)
                    {
                        return;
                    }
                    else
                    {
                        token.ThrowIfCancellationRequested();
                    }
                }

                var read = await stream.ReadAsync(buffer, token);

                if (read == 0)
                {
                    isMoreToRead = false;
                }
                else
                {
                    var data = new byte[read];
                    buffer.ToList().CopyTo(0, data, 0, read);

                    totalRead += read;

                    var ts = (DateTime.Now - totalDatetime);
                    if (progress != null && ts.Seconds > totalSecond)
                    {
                        totalSecond = ts.Seconds;
                        var speed = (totalRead * 1d / ts.TotalMilliseconds / 1000).ToString("#0.0");
                        if (progressSpeed != speed)
                        {
                            progressSpeed = speed;
                            progress.Report(speed);
                        }
                    }
                }
            } while (isMoreToRead);
        }
    }
}