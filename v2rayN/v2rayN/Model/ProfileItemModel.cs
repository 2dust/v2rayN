namespace v2rayN.Model
{
    [Serializable]
    public class ProfileItemModel : ProfileItem
    {
        public bool isActive { get; set; }
        public string subRemarks { get; set; }
        public int delay { get; set; }
        public decimal speed { get; set; }
        public int sort { get; set; }
        public string delayVal { get; set; }
        public string speedVal { get; set; }
        public string todayUp { get; set; }
        public string todayDown { get; set; }
        public string totalUp { get; set; }
        public string totalDown { get; set; }
    }
}