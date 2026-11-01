namespace ServiceLib.Models.CoreConfigs;

/// <summary>A complete, detached launch candidate. Preparation never writes the live core files.</summary>
public sealed class PreparedCoreConfigBundle
{
    internal CoreConfigContext MainContext { get; }
    internal CoreConfigContext? PreContext { get; }
    public string MainContent { get; }
    public string? PreContent { get; }
    private readonly string _mainLaunch;
    private readonly string? _preLaunch;
    private readonly bool _mainNative;
    private readonly bool _preNative;

    private PreparedCoreConfigBundle(CoreConfigContext main, string mainContent, CoreConfigContext? pre, string? preContent)
    {
        MainContext = main;
        MainContent = mainContent;
        PreContext = pre;
        PreContent = preContent;
        _mainLaunch = LaunchIdentity(main);
        _preLaunch = pre == null ? null : LaunchIdentity(pre);
        _mainNative = IsNative(main);
        _preNative = pre != null && IsNative(pre);
    }

    public static async Task<PreparedCoreConfigBundle> CreateAsync(CoreConfigContext main, CoreConfigContext? pre)
    {
        // The generators keep per-build maps and may normalize values. Never mutate caller/live state.
        var mainSnapshot = Snapshot(main);
        var preSnapshot = pre == null ? null : Snapshot(pre);
        var mainContent = await Generate(mainSnapshot);
        var preContent = preSnapshot == null ? null : await Generate(preSnapshot);
        return new(mainSnapshot, mainContent, preSnapshot, preContent);
    }

    public bool IsEquivalentTo(PreparedCoreConfigBundle? other) => CompareTo(other) == ECoreConfigChange.Unchanged;

    public ECoreConfigChange CompareTo(PreparedCoreConfigBundle? other)
    {
        if (other == null)
            return ECoreConfigChange.Unknown;
        var main = CompareContent(MainContent, other.MainContent, _mainNative && other._mainNative, MainContext.RunCoreType);
        var pre = CompareContent(PreContent, other.PreContent, _preNative && other._preNative, PreContext?.RunCoreType);
        if (main == ECoreConfigChange.Unknown || pre == ECoreConfigChange.Unknown)
            return ECoreConfigChange.Unknown;
        return _mainLaunch != other._mainLaunch || _preLaunch != other._preLaunch
            || main == ECoreConfigChange.Changed || pre == ECoreConfigChange.Changed
            ? ECoreConfigChange.Changed : ECoreConfigChange.Unchanged;
    }

    private static bool IsNative(CoreConfigContext context) =>
        context.Node.ConfigType is not (EConfigType.Custom or EConfigType.Outbound)
        && context.FullConfigTemplate?.Enabled != true && context.CustomOutboundContent.Count == 0
        && context.AllProxiesMap.Values.All(node => node.ConfigType is not (EConfigType.Custom or EConfigType.Outbound));

    internal static string CaptureConfigState(Config config)
    {
        var state = JsonUtils.ParseJson(JsonUtils.Serialize(config, false)) as JsonObject
            ?? throw new InvalidOperationException(ResUI.CheckServerSettings);
        foreach (var key in state.Select(item => item.Key).Where(key =>
                     key.Equals(nameof(Config.SubIndexId), StringComparison.OrdinalIgnoreCase)
                     || key.Equals(nameof(Config.UiItem), StringComparison.OrdinalIgnoreCase)).ToList())
            state.Remove(key);
        return state.ToJsonString();
    }

    internal void CheckExecutables()
    {
        foreach (var context in new[] { MainContext, PreContext }.OfType<CoreConfigContext>())
        {
            var info = CoreInfoManager.Instance.GetCoreInfo(context.RunCoreType);
            if (info == null)
                throw new InvalidOperationException(ResUI.CheckServerSettings);
            if (CoreInfoManager.Instance.GetCoreExecFile(info, out var message).IsNullOrEmpty())
                throw new InvalidOperationException(message);
        }
    }

