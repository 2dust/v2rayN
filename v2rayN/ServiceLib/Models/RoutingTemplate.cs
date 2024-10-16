namespace ServiceLib.Models
{
    [Serializable]
    public class RoutingTemplate
    {
        public string version { get; set; }
        public RoutingItem[] routingItems { get; set; }
    }
}
