using System.Reactive;
using System.Reactive.Subjects;

namespace ServiceLib.Handler;

public static class AppEvents
{
    public static readonly Subject<Unit> ProfilesRefreshRequested = new();
}
