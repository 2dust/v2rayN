namespace ServiceLib.Base;

public class MyReactiveObject : ReactiveObject, IActivatableViewModel
{
    protected static Config? _config;

    public ViewModelActivator Activator { get; } = new();
}
