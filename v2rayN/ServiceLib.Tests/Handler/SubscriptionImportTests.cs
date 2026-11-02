using ServiceLib.Handler.Fmt;
using ServiceLib.Tests.CoreConfig;

namespace ServiceLib.Tests.Handler;

[NotInParallel]
public class SubscriptionImportTests
{
    private const string OriginalUri = "vless://00000000-0000-0000-0000-000000000001@original.example:443?type=tcp&security=tls&sni=original.example#selected";

    [Test]
    public async Task MalformedSubscriptionPreservesExistingProfileAndCustomFile()
    {
        await using var scope = await ImportScope.Create();
        var fileName = $"subscription-test-{Guid.NewGuid():N}.json";
        var filePath = Utils.GetConfigPath(fileName);
        await File.WriteAllTextAsync(filePath, "{\"outbounds\":[{\"protocol\":\"freedom\"}]}");
        var profile = new ProfileItem
        {
            IndexId = $"custom-{Guid.NewGuid():N}",
            ConfigType = EConfigType.Custom,
            Address = fileName,
            Subid = scope.SubId,
            IsSub = true,
            Remarks = "selected",
        };
        await SQLiteHelper.Instance.ReplaceAsync(profile);
        scope.Track(profile);
        scope.Config.IndexId = profile.IndexId;

        var count = await ConfigHandler.AddBatchServers(scope.Config, "not a subscription", scope.SubId, true);

        await (count <= 0).Should().BeTrue();
        await ((await AppManager.Instance.GetProfileItem(profile.IndexId)) is not null).Should().BeTrue();
        await File.Exists(filePath).Should().BeTrue();
        await scope.Config.IndexId.Should().BeEqualTo(profile.IndexId);
    }

    [Test]
    public async Task PartiallyMalformedSubscriptionDoesNotReplaceExistingProfiles()
    {
        await using var scope = await ImportScope.Create();
        var original = await scope.Seed(OriginalUri);

        var count = await ConfigHandler.AddBatchServers(scope.Config,
            OriginalUri + "\n" + "vless://invalid", scope.SubId, true);

        await (count <= 0).Should().BeTrue();
        var profiles = await AppManager.Instance.ProfileItems(scope.SubId);
        await profiles!.Should().HaveCount(1);
        await profiles[0].IndexId.Should().BeEqualTo(original.IndexId);
    }

    [Test]
    public async Task EquivalentSubscriptionPreservesIdentityStatisticsAndExternalReferences()
    {
        await using var scope = await ImportScope.Create();
        var original = await scope.Seed(OriginalUri);
        scope.Config.IndexId = original.IndexId;
        await SQLiteHelper.Instance.ReplaceAsync(new ProfileExItem { IndexId = original.IndexId, Delay = 321, Sort = 17 });
        await SQLiteHelper.Instance.ReplaceAsync(new ServerStatItem { IndexId = original.IndexId, TotalDown = 1234, TotalUp = 5678 });
        var group = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, $"group-{Guid.NewGuid():N}", "outside", [original.IndexId]);
        group.Subid = scope.SubId;
        group.IsSub = false;
        await SQLiteHelper.Instance.ReplaceAsync(group);

        var count = await ConfigHandler.AddBatchServers(scope.Config, OriginalUri, scope.SubId, true);

