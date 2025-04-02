using ReactiveUI;

namespace ServiceLib.Base;

public class MyReactiveObject : ReactiveObject
{
    protected static Config? _config;
    protected Func<EViewAction, object?, Task<bool>>? _updateView;
}
