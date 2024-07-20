using v2rayN.Enums;
using v2rayN.Models;

namespace v2rayN.Handler.Fmt
{
    internal class NaiveproxyFmt : BaseFmt
    {
        public static ProfileItem? ResolveFull(string strData, string? subRemarks)
        {
            if (Contains(strData, "listen", "proxy", "<html>", "<body>"))
            {
                var fileName = WriteAllText(strData);

                var profileItem = new ProfileItem
                {
                    coreType = ECoreType.naiveproxy,
                    address = fileName,
                    remarks = subRemarks ?? "naiveproxy_custom"
                };
                return profileItem;
            }

            return null;
        }
    }
}