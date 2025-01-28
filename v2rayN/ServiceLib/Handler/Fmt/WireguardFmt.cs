namespace ServiceLib.Handler.Fmt
{
	public class WireguardFmt : BaseFmt
	{
		public static ProfileItem? Resolve(string str, out string msg)
		{
			msg = ResUI.ConfigurationFormatIncorrect;

			ProfileItem item = new()
			{
				ConfigType = EConfigType.WireGuard
			};

			var url = Utils.TryUri(str);
			if (url == null)
				return null;

			item.Address = url.IdnHost;
			item.Port = url.Port;
			item.Remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
			item.Id = Utils.UrlDecode(url.UserInfo);

			var query = Utils.ParseQueryString(url.Query);

			item.PublicKey = Utils.UrlDecode(query["publickey"] ?? "");
			item.Path = Utils.UrlDecode(query["reserved"] ?? "");
			item.RequestHost = Utils.UrlDecode(query["address"] ?? "");
			item.ShortId = Utils.UrlDecode(query["mtu"] ?? "");

			return item;
		}

		public static string? ToUri(ProfileItem? item)
		{
			if (item == null)
				return null;
			string url = string.Empty;

			string remark = string.Empty;
			if (Utils.IsNotEmpty(item.Remarks))
			{
				remark = "#" + Utils.UrlEncode(item.Remarks);
			}

			var dicQuery = new Dictionary<string, string>();
			if (Utils.IsNotEmpty(item.PublicKey))
			{
				dicQuery.Add("publickey", Utils.UrlEncode(item.PublicKey));
			}
			if (Utils.IsNotEmpty(item.Path))
			{
				dicQuery.Add("reserved", Utils.UrlEncode(item.Path));
			}
			if (Utils.IsNotEmpty(item.RequestHost))
			{
				dicQuery.Add("address", Utils.UrlEncode(item.RequestHost));
			}
			if (Utils.IsNotEmpty(item.ShortId))
			{
				dicQuery.Add("mtu", Utils.UrlEncode(item.ShortId));
			}
			return ToUri(EConfigType.WireGuard, item.Address, item.Port, item.Id, dicQuery, remark);
		}
	}
}
