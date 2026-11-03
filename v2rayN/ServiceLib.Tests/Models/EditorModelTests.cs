using System.ComponentModel;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;
using ServiceLib.Models.Editors;

namespace ServiceLib.Tests.Models;

public class EditorModelTests
{
    [Test]
    public async Task ProfileEditorRoundTrip_PreservesIdentityAndExtraData()
    {
        var entity = JsonUtils.Deserialize<ProfileItem>("""
            {"IndexId":"profile-id","ConfigType":2,"ConfigVersion":3,"Subid":"subscription-id",
             "IsSub":false,"CoreType":1,"Network":"grpc","AllowInsecure":"true","MuxEnabled":true,
             "Cert":"certificate","CertSha":"certificate-hash","HeaderType":"legacy-header",
             "RequestHost":"legacy-host","Path":"legacy-path","Extra":"legacy-extra","Ports":"legacy-ports",
             "AlterId":42,"Flow":"legacy-flow","Id":"legacy-id","Security":"legacy-security",
             "ProtoExtra":"{\"Flow\":\"xtls-rprx-vision\",\"ChildItems\":\"child-id\"}",
             "TransportExtra":"{\"GrpcServiceName\":\"service\",\"XhttpExtra\":\"extra\"}",
             "Remarks":"original","Address":"original.example","Port":443,"Password":"old-password",
             "Username":"old-user","StreamSecurity":"tls","Sni":"old-sni","Fingerprint":"old-fingerprint",
             "Alpn":"h2","EchConfigList":"old-ech","VerifyPeerCertByName":"old-verify","PublicKey":"old-key",
             "ShortId":"old-short","SpiderX":"old-spider","Mldsa65Verify":"old-mldsa","Finalmask":"old-mask",
             "DisplayLog":true,"PreSocksPort":1080}
            """)!;
        var original = JsonUtils.Serialize(entity);
        var editor = ProfileEditorModel.FromEntity(entity);
        await JsonUtils.Serialize(editor.ToEntity()).Should().BeEqualTo(original);

        editor.Remarks = "edited";
        editor.Address = "edited.example";
        editor.Port = 8443;
        editor.Password = "password";
        editor.Username = "user";
        editor.StreamSecurity = "reality";
        editor.Sni = "sni";
        editor.Fingerprint = "fingerprint";
        editor.Alpn = "http/1.1";
        editor.EchConfigList = "ech";
        editor.VerifyPeerCertByName = "verify";
        editor.PublicKey = "key";
        editor.ShortId = "short";
        editor.SpiderX = "spider";
        editor.Mldsa65Verify = "mldsa";
        editor.Finalmask = "mask";
        editor.DisplayLog = false;
        editor.PreSocksPort = null;
        editor.ConfigType = EConfigType.Trojan;

        var result = editor.ToEntity();
        await result.Remarks.Should().BeEqualTo("edited");
        await result.Address.Should().BeEqualTo("edited.example");
        await result.Port.Should().BeEqualTo(8443);
        await result.Password.Should().BeEqualTo("password");
        await result.Username.Should().BeEqualTo("user");
        await result.StreamSecurity.Should().BeEqualTo("reality");
        await result.Sni.Should().BeEqualTo("sni");
        await result.Fingerprint.Should().BeEqualTo("fingerprint");
        await result.Alpn.Should().BeEqualTo("http/1.1");
        await result.EchConfigList.Should().BeEqualTo("ech");
        await result.VerifyPeerCertByName.Should().BeEqualTo("verify");
        await result.PublicKey.Should().BeEqualTo("key");
        await result.ShortId.Should().BeEqualTo("short");
        await result.SpiderX.Should().BeEqualTo("spider");
        await result.Mldsa65Verify.Should().BeEqualTo("mldsa");
        await result.Finalmask.Should().BeEqualTo("mask");
        await result.DisplayLog.Should().BeFalse();
        await result.PreSocksPort.Should().BeNull();
        await result.ConfigType.Should().BeEqualTo(EConfigType.Trojan);
        await result.IndexId.Should().BeEqualTo("profile-id");
        await result.Subid.Should().BeEqualTo("subscription-id");
        await result.ConfigVersion.Should().BeEqualTo(3);
        await result.Network.Should().BeEqualTo("grpc");
        await result.AllowInsecure.Should().BeEqualTo("true");
        await result.Cert.Should().BeEqualTo("certificate");
        await result.CertSha.Should().BeEqualTo("certificate-hash");
        await result.IsSub.Should().BeFalse();
        await result.MuxEnabled.Should().BeEqualTo(true);
        await result.CoreType.Should().BeEqualTo(entity.CoreType);
        await result.ProtoExtra.Should().BeEqualTo(entity.ProtoExtra);
        await result.TransportExtra.Should().BeEqualTo(entity.TransportExtra);
        await result.GetProtocolExtra().Flow.Should().BeEqualTo("xtls-rprx-vision");
        await result.GetTransportExtra().GrpcServiceName.Should().BeEqualTo("service");
        var json = JsonNode.Parse(JsonUtils.Serialize(result))!;
        await json["HeaderType"]!.GetValue<string>().Should().BeEqualTo("legacy-header");
        await json["RequestHost"]!.GetValue<string>().Should().BeEqualTo("legacy-host");
        await json["Path"]!.GetValue<string>().Should().BeEqualTo("legacy-path");
        await json["Extra"]!.GetValue<string>().Should().BeEqualTo("legacy-extra");
        await json["Ports"]!.GetValue<string>().Should().BeEqualTo("legacy-ports");
        await json["AlterId"]!.GetValue<int>().Should().BeEqualTo(42);
        await json["Flow"]!.GetValue<string>().Should().BeEqualTo("legacy-flow");
        await json["Id"]!.GetValue<string>().Should().BeEqualTo("legacy-id");
        await json["Security"]!.GetValue<string>().Should().BeEqualTo("legacy-security");
        await JsonUtils.Serialize(entity).Should().BeEqualTo(original);
    }

