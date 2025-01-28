namespace ServiceLib.Models
{
	[Serializable]
	public class ProfileItemModel : ProfileItem
	{
		public bool IsActive { get; set; }
		public string SubRemarks { get; set; }
		public int Delay { get; set; }
		public decimal Speed { get; set; }
		public int Sort { get; set; }
		public string DelayVal { get; set; }
		public string SpeedVal { get; set; }
		public string TodayUp { get; set; }
		public string TodayDown { get; set; }
		public string TotalUp { get; set; }
		public string TotalDown { get; set; }
	}
}
