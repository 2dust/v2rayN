using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ServiceLib.Manager;

/// <summary>
///     Manager for certificate operations with CA pinning to prevent MITM attacks
/// </summary>
public class CertPemManager
{
    private static readonly string _tag = "CertPemManager";
    private static readonly Lazy<CertPemManager> _instance = new(() => new());
    private Config _config;

    public async Task Init(Config config)
    {
        if (_config != null)
        {
            return;
        }
        _config = config;
        await Task.CompletedTask;
    }

    public static CertPemManager Instance => _instance.Value;

    /// <summary>
    ///     Get certificate in PEM format from a server with CA pinning validation
    /// </summary>
    public async Task<(string?, string?)> GetCertPemAsync(string target, string serverName,
        List<string>? verifyPeerCertByName = null, int timeout = 4)
    {
        try
        {
            var (domain, _, port, _) = Utils.ParseUrl(target);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));

            using var client = new TcpClient();
            await client.ConnectAsync(domain, port > 0 ? port : 443, cts.Token);

            var callback = new RemoteCertificateValidationCallback((sender, certificate, chain, sslPolicyErrors) =>
                ValidateServerCertificate(sender, certificate, chain, sslPolicyErrors, verifyPeerCertByName ?? []));
            await using var ssl = new SslStream(client.GetStream(), false, callback);

            var sslOptions = new SslClientAuthenticationOptions
            {
                TargetHost = serverName,
                RemoteCertificateValidationCallback = callback,
            };

            await ssl.AuthenticateAsClientAsync(sslOptions, cts.Token);

            var remote = ssl.RemoteCertificate;
            if (remote == null)
            {
                return (null, null);
            }

