namespace ServiceLib.Models;

[Serializable]
public class ServerTestItem
{
    public string? IndexId { get; set; }
    public string? Address { get; set; }
    public int Port { get; set; }
    public EConfigType ConfigType { get; set; }
    public bool AllowTest { get; set; }
    public int QueueNum { get; set; }
}