    [Test]
    public async Task SubscriptionEditorRoundTrip_PreservesHiddenFieldsAndAppliesEdits()
    {
        var entity = new SubItem
        {
            Id = "subscription-id", Remarks = "original", Url = "old-url", MoreUrl = "old-more",
            Enabled = true, AutoUpdateInterval = 10, UserAgent = "old-agent", RequestHeaders = "old-headers",
            Sort = 1, Filter = "old-filter", ConvertTarget = "old-target", PreSocksPort = 1080,
            Memo = "old-memo", PreResolver = "old-resolver", UpdateTime = 123456,
            PrevProfile = "previous", NextProfile = "next", CustomCoreType = ECoreType.Xray,
        };
        var original = JsonUtils.Serialize(entity);
        var editor = SubscriptionEditorModel.FromEntity(entity);
        await JsonUtils.Serialize(editor.ToEntity()).Should().BeEqualTo(original);
        editor.Remarks = "edited";
        editor.Url = "url";
        editor.MoreUrl = "more";
        editor.Enabled = false;
        editor.AutoUpdateInterval = 20;
        editor.UserAgent = "agent";
        editor.RequestHeaders = null;
        editor.Sort = 2;
        editor.Filter = "filter";
        editor.ConvertTarget = "target";
        editor.PreSocksPort = null;
        editor.Memo = "memo";
        editor.PreResolver = "resolver";
        var expected = new SubItem
        {
            Id = "subscription-id", Remarks = "edited", Url = "url", MoreUrl = "more",
            Enabled = false, AutoUpdateInterval = 20, UserAgent = "agent", RequestHeaders = null,
            Sort = 2, Filter = "filter", ConvertTarget = "target", PreSocksPort = null,
            Memo = "memo", PreResolver = "resolver", UpdateTime = 123456,
            PrevProfile = "previous", NextProfile = "next", CustomCoreType = ECoreType.Xray,
        };
        await JsonUtils.Serialize(editor.ToEntity()).Should().BeEqualTo(JsonUtils.Serialize(expected));
        await JsonUtils.Serialize(entity).Should().BeEqualTo(original);
    }

    [Test]
    public async Task RoutingEditorRoundTrip_PreservesRulesAndFlagsAndAppliesEdits()
    {
        var entity = new RoutingItem
        {
            Id = "routing-id", Remarks = "original", DomainStrategy = "AsIs", DomainStrategy4Singbox = "old-strategy",
            Url = "old-url", CustomIcon = "old-icon", CustomRulesetPath4Singbox = "old-path", Sort = 1,
            RuleSet = "[{\"id\":\"rule-id\",\"domain\":[\"example.com\"]}]", RuleNum = 1,
            Enabled = false, Locked = true, IsActive = true,
        };
        var original = JsonUtils.Serialize(entity);
        var editor = RoutingEditorModel.FromEntity(entity);
        await JsonUtils.Serialize(editor.ToEntity()).Should().BeEqualTo(original);
        editor.Remarks = "edited";
        editor.DomainStrategy = "IPIfNonMatch";
        editor.DomainStrategy4Singbox = "prefer_ipv4";
        editor.Url = "url";
        editor.CustomIcon = "icon";
        editor.CustomRulesetPath4Singbox = "path";
        editor.Sort = 2;
        var expected = new RoutingItem
        {
            Id = "routing-id", Remarks = "edited", DomainStrategy = "IPIfNonMatch", DomainStrategy4Singbox = "prefer_ipv4",
            Url = "url", CustomIcon = "icon", CustomRulesetPath4Singbox = "path", Sort = 2,
            RuleSet = "[{\"id\":\"rule-id\",\"domain\":[\"example.com\"]}]", RuleNum = 1,
            Enabled = false, Locked = true, IsActive = true,
        };
        await JsonUtils.Serialize(editor.ToEntity()).Should().BeEqualTo(JsonUtils.Serialize(expected));
        await JsonUtils.Serialize(entity).Should().BeEqualTo(original);
    }

