namespace ServiceLib.Handler.Fmt;

public class NaiveproxyFmt : BaseFmt
{
    public static ProfileItem? ResolveFull(string strData, string? subRemarks)
    {
        if (Contains(strData, "listen", "proxy", "<html>", "<body>"))
        {
            var fileName = WriteAllText(strData);

            var profileItem = new ProfileItem
            {
                CoreType = ECoreType.naiveproxy,
                Address = fileName,
                Remarks = subRemarks ?? "naiveproxy_custom"
            };
            return profileItem;
        }

        return null;
    }
}
