﻿using v2rayN.Enums;
using v2rayN.Models;

namespace v2rayN.Handler.Fmt
{
    internal class SingboxFmt : BaseFmt
    {
        public static List<ProfileItem>? ResolveFullArray(string strData, string? subRemarks)
        {
            var configObjects = JsonUtils.Deserialize<Object[]>(strData);
            if (configObjects != null && configObjects.Length > 0)
            {
                List<ProfileItem> lstResult = [];
                foreach (var configObject in configObjects)
                {
                    var objectString = JsonUtils.Serialize(configObject);
                    var singboxCon = JsonUtils.Deserialize<SingboxConfig>(objectString);
                    if (singboxCon?.inbounds?.Count > 0
                        && singboxCon.outbounds?.Count > 0
                        && singboxCon.route != null)
                    {
                        var fileName = WriteAllText(objectString);

                        var profileIt = new ProfileItem
                        {
                            coreType = ECoreType.sing_box,
                            address = fileName,
                            remarks = subRemarks ?? "singbox_custom",
                        };
                        lstResult.Add(profileIt);
                    }
                }
                return lstResult;
            }
            return null;
        }

        public static ProfileItem? ResolveFull(string strData, string? subRemarks)
        {
            var singboxConfig = JsonUtils.Deserialize<SingboxConfig>(strData);
            if (singboxConfig?.inbounds?.Count > 0
                && singboxConfig.outbounds?.Count > 0
                && singboxConfig.route != null)
            {
                var fileName = WriteAllText(strData);
                var profileItem = new ProfileItem
                {
                    coreType = ECoreType.sing_box,
                    address = fileName,
                    remarks = subRemarks ?? "singbox_custom"
                };

                return profileItem;
            }
            return null;
        }
    }
}