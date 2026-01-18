namespace ServiceLib.Handler.Fmt;

public class SingboxFmt : BaseFmt
{
    public static List<ProfileItem>? ResolveFullArray(string strData, string? subRemarks)
    {
        var configObjects = JsonUtils.Deserialize<object[]>(strData);
        if (configObjects is not { Length: > 0 })
        {
            return null;
        }

        List<ProfileItem> lstResult = [];
        foreach (var configObject in configObjects)
        {
            var objectString = JsonUtils.Serialize(configObject);
            var profileIt = ResolveFull(objectString, subRemarks);
            if (profileIt is not null)
            {
                lstResult.Add(profileIt);
            }
        }
        return lstResult;
    }

    public static ProfileItem? ResolveFull(string strData, string? subRemarks)
    {
        var config = JsonUtils.ParseJson(strData);
        if (config?["inbounds"] is null
            || config["outbounds"] is null
            || config["route"] is null
            || config["dns"] is null)
        {
            return null;
        }

        var fileName = WriteAllText(strData);
        var profileItem = new ProfileItem
        {
            CoreType = ECoreType.sing_box,
            Address = fileName,
            Remarks = subRemarks ?? "singbox_custom"
        };

        return profileItem;
    }
}
