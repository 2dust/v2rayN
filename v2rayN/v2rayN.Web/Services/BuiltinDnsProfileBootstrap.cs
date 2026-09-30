using ServiceLib;
using ServiceLib.Enums;
using ServiceLib.Models.Configs;
using ServiceLib.Models.Entities;

namespace v2rayN.Web.Services;

internal static class BuiltinDnsProfileBootstrap
{
    private static readonly (ECoreType CoreType, string Remarks)[] BuiltinProfiles =
    [
        (ECoreType.Xray, "V2ray"),
        (ECoreType.sing_box, "sing-box"),
    ];

    public static async Task EnsureAsync(
        Config config,
        Func<Task<IReadOnlyList<DNSItem>>> load,
        Func<Config, DNSItem, Task<int>> save)
    {
        var existing = await load();
        foreach (var (coreType, remarks) in BuiltinProfiles)
        {
            if (existing.Any(item => item.CoreType == coreType))
            {
                continue;
            }

            var item = new DNSItem
            {
                Remarks = remarks,
                CoreType = coreType,
                Enabled = false,
            };
            if (await save(config, item) != 0)
            {
                throw new IOException($"The built-in {coreType} DNS profile could not be created.");
            }

            existing = [.. existing, item];
        }
    }
}
