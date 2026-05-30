namespace ServiceLib.Models.Dto;

[Serializable]
public class RoutingTemplate
{
    public string Version { get; set; }
    public RoutingItem[] RoutingItems { get; set; }
}
