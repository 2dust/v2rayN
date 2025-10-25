namespace ServiceLib.Models;

[Serializable]
public partial class ProfileItemModel : ProfileItem
{
    public bool IsActive { get; set; }
    public string SubRemarks { get; set; }

    [Reactive]
    private int _delay;

    public decimal Speed { get; set; }
    public int Sort { get; set; }

    [Reactive]
    private string _delayVal;

    [Reactive]
    private string _speedVal;

    [Reactive]
    private string _todayUp;

    [Reactive]
    private string _todayDown;

    [Reactive]
    private string _totalUp;

    [Reactive]
    private string _totalDown;
}
