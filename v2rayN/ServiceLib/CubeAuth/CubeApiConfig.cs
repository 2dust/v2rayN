namespace ServiceLib.CubeAuth;

/// <summary>
/// The base URL of the CubeVPN account API (see docs/api-contract.md in the
/// CubeVPN Android repo — this client talks to the same three endpoints:
/// requestcode.php / verifycode.php / accountme.php).
///
/// Left empty in source control on purpose. The release workflow
/// (.github/workflows/release-cubevpn-windows.yml) replaces the value below
/// at publish time from a repo secret, the same way the Android app injects
/// API_BASE_URL from secrets.properties at build time.
/// </summary>
public static class CubeApiConfig
{
    public const string BaseUrl = "";
}
