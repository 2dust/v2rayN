using ServiceLib.Tests.CoreConfig;

namespace ServiceLib.Tests.Handler;

[NotInParallel]
public class ConfigHandlerSubscriptionTests
{
    private const string FullSingboxConfig = """
        {
          "experimental": {
            "clash_api": {
              "external_controller": "0.0.0.0:9090"
            }
          },
          "dns": {
            "final": "remote"
          },
          "inbounds": [
            {
              "type": "mixed",
              "listen": "0.0.0.0",
              "listen_port": 1080
            }
          ],
          "outbounds": [
            {
              "type": "vless",
              "tag": "proxy",
              "server": "proxy.example",
              "server_port": 443
            }
          ],
          "route": {
            "final": "proxy"
          }
        }
        """;

    private const string FullXrayConfig = """
        {
          "log": {
            "access": "/tmp/subscription-access.log"
          },
          "inbounds": [
            {
              "listen": "0.0.0.0",
              "port": 1080,
              "protocol": "socks"
            }
          ],
          "outbounds": [
            {
              "protocol": "vmess",
              "tag": "proxy",
              "settings": {
                "vnext": []
              },
              "streamSettings": {
                "network": "tcp"
              }
            }
          ],
          "routing": {
            "domainStrategy": "AsIs"
          }
        }
        """;

    [Test]
    public async Task AddBatchServers_DefaultSubscriptionFullSingboxConfig_ShouldImportOutboundOnly()
    {
        var (config, subId) = await CreateSubscriptionAsync(null);
        try
        {
            await AssertSubscriptionImportsOutboundOnly(config, subId);
        }
        finally
        {
            await CleanupSubscriptionAsync(config, subId);
        }
    }

    [Test]
    public async Task AddBatchServers_DefaultSubscriptionFullXrayConfig_ShouldImportOutboundOnly()
    {
        var (config, subId) = await CreateSubscriptionAsync(null);
        try
        {
            var count = await ConfigHandler.AddBatchServers(config, FullXrayConfig, subId, true);
            var profiles = await AppManager.Instance.ProfileItems(subId);

            await count.Should().BeEqualTo(1);
            await profiles.Should().HaveCount(1);

            var profile = profiles![0];
            await profile.ConfigType.Should().BeEqualTo(EConfigType.Outbound);
            await profile.CoreType.Should().BeEqualTo(ECoreType.Xray);
            await profile.IsSub.Should().BeTrue();

            var stored = JsonNode.Parse(await File.ReadAllTextAsync(Utils.GetConfigPath(profile.Address)))!.AsObject();
            await stored["protocol"]!.GetValue<string>().Should().BeEqualTo("vmess");
            await stored.ContainsKey("inbounds").Should().BeFalse();
            await stored.ContainsKey("routing").Should().BeFalse();
            await stored.ContainsKey("log").Should().BeFalse();
        }
        finally
        {
            await CleanupSubscriptionAsync(config, subId);
        }
    }

    [Test]
    public async Task AddBatchServers_ExplicitSingboxSubscriptionFullConfig_ShouldKeepCustomConfig()
    {
        var (config, subId) = await CreateSubscriptionAsync(ECoreType.sing_box);
        try
        {
            await AssertFullCustomConfig(config, subId, true);
        }
        finally
        {
            await CleanupSubscriptionAsync(config, subId);
        }
    }

    [Test]
    public async Task AddBatchServers_ManualFullSingboxConfig_ShouldKeepCustomConfig()
    {
        var (config, subId) = await CreateSubscriptionAsync(ECoreType.sing_box);
        try
        {
            await AssertFullCustomConfig(config, subId, false);
        }
        finally
        {
            await CleanupSubscriptionAsync(config, subId);
        }
    }

    [Test]
    public async Task AddBatchServers_DefaultSubscriptionWithoutUsableOutbound_ShouldRejectFullConfig()
    {
        const string rawConfig = """
            {
              "inbounds": [
                {
                  "type": "mixed",
                  "listen": "0.0.0.0",
                  "listen_port": 1080
                }
              ],
              "outbounds": [
                {
                  "type": "direct"
                }
              ]
            }
            """;

        var (config, subId) = await CreateSubscriptionAsync(null);
        try
        {
            var count = await ConfigHandler.AddBatchServers(config, rawConfig, subId, true);
            var profiles = await AppManager.Instance.ProfileItems(subId);

            await count.Should().BeEqualTo(-1);
            await profiles.Should().BeEmpty();
        }
        finally
        {
            await CleanupSubscriptionAsync(config, subId);
        }
    }

    private static async Task AssertSubscriptionImportsOutboundOnly(Config config, string subId)
    {
        var count = await ConfigHandler.AddBatchServers(config, FullSingboxConfig, subId, true);
        var profiles = await AppManager.Instance.ProfileItems(subId);

        await count.Should().BeEqualTo(1);
        await profiles.Should().HaveCount(1);

        var profile = profiles![0];
        await profile.ConfigType.Should().BeEqualTo(EConfigType.Outbound);
        await profile.CoreType.Should().BeEqualTo(ECoreType.sing_box);
        await profile.IsSub.Should().BeTrue();

        var stored = JsonNode.Parse(await File.ReadAllTextAsync(Utils.GetConfigPath(profile.Address)))!.AsObject();
        await stored["type"]!.GetValue<string>().Should().BeEqualTo("vless");
        await stored["server"]!.GetValue<string>().Should().BeEqualTo("proxy.example");
        await stored.ContainsKey("inbounds").Should().BeFalse();
        await stored.ContainsKey("experimental").Should().BeFalse();
        await stored.ContainsKey("route").Should().BeFalse();
        await stored.ContainsKey("dns").Should().BeFalse();
    }

    private static async Task AssertFullCustomConfig(Config config, string subId, bool isSub)
    {
        var count = await ConfigHandler.AddBatchServers(config, FullSingboxConfig, subId, isSub);
        var profiles = await AppManager.Instance.ProfileItems(subId);

        await count.Should().BeEqualTo(1);
        await profiles.Should().HaveCount(1);

        var profile = profiles![0];
        await profile.ConfigType.Should().BeEqualTo(EConfigType.Custom);
        await profile.IsSub.Should().BeEqualTo(isSub);

        var stored = JsonNode.Parse(await File.ReadAllTextAsync(Utils.GetConfigPath(profile.Address)))!.AsObject();
        await stored.ContainsKey("inbounds").Should().BeTrue();
        await stored.ContainsKey("experimental").Should().BeTrue();
        await stored.ContainsKey("route").Should().BeTrue();
        await stored.ContainsKey("dns").Should().BeTrue();
    }

    private static async Task<(Config Config, string SubId)> CreateSubscriptionAsync(ECoreType? customCoreType)
    {
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);

        SQLiteHelper.Instance.CreateTable<SubItem>();
        SQLiteHelper.Instance.CreateTable<ProfileItem>();

        var subId = $"subscription-test-{Guid.NewGuid():N}";
        await SQLiteHelper.Instance.ReplaceAsync(new SubItem
        {
            Id = subId,
            Remarks = "subscription-test",
            Url = "https://subscription.example/config",
            MoreUrl = string.Empty,
            CustomCoreType = customCoreType,
        });

        return (config, subId);
    }

    private static async Task CleanupSubscriptionAsync(Config config, string subId)
    {
        await ConfigHandler.RemoveServersViaSubid(config, subId, false);
        var subItem = await AppManager.Instance.GetSubItem(subId);
        if (subItem is not null)
        {
            await SQLiteHelper.Instance.DeleteAsync(subItem);
        }
    }
}