    internal async Task<PreparedCoreConfigFiles> StageAsync()
    {
        var files = new PreparedCoreConfigFiles(PreContent != null);
        try
        {
            await File.WriteAllTextAsync(files.MainPath, MainContent);
            if (files.PrePath != null)
                await File.WriteAllTextAsync(files.PrePath, PreContent);
            return files;
        }
        catch
        {
            files.Dispose();
            throw;
        }
    }

    private static CoreConfigContext Snapshot(CoreConfigContext context) =>
        JsonUtils.DeepCopy(context with { CustomOutboundMap = [] })
        ?? throw new InvalidOperationException(ResUI.FailedGenDefaultConfiguration);

    private static async Task<string> Generate(CoreConfigContext context)
    {
        var result = await CoreConfigHandler.GenerateClientConfig(context, null);
        if (!result.Success || result.Data == null)
        {
            throw new InvalidOperationException(result.Msg ?? ResUI.FailedGenDefaultConfiguration);
        }
        return result.Data.ToString()!;
    }

    private static string LaunchIdentity(CoreConfigContext context)
    {
        var info = CoreInfoManager.Instance.GetCoreInfo(context.RunCoreType);
        var executable = info?.CoreExes.Select(name => Utils.GetBinPath(Utils.GetExeName(name), context.RunCoreType.ToString()))
            .FirstOrDefault(File.Exists);
        var file = executable == null ? null : new FileInfo(executable);
        return JsonUtils.Serialize(new
        {
            context.RunCoreType,
            context.IsTunEnabled,
            info?.Arguments,
            info?.AbsolutePath,
            info?.Environment,
            Executable = executable,
            Length = file?.Length,
            LastWrite = file?.LastWriteTimeUtc.Ticks
        }, false);
    }

    private static ECoreConfigChange CompareContent(string? left, string? right, bool generated, ECoreType? coreType)
    {
        if (left == right)
            return ECoreConfigChange.Unchanged;
        if (left == null || right == null)
            return ECoreConfigChange.Changed;
        var leftJson = JsonUtils.ParseJson(left);
        var rightJson = JsonUtils.ParseJson(right);
        if (leftJson == null || rightJson == null)
            return ECoreConfigChange.Unknown;
        if (generated)
        {
            NormalizeGeneratedLogDate(leftJson);
            NormalizeGeneratedLogDate(rightJson);
            NormalizeNativeTags(leftJson, coreType);
            NormalizeNativeTags(rightJson, coreType);
        }
        // Object property order/whitespace are irrelevant. Array and rule order remain significant.
        return JsonNode.DeepEquals(leftJson, rightJson) ? ECoreConfigChange.Unchanged : ECoreConfigChange.Changed;
    }

    private static void NormalizeNativeTags(JsonNode content, ECoreType? coreType)
    {
        // Only wholly native output is eligible: template/custom tags are user-owned. This is an
        // alpha-renaming of internal labels and schema-defined references; payload strings stay literal.
        var outbounds = ((content["outbounds"] as JsonArray) ?? []).Concat((content["endpoints"] as JsonArray) ?? []).ToList();
        var originalTags = outbounds.Select(node => node?["tag"]?.GetValue<string>() ?? string.Empty).ToList();
        if (originalTags.Distinct(StringComparer.Ordinal).Count() != originalTags.Count)
            return;
        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < originalTags.Count; i++)
            if (originalTags[i].IsNotEmpty() && !Global.OutboundTags.Contains(originalTags[i]))
                tags[originalTags[i]] = $"@outbound:{i}";
        var balancers = content["routing"]?["balancers"] as JsonArray;
        if (balancers != null)
            for (var i = 0; i < balancers.Count; i++)
                if (balancers[i]?["tag"] is JsonValue value && value.TryGetValue<string>(out var tag))
                {
                    if (tags.ContainsKey(tag))
                        return;
                    tags[tag] = $"@balancer:{i}";
                }

