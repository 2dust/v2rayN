using SQLite;

namespace ServiceLib.Models
{
	[Serializable]
	public class ProfileExItem
	{
		[PrimaryKey]
		public string IndexId { get; set; }

		public int Delay { get; set; }
		public decimal Speed { get; set; }
		public int Sort { get; set; }
	}
}
