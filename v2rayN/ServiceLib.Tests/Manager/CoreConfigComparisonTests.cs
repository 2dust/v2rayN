using ServiceLib.Models.CoreConfigs;
using ServiceLib.Tests.CoreConfig;

namespace ServiceLib.Tests.Manager;

public class CoreConfigComparisonTests
{
    [Test]
    public async Task XrayGeneratedLogDateDoesNotChangeTheConnection()
    {
        var context = Context(ECoreType.Xray);
        context.AppConfig.CoreBasicItem.LogEnabled = true;
        const string before = """{"log":{"access":"/logs/Vaccess_2026-11-12.txt","error":"/logs/Verror_2026-11-12.txt"},"outbounds":[{"password":"secret"}]}""";
        const string after = """{"log":{"access":"/logs/Vaccess_2026-11-13.txt","error":"/logs/Verror_2026-11-13.txt"},"outbounds":[{"password":"secret"}]}""";

        await CoreConfigComparison.AreEquivalent(context, before, after).Should().BeTrue();
    }

    [Test]
    public async Task SingboxGeneratedLogDateDoesNotChangeTheConnection()
    {
        var context = Context(ECoreType.sing_box);
        context.AppConfig.CoreBasicItem.LogEnabled = true;
        const string before = """{"log":{"output":"/logs/sbox_2026-11-12.txt"},"outbounds":[{"password":"secret"}]}""";
        const string after = """{"log":{"output":"/logs/sbox_2026-11-13.txt"},"outbounds":[{"password":"secret"}]}""";

        await CoreConfigComparison.AreEquivalent(context, before, after).Should().BeTrue();
    }

    [Test]
    [Arguments("/elsewhere/Vaccess_2026-11-13.txt")]
    [Arguments("/logs/user-log.txt")]
    [Arguments("/logs/Vaccess_2026-99-99.txt")]
    public async Task OtherLogPathChangesAreNotDiscarded(string changedPath)
    {
        var context = Context(ECoreType.Xray);
        context.AppConfig.CoreBasicItem.LogEnabled = true;
        const string before = """{"log":{"access":"/logs/Vaccess_2026-11-12.txt"}}""";
        var after = "{\"log\":{\"access\":\"" + changedPath + "\"}}";

        await CoreConfigComparison.AreEquivalent(context, before, after).Should().BeFalse();
    }

    [Test]
    [Arguments(true, true, "utun37", 1500, true)]
    [Arguments(false, true, "utun37", 1500, false)]
    [Arguments(true, false, "utun37", 1500, false)]
    [Arguments(true, true, "utun37", 1400, false)]
    [Arguments(true, true, "user-tun", 1500, false)]
    [Arguments(true, true, "utun99", 1500, false)]
    public async Task OnlyTheGeneratedMacosTunNameMayDiffer(bool macos, bool tun, string name, int mtu, bool reusable)
    {
        var context = Context(ECoreType.Xray) with { IsMacOS = macos, IsTunEnabled = tun };
        const string before = """{"inbounds":[{"protocol":"tun","settings":{"name":"utun12","mtu":1500}}]}""";
        var after = "{\"inbounds\":[{\"protocol\":\"tun\",\"settings\":{\"name\":\"" + name + "\",\"mtu\":" + mtu + "}}]}";

        await CoreConfigComparison.AreEquivalent(context, before, after).Should().BeEqualTo(reusable);
    }

    [Test]
    [Arguments(ECoreType.Xray)]
    [Arguments(ECoreType.sing_box)]
    public async Task DisabledTemplateAndDnsRowsAllowOrdinaryConfiguration(ECoreType coreType)
    {
        var context = Context(coreType);
        await CoreConfigComparison.AreEquivalent(context, "{\"port\":443}", "{\"port\":443}").Should().BeTrue();
    }

    [Test]
    [Arguments("template")]
    [Arguments("dns")]
    [Arguments("custom-outbound")]
    [Arguments("group")]
    public async Task UnsupportedInputsCannotProduceAReuseDecision(string input)
    {
        var context = Context(ECoreType.Xray);
        switch (input)
        {
            case "template": context.FullConfigTemplate!.Enabled = true; break;
            case "dns": context.RawDnsItem!.Enabled = true; break;
            case "custom-outbound": context.CustomOutboundContent["external"] = "{}"; break;
            case "group": context.Node.ConfigType = EConfigType.PolicyGroup; break;
        }
        await CoreConfigComparison.AreEquivalent(context, "{}", "{}").Should().BeFalse();
    }

    [Test]
    [Arguments("{\"route\":{\"rule_set\":[{\"type\":\"local\",\"path\":\"rules.srs\"}]}}")]
    [Arguments("{\"routing\":{\"rules\":[{\"domain\":[\"geosite:cn\"]}]}}")]
    [Arguments("{\"routing\":{\"rules\":[{\"ip\":[\"geoip:cn\"]}]}}")]
    public async Task UnresolvedFileInputsAreNotProvedEqualByTheirPaths(string content)
    {
        await CoreConfigComparison.AreEquivalent(Context(ECoreType.sing_box), content, content).Should().BeFalse();
    }

    [Test]
    [Arguments("{\"password\":\"different\"}")]
    [Arguments("{\"password\":\"secret\",\"unknown-option\":true}")]
    public async Task ActualAndUnknownSettingsRemainPartOfTheComparison(string content)
    {
        await CoreConfigComparison.AreEquivalent(Context(ECoreType.Xray), "{\"password\":\"secret\"}", content).Should().BeFalse();
    }

    [Test]
    public async Task AnInvalidPreviousConfigurationCannotSuppressARestart()
    {
        await CoreConfigComparison.AreEquivalent(Context(ECoreType.Xray), "invalid", "{}").Should().BeFalse();
    }

    private static CoreConfigContext Context(ECoreType coreType)
    {
        var config = CoreConfigTestFactory.CreateConfig(coreType);
        return CoreConfigTestFactory.CreateContext(config, CoreConfigTestFactory.CreateSocksNode(coreType), coreType) with
        {
            FullConfigTemplate = new FullConfigTemplateItem { Enabled = false },
            RawDnsItem = new DNSItem { Enabled = false },
        };
    }
}
