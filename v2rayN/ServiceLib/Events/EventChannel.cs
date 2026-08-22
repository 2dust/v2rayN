namespace ServiceLib.Events;

public sealed class EventChannel<T>
{
    private readonly Signal<T> _signal = new();
    private readonly Lock _gate = new();
    private readonly IObservable<T> _observable;

    public EventChannel()
    {
        _observable = _signal.Synchronize(_gate);
    }

    public IObservable<T> AsObservable()
    {
        return _observable;
    }

    public void Publish(T value)
    {
        lock (_gate)
        {
            _signal.OnNext(value);
        }
    }

    public void Publish()
    {
        if (typeof(T) != typeof(RxVoid))
        {
            throw new InvalidOperationException("Publish() without value is only valid for EventChannel<RxVoid>.");
        }
        lock (_gate)
        {
            _signal.OnNext((T)(object)RxVoid.Default);
        }
    }
}
