namespace ServiceLib.Handler.Fmt;

public class ClashFmt : BaseFmt
{
    public static ProfileItem? ResolveFull(string strData, string? subRemarks)
    {
        if (Contains(strData, "rules", "-port", "proxies"))
        {
            var fileName = WriteAllText(strData, "yaml");

            var profileItem = new ProfileItem
            {
                CoreType = ECoreType.mihomo,
                Address = fileName,
                Remarks = subRemarks ?? "clash_custom"
            };
            return profileItem;
        }

        return null;
    }
}
