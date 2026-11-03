namespace ServiceLib.Tests.Fmt;

public class NaiveFmtUserInfoTests
{
    [Test]
    [Arguments("pa:ss")]
    [Arguments("a:b:c")]
    [Arguments(":leading")]
    [Arguments("trailing:")]
    public async Task GetShareUriAndResolveConfig_PasswordOnlyWithColon_ShouldKeepPassword(string password)
    {
        var source = CreateNaiveProfile(string.Empty, password);

        var uri = FmtHandler.GetShareUri(source);
        await uri.Should().NotBeNull();

        var resolved = FmtHandler.ResolveConfig(uri!, out var msg);

        await resolved.Should().NotBeNull().Because($"uri: {uri}, msg: {msg}");
        await resolved!.Username.Should().BeEqualTo(string.Empty);
        await resolved.Password.Should().BeEqualTo(password);
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_UsernameAndPasswordWithColons_ShouldKeepBoth()
    {
        var source = CreateNaiveProfile("us:er", "pa:ss");

        var uri = FmtHandler.GetShareUri(source);
        await uri.Should().NotBeNull();

        var resolved = FmtHandler.ResolveConfig(uri!, out var msg);

        await resolved.Should().NotBeNull().Because($"uri: {uri}, msg: {msg}");
        await resolved!.Username.Should().BeEqualTo("us:er");
        await resolved.Password.Should().BeEqualTo("pa:ss");
    }

    [Test]
    [Arguments("naive+https://what:happened@test.someone.cf:443", "what", "happened")]
    [Arguments("naive+https://what:hap:pened@test.someone.cf:443", "what", "hap:pened")]
    [Arguments("naive+https://wh%40t:hap%3Apened@test.someone.cf:443", "wh@t", "hap:pened")]
    [Arguments("naive+https://pa%3Ass@test.someone.cf:443", "", "pa:ss")]
    [Arguments("naive+quic://secret@test.someone.cf:443", "", "secret")]
    public async Task ResolveConfig_ShouldSplitUserInfoBeforeDecoding(string shareUri, string username, string password)
    {
        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        await resolved.Should().NotBeNull().Because($"uri: {shareUri}, msg: {msg}");
        await resolved!.Username.Should().BeEqualTo(username);
        await resolved.Password.Should().BeEqualTo(password);
    }

    private static ProfileItem CreateNaiveProfile(string username, string password)
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.Naive,
            Remarks = "naive demo",
            Address = "naive.example",
            Port = 443,
            Username = username,
            Password = password,
            Network = nameof(ETransport.raw),
            StreamSecurity = Global.None,
        };
        item.SetTransportExtra(new TransportExtraItem { RawHeaderType = Global.None, });

        return item;
    }
}
