namespace ServiceLib.Handler.Data;

public sealed class ConfigState
{
    private static readonly Lazy<ConfigState> _instance = new(() => new());
    private readonly Lock _lock = new();
    private Config? _current;

    public static ConfigState Instance => _instance.Value;

    public Config Current
    {
        get
        {
            lock (_lock)
            {
                return _current ??= new Config();
            }
        }
    }

    public bool IsInitialized
    {
        get
        {
            lock (_lock)
            {
                return _current is not null;
            }
        }
    }

    public void SetCurrent(Config config)
    {
        ArgumentNullException.ThrowIfNull(config);

        lock (_lock)
        {
            _current = config;
        }
    }
}
