namespace ServiceLib.Models
{
	[Serializable]
	public class ClashProxyModel
	{
		public string? Name { get; set; }

		public string? Type { get; set; }

		public string? Now { get; set; }

		public int Delay { get; set; }

		public string? DelayName { get; set; }

		public bool IsActive { get; set; }
	}
}
