namespace ServiceLib.Tests.Manager;

public class AppManagerTests
{
    [Test]
    [NotInParallel]
    [Arguments(null)]
    [Arguments(ECoreType.Xray)]
    public async Task GetCoreType_Hysteria2FallbackPreservesCertificateChecksAndNodeOverrides(ECoreType? configuredCore)
    {
        var state = ConfigState.Instance;
        var previous = state.Current;
        var config = new Config();
        if (configuredCore is { } core)
        {
            config.CoreTypeItem = new()
            {
                new CoreTypeItem { ConfigType = EConfigType.Hysteria2, CoreType = core }
            };
        }
        state.SetCurrent(config);
        try
        {
            var manager = AppManager.Instance;
            var profile = new ProfileItem
            {
                ConfigType = EConfigType.Hysteria2,
                StreamSecurity = Global.StreamSecurity,
                AllowInsecure = Global.StringTrue,
            };

            await manager.GetCoreType(profile, EConfigType.Hysteria2).Should().BeEqualTo(ECoreType.sing_box);
            profile.ConfigType = EConfigType.MASQUE;
            await manager.GetCoreType(profile, profile.ConfigType).Should().BeEqualTo(ECoreType.Xray);
            profile.ConfigType = EConfigType.Hysteria2;

            profile.CoreType = ECoreType.Xray;
            await manager.GetCoreType(profile, EConfigType.Hysteria2).Should().BeEqualTo(ECoreType.Xray);
            profile.CoreType = null;

            profile.CertSha = "pin";
            await manager.GetCoreType(profile, EConfigType.Hysteria2).Should().BeEqualTo(ECoreType.Xray);
            profile.CertSha = string.Empty;

            profile.Cert = "PEM";
            await manager.GetCoreType(profile, EConfigType.Hysteria2).Should().BeEqualTo(ECoreType.Xray);
            profile.Cert = string.Empty;

            profile.VerifyPeerCertByName = "example.com";
            await manager.GetCoreType(profile, EConfigType.Hysteria2).Should().BeEqualTo(ECoreType.Xray);
            profile.VerifyPeerCertByName = string.Empty;

            profile.Finalmask = "{}";
            await manager.GetCoreType(profile, EConfigType.Hysteria2).Should().BeEqualTo(ECoreType.Xray);
            profile.Finalmask = string.Empty;

            profile.AllowInsecure = Global.StringFalse;
            await manager.GetCoreType(profile, EConfigType.Hysteria2).Should().BeEqualTo(ECoreType.Xray);
        }
        finally
        {
            state.SetCurrent(previous);
        }
    }
}