        await count.Should().BeEqualTo(1);
        var profiles = (await AppManager.Instance.ProfileItems(scope.SubId))!.Where(p => p.IsSub).ToList();
        await profiles.Should().HaveCount(1);
        await profiles[0].IndexId.Should().BeEqualTo(original.IndexId);
        await scope.Config.IndexId.Should().BeEqualTo(original.IndexId);
        await (await SQLiteHelper.Instance.TableAsync<ProfileExItem>().FirstAsync(p => p.IndexId == original.IndexId)).Delay.Should().BeEqualTo(321);
        await (await SQLiteHelper.Instance.TableAsync<ServerStatItem>().FirstAsync(p => p.IndexId == original.IndexId)).TotalDown.Should().BeEqualTo(1234L);
        await (await AppManager.Instance.GetProfileItem(group.IndexId))!.GetProtocolExtra().ChildItems.Should().BeEqualTo(original.IndexId);
    }

    [Test]
    public async Task UniqueNamedChangedCredentialsKeepActiveIdentityAndApplyNewCredentials()
    {
        await using var scope = await ImportScope.Create();
        var original = await scope.Seed(OriginalUri);
        scope.Config.IndexId = original.IndexId;

        var count = await ConfigHandler.AddBatchServers(scope.Config,
            OriginalUri.Replace("000000000001", "000000000002"), scope.SubId, true);

        await count.Should().BeEqualTo(1);
        var updated = (await AppManager.Instance.ProfileItems(scope.SubId))!.Single();
        await updated.IndexId.Should().BeEqualTo(original.IndexId);
        await updated.Password.Should().BeEqualTo("00000000-0000-0000-0000-000000000002");
        await scope.Config.IndexId.Should().BeEqualTo(original.IndexId);
    }

    [Test]
    public async Task AmbiguousNamesDoNotSwitchActiveIdentityToAnUnrelatedProfile()
    {
        await using var scope = await ImportScope.Create();
        var original = await scope.Seed(OriginalUri);
        scope.Config.IndexId = original.IndexId;
        var changed = OriginalUri.Replace("000000000001", "000000000002");
        var another = OriginalUri.Replace("000000000001", "000000000003");

        await ConfigHandler.AddBatchServers(scope.Config, changed + "\n" + another, scope.SubId, true);

        await scope.Config.IndexId.Should().BeEqualTo(original.IndexId);
        await ((await AppManager.Instance.GetProfileItem(original.IndexId)) is null).Should().BeTrue();
    }

    [Test]
    public async Task PartiallyMalformedCustomArrayPreservesExistingProfileAndFile()
    {
        await using var scope = await ImportScope.Create();
        const string valid = """
            {"remarks":"selected","inbounds":[],"outbounds":[{"tag":"proxy","protocol":"vless","settings":{"vnext":[{"address":"example.com","port":443,"users":[{"id":"00000000-0000-0000-0000-000000000001"}]}]}}]}
            """;
        await ConfigHandler.AddBatchServers(scope.Config, valid, scope.SubId, true);
        var original = (await AppManager.Instance.ProfileItems(scope.SubId))!.Single();
        var filePath = Utils.GetConfigPath(original.Address);

        var count = await ConfigHandler.AddBatchServers(scope.Config, $"[{valid}, {{}}]", scope.SubId, true);

        await (count <= 0).Should().BeTrue();
        await ((await AppManager.Instance.GetProfileItem(original.IndexId)) is not null).Should().BeTrue();
        await File.Exists(filePath).Should().BeTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FailedRequiredSourcePreservesTheEntireSubscription(bool mainFails)
    {
        await using var scope = await ImportScope.Create();
        var original = await scope.Seed(OriginalUri);
        await using var server = new SubscriptionServer((path, _) => Task.FromResult(
            (path == "/main") == mainFails ? string.Empty : OriginalUri.Replace("#selected", "#downloaded")));
        await scope.SetUrls(server.Url + "/main", server.Url + "/additional");
        var completionStates = new List<bool>();

        await SubscriptionHandler.UpdateProcess(scope.Config, scope.SubId, false, (success, _) =>
        {
            completionStates.Add(success);
            return Task.CompletedTask;
        });

        await ((await AppManager.Instance.GetProfileItem(original.IndexId)) is not null).Should().BeTrue();
        await (await AppManager.Instance.ProfileItems(scope.SubId))!.Single().Remarks.Should().BeEqualTo("selected");
        await completionStates.Any(state => state).Should().BeFalse();
    }

    [Test]
    public async Task SlowerOlderDownloadCannotOverwriteANewerRefresh()
    {
        await using var scope = await ImportScope.Create();
        await scope.Seed(OriginalUri);
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new SubscriptionServer(async (_, sequence) =>
        {
            if (sequence == 1)
            {
                requested.SetResult();
                await release.Task;
                return OriginalUri.Replace("#selected", "#obsolete");
            }
            return OriginalUri.Replace("#selected", "#newest");
        });
        await scope.SetUrls(server.Url + "/main");
        var older = SubscriptionHandler.UpdateProcess(scope.Config, scope.SubId, false, IgnoreUpdate);
        try
        {
            await requested.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await SubscriptionHandler.UpdateProcess(scope.Config, scope.SubId, false, IgnoreUpdate).WaitAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            release.TrySetResult();
        }
        await older.WaitAsync(TimeSpan.FromSeconds(15));

        await (await AppManager.Instance.ProfileItems(scope.SubId))!.Single().Remarks.Should().BeEqualTo("newest");
    }

    [Test]
    public async Task UserSelectionDuringDownloadSurvivesRemovalOfThePreviouslySelectedNode()
    {
        await using var scope = await ImportScope.Create();
        var original = await scope.Seed(OriginalUri);
        scope.Config.IndexId = original.IndexId;
        var manual = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, $"manual-{Guid.NewGuid():N}", "manual");
        manual.Subid = scope.SubId;
        manual.IsSub = false;
        await SQLiteHelper.Instance.ReplaceAsync(manual);
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new SubscriptionServer(async (_, _) =>
        {
            requested.SetResult();
            await release.Task;
            return OriginalUri.Replace("000000000001", "000000000002").Replace("#selected", "#replacement");
        });
        await scope.SetUrls(server.Url + "/main");
        var refresh = SubscriptionHandler.UpdateProcess(scope.Config, scope.SubId, false, IgnoreUpdate);
        try
        {
            await requested.Task.WaitAsync(TimeSpan.FromSeconds(10));
            scope.Config.IndexId = manual.IndexId;
        }
        finally
        {
            release.TrySetResult();
        }
        await refresh.WaitAsync(TimeSpan.FromSeconds(15));

        await scope.Config.IndexId.Should().BeEqualTo(manual.IndexId);
        await ((await AppManager.Instance.GetProfileItem(manual.IndexId)) is not null).Should().BeTrue();
        await ((await AppManager.Instance.GetProfileItem(original.IndexId)) is null).Should().BeTrue();
    }

    [Test]
    [Arguments("sip008-array")]
    [Arguments("sip008-object")]
    [Arguments("wireguard")]
    [Arguments("internal")]
    [Arguments("xray-outbound")]
    [Arguments("singbox-outbound")]
    [Arguments("clash-yaml")]
    [Arguments("json-comments")]
    [Arguments("xray-with-direct")]
    [Arguments("singbox-with-direct")]
    [Arguments("xray-configured-raw")]
    [Arguments("mihomo-configured-raw")]
    public async Task ExistingSubscriptionFormatsRemainImportable(string format)
    {
        await using var scope = await ImportScope.Create();
        if (format is "xray-configured-raw" or "mihomo-configured-raw")
        {
            var subscription = (await AppManager.Instance.GetSubItem(scope.SubId))!;
            subscription.CustomCoreType = format == "xray-configured-raw" ? ECoreType.Xray : ECoreType.mihomo;
            await SQLiteHelper.Instance.ReplaceAsync(subscription);
        }
        const string ssArray = """
            [{"server":"example.com","server_port":443,"method":"aes-128-gcm","password":"test-password","remarks":"SIP008"}]
            """;
        var data = format switch
        {
            "sip008-array" => ssArray,
            "sip008-object" => "{\"version\":1,\"servers\":" + ssArray + "}",
            "wireguard" => """
                [Interface]
                PrivateKey = AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=
                Address = 10.0.0.2/32
                [Peer]
                PublicKey = AQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQE=
                Endpoint = example.com:51820
                AllowedIPs = 0.0.0.0/0
                """,
            "internal" => InnerFmt.ToUri([CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray)])!,
            "xray-outbound" => """{"protocol":"socks","tag":"proxy","settings":{"servers":[{"address":"example.com","port":1080}]}}""",
            "singbox-outbound" => """{"type":"socks","tag":"proxy","server":"example.com","server_port":1080}""",
            "clash-yaml" => """
                ---
                mixed-port: 10808
                proxies:
                  - name: test
                    type: ss
                    server: example.com
                    port: 443
                    cipher: aes-128-gcm
                    password: test-password
                rules:
                  - MATCH,test
                """,
            "json-comments" => """
                { // Xray custom configuration
                  "inbounds": [],
                  "outbounds": [{"protocol":"socks","tag":"proxy","settings":{"servers":[{"address":"example.com","port":1080}]}}]
                }
                """,
            "xray-with-direct" => """{"inbounds":[],"outbounds":[{"protocol":"socks","tag":"proxy","settings":{"servers":[{"address":"example.com","port":1080}]}},{"protocol":"freedom","tag":"direct"},{"protocol":"blackhole","tag":"block"}]}""",
            "singbox-with-direct" => """{"inbounds":[],"outbounds":[{"type":"socks","tag":"proxy","server":"example.com","server_port":1080},{"type":"direct","tag":"direct"}]}""",
            "xray-configured-raw" => """{"inbounds":[],"outbounds":[{"protocol":"freedom","tag":"direct"}]}""",
            "mihomo-configured-raw" => "mixed-port: 10808\nmode: direct\nproxies: []\nrules: ['MATCH,DIRECT']",
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        var count = await ConfigHandler.AddBatchServers(scope.Config, data, scope.SubId, true);

        await count.Should().BeEqualTo(1);
        await (await AppManager.Instance.ProfileItems(scope.SubId))!.Should().HaveCount(1);
    }

    private static Task IgnoreUpdate(bool success, string message) => Task.CompletedTask;

    [Test]
    public async Task RefreshResultReportsUpdatedSubscriptionAndKeepsLegacyCompletion()
    {
        await using var scope = await ImportScope.Create();
        await using var server = new SubscriptionServer((_, _) => Task.FromResult(OriginalUri));
        await scope.SetUrls(server.Url + "/main");
        var states = new List<bool>();

        var result = await SubscriptionHandler.UpdateProcess(scope.Config, scope.SubId, false, (success, _) =>
        {
            states.Add(success);
            return Task.CompletedTask;
        });

        await result.Success.Should().BeTrue();
        await result.UpdatedSubscriptionIds.Single().Should().BeEqualTo(scope.SubId);
        await result.ActiveProfileUnavailable.Should().BeFalse();
        await states.Count(success => success).Should().BeEqualTo(1);
        await states.Last().Should().BeTrue();
    }

    [Test]
    public async Task RefreshResultReportsRemovedActiveIdentityWithoutSelectingAReplacement()
    {
        await using var scope = await ImportScope.Create();
        var original = await scope.Seed(OriginalUri);
        scope.Config.IndexId = original.IndexId;
        var replacement = OriginalUri.Replace("000000000001", "000000000002").Replace("#selected", "#replacement");
        await using var server = new SubscriptionServer((_, _) => Task.FromResult(replacement));
        await scope.SetUrls(server.Url + "/main");

        var result = await SubscriptionHandler.UpdateProcess(scope.Config, scope.SubId, false, IgnoreUpdate);

        await result.Success.Should().BeTrue();
        await result.ActiveProfileUnavailable.Should().BeTrue();
        await result.UnavailableActiveProfileIds.Single().Should().BeEqualTo(original.IndexId);
        await scope.Config.IndexId.Should().BeEqualTo(original.IndexId);
    }

    [Test]
    public async Task CustomJsonSourcesAreStagedIndependently()
    {
        await using var scope = await ImportScope.Create();
        const string document = """
            {"remarks":"first","inbounds":[],"outbounds":[{"protocol":"socks","tag":"proxy","settings":{"servers":[{"address":"example.com","port":1080}]}}]}
            """;
        await using var server = new SubscriptionServer((path, _) => Task.FromResult(
            path == "/main" ? document : document.Replace("first", "second")));
        await scope.SetUrls(server.Url + "/main", server.Url + "/additional");

        var result = await SubscriptionHandler.UpdateProcess(scope.Config, scope.SubId, false, IgnoreUpdate);

        await result.Success.Should().BeTrue();
        var profiles = (await AppManager.Instance.ProfileItems(scope.SubId))!;
        await profiles.Should().HaveCount(2);
        await profiles.Select(profile => profile.Remarks).OrderBy(name => name).SequenceEqual(["first", "second"]).Should().BeTrue();
        await profiles.All(profile => File.Exists(Utils.GetConfigPath(profile.Address))).Should().BeTrue();
    }

    [Test]
    public async Task EquivalentCustomContentRetainsItsIdentityAndFileAcrossJsonPropertyReordering()
    {
        await using var scope = await ImportScope.Create();
        const string first = """{"inbounds":[],"outbounds":[{"protocol":"socks","tag":"proxy","settings":{"servers":[{"address":"example.com","port":1080}]}}]}""";
        const string reordered = """{"outbounds":[{"settings":{"servers":[{"port":1080,"address":"example.com"}]},"tag":"proxy","protocol":"socks"}],"inbounds":[]}""";
        await ConfigHandler.AddBatchServers(scope.Config, first, scope.SubId, true);
        var original = (await AppManager.Instance.ProfileItems(scope.SubId))!.Single();
        scope.Track(original);

        var count = await ConfigHandler.AddBatchServers(scope.Config, reordered, scope.SubId, true);

        await count.Should().BeEqualTo(1);
        var current = (await AppManager.Instance.ProfileItems(scope.SubId))!.Single();
        await current.IndexId.Should().BeEqualTo(original.IndexId);
        await current.Address.Should().BeEqualTo(original.Address);
    }

    [Test]
    [Arguments("certificate")]
    [Arguments("ech")]
    [Arguments("mux")]
    public async Task ChangedRuntimeFieldsPreventRenamedNodesFromMatchingAsEquivalent(string field)
    {
        await using var scope = await ImportScope.Create();
        var original = await scope.Seed(OriginalUri);
        scope.Config.IndexId = original.IndexId;
        var changed = JsonUtils.DeepCopy(original)!;
        changed.Remarks = "renamed";
        switch (field)
        {
            case "certificate":
                changed.CertSha = "changed-certificate-sha";
                break;
            case "ech":
                changed.EchConfigList = "changed-ech-config";
                break;
            case "mux":
                changed.MuxEnabled = true;
                break;
        }

        var count = await ConfigHandler.AddBatchServers(scope.Config, InnerFmt.ToUri([changed])!, scope.SubId, true);

        await count.Should().BeEqualTo(1);
        var current = (await AppManager.Instance.ProfileItems(scope.SubId))!.Single();
        await (current.IndexId != original.IndexId).Should().BeTrue();
        await scope.Config.IndexId.Should().BeEqualTo(original.IndexId);
    }

    [Test]
    public async Task DuplicateUriAcrossSourcesRetainsTheEquivalentActiveNode()
    {
        await using var scope = await ImportScope.Create();
        var original = await scope.Seed(OriginalUri);
        scope.Config.IndexId = original.IndexId;
        await using var server = new SubscriptionServer((path, _) => Task.FromResult(
            path == "/main" ? OriginalUri : OriginalUri + "\n" + OriginalUri.Replace("#selected", "#second")));
        await scope.SetUrls(server.Url + "/main", server.Url + "/additional");

        var result = await SubscriptionHandler.UpdateProcess(scope.Config, scope.SubId, false, IgnoreUpdate);

        await result.Success.Should().BeTrue();
        var profiles = (await AppManager.Instance.ProfileItems(scope.SubId))!;
        await profiles.Should().HaveCount(2);
        await profiles.Single(profile => profile.Remarks == "selected").IndexId.Should().BeEqualTo(original.IndexId);
        await result.ActiveProfileUnavailable.Should().BeFalse();
    }

    [Test]
    [Arguments("{}")]
    [Arguments("{\"error\":\"temporary source failure\"}")]
    public async Task ExplicitCustomCoreRejectsMalformedContentWithoutReplacingExistingNodes(string content)
    {
        await using var scope = await ImportScope.Create();
        var original = await scope.Seed(OriginalUri);
        var subscription = (await AppManager.Instance.GetSubItem(scope.SubId))!;
        subscription.CustomCoreType = ECoreType.Xray;
        await SQLiteHelper.Instance.ReplaceAsync(subscription);

        var count = await ConfigHandler.AddBatchServers(scope.Config, content, scope.SubId, true);

        await (count <= 0).Should().BeTrue();
        await ((await AppManager.Instance.GetProfileItem(original.IndexId)) is not null).Should().BeTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MalformedNestedOutboundPreservesExistingSubscription(bool singbox)
    {
        await using var scope = await ImportScope.Create();
        var original = await scope.Seed(OriginalUri);
        var outbound = singbox
            ? """{"type":"socks","tag":"proxy","server":"example.com","server_port":1080}"""
            : """{"protocol":"socks","tag":"proxy","settings":{"servers":[{"address":"example.com","port":1080}]}}""";
        var content = "{\"inbounds\":[],\"outbounds\":[" + outbound + ",{}]}";

        var count = await ConfigHandler.AddBatchServers(scope.Config, content, scope.SubId, true);

        await (count <= 0).Should().BeTrue();
        await ((await AppManager.Instance.GetProfileItem(original.IndexId)) is not null).Should().BeTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DroppedRequiredOutboundPreservesExistingCustomProfileAndFile(bool singbox)
    {
        await using var scope = await ImportScope.Create();
        const string oldContent = """{"inbounds":[],"outbounds":[{"protocol":"socks","tag":"old","settings":{"servers":[{"address":"old.example","port":1080}]}}]}""";
        await ConfigHandler.AddBatchServers(scope.Config, oldContent, scope.SubId, true);
        var original = (await AppManager.Instance.ProfileItems(scope.SubId))!.Single();
        scope.Track(original);
        var originalFile = Utils.GetConfigPath(original.Address);
        var savedContent = await File.ReadAllTextAsync(originalFile);
        var validOutbound = singbox
            ? """{"type":"socks","tag":"new","server":"new.example","server_port":1080}"""
            : """{"protocol":"socks","tag":"new","settings":{"servers":[{"address":"new.example","port":1080}]}}""";
        var invalidOutbound = singbox ? """{"type":"socks"}""" : """{"protocol":"socks"}""";
        var payload = "{\"outbounds\":[" + validOutbound + "," + invalidOutbound + "]}";

        var count = await ConfigHandler.AddBatchServers(scope.Config, payload, scope.SubId, true);

        await (count <= 0).Should().BeTrue();
        await (await AppManager.Instance.ProfileItems(scope.SubId))!.Single().IndexId.Should().BeEqualTo(original.IndexId);
        await File.Exists(originalFile).Should().BeTrue();
        await (await File.ReadAllTextAsync(originalFile)).Should().BeEqualTo(savedContent);
    }

    [Test]
    [Arguments("error: upstream unavailable")]
    [Arguments("{\"error\":\"upstream unavailable\"}")]
    public async Task SelectedMihomoCoreRejectsSourceErrorMappings(string payload)
    {
        await using var scope = await ImportScope.Create();
        var original = await scope.Seed(OriginalUri);
        var subscription = (await AppManager.Instance.GetSubItem(scope.SubId))!;
        subscription.CustomCoreType = ECoreType.mihomo;
        await SQLiteHelper.Instance.ReplaceAsync(subscription);

        var count = await ConfigHandler.AddBatchServers(scope.Config, payload, scope.SubId, true);

        await (count <= 0).Should().BeTrue();
        await (await AppManager.Instance.ProfileItems(scope.SubId))!.Single().IndexId.Should().BeEqualTo(original.IndexId);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NestedGroupIdentitySurvivesRenamedOuterGroupInEitherInputOrder(bool outerFirst)
    {
        await using var scope = await ImportScope.Create();
        var leaf = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, "leaf-export", "leaf");
        var inner = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, "inner-export", "inner", [leaf.IndexId]);
        var outer = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, "outer-export", "outer", [inner.IndexId]);
        await ConfigHandler.AddBatchServers(scope.Config, InnerFmt.ToUri([leaf, inner, outer])!, scope.SubId, true);
        var original = (await AppManager.Instance.ProfileItems(scope.SubId))!.ToDictionary(profile => profile.Remarks);
        foreach (var profile in original.Values)
        {
            scope.Track(profile);
        }
        var external = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, $"external-{Guid.NewGuid():N}", "external", [original["outer"].IndexId]);
        external.Subid = scope.SubId;
        external.IsSub = false;
        await SQLiteHelper.Instance.ReplaceAsync(external);
        var incoming = original.Values.Select(profile => JsonUtils.DeepCopy(profile)!).ToDictionary(profile => profile.Remarks);
        incoming["outer"].Remarks = "renamed outer";
        var profiles = outerFirst
            ? new List<ProfileItem> { incoming["outer"], incoming["inner"], incoming["leaf"] }
            : [incoming["leaf"], incoming["inner"], incoming["outer"]];

        var count = await ConfigHandler.AddBatchServers(scope.Config, InnerFmt.ToUri(profiles)!, scope.SubId, true);

        await count.Should().BeEqualTo(3);
        var current = (await AppManager.Instance.ProfileItems(scope.SubId))!.Where(profile => profile.IsSub).ToDictionary(profile => profile.Remarks);
        await current["leaf"].IndexId.Should().BeEqualTo(original["leaf"].IndexId);
        await current["inner"].IndexId.Should().BeEqualTo(original["inner"].IndexId);
        await current["renamed outer"].IndexId.Should().BeEqualTo(original["outer"].IndexId);
        await current["inner"].GetProtocolExtra().ChildItems.Should().BeEqualTo(original["leaf"].IndexId);
        await current["renamed outer"].GetProtocolExtra().ChildItems.Should().BeEqualTo(original["inner"].IndexId);
        await (await AppManager.Instance.GetProfileItem(external.IndexId))!.GetProtocolExtra().ChildItems.Should().BeEqualTo(original["outer"].IndexId);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ServiceOutboundsAreExplicitlyExcludedFromExtractedProxyCompleteness(bool singbox)
    {
        await using var scope = await ImportScope.Create();
        var payload = singbox
            ? """{"outbounds":[{"type":"socks","tag":"proxy","server":"example.com","server_port":1080},{"type":"direct"},{"type":"block"},{"type":"dns"}]}"""
            : """{"outbounds":[{"protocol":"socks","tag":"proxy","settings":{"servers":[{"address":"example.com","port":1080}]}},{"protocol":"freedom"},{"protocol":"blackhole"},{"protocol":"dns"}]}""";

        var count = await ConfigHandler.AddBatchServers(scope.Config, payload, scope.SubId, true);

        await count.Should().BeEqualTo(1);
        var profile = (await AppManager.Instance.ProfileItems(scope.SubId))!.Single();
        await profile.ConfigType.Should().BeEqualTo(EConfigType.Outbound);
        await File.Exists(Utils.GetConfigPath(profile.Address)).Should().BeTrue();
    }

    [Test]
    public async Task ExplicitRawXrayCorePreservesValidUntaggedOutboundConfiguration()
    {
        await using var scope = await ImportScope.Create();
        var subscription = (await AppManager.Instance.GetSubItem(scope.SubId))!;
        subscription.CustomCoreType = ECoreType.Xray;
        await SQLiteHelper.Instance.ReplaceAsync(subscription);
        const string payload = """{"inbounds":[],"outbounds":[{"protocol":"socks","settings":{"servers":[{"address":"example.com","port":1080}]}}]}""";

        var count = await ConfigHandler.AddBatchServers(scope.Config, payload, scope.SubId, true);

        await count.Should().BeEqualTo(1);
        var profile = (await AppManager.Instance.ProfileItems(scope.SubId))!.Single();
        await profile.ConfigType.Should().BeEqualTo(EConfigType.Custom);
        await (await File.ReadAllTextAsync(Utils.GetConfigPath(profile.Address))).Should().BeEqualTo(payload);
    }

    [Test]
    public async Task PartialExtractionOfValidButUntaggedOutboundPreservesOldData()
    {
        await using var scope = await ImportScope.Create();
        var original = await scope.Seed(OriginalUri);
        const string payload = """{"outbounds":[{"protocol":"socks","tag":"tagged","settings":{"servers":[{"address":"first.example","port":1080}]}},{"protocol":"socks","settings":{"servers":[{"address":"second.example","port":1080}]}}]}""";

        var count = await ConfigHandler.AddBatchServers(scope.Config, payload, scope.SubId, true);

        await (count <= 0).Should().BeTrue();
        await (await AppManager.Instance.ProfileItems(scope.SubId))!.Single().IndexId.Should().BeEqualTo(original.IndexId);
    }

    [Test]
    public async Task CyclicGroupsDoNotHangOrClaimRenamedOldIdentities()
    {
        await using var scope = await ImportScope.Create();
        var first = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, "cycle-a", "first", ["cycle-b"]);
        var second = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, "cycle-b", "second", ["cycle-a"]);
        await ConfigHandler.AddBatchServers(scope.Config, InnerFmt.ToUri([first, second])!, scope.SubId, true);
        var original = (await AppManager.Instance.ProfileItems(scope.SubId))!;
        original.ForEach(scope.Track);
        scope.Config.IndexId = original[0].IndexId;
        var changed = original.Select(profile => JsonUtils.DeepCopy(profile)!).ToList();
        changed.ForEach(profile => profile.Remarks += " renamed");

        var count = await ConfigHandler.AddBatchServers(scope.Config, InnerFmt.ToUri(changed)!, scope.SubId, true).WaitAsync(TimeSpan.FromSeconds(10));

        await count.Should().BeEqualTo(2);
        var current = (await AppManager.Instance.ProfileItems(scope.SubId))!;
        await current.All(profile => original.All(old => old.IndexId != profile.IndexId)).Should().BeTrue();
        await scope.Config.IndexId.Should().BeEqualTo(original[0].IndexId);
    }

    [Test]
    public async Task AmbiguousEquivalentGroupsDoNotClaimAnOldActiveIdentity()
    {
        await using var scope = await ImportScope.Create();
        var leaf = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, "leaf", "leaf");
        var first = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, "group-a", "duplicate", [leaf.IndexId]);
        var second = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, "group-b", "duplicate", [leaf.IndexId]);
        await ConfigHandler.AddBatchServers(scope.Config, InnerFmt.ToUri([leaf, first, second])!, scope.SubId, true);
        var original = (await AppManager.Instance.ProfileItems(scope.SubId))!;
        original.ForEach(scope.Track);
        var active = original.First(profile => profile.ConfigType.IsGroupType());
        scope.Config.IndexId = active.IndexId;

        await ConfigHandler.AddBatchServers(scope.Config, InnerFmt.ToUri(original)!, scope.SubId, true);

        var current = (await AppManager.Instance.ProfileItems(scope.SubId))!;
        await current.Where(profile => profile.ConfigType.IsGroupType()).All(profile => original.All(old => old.IndexId != profile.IndexId)).Should().BeTrue();
        await scope.Config.IndexId.Should().BeEqualTo(active.IndexId);
    }

    private sealed class SubscriptionServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Func<string, int, Task<string>> _response;
        private readonly ConcurrentBag<Task> _requests = [];
        private readonly Task _accept;
        private int _sequence;
        public string Url { get; }

        public SubscriptionServer(Func<string, int, Task<string>> response)
        {
            _response = response;
            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
            _accept = Accept();
        }

        private async Task Accept()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_cancellation.Token);
                _requests.Add(Serve(client));
            }
        }

        private async Task Serve(TcpClient client)
        {
            using (client)
            {
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var request = await reader.ReadLineAsync(_cancellation.Token);
                while (await reader.ReadLineAsync(_cancellation.Token) is { Length: > 0 })
                {
                }
                if (request is null)
                {
                    return;
                }
                var body = await _response(request.Split(' ')[1], Interlocked.Increment(ref _sequence)).WaitAsync(_cancellation.Token);
                var bytes = Encoding.UTF8.GetBytes(body);
                var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header, _cancellation.Token);
                if (!request.StartsWith("HEAD ", StringComparison.Ordinal))
                {
                    await stream.WriteAsync(bytes, _cancellation.Token);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cancellation.CancelAsync();
            _listener.Stop();
            try
            {
                await _accept;
            }
            catch (OperationCanceledException)
            {
            }
            try
            {
                await Task.WhenAll(_requests);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _cancellation.Dispose();
            }
        }
    }

    private sealed class ImportScope : IAsyncDisposable
    {
        private readonly object? _previousConfig;
        private readonly string? _previousLocalData;
        private readonly HashSet<string> _profileIds = [];
        private readonly HashSet<string> _files = [];
        private static readonly FieldInfo ConfigField = typeof(AppManager).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)!;
        public Config Config { get; } = CoreConfigTestFactory.CreateConfig();
        public string SubId { get; } = $"import-test-{Guid.NewGuid():N}";

        private ImportScope()
        {
            _previousLocalData = Environment.GetEnvironmentVariable(Global.LocalAppData);
            Environment.SetEnvironmentVariable(Global.LocalAppData, "0");
            _previousConfig = ConfigField.GetValue(AppManager.Instance);
            ConfigField.SetValue(AppManager.Instance, Config);
            SQLiteHelper.Instance.CreateTable<SubItem>();
            SQLiteHelper.Instance.CreateTable<ProfileItem>();
            SQLiteHelper.Instance.CreateTable<ProfileExItem>();
            SQLiteHelper.Instance.CreateTable<ServerStatItem>();
        }

        public static async Task<ImportScope> Create()
        {
            var scope = new ImportScope();
            await SQLiteHelper.Instance.ReplaceAsync(new SubItem { Id = scope.SubId, Remarks = "test", Url = "http://127.0.0.1/sub", Enabled = true });
            return scope;
        }

        public async Task<ProfileItem> Seed(string uri)
        {
            await ConfigHandler.AddBatchServers(Config, uri, SubId, true);
            var profile = (await AppManager.Instance.ProfileItems(SubId))!.Single();
            Track(profile);
            return profile;
        }

        public void Track(ProfileItem profile)
        {
            _profileIds.Add(profile.IndexId);
            if (profile.ConfigType is EConfigType.Custom or EConfigType.Outbound)
            {
                _files.Add(Utils.GetConfigPath(profile.Address));
            }
        }

        public async Task SetUrls(string main, string more = "")
        {
            await CertPemManager.Instance.Init(Config);
            var subscription = (await AppManager.Instance.GetSubItem(SubId))!;
            subscription.Url = main;
            subscription.MoreUrl = more;
            await SQLiteHelper.Instance.ReplaceAsync(subscription);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                foreach (var profile in await AppManager.Instance.ProfileItems(SubId) ?? [])
                {
                    Track(profile);
                }
                foreach (var path in _files)
                {
                    File.Delete(path);
                }
                foreach (var id in _profileIds)
                {
                    await SQLiteHelper.Instance.DeleteAsync(new ProfileItem { IndexId = id });
                    await SQLiteHelper.Instance.DeleteAsync(new ProfileExItem { IndexId = id });
                    await SQLiteHelper.Instance.DeleteAsync(new ServerStatItem { IndexId = id });
                }
                await SQLiteHelper.Instance.DeleteAsync(new SubItem { Id = SubId });
            }
            finally
            {
                ConfigField.SetValue(AppManager.Instance, _previousConfig);
                Environment.SetEnvironmentVariable(Global.LocalAppData, _previousLocalData);
            }
        }
    }
}
