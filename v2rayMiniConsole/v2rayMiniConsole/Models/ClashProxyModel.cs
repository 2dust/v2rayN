namespace v2rayN.Models
{
    [Serializable]
    public class ClashProxyModel
    {

        public string name { get; set; }


        public string type { get; set; }


        public string now { get; set; }


        public int delay { get; set; }


        public string delayName { get; set; }


        public bool isActive { get; set; }
    }
}