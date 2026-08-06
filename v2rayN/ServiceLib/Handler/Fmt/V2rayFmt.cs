namespace ServiceLib.Handler.Fmt;

public class V2rayFmt : BaseFmt
{
    public static List<ProfileItem> ResolveToCustom(string strData, string? subRemarks)
    {
        var jsonNode = JsonUtils.ParseJson(strData);
        return ResolveCommon(jsonNode, subRemarks, false);
    }

    public static List<ProfileItem> ResolveToCustomOutbound(string strData, string? subRemarks)
    {
        var jsonNode = JsonUtils.ParseJson(strData);
        return ResolveCommon(jsonNode, subRemarks, true);
    }

    private static List<ProfileItem> ResolveCommon(JsonNode? jsonNode, string? subRemarks, bool isOutbound)
    {
        if (jsonNode is JsonArray jsonArray)
        {
            return
            [
                .. jsonArray.Select(item => ResolveCommon(item, subRemarks, isOutbound))
                    .Where(list => list is { Count: > 0 })
                    .SelectMany(list => list),
            ];
        }
        if (jsonNode is not JsonObject jsonObject)
        {
            return [];
        }
        // Process the individual JSON object
        var profileList = new List<ProfileItem>();
        if (!isOutbound)
        {
            var fullProfile = ResolveFull(jsonObject, subRemarks);
            profileList.Add(fullProfile);
            if (fullProfile is not null)
            {
                return profileList;
            }
        }
        profileList.AddRange(ResolveFullToOutbound(jsonObject, subRemarks));
        if (profileList.Count != 0)
        {
            return profileList;
        }
        var outboundProfile = ResolveOutbound(jsonObject, subRemarks);
        if (outboundProfile is not null)
        {
            profileList.Add(outboundProfile);
        }
        return profileList;
    }

    private static ProfileItem? ResolveFull(JsonObject jsonObject, string? subRemarks)
    {
        if (jsonObject?["inbounds"] == null
            || jsonObject["outbounds"] == null)
        {
            return null;
        }

        if (jsonObject["outbounds"] is JsonArray outboundsArray)
        {
            if (!outboundsArray.Any(IsValidV2rayOutbound))
            {
                return null;
            }
        }
        else
        {
            return null;
        }

        var fileName = WriteAllText(JsonUtils.Serialize(jsonObject));

        var profileItem = new ProfileItem
        {
            ConfigType = EConfigType.Custom,
            CoreType = ECoreType.Xray,
            Address = fileName,
            Remarks = jsonObject["remarks"]?.ToString() ?? subRemarks ?? "v2ray_custom",
        };

        return profileItem;
    }

    public static List<ProfileItem> ResolveFullToOutbound(JsonObject jsonObject, string? subRemarks)
    {
        if (jsonObject["outbounds"] is not JsonArray outboundsArray)
        {
            return [];
        }
        List<ProfileItem> lstResult = [];
        foreach (var outbound in outboundsArray)
        {
            if (outbound is not JsonObject outboundObj)
            {
                continue;
            }
            var profileIt = ResolveOutbound(outboundObj, subRemarks);
            if (profileIt != null)
            {
                lstResult.Add(profileIt);
            }
        }
        return lstResult;
    }

    public static ProfileItem? ResolveOutbound(JsonObject jsonObject, string? subRemarks)
    {
        if (!IsValidV2rayOutbound(jsonObject))
        {
            return null;
        }
        var protocol = jsonObject["protocol"]?.ToString();
        if (protocol is null or "freedom" or "blackhole" or "dns" or "loopback")
        {
            return null;
        }
        var tag = jsonObject["tag"]?.ToString();
        var remarks = $"{protocol}_{tag}";
        var fileName = WriteAllText(JsonUtils.Serialize(jsonObject));
        var profileItem = new ProfileItem
        {
            ConfigType = EConfigType.Outbound,
            CoreType = ECoreType.Xray,
            Address = fileName,
            Remarks = remarks,
        };
        return profileItem;
    }

    private static bool IsValidV2rayOutbound(JsonNode? jsonNode)
    {
        if (jsonNode is not JsonObject jsonObject)
        {
            return false;
        }

        var matchedCounter = 0;
        if (string.IsNullOrEmpty(jsonObject["protocol"]?.ToString()))
        {
            return false;
        }
        matchedCounter += 1;
        if (!string.IsNullOrEmpty(jsonObject["settings"]?.ToString()))
        {
            matchedCounter += 1;
        }
        if (!string.IsNullOrEmpty(jsonObject["streamSettings"]?.ToString()))
        {
            matchedCounter += 1;
        }
        if (!string.IsNullOrEmpty(jsonObject["tag"]?.ToString()))
        {
            matchedCounter += 1;
        }
        if (!string.IsNullOrEmpty(jsonObject["mux"]?.ToString()))
        {
            matchedCounter += 1;
        }

        return matchedCounter >= 3;
    }
}
