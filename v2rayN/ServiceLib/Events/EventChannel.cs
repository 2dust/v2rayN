using System.Reactive.Subjects;

namespace ServiceLib.Events;

public sealed class EventChannel<T>
{
    private readonly ISubject<T> _subject = Subject.Synchronize(new Subject<T>());

    public IObservable<T> AsObservable()
    {
        return _subject.AsObservable();
    }

    public void Publish(T value)
    {
        _subject.OnNext(value);
    }

    public void Publish()
    {
        if (typeof(T) != typeof(Unit))
        {
            throw new InvalidOperationException("Publish() without value is only valid for EventChannel<Unit>.");
        }
        _subject.OnNext((T)(object)Unit.Default);
    }
}
