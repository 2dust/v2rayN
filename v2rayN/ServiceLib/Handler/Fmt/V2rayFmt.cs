namespace ServiceLib.Handler.Fmt
{
	public class V2rayFmt : BaseFmt
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
					var v2rayCon = JsonUtils.Deserialize<V2rayConfig>(objectString);
					if (v2rayCon?.inbounds?.Count > 0
						&& v2rayCon.outbounds?.Count > 0
						&& v2rayCon.routing != null)
					{
						var fileName = WriteAllText(objectString);

						var profileIt = new ProfileItem
						{
							CoreType = ECoreType.Xray,
							Address = fileName,
							Remarks = v2rayCon.remarks ?? subRemarks ?? "v2ray_custom",
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
			var v2rayConfig = JsonUtils.Deserialize<V2rayConfig>(strData);
			if (v2rayConfig?.inbounds?.Count > 0
				&& v2rayConfig.outbounds?.Count > 0
				&& v2rayConfig.routing != null)
			{
				var fileName = WriteAllText(strData);

				var profileItem = new ProfileItem
				{
					CoreType = ECoreType.Xray,
					Address = fileName,
					Remarks = v2rayConfig.remarks ?? subRemarks ?? "v2ray_custom"
				};

				return profileItem;
			}
			return null;
		}
	}
}