            var leaf = new X509Certificate2(remote);
            return (ExportCertToPem(leaf), null);
        }
        catch (OperationCanceledException)
        {
            Logging.SaveLog(_tag, new TimeoutException($"Connection timeout after {timeout} seconds"));
            return (null, $"Connection timeout after {timeout} seconds");
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return (null, ex.Message);
        }
    }

    /// <summary>
    ///     Get certificate chain in PEM format from a server with CA pinning validation
    /// </summary>
    public async Task<(List<string>, string?)> GetCertChainPemAsync(string target, string serverName,
        List<string>? verifyPeerCertByName = null, int timeout = 4)
    {
        var pemList = new List<string>();
        try
        {
            var (domain, _, port, _) = Utils.ParseUrl(target);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));

            using var client = new TcpClient();
            await client.ConnectAsync(domain, port > 0 ? port : 443, cts.Token);

            var callback = new RemoteCertificateValidationCallback((sender, certificate, chain, sslPolicyErrors) =>
                ValidateServerCertificate(sender, certificate, chain, sslPolicyErrors, verifyPeerCertByName ?? []));
            await using var ssl = new SslStream(client.GetStream(), false, callback);

            var sslOptions = new SslClientAuthenticationOptions
            {
                TargetHost = serverName,
                RemoteCertificateValidationCallback = callback,
            };

            await ssl.AuthenticateAsClientAsync(sslOptions, cts.Token);

            if (ssl.RemoteCertificate is not X509Certificate2 certChain)
            {
                return (pemList, null);
            }

            var chain = new X509Chain();
            chain.Build(certChain);

            pemList.AddRange(chain.ChainElements.Select(element => ExportCertToPem(element.Certificate)));

            return (pemList, null);
        }
        catch (OperationCanceledException)
        {
            Logging.SaveLog(_tag, new TimeoutException($"Connection timeout after {timeout} seconds"));
            return (pemList, $"Connection timeout after {timeout} seconds");
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return (pemList, ex.Message);
        }
    }

    /// <summary>
    ///     Validate server certificate with CA pinning
    /// </summary>
    private bool ValidateServerCertificate(
        object _,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors,
        List<string> verifyPeerCertByName)
    {
        if (certificate == null)
        {
            return false;
        }

        // Build certificate chain
        var cert2 = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
        var certChain = chain ?? new X509Chain();

        if (chain == null)
        {
            certChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            certChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            certChain.ChainPolicy.VerificationTime = DateTime.UtcNow;

            if (!certChain.Build(cert2))
            {
                return false;
            }
        }

        // Find root CA
        if (certChain.ChainElements.Count == 0)
        {
            return false;
        }

        var rootCert = certChain.ChainElements[^1].Certificate;

        var trustedCerts = BuildTrustedCertificateCollection();

        if (!trustedCerts.Contains(rootCert))
        {
            return false;
        }

        if (!sslPolicyErrors.HasFlag(
                SslPolicyErrors.RemoteCertificateNameMismatch))
        {
            return true;
        }

        if (verifyPeerCertByName.Count == 0)
        {
            return false;
        }

        foreach (var ext in cert2.Extensions)
        {
            if (ext is not X509SubjectAlternativeNameExtension san)
            {
                continue;
            }

            return san.EnumerateDnsNames().Any(dnsName =>
                verifyPeerCertByName.Contains(dnsName, StringComparer.OrdinalIgnoreCase));
        }

        return false;
    }

    public static string ExportCertToPem(X509Certificate2 cert)
    {
        var der = cert.Export(X509ContentType.Cert);
        var b64 = Convert.ToBase64String(der);
        return $"-----BEGIN CERTIFICATE-----\n{b64}\n-----END CERTIFICATE-----\n";
    }

    /// <summary>
    ///     Parse concatenated PEM certificates string into a list of individual certificates
    ///     Normalizes format: removes line breaks from base64 content for better compatibility
    /// </summary>
    /// <param name="pemChain">Concatenated PEM certificates string (supports both \r\n and \n line endings)</param>
    /// <returns>List of individual PEM certificate strings with normalized format</returns>
    public static List<string> ParsePemChain(string pemChain)
    {
        var certs = new List<string>();
        if (string.IsNullOrWhiteSpace(pemChain))
        {
            return certs;
        }

        // Normalize line endings (CRLF -> LF) at the beginning
        pemChain = pemChain.Replace("\r\n", "\n").Replace("\r", "\n");

        const string beginMarker = "-----BEGIN CERTIFICATE-----";
        const string endMarker = "-----END CERTIFICATE-----";

        var index = 0;
        while (index < pemChain.Length)
        {
            var beginIndex = pemChain.IndexOf(beginMarker, index, StringComparison.Ordinal);
            if (beginIndex == -1)
            {
                break;
            }

            var endIndex = pemChain.IndexOf(endMarker, beginIndex, StringComparison.Ordinal);
            if (endIndex == -1)
            {
                break;
            }

            // Extract certificate content
            var base64Start = beginIndex + beginMarker.Length;
            var base64Content = pemChain.Substring(base64Start, endIndex - base64Start);

            // Remove all whitespace from base64 content
            base64Content = new string(base64Content.Where(c => !char.IsWhiteSpace(c)).ToArray());

            // Reconstruct with clean format: BEGIN marker + base64 (no line breaks) + END marker
            var normalizedCert = $"{beginMarker}\n{base64Content}\n{endMarker}\n";
            certs.Add(normalizedCert);

            // Move to next certificate
            index = endIndex + endMarker.Length;
        }

        return certs;
    }

    /// <summary>
    ///     Concatenate a list of PEM certificates into a single string
    /// </summary>
    /// <param name="pemList">List of individual PEM certificate strings</param>
    /// <returns>Concatenated PEM certificates string</returns>
    public static string ConcatenatePemChain(IEnumerable<string>? pemList)
    {
        return pemList == null ? string.Empty : string.Concat(pemList);
    }

    public static string GetCertSha256Thumbprint(string pemCert, bool includeColon = false)
    {
        try
        {
            var cert = X509Certificate2.CreateFromPem(pemCert);
            var thumbprint = cert.GetCertHashString(HashAlgorithmName.SHA256);
            return includeColon ? string.Join(":", thumbprint.Chunk(2).Select(c => new string(c))) : thumbprint;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static readonly Lazy<X509Certificate2Collection> _chromeRootCerts = new(() =>
    {
        var pemText = EmbedUtils.GetEmbedText(Global.ChromeRootCertFileName);
        var collection = new X509Certificate2Collection();
        collection.ImportFromPem(pemText);
        return collection;
    });
    private static readonly Lazy<X509Certificate2Collection> _mozillaRootCerts = new(() =>
    {
        var pemText = EmbedUtils.GetEmbedText(Global.MozillaRootCertFileName);
        var collection = new X509Certificate2Collection();
        collection.ImportFromPem(pemText);
        return collection;
    });
    private X509Certificate2Collection BuildTrustedCertificateCollection()
    {
        if (_config.GuiItem.RootCertProvider == Global.ChromeRootProvider)
        {
            return _chromeRootCerts.Value;
        }
        return _mozillaRootCerts.Value;
    }

    private bool IsSystemRootCertProvider()
    {
        return _config.GuiItem.RootCertProvider != Global.ChromeRootProvider && _config.GuiItem.RootCertProvider != Global.MozillaRootProvider;
    }

    public X509ChainPolicy? BuildCertificateChainPolicy()
    {
        if (IsSystemRootCertProvider())
        {
            return null;
        }
        var trustedCerts = BuildTrustedCertificateCollection();
        var chainPolicy = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            RevocationMode = X509RevocationMode.NoCheck,
        };
        chainPolicy.CustomTrustStore.AddRange(trustedCerts);
        return chainPolicy;
    }
}
