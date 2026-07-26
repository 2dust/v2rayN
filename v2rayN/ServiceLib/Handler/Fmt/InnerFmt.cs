namespace ServiceLib.Handler.Fmt;

public class InnerFmt
{
    private static readonly Lazy<string> SessionSalt = new(() => Utils.GetGuid(false));

    public static List<ProfileItem>? Resolve(string strData, string subid)
    {
        var list = new List<ProfileItem>();
        // Overwrite externally imported indexIds to avoid possible sources of attacks
        var indexIdMap = new Dictionary<string, string>();
        using (var reader = new StringReader(strData))
        {
            while (reader.ReadLine() is { } line)
            {
                if (line.IsNullOrEmpty())
                {
                    continue;
                }
                var trimmedLine = line.Trim();
                if (!trimmedLine.StartsWith(Global.InnerUriProtocol, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var profileItem = ResolveSingle(trimmedLine);
                if (profileItem is null)
                {
                    continue;
                }
                if (profileItem.ConfigType == EConfigType.Custom)
                {
                    // Unsupported, also to avoid possible sources of attacks, skip it
                    continue;
                }
                // overwrite indexId
                var newIndexId = Utils.GetGuid(false);
                if (!profileItem.IndexId.IsNullOrEmpty())
                {
                    // Ignore duplicated indexId
                    indexIdMap[profileItem.IndexId] = newIndexId;
                }
                profileItem.IndexId = newIndexId;
                list.Add(profileItem);
            }
        }
        // For group-type profile items, also overwrite the ChildItems and ChildSubId
        var emptyGroupProfileList = new List<ProfileItem>();
        foreach (var item in list.Where(i => i.ConfigType.IsGroupType()))
        {
            var protocolExtra = item.GetProtocolExtra();
            // Only allow "self" as a special value for SubChildItems to avoid possible sources of attacks,
            // which means it will be replaced with the subid, otherwise set it to null
            //if (!protocolExtra.SubChildItems.IsNullOrEmpty())
            if (protocolExtra.SubChildItems == "self")
            {
                protocolExtra = protocolExtra with
                {
                    SubChildItems = subid
                };
            }
            else
            {
                protocolExtra = protocolExtra with
                {
                    SubChildItems = null
                };
            }
            if (Utils.String2List(protocolExtra.ChildItems) is { Count: > 0 } childIndexIds)
            {
                var newChildIndexIds = childIndexIds
                    .Select(id => indexIdMap.GetValueOrDefault(id, null))
                    .Where(id => !id.IsNullOrEmpty())
                    .ToList();
                protocolExtra = protocolExtra with
                {
                    ChildItems = Utils.List2String(newChildIndexIds)
                };
            }
            else
            {
                protocolExtra = protocolExtra with
                {
                    ChildItems = null
                };
            }
            item.SetProtocolExtra(protocolExtra);
            if (protocolExtra.SubChildItems.IsNullOrEmpty()
                && protocolExtra.ChildItems.IsNullOrEmpty())
            {
                emptyGroupProfileList.Add(item);
            }
        }
        // Remove empty group profile items
        list.RemoveAll(emptyGroupProfileList.Contains);
        return list;
    }

    public static string? ToUri(List<ProfileItem> items)
    {
        var sb = new StringBuilder();
        foreach (var item in items)
        {
            if (item.ConfigType == EConfigType.Custom)
            {
                continue;
            }
            var itemClone = JsonUtils.DeepCopy(item);
            if (itemClone is null)
            {
                continue;
            }
            // overwrite indexId
            var originalIndexId = itemClone.IndexId;
            var newIndexId = GetReproducibleExportId(originalIndexId);
            itemClone.IndexId = newIndexId;
            if (itemClone.ConfigType.IsGroupType())
            {
                var protocolExtra = itemClone.GetProtocolExtra();
                if (!protocolExtra.SubChildItems.IsNullOrEmpty())
                {
                    protocolExtra = protocolExtra with
                    {
                        SubChildItems = "self"
                    };
                }
                if (Utils.String2List(protocolExtra.ChildItems) is { Count: > 0 } childIndexIds)
                {
                    var newChildIndexIds = childIndexIds
                        .Select(GetReproducibleExportId)
                        .Where(id => !id.IsNullOrEmpty())
                        .ToList();
                    protocolExtra = protocolExtra with
                    {
                        ChildItems = Utils.List2String(newChildIndexIds)
                    };
                }
                itemClone.SetProtocolExtra(protocolExtra);
            }
            var uri = ToUriSingle(itemClone);
            if (!uri.IsNullOrEmpty())
            {
                sb.AppendLine(uri);
            }
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }

    private static ProfileItem? ResolveSingle(string str)
    {
        var profileItem = ResolveSingle4Path(str);
        profileItem ??= ResolveSingle4Query(str);
        if (profileItem is null)
        {
            return null;
        }
        if (profileItem.ConfigVersion != 4)
        {
            return null;
        }
        // Check Enum.IsDefined
        if (!Enum.IsDefined(profileItem.ConfigType))
        {
            return null;
        }
        if (profileItem.CoreType is not (null or ECoreType.Xray or ECoreType.sing_box))
        {
            return null;
        }
        var protocolExtra = profileItem.GetProtocolExtra();
        var multipleLoad = protocolExtra.MultipleLoad;
        if (multipleLoad is not null && !Enum.IsDefined(typeof(EMultipleLoad), multipleLoad))
        {
            return null;
        }
        return profileItem;
    }

    private static ProfileItem? ResolveSingle4Path(string str)
    {
        // format: v2rayn://vless/{url-safe base64 encoded_string}
        var parsedUri = Utils.TryUri(str);
        if (parsedUri is null)
        {
            return null;
        }
        var segment = parsedUri.AbsolutePath.TrimStart('/');
        var decodedResult = Utils.Base64Decode(segment);
        var jsonNode = JsonUtils.ParseJson(decodedResult);
        if (jsonNode is not JsonObject jsonObj)
        {
            return null;
        }
        // flatten
        // move jsonObj.ProtoExtraObj to jsonObj.ProtoExtra (string)
        // move jsonObj.TransportExtraObj to jsonObj.TransportExtra (string)
        if (jsonObj.TryGetPropertyValue("ProtoExtraObj", out var protoExtraNode)
            && protoExtraNode is JsonObject protoExtraObj)
        {
            jsonObj["ProtoExtra"] = JsonUtils.Serialize(protoExtraObj, false);
            jsonObj.Remove("ProtoExtraObj");
        }
        if (jsonObj.TryGetPropertyValue("TransportExtraObj", out var transportExtraNode)
            && transportExtraNode is JsonObject transportExtraObj)
        {
            jsonObj["TransportExtra"] = JsonUtils.Serialize(transportExtraObj, false);
            jsonObj.Remove("TransportExtraObj");
        }
        var profileItem = JsonUtils.Deserialize<ProfileItem>(JsonUtils.Serialize(jsonObj, false));
        return profileItem;
    }

    private static ProfileItem? ResolveSingle4Query(string str)
    {
        var parsedUri = Utils.TryUri(str);
        if (parsedUri is null)
        {
            return null;
        }
        var query = Utils.ParseQueryString(parsedUri.Query);
        var jsonObj = new JsonObject();
        var protoExtraObj = new JsonObject();
        var transportExtraObj = new JsonObject();
        var keyObjMap = new Dictionary<string, JsonObject>
        {
            { "ProtoExtra.", protoExtraObj },
            { "TransportExtra.", transportExtraObj },
        };
        var boolKeyList = new HashSet<string>
        {
            "DisplayLog",
            "IsSub",
            "MuxEnabled",
            "ProtoExtra.Uot",
            "ProtoExtra.NaiveQuic",
        };
        var intKeyList = new HashSet<string>
        {
            "ConfigVersion",
            "ConfigType",
            "CoreType",
            "PreSocksPort",
            "Port",
            "AlterId",
            "ProtoExtra.WgMtu",
            "ProtoExtra.UpMbps",
            "ProtoExtra.DownMbps",
            "ProtoExtra.InsecureConcurrency",
            "ProtoExtra.MultipleLoad",
            "TransportExtra.KcpMtu",
        };
        foreach (var key in query.AllKeys)
        {
            if (key is null)
            {
                continue;
            }
            var value = query[key];
            if (value is null)
            {
                continue;
            }
            var valueToUse = boolKeyList.Contains(key)
                ? JsonValue.Create(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "1", "true", "yes" }.Contains(value))
                : intKeyList.Contains(key)
                    ? int.TryParse(value, out var intValue) ? JsonValue.Create(intValue) : JsonValue.Create(0)
                    : JsonValue.Create(value);
            var jsonObjToUse = keyObjMap.FirstOrDefault(kvp => key.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase)).Value ?? jsonObj;
            if (jsonObjToUse == protoExtraObj || jsonObjToUse == transportExtraObj)
            {
                var subKey = key.Substring(jsonObjToUse == protoExtraObj ? "ProtoExtra.".Length : "TransportExtra.".Length);
                jsonObjToUse[subKey] = valueToUse;
            }
            else
            {
                jsonObj[key] = valueToUse;
            }
        }
        jsonObj.Remove("ProtoExtraObj");
        jsonObj.Remove("TransportExtraObj");
        jsonObj["ProtoExtra"] = JsonUtils.Serialize(protoExtraObj, false);
        jsonObj["TransportExtra"] = JsonUtils.Serialize(transportExtraObj, false);
        var profileItem = JsonUtils.Deserialize<ProfileItem>(JsonUtils.Serialize(jsonObj, false));
        return profileItem;
    }

    private static string? ToUriSingle(ProfileItem item)
    {
        if (true)
        {
            return ToUriSingle4Query(item);
        }
        var jsonNode = JsonUtils.ParseJson(JsonUtils.Serialize(item, false));
        if (jsonNode is not JsonObject jsonObj)
        {
            return null;
        }
        // unflatten
        // move jsonObj.ProtoExtra (string) to jsonObj.ProtoExtraObj
        // move jsonObj.TransportExtra (string) to jsonObj.TransportExtraObj
        if (jsonObj.TryGetPropertyValue("ProtoExtra", out var protoExtraNode)
            && protoExtraNode is JsonValue protoExtraValue
            && protoExtraValue.TryGetValue<string>(out var protoExtraStr)
            && !protoExtraStr.IsNullOrEmpty()
            && JsonUtils.ParseJson(protoExtraStr) is JsonObject protoExtraObj)
        {
            jsonObj["ProtoExtraObj"] = protoExtraObj;
            jsonObj.Remove("ProtoExtra");
        }
        if (jsonObj.TryGetPropertyValue("TransportExtra", out var transportExtraNode)
            && transportExtraNode is JsonValue transportExtraValue
            && transportExtraValue.TryGetValue<string>(out var transportExtraStr)
            && !transportExtraStr.IsNullOrEmpty()
            && JsonUtils.ParseJson(transportExtraStr) is JsonObject transportExtraObj)
        {
            jsonObj["TransportExtraObj"] = transportExtraObj;
            jsonObj.Remove("TransportExtra");
        }
        // remove subid and isSub
        jsonObj.Remove("Subid");
        jsonObj.Remove("IsSub");
        // Remove empty properties to reduce the length of the exported string
        RemoveEmptyJson(jsonObj);
        var jsonStr = JsonUtils.Serialize(jsonObj, false);
        var encodedStr = Utils.Base64Encode(jsonStr).Replace('+', '-').Replace('/', '_').Replace("=", "");
        return $"{Global.InnerUriProtocol}{item.ConfigType.ToString().ToLower()}/{encodedStr}";
    }

    private static string? ToUriSingle4Query(ProfileItem item)
    {
        var jsonNode = JsonUtils.ParseJson(JsonUtils.Serialize(item, false));
        if (jsonNode is not JsonObject jsonObj)
        {
            return null;
        }
        // unflatten
        // move jsonObj.ProtoExtra (string) to jsonObj.ProtoExtraObj
        // move jsonObj.TransportExtra (string) to jsonObj.TransportExtraObj
        if (jsonObj.TryGetPropertyValue("ProtoExtra", out var protoExtraNode)
            && protoExtraNode is JsonValue protoExtraValue
            && protoExtraValue.TryGetValue<string>(out var protoExtraStr)
            && !protoExtraStr.IsNullOrEmpty()
            && JsonUtils.ParseJson(protoExtraStr) is JsonObject protoExtraObj)
        {
            jsonObj["ProtoExtraObj"] = protoExtraObj;
            jsonObj.Remove("ProtoExtra");
        }
        if (jsonObj.TryGetPropertyValue("TransportExtra", out var transportExtraNode)
            && transportExtraNode is JsonValue transportExtraValue
            && transportExtraValue.TryGetValue<string>(out var transportExtraStr)
            && !transportExtraStr.IsNullOrEmpty()
            && JsonUtils.ParseJson(transportExtraStr) is JsonObject transportExtraObj)
        {
            jsonObj["TransportExtraObj"] = transportExtraObj;
            jsonObj.Remove("TransportExtra");
        }
        // remove subid and isSub
        jsonObj.Remove("Subid");
        jsonObj.Remove("IsSub");
        // Remove empty properties to reduce the length of the exported string
        RemoveEmptyJson(jsonObj);
        var dicQuery = new Dictionary<string, string>();
        foreach (var property in jsonObj)
        {
            if (property.Value is JsonValue valueNode && valueNode.GetValueKind() is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                dicQuery[property.Key] = Utils.UrlEncode(valueNode.ToString());
            }
            else if (property.Value is JsonObject objNode)
            {
                var key = property.Key + ".";
                foreach (var subProperty in objNode)
                {
                    if (subProperty.Value is JsonValue subValueNode && subValueNode.TryGetValue<string>(out var subValue))
                    {
                        dicQuery[key + subProperty.Key] = Utils.UrlEncode(subValue);
                    }
                }
            }
        }
        var queryBuilder = new StringBuilder();
        queryBuilder.Append('?');
        foreach (var kv in dicQuery)
        {
            queryBuilder.Append(kv.Key);
            queryBuilder.Append('=');
            queryBuilder.Append(kv.Value);
            queryBuilder.Append('&');
        }
        var query = queryBuilder.ToString().TrimEnd('&');
        return $"{Global.InnerUriProtocol}{item.ConfigType.ToString().ToLower()}{query}";
    }

    private static string GetReproducibleExportId(string originalIndexId)
    {
        if (originalIndexId.IsNullOrEmpty())
        {
            return originalIndexId;
        }

        var hash = HashCode.Combine(SessionSalt.Value, originalIndexId) & 0x7FFFFFFF;
        var bytes = BitConverter.GetBytes(hash);
        return Convert.ToBase64String(bytes).Replace("=", "");
    }

    private static void RemoveEmptyJson(JsonNode? node)
    {
        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (node is JsonObject jsonObject)
        {
            var propertiesToRemove = new List<string>();

            foreach (var property in jsonObject)
            {
                RemoveEmptyJson(property.Value);

                if (IsEmpty(property.Value))
                {
                    propertiesToRemove.Add(property.Key);
                }
            }

            foreach (var key in propertiesToRemove)
            {
                jsonObject.Remove(key);
            }
        }
        else if (node is JsonArray jsonArray)
        {
            for (var i = jsonArray.Count - 1; i >= 0; i--)
            {
                RemoveEmptyJson(jsonArray[i]);

                if (IsEmpty(jsonArray[i]))
                {
                    jsonArray.RemoveAt(i);
                }
            }
        }
    }

    private static bool IsEmpty(JsonNode? node)
    {
        return node switch
        {
            null => true,
            JsonValue value when value.TryGetValue<string>(out var str) => string.IsNullOrEmpty(str),
            JsonObject obj => obj.Count == 0,
            JsonArray arr => arr.Count == 0,
            _ => false
        };
    }
}
