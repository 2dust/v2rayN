namespace ServiceLib.Models.Entities;

[Serializable]
public class WorkflowItem
{
    [PrimaryKey]
    public string Id { get; set; }

    public string Remarks { get; set; }

    public bool Enabled { get; set; } = true;

    public int Sort { get; set; }

    public string? Memo { get; set; }

    /// <summary>Serialized <see cref="List{WorkflowStep}"/>.</summary>
    public string? StepsJson { get; set; }
}