    [Test]
    public async Task WebDavEditorRoundTrip_PreservesNullableValuesAndAppliesEdits()
    {
        var entity = new WebDavItem { Url = "old-url", UserName = "old-user", Password = "old-password", DirName = null };
        var editor = WebDavEditorModel.FromEntity(entity);
        await JsonUtils.Serialize(editor.ToEntity()).Should().BeEqualTo(JsonUtils.Serialize(entity));
        editor.Url = null;
        editor.UserName = "user";
        editor.Password = "password";
        editor.DirName = "directory";
        var result = editor.ToEntity();
        await result.Url.Should().BeNull();
        await result.UserName.Should().BeEqualTo("user");
        await result.Password.Should().BeEqualTo("password");
        await result.DirName.Should().BeEqualTo("directory");
        await entity.Url.Should().BeEqualTo("old-url");
        await entity.UserName.Should().BeEqualTo("old-user");
        await entity.Password.Should().BeEqualTo("old-password");
        await entity.DirName.Should().BeNull();
    }

    [Test]
    public async Task Editors_DetachInputAndOutput()
    {
        var profile = new ProfileItem { IndexId = "profile-id", Remarks = "profile" };
        profile.SetProtocolExtra(new ProtocolExtraItem { Flow = "original-flow" });
        profile.SetTransportExtra(new TransportExtraItem { Host = "original-host" });
        var subscription = new SubItem { Id = "sub-id", Remarks = "subscription" };
        var routing = new RoutingItem { Id = "routing-id", Remarks = "routing", RuleSet = "original-rules" };
        var webDav = new WebDavItem { Url = "original-url" };
        var profileEditor = ProfileEditorModel.FromEntity(profile);
        var subscriptionEditor = SubscriptionEditorModel.FromEntity(subscription);
        var routingEditor = RoutingEditorModel.FromEntity(routing);
        var webDavEditor = WebDavEditorModel.FromEntity(webDav);

        profile.IndexId = "changed-input";
        profile.Remarks = "changed-input";
        profile.SetProtocolExtra(new ProtocolExtraItem { Flow = "changed-input" });
        subscription.Id = "changed-input";
        subscription.Remarks = "changed-input";
        routing.Id = "changed-input";
        routing.RuleSet = "changed-input";
        webDav.Url = "changed-input";

        var profileOutput = profileEditor.ToEntity();
        var subscriptionOutput = subscriptionEditor.ToEntity();
        var routingOutput = routingEditor.ToEntity();
        var webDavOutput = webDavEditor.ToEntity();
        await profileOutput.IndexId.Should().BeEqualTo("profile-id");
        await profileOutput.Remarks.Should().BeEqualTo("profile");
        await profileOutput.GetProtocolExtra().Flow.Should().BeEqualTo("original-flow");
        await subscriptionOutput.Id.Should().BeEqualTo("sub-id");
        await subscriptionOutput.Remarks.Should().BeEqualTo("subscription");
        await routingOutput.Id.Should().BeEqualTo("routing-id");
        await routingOutput.RuleSet.Should().BeEqualTo("original-rules");
        await webDavOutput.Url.Should().BeEqualTo("original-url");

        profileOutput.IndexId = "changed-output";
        profileOutput.Remarks = "changed-output";
        profileOutput.SetProtocolExtra(new ProtocolExtraItem { Flow = "changed-output" });
        profileOutput.SetTransportExtra(new TransportExtraItem { Host = "changed-output" });
        subscriptionOutput.Id = "changed-output";
        subscriptionOutput.Remarks = "changed-output";
        routingOutput.Id = "changed-output";
        routingOutput.RuleSet = "changed-output";
        webDavOutput.Url = "changed-output";

        await profileEditor.ToEntity().IndexId.Should().BeEqualTo("profile-id");
        await profileEditor.ToEntity().Remarks.Should().BeEqualTo("profile");
        await profileEditor.ToEntity().GetProtocolExtra().Flow.Should().BeEqualTo("original-flow");
        await profileEditor.ToEntity().GetTransportExtra().Host.Should().BeEqualTo("original-host");
        await subscriptionEditor.ToEntity().Id.Should().BeEqualTo("sub-id");
        await subscriptionEditor.ToEntity().Remarks.Should().BeEqualTo("subscription");
        await routingEditor.ToEntity().Id.Should().BeEqualTo("routing-id");
        await routingEditor.ToEntity().RuleSet.Should().BeEqualTo("original-rules");
        await webDavEditor.ToEntity().Url.Should().BeEqualTo("original-url");
        await ReferenceEquals(profileOutput, profileEditor.ToEntity()).Should().BeFalse();
        await ReferenceEquals(subscriptionOutput, subscriptionEditor.ToEntity()).Should().BeFalse();
        await ReferenceEquals(routingOutput, routingEditor.ToEntity()).Should().BeFalse();
        await ReferenceEquals(webDavOutput, webDavEditor.ToEntity()).Should().BeFalse();
    }

