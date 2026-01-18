namespace ServiceLib.Base;

public class MyReactiveObject : ReactiveObject
{
    protected static Config? Config { get; set; }
    protected Func<EViewAction, object?, Task<bool>>? UpdateView { get; set; }
}