        foreach (var outbound in outbounds)
        {
            Replace(outbound, "tag");
            if (coreType == ECoreType.sing_box)
            {
                Replace(outbound, "detour");
                Replace(outbound, "default");
                ReplaceArray(outbound?["outbounds"] as JsonArray, false);
            }
            else
            {
                Replace(outbound?["proxySettings"], "tag");
                ReplaceRecursive(outbound?["streamSettings"], "dialerProxy");
            }
        }
        if (coreType == ECoreType.sing_box)
        {
            Replace(content["route"], "final");
            ReplaceRecursive(content["route"]?["rules"], "outbound");
            foreach (var server in (content["dns"]?["servers"] as JsonArray) ?? [])
                Replace(server, "detour");
            foreach (var client in (content["http_clients"] as JsonArray) ?? [])
                Replace(client, "detour");
        }
        else
        {
            foreach (var rule in (content["routing"]?["rules"] as JsonArray) ?? [])
            {
                Replace(rule, "outboundTag");
                Replace(rule, "balancerTag");
            }
            foreach (var balancer in balancers ?? [])
            {
                Replace(balancer, "tag");
                ReplaceArray(balancer?["selector"] as JsonArray, true);
            }
            ReplaceArray(content["observatory"]?["subjectSelector"] as JsonArray, true);
            ReplaceArray(content["burstObservatory"]?["subjectSelector"] as JsonArray, true);
        }
        return;

        void Replace(JsonNode? node, string key)
        {
            if (node is JsonObject obj && obj[key] is JsonValue value && value.TryGetValue<string>(out var text)
                && tags.TryGetValue(text, out var replacement))
                obj[key] = replacement;
        }

        void ReplaceArray(JsonArray? array, bool prefix)
        {
            if (array == null)
                return;
            for (var i = 0; i < array.Count; i++)
                if (array[i] is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    if (prefix)
                    {
                        var matches = originalTags.Select((tag, index) => (tag, index))
                            .Where(item => item.tag.StartsWith(text, StringComparison.Ordinal)).Select(item => item.index).ToArray();
                        if (matches.Length > 0)
                            array[i] = $"@selector:{string.Join(',', matches)}";
                    }
                    else if (tags.TryGetValue(text, out var replacement))
                        array[i] = replacement;
                }
        }

        void ReplaceRecursive(JsonNode? node, string key)
        {
            if (node is JsonObject obj)
            {
                Replace(obj, key);
                foreach (var value in obj.ToList())
                    ReplaceRecursive(value.Value, key);
            }
            else if (node is JsonArray array)
                foreach (var value in array)
                    ReplaceRecursive(value, key);
        }
    }

    private static void NormalizeGeneratedLogDate(JsonNode content)
    {
        if (content["log"] is not JsonObject log)
            return;
        foreach (var key in new[] { "access", "error", "output" })
        {
            if (log[key] is JsonValue value && value.TryGetValue<string>(out var path))
            {
                log[key] = Regex.Replace(path, @"(?<=(?:Vaccess_|Verror_|sbox_))\d{4}-\d{2}-\d{2}(?=\.txt$)", "date");
            }
        }
    }
}

internal sealed class PreparedCoreConfigFiles(bool hasPre) : IDisposable
{
    public string MainPath { get; } = Utils.GetBinConfigPath($".next-{Guid.NewGuid():N}-{Global.CoreConfigFileName}");
    public string? PrePath { get; } = hasPre ? Utils.GetBinConfigPath($".next-{Guid.NewGuid():N}-{Global.CorePreConfigFileName}") : null;

    public void Commit()
    {
        File.Move(MainPath, Utils.GetBinConfigPath(Global.CoreConfigFileName), true);
        if (PrePath != null)
            File.Move(PrePath, Utils.GetBinConfigPath(Global.CorePreConfigFileName), true);
    }

    public void Dispose()
    {
        foreach (var path in new[] { MainPath, PrePath }.OfType<string>())
            try
            { File.Delete(path); }
            catch (Exception ex) { Logging.SaveLog("CoreConfigStagingCleanup", ex); }
    }
}