    [Test]
    public async Task Editors_RaiseChangedOnlyWhenValuesDiffer()
    {
        var profile = ProfileEditorModel.FromEntity(new ProfileItem { Remarks = "original" });
        var subscription = SubscriptionEditorModel.FromEntity(new SubItem { RequestHeaders = "original" });
        var routing = RoutingEditorModel.FromEntity(new RoutingItem { Remarks = "original" });
        var webDav = WebDavEditorModel.FromEntity(new WebDavItem { Url = "original" });
        await AssertNotifications(profile, nameof(profile.Remarks), () => profile.Remarks, value => profile.Remarks = value!);
        await AssertNotifications(subscription, nameof(subscription.RequestHeaders), () => subscription.RequestHeaders, value => subscription.RequestHeaders = value);
        await AssertNotifications(routing, nameof(routing.Remarks), () => routing.Remarks, value => routing.Remarks = value!);
        await AssertNotifications(webDav, nameof(webDav.Url), () => webDav.Url, value => webDav.Url = value);

        var observedPorts = new List<int?>();
        profile.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(profile.PreSocksPort))
            {
                observedPorts.Add(profile.PreSocksPort);
            }
        };
        profile.PreSocksPort = null;
        profile.PreSocksPort = 1080;
        profile.PreSocksPort = 1080;
        profile.PreSocksPort = null;
        await observedPorts.Count.Should().BeEqualTo(2);
        await observedPorts[0].Should().BeEqualTo(1080);
        await observedPorts[1].Should().BeNull();
    }

    [Test]
    public async Task NestedObservation_FollowsEditorReplacementAndStopsAfterDisposal()
    {
        var first = ProfileEditorModel.FromEntity(new ProfileItem { Remarks = "first" });
        var second = ProfileEditorModel.FromEntity(new ProfileItem { Remarks = "second" });
        var host = new EditorHost { SelectedSource = first };
        var observed = new List<string>();
        using var observation = host.WhenAnyValue(x => x.SelectedSource.Remarks).Subscribe(value => observed.Add(value));
        first.Remarks = "first-edited";
        host.SelectedSource = second;
        first.Remarks = "detached";
        second.Remarks = "second-edited";
        await observed.Count.Should().BeEqualTo(4);
        await observed[0].Should().BeEqualTo("first");
        await observed[1].Should().BeEqualTo("first-edited");
        await observed[2].Should().BeEqualTo("second");
        await observed[3].Should().BeEqualTo("second-edited");
        observation.Dispose();
        second.Remarks = "after-disposal";
        host.SelectedSource = first;
        first.Remarks = "after-replacement";
        await observed.Count.Should().BeEqualTo(4);
    }

    private static async Task AssertNotifications(INotifyPropertyChanged editor, string propertyName, Func<string?> get, Action<string?> set)
    {
        var observed = new List<string?>();
        PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName == propertyName)
            {
                observed.Add(get());
            }
        };
        editor.PropertyChanged += handler;
        try
        {
            set("original");
            set("edited");
            set("edited");
            set(null);
            set(null);
            set("restored");
            await observed.Count.Should().BeEqualTo(3);
            await observed[0].Should().BeEqualTo("edited");
            await observed[1].Should().BeNull();
            await observed[2].Should().BeEqualTo("restored");
        }
        finally
        {
            editor.PropertyChanged -= handler;
        }
    }

    internal sealed class EditorHost : ReactiveObject
    {
        public ProfileEditorModel SelectedSource
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        } = null!;
    }
}
