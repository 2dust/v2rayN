namespace ServiceLib.Models
{
	[Serializable]
	public class SpeedTestResult
	{
		public string? IndexId { get; set; }

		public string? Delay { get; set; }

		public string? Speed { get; set; }
	}
}
