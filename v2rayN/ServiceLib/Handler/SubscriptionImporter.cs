using YamlDotNet.RepresentationModel;

namespace ServiceLib.Handler;

internal static class SubscriptionImporter
{
    private sealed class RefreshState
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public long Generation;
    }

    private sealed record StagedProfile(ProfileItem Profile, string? Content = null, string Extension = ".json", string? SourceUri = null);

    internal sealed record ImportResult(int Count, string? UnavailableActiveProfileId = null);

    private static readonly ConcurrentDictionary<string, RefreshState> _states = new();

    internal static long BeginRefresh(string subId)
        => Interlocked.Increment(ref _states.GetOrAdd(subId, _ => new RefreshState()).Generation);

    internal static async Task<ImportResult> Import(Config config, IReadOnlyList<string> sources, string subId, long? generation = null)
    {
        var state = _states.GetOrAdd(subId, _ => new RefreshState());
        var expectedGeneration = generation ?? BeginRefresh(subId);
        var subscription = await AppManager.Instance.GetSubItem(subId);
        var staged = new List<StagedProfile>();
        var importedUris = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (var content in sources)
            {
                var profiles = await Stage(config, content, subId, subscription);
                staged.AddRange(profiles.Where(profile => profile.SourceUri == null || importedUris.Add(profile.SourceUri)));
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("StageSubscription", ex);
            return new(-1);
        }

        if (staged.Count == 0)
        {
            return new(-1);
        }

        await state.Gate.WaitAsync();
        try
        {
            if (Volatile.Read(ref state.Generation) != expectedGeneration)
            {
                return new(-1);
            }
            var original = (await AppManager.Instance.ProfileItems(subId) ?? []).Where(profile => profile.IsSub).ToList();
            var activeId = config.IndexId;
            await PreserveIdentities(original, staged);
            return await Commit(config, original, staged, activeId, state, expectedGeneration);
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private static async Task<List<StagedProfile>> Stage(Config config, string content, string subId, SubItem? subscription)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new FormatException("Empty subscription source.");
        }
        if (Utils.IsBase64String(content))
        {
            content = Utils.Base64Decode(content);
        }
        content = content.Trim().TrimStart('\uFEFF');
        var document = JsonUtils.ParseJson(content);
        var profiles = new List<ProfileItem>();
        var profileUris = new Dictionary<ProfileItem, string>();
        try
        {
            if (content.Contains("[Interface]", StringComparison.OrdinalIgnoreCase)
                && content.Contains("[Peer]", StringComparison.OrdinalIgnoreCase))
            {
                profiles = WireguardFmt.ResolveConfig(content) ?? [];
                var peerCount = content.Split('\n').Count(line => line.Trim().Equals("[Peer]", StringComparison.OrdinalIgnoreCase));
                if (profiles.Count != peerCount)
                {
                    throw new FormatException("Incomplete WireGuard subscription.");
                }
            }
            else if (document != null || content.StartsWith('{') || content.StartsWith('['))
            {
                if (document == null)
                {
                    throw new FormatException("Invalid subscription JSON.");
                }
                if (TryGetSip008(document, out var servers))
                {
                    var normalizedServers = (JsonArray)servers!.DeepClone();
                    foreach (var server in normalizedServers.OfType<JsonObject>())
                    {
                        if (!int.TryParse(server["server_port"]?.ToString(), out var port) || port is <= 0 or > 65535)
                        {
                            throw new FormatException("Invalid SIP008 server port.");
                        }
                        server["server_port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }
                    profiles = ShadowsocksFmt.ResolveSip008(normalizedServers.ToJsonString()) ?? [];
                    if (profiles.Count != servers.Count)
                    {
                        throw new FormatException("Incomplete SIP008 subscription.");
                    }
                    profiles.ForEach(profile => profile.ConfigType = EConfigType.Shadowsocks);
                }
                else
                {
                    var entries = document is JsonArray array ? array.ToArray() : [document];
                    foreach (var entry in entries)
                    {
                        if (entry is not JsonObject jsonObject)
                        {
                            throw new FormatException("Invalid custom subscription entry.");
                        }
                        var parsed = ResolveCustom(jsonObject.ToJsonString(), subscription);
                        if (parsed.Count == 0)
                        {
                            throw new FormatException("Unrecognized custom subscription entry.");
                        }
                        profiles.AddRange(parsed);
                    }
                }
            }
            else
            {
                var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
                var isUriList = lines[0].Contains("://", StringComparison.Ordinal);
                if (isUriList && !lines[0].Contains(": ", StringComparison.Ordinal))
                {
                    var innerLines = lines.Where(line => line.StartsWith(Global.InnerUriProtocol, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (innerLines.Count > 0)
                    {
                        var innerProfiles = InnerFmt.Resolve(string.Join('\n', innerLines), subId) ?? [];
                        if (innerProfiles.Count != innerLines.Count)
                        {
                            throw new FormatException("Incomplete internal subscription.");
                        }
                        profiles.AddRange(innerProfiles);
                    }
                    foreach (var line in lines.Except(innerLines))
                    {
                        var profile = FmtHandler.ResolveConfig(line, out _) ?? throw new FormatException("Invalid subscription URI.");
                        profiles.Add(profile);
                        profileUris[profile] = line;
                    }
                }
                else
                {
                    profiles = ResolveCustom(content, subscription);
                }
            }

            if (profiles.Count == 0)
            {
                throw new FormatException("No subscription profiles were parsed.");
            }
            var staged = new List<StagedProfile>();
            foreach (var profile in profiles)
            {
                profile.Subid = subId;
                profile.IsSub = true;
                if (profile.ConfigType == EConfigType.Custom)
                {
                    profile.PreSocksPort = subscription?.PreSocksPort;
                }
                if (!await ConfigHandler.ValidateSubscriptionProfile(config, profile))
                {
                    throw new FormatException("Invalid subscription profile.");
                }
                if (profile.ConfigType is not (EConfigType.Custom or EConfigType.Outbound)
                    && subscription?.Filter.IsNotEmpty() == true
                    && !Utils.IsRegexMatch(profile.Remarks, subscription.Filter))
                {
                    continue;
                }
                if (profile.IndexId.IsNullOrEmpty())
                {
                    profile.IndexId = Utils.GetGuid(false);
                }
                if (profile.ConfigType is EConfigType.Custom or EConfigType.Outbound)
                {
                    var fileContent = await File.ReadAllTextAsync(profile.Address);
                    ValidateDocument(fileContent);
                    if (profile.CoreType == ECoreType.mihomo)
                    {
                        ValidateMihomoDocument(fileContent);
                    }
                    staged.Add(new(profile, fileContent, Path.GetExtension(profile.Address)));
                }
                else
                {
                    staged.Add(new(profile, SourceUri: profileUris.GetValueOrDefault(profile)));
                }
            }
            return staged;
        }
        finally
        {
            foreach (var path in profiles.Where(profile => profile.ConfigType is EConfigType.Custom or EConfigType.Outbound).Select(profile => profile.Address))
            {
                File.Delete(path);
            }
        }
    }

    private static bool TryGetSip008(JsonNode document, out JsonArray? servers)
    {
        servers = document is JsonObject obj && obj["servers"] is JsonArray nested ? nested : document as JsonArray;
        return servers is { Count: > 0 } && servers.All(server => server is JsonObject value && value["server"] != null);
    }

    private static List<ProfileItem> ResolveCustom(string content, SubItem? subscription)
    {
        var requiredOutbounds = ValidateDocument(content);
        List<ProfileItem> profiles;
        if (subscription?.CustomCoreType is { } core)
        {
            profiles = core switch
            {
                ECoreType.Xray => V2rayFmt.ResolveToCustom(content, subscription.Remarks),
                ECoreType.sing_box => SingboxFmt.ResolveToCustom(content, subscription.Remarks),
                _ => [],
            };
            if (profiles.Count > 0)
            {
                return RequireCompleteExtraction(profiles, requiredOutbounds);
            }
            var document = JsonUtils.ParseJson(content);
            if (core is ECoreType.Xray or ECoreType.sing_box
                && (document is not JsonObject root
                    || root["inbounds"] is not JsonArray && root["outbounds"] is not JsonArray && root["endpoints"] is not JsonArray))
            {
                throw new FormatException("Unrecognized custom core configuration.");
            }
            var filePath = Utils.GetTempPath($"{Utils.GetGuid(false)}{(document != null ? ".json" : ".yaml")}");
            File.WriteAllText(filePath, content);
            return [new ProfileItem { ConfigType = EConfigType.Custom, CoreType = core, Address = filePath, Remarks = subscription.Remarks }];
        }
        profiles = V2rayFmt.ResolveToCustom(content, subscription?.Remarks);
        if (profiles.Count == 0)
        {
            profiles = SingboxFmt.ResolveToCustom(content, subscription?.Remarks);
        }
        if (profiles.Count == 0)
        {
            profiles = V2rayFmt.ResolveToCustomOutbound(content, subscription?.Remarks);
        }
        if (profiles.Count == 0)
        {
            profiles = SingboxFmt.ResolveToCustomOutbound(content, subscription?.Remarks);
        }
        if (profiles.Count > 0)
        {
            return RequireCompleteExtraction(profiles, requiredOutbounds);
        }
        if (HtmlPageFmt.IsHtmlPage(content))
        {
            return [];
        }
        var profile = ClashFmt.ResolveFull(content, subscription?.Remarks) ?? Hysteria2Fmt.ResolveFull2(content, subscription?.Remarks);
        if (profile == null)
        {
            return [];
        }
        profile.ConfigType = EConfigType.Custom;
        return [profile];
    }

    private static List<ProfileItem> RequireCompleteExtraction(List<ProfileItem> profiles, int? requiredOutbounds)
    {
        if (requiredOutbounds == null || profiles.Any(profile => profile.ConfigType == EConfigType.Custom)
            || profiles.Count == requiredOutbounds)
        {
            return profiles;
        }
        foreach (var profile in profiles)
        {
            File.Delete(profile.Address);
        }
        throw new FormatException("Incomplete custom outbound extraction.");
    }

    private static int? ValidateDocument(string content)
    {
        var document = JsonUtils.ParseJson(content);
        if (document is JsonObject root)
        {
            if (root.Count == 0)
            {
                throw new FormatException("Empty custom JSON.");
            }
            if (HasProtocol(root))
            {
                ValidateOutbounds([root.DeepClone()]);
                return null;
            }
            foreach (var name in new[] { "inbounds", "outbounds" })
            {
                if (root[name] == null)
                {
                    continue;
                }
                if (root[name] is not JsonArray entries)
                {
                    throw new FormatException("Invalid custom configuration array.");
                }
                foreach (var entry in entries)
                {
                    if (entry is not JsonObject settings || !HasProtocol(settings))
                    {
                        throw new FormatException("Invalid custom configuration entry.");
                    }
                }
            }
            return root["outbounds"] is JsonArray outbounds ? ValidateOutbounds(outbounds) : null;
        }
        if (document is JsonArray { Count: > 0 })
        {
            return null;
        }
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            throw new FormatException("Invalid custom JSON.");
        }
        var yaml = new YamlStream();
        yaml.Load(new StringReader(content));
        if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode { Children.Count: > 0 })
        {
            throw new FormatException("Invalid custom YAML.");
        }
        return null;

        static bool HasProtocol(JsonObject value)
        {
            return new[] { "protocol", "type" }.Any(name => value[name] is JsonValue field
                && field.TryGetValue<string>(out var protocol) && !string.IsNullOrWhiteSpace(protocol));
        }
    }

    private static int ValidateOutbounds(JsonArray outbounds)
    {
        var requiredCount = 0;
        ECoreType? format = null;
        foreach (var node in outbounds)
        {
            if (node is not JsonObject outbound)
            {
                throw new FormatException("Invalid custom outbound.");
            }
            var xray = outbound["protocol"] is JsonValue value && value.TryGetValue<string>(out var protocol) && protocol.IsNotEmpty();
            var core = xray ? ECoreType.Xray : ECoreType.sing_box;
            var name = (xray ? outbound["protocol"] : outbound["type"])?.GetValue<string>();
            if (name.IsNullOrEmpty() || format != null && format != core)
            {
                throw new FormatException("Unrecognized custom outbound format.");
            }
            format = core;
            if (xray ? name is "freedom" or "blackhole" or "dns" or "loopback"
                : name is "direct" or "block" or "dns" or "selector" or "urltest")
            {
                continue;
            }

            var candidate = (JsonObject)outbound.DeepClone();
            if (xray && candidate["tag"]?.ToString().IsNullOrEmpty() != false)
            {
                // A native full Xray config does not require outbound tags.
                candidate["tag"] = "subscription-validation";
            }
            var parsed = xray
                ? V2rayFmt.ResolveToCustomOutbound(candidate.ToJsonString(), null)
                : SingboxFmt.ResolveToCustomOutbound(candidate.ToJsonString(), null);
            try
            {
                if (parsed.Count != 1)
                {
                    throw new FormatException("Unrecognized required custom outbound.");
                }
                requiredCount++;
            }
            finally
            {
                foreach (var profile in parsed)
                {
                    File.Delete(profile.Address);
                }
            }
        }
        return requiredCount;
    }

    private static void ValidateMihomoDocument(string content)
    {
        var yaml = new YamlStream();
        yaml.Load(new StringReader(content));
        if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new FormatException("Invalid Mihomo configuration.");
        }

        var recognized = false;
        foreach (var field in root.Children)
        {
            var key = (field.Key as YamlScalarNode)?.Value;
            var valid = key switch
            {
                "proxies" or "proxy-groups" or "rules" or "listeners" => field.Value is YamlSequenceNode,
                "proxy-providers" or "rule-providers" or "dns" or "tun" or "hosts" => field.Value is YamlMappingNode,
                "mode" => field.Value is YamlScalarNode mode && mode.Value is "direct" or "global" or "rule",
                "port" or "socks-port" or "mixed-port" or "redir-port" or "tproxy-port"
                    => field.Value is YamlScalarNode port && int.TryParse(port.Value, out var number) && number is >= 0 and <= 65535,
                _ => (bool?)null,
            };
            if (valid == false)
            {
                throw new FormatException("Invalid Mihomo configuration structure.");
            }
            recognized |= valid == true;
        }
        if (!recognized)
        {
            throw new FormatException("Unrecognized Mihomo configuration structure.");
        }
    }

    private static async Task PreserveIdentities(List<ProfileItem> original, List<StagedProfile> staged)
    {
        var originalContent = new Dictionary<string, string?>();
        foreach (var profile in original.Where(profile => profile.ConfigType is EConfigType.Custom or EConfigType.Outbound))
        {
            var path = Utils.GetConfigPath(profile.Address);
            originalContent[profile.IndexId] = File.Exists(path) ? await File.ReadAllTextAsync(path) : null;
        }
        var usedIds = new HashSet<string>();
        var matchedProfiles = new HashSet<ProfileItem>();
        var effectiveFields = original.Concat(staged.Select(item => item.Profile)).ToDictionary(profile => profile, EffectiveFields);
        var idMap = new Dictionary<string, string>();
        var groups = staged.Where(item => item.Profile.ConfigType.IsGroupType()).ToList();
        var groupById = groups.ToDictionary(item => item.Profile.IndexId, item => item.Profile);
        var dependencies = groups.ToDictionary(item => item.Profile, item => Utils.String2List(item.Profile.GetProtocolExtra().ChildItems)
            .Where(groupById.ContainsKey).Select(id => groupById[id]).ToHashSet());
        var pending = groups.Select(item => item.Profile).ToHashSet();
        Match(staged.Where(item => !item.Profile.ConfigType.IsGroupType()).ToList());
        while (pending.Count > 0)
        {
            var ready = groups.Where(item => pending.Contains(item.Profile) && !dependencies[item.Profile].Any(pending.Contains)).ToList();
            if (ready.Count == 0)
            {
                // Cyclic group dependencies cannot establish an unambiguous identity.
                break;
            }
            RemapChildren();
            Match(ready);
            foreach (var item in ready)
            {
                pending.Remove(item.Profile);
            }
        }
        RemapChildren();

        void Match(List<StagedProfile> candidates)
        {
            foreach (var item in candidates)
            {
                var equivalent = original.Where(old => !usedIds.Contains(old.IndexId)
                    && Equivalent(old, item)).ToList();
                if (equivalent.Count == 1 && candidates.Count(candidate => Equivalent(equivalent[0], candidate)) == 1)
                {
                    Retain(item.Profile, equivalent[0]);
                }
            }
            foreach (var item in candidates.Where(item => !matchedProfiles.Contains(item.Profile)))
            {
                var profile = item.Profile;
                if (profile.Remarks.IsNullOrEmpty())
                {
                    continue;
                }
                var matches = original.Where(old => old.ConfigType == profile.ConfigType && old.Remarks == profile.Remarks).ToList();
                if (matches.Count == 1 && !usedIds.Contains(matches[0].IndexId)
                    && staged.Count(other => other.Profile.ConfigType == profile.ConfigType && other.Profile.Remarks == profile.Remarks) == 1)
                {
                    Retain(profile, matches[0]);
                }
            }
        }

        void Retain(ProfileItem profile, ProfileItem old)
        {
            idMap[profile.IndexId] = old.IndexId;
            profile.IndexId = old.IndexId;
            usedIds.Add(old.IndexId);
            matchedProfiles.Add(profile);
        }

        void RemapChildren()
        {
            foreach (var item in staged.Where(item => item.Profile.ConfigType.IsGroupType()))
            {
                var extra = item.Profile.GetProtocolExtra();
                if (extra.ChildItems.IsNotEmpty())
                {
                    item.Profile.SetProtocolExtra(extra with
                    {
                        ChildItems = Utils.List2String(Utils.String2List(extra.ChildItems).Select(id => idMap.GetValueOrDefault(id, id)).ToList()),
                    });
                    effectiveFields[item.Profile] = EffectiveFields(item.Profile);
                }
            }
        }

        bool Equivalent(ProfileItem old, StagedProfile incoming)
        {
            if (!JsonNode.DeepEquals(effectiveFields[old], effectiveFields[incoming.Profile]))
            {
                return false;
            }
            return incoming.Profile.ConfigType is not (EConfigType.Custom or EConfigType.Outbound)
                || originalContent.GetValueOrDefault(old.IndexId) is { } content && incoming.Content is { } incomingContent
                && ContentEquals(content, incomingContent);
        }
    }

    private static JsonNode EffectiveFields(ProfileItem profile)
    {
        var fields = JsonUtils.SerializeToNode(profile)!.AsObject();
        foreach (var name in new[] { nameof(ProfileItem.IndexId), nameof(ProfileItem.Subid), nameof(ProfileItem.IsSub), nameof(ProfileItem.Remarks), nameof(ProfileItem.ConfigVersion) })
        {
            fields.Remove(name);
        }
        // Ignore legacy fields without requiring reflection metadata in NativeAOT builds.
        foreach (var name in new[] { "HeaderType", "RequestHost", "Path", "Extra", "Ports", "AlterId", "Flow", "Id", "Security" })
        {
            fields.Remove(name);
        }
        if (profile.ConfigType is EConfigType.Custom or EConfigType.Outbound)
        {
            fields.Remove(nameof(ProfileItem.Address));
        }
        fields[nameof(ProfileItem.Network)] = profile.GetNetwork();
        fields[nameof(ProfileItem.ProtoExtra)] = JsonUtils.SerializeToNode(profile.GetProtocolExtra());
        fields[nameof(ProfileItem.TransportExtra)] = JsonUtils.SerializeToNode(profile.GetTransportExtra());
        NormalizeEmpty(fields);
        return fields;
    }

    private static void NormalizeEmpty(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToArray())
            {
                if (property.Value == null || property.Value is JsonValue value && value.TryGetValue<string>(out var text) && string.IsNullOrEmpty(text))
                {
                    obj.Remove(property.Key);
                }
                else
                {
                    NormalizeEmpty(property.Value);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array.OfType<JsonNode>())
            {
                NormalizeEmpty(item);
            }
        }
    }

    private static bool ContentEquals(string left, string right)
    {
        var leftJson = JsonUtils.ParseJson(left);
        var rightJson = JsonUtils.ParseJson(right);
        if (leftJson != null && rightJson != null)
        {
            return JsonNode.DeepEquals(leftJson, rightJson);
        }
        return left.Replace("\r\n", "\n", StringComparison.Ordinal).Trim() == right.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    }

    private static async Task<ImportResult> Commit(Config config, List<ProfileItem> original, List<StagedProfile> staged,
        string activeId, RefreshState state, long generation)
    {
        var createdFiles = new List<string>();
        var committed = false;
        try
        {
            foreach (var item in staged.Where(item => item.Content != null))
            {
                var old = original.FirstOrDefault(profile => profile.IndexId == item.Profile.IndexId);
                if (old != null && File.Exists(Utils.GetConfigPath(old.Address))
                    && ContentEquals(await File.ReadAllTextAsync(Utils.GetConfigPath(old.Address)), item.Content!))
                {
                    item.Profile.Address = old.Address;
                    continue;
                }
                var fileName = $"{Utils.GetGuid()}{item.Extension}";
                var path = Utils.GetConfigPath(fileName);
                createdFiles.Add(path);
                await File.WriteAllTextAsync(path, item.Content);
                item.Profile.Address = fileName;
            }
            var database = new SQLiteAsyncConnection(Utils.GetConfigPath("guiNDB.db"), false);
            await database.RunInTransactionAsync(connection =>
            {
                if (Volatile.Read(ref state.Generation) != generation)
                {
                    throw new OperationCanceledException("A newer subscription refresh superseded this import.");
                }
                foreach (var old in original)
                {
                    connection.Delete<ProfileItem>(old.IndexId);
                }
                foreach (var item in staged)
                {
                    connection.InsertOrReplace(item.Profile);
                }
            });
            committed = true;
            var retainedIds = staged.Select(item => item.Profile.IndexId).ToHashSet();
            var retainedFiles = (await AppManager.Instance.ProfileItems(string.Empty) ?? [])
                .Where(profile => profile.ConfigType is EConfigType.Custom or EConfigType.Outbound)
                .Select(profile => profile.Address).ToHashSet();
            foreach (var old in original.Where(profile => profile.ConfigType is EConfigType.Custom or EConfigType.Outbound))
            {
                if (!retainedFiles.Contains(old.Address))
                {
                    try
                    {
                        File.Delete(Utils.GetConfigPath(old.Address));
                    }
                    catch (Exception ex)
                    {
                        Logging.SaveLog("RemoveSubscriptionFile", ex);
                    }
                }
            }
            var maxSort = ProfileExManager.Instance.GetMaxSort();
            foreach (var item in staged.Where(item => original.All(old => old.IndexId != item.Profile.IndexId)))
            {
                ProfileExManager.Instance.SetSort(item.Profile.IndexId, ++maxSort);
            }
            var unavailable = config.IndexId == activeId && original.Any(old => old.IndexId == activeId) && !retainedIds.Contains(activeId);
            return new(staged.Count, unavailable ? activeId : null);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("CommitSubscription", ex);
            return new(-1);
        }
        finally
        {
            if (!committed)
            {
                foreach (var path in createdFiles)
                {
                    File.Delete(path);
                }
            }
        }
    }
}
