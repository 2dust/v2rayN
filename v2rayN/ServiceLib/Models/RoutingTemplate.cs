namespace ServiceLib.Models;

[Serializable]
public class RoutingTemplate
{
    public string Version { get; set; }
    public RoutingItem[] RoutingItems { get; set; }
}
