using v2rayN.Enums;
using v2rayN.Models;

namespace v2rayN.Handler.Fmt
{
    internal class ClashFmt : BaseFmt
    {
        public static ProfileItem? ResolveFull(string strData, string? subRemarks)
        {
            if (Contains(strData, "port", "socks-port", "proxies"))
            {
                var fileName = WriteAllText(strData, "yaml");

                var profileItem = new ProfileItem
                {
                    coreType = ECoreType.mihomo,
                    address = fileName,
                    remarks = subRemarks ?? "clash_custom"
                };
                return profileItem;
            }

            return null;
        }
    }
}