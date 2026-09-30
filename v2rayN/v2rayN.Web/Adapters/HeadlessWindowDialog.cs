using ServiceLib.Base;

namespace v2rayN.Web.Adapters;

public sealed class HeadlessWindowDialog : IWindowDialog
{
    public Task<bool> ShowDialogAsync<TViewModel>(TViewModel vm)
        where TViewModel : class => Task.FromResult(false);
}
