namespace ServiceLib.Models;

public partial class CheckUpdateModel : ReactiveObject
{
    public bool? IsSelected { get; set; }
    public string? CoreType { get; set; }
    [Reactive] private string? _remarks;
    public string? FileName { get; set; }
    public bool? IsFinished { get; set; }
}
