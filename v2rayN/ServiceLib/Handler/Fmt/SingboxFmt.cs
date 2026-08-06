namespace ServiceLib.Handler.Fmt;

public class SingboxFmt : BaseFmt
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
            if (!outboundsArray.Any(IsValidSingboxOutbound))
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
            CoreType = ECoreType.sing_box,
            Address = fileName,
            Remarks = subRemarks ?? "singbox_custom",
        };

        return profileItem;
    }

    private static List<ProfileItem> ResolveFullToOutbound(JsonObject jsonObject, string? subRemarks)
    {
        if (jsonObject?["outbounds"] is not JsonArray outboundsArray)
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

    private static ProfileItem? ResolveOutbound(JsonObject jsonObject, string? subRemarks)
    {
        if (!IsValidSingboxOutbound(jsonObject))
        {
            return null;
        }
        var type = jsonObject["type"]?.ToString();
        if (type is null or "direct" or "block" or "dns" or "selector" or "urltest")
        {
            return null;
        }
        var tag = jsonObject["tag"]?.ToString();
        var remarks = $"{type}_{tag}";
        var fileName = WriteAllText(JsonUtils.Serialize(jsonObject));
        var profileItem = new ProfileItem
        {
            ConfigType = EConfigType.Outbound,
            CoreType = ECoreType.sing_box,
            Address = fileName,
            Remarks = remarks,
        };
        return profileItem;
    }

    private static bool IsValidSingboxOutbound(JsonNode? jsonNode)
    {
        if (jsonNode is not JsonObject jsonObject)
        {
            return false;
        }
        var matchedCounter = 0;
        if (string.IsNullOrEmpty(jsonObject["type"]?.ToString()))
        {
            return false;
        }
        matchedCounter += 1;
        if (!string.IsNullOrEmpty(jsonObject["tag"]?.ToString()))
        {
            matchedCounter += 1;
        }
        if (!string.IsNullOrEmpty(jsonObject["server"]?.ToString()))
        {
            matchedCounter += 1;
        }
        if (!string.IsNullOrEmpty(jsonObject["server_port"]?.ToString()))
        {
            matchedCounter += 1;
        }
        if (!string.IsNullOrEmpty(jsonObject["tls"]?.ToString()))
        {
            matchedCounter += 1;
        }
        return matchedCounter >= 2;
    }
}
