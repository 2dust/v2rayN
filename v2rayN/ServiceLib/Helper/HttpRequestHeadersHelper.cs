namespace ServiceLib.Helper;

public static class HttpRequestHeadersHelper
{
    public static bool TryParse(string? json, out Dictionary<string, string> headers)
    {
        headers = new(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var request = new HttpRequestMessage { Content = new ByteArrayContent([]) };
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String
                    || !parsed.TryAdd(property.Name, property.Value.GetString()!)
                    || !TryAddHeader(request, property.Name, parsed[property.Name]))
                {
                    return false;
                }
            }

            headers = parsed;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or FormatException)
        {
            return false;
        }
    }

    public static HttpMessageHandler CreateHandler(HttpMessageHandler innerHandler, IReadOnlyDictionary<string, string>? headers)
    {
        return headers is { Count: > 0 } ? new RequestHeadersHandler(innerHandler, headers) : innerHandler;
    }

    private static bool TryAddHeader(HttpRequestMessage request, string name, string value)
    {
        if (value.Any(c => char.IsControl(c) && c != '\t'))
        {
            return false;
        }

        return request.Headers.TryAddWithoutValidation(name, value)
            || request.Content!.Headers.TryAddWithoutValidation(name, value);
    }

    private sealed class RequestHeadersHandler(HttpMessageHandler innerHandler, IReadOnlyDictionary<string, string> headers)
        : DelegatingHandler(innerHandler)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var customHeaders = new HttpRequestMessage { Content = new ByteArrayContent([]) };
            foreach (var header in headers)
            {
                if (!TryAddHeader(customHeaders, header.Key, header.Value))
                {
                    throw new FormatException(ResUI.SubRequestHeadersInvalid);
                }
            }

            // Apply after each downloader's defaults, replacing headers without changing their values.
            foreach (var header in customHeaders.Headers.NonValidated)
            {
                request.Headers.Remove(header.Key);
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            foreach (var header in customHeaders.Content.Headers.NonValidated)
            {
                request.Content ??= new ByteArrayContent([]);
                request.Content.Headers.Remove(header.Key);
                request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
