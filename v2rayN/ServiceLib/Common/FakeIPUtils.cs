namespace ServiceLib.Common;

public static class FakeIPUtils
{
    public static bool TryParseIPv6Range(string? value, out string range, out long poolSize)
    {
        range = string.IsNullOrWhiteSpace(value) ? Global.FakeIPv6Ranges.First() : value.Trim();
        poolSize = 65535;
        if (!System.Net.IPNetwork.TryParse(range, out var network)
            || network.BaseAddress.AddressFamily != AddressFamily.InterNetworkV6
            || network.PrefixLength == 128)
        {
            return false;
        }

        range = network.ToString();
        var hostBits = 128 - network.PrefixLength;
        if (hostBits < 16)
        {
            // Xray requires the mapping capacity to be smaller than the address pool.
            poolSize = (1L << hostBits) - 1;
        }
        return true;
    }
}
