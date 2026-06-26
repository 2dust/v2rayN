using v2rayN.Base;

namespace v2rayN.Manager;

public class WindowDialog: IWindowDialog
{
    public Task<bool> ShowDialogAsync<TViewModel>(TViewModel vm)
        where TViewModel : class
    {
        var owner = Application.Current.MainWindow;

        var viewFor = SimpleViewLocator.Instance.ResolveView(vm);
        //var viewFor = SimpleViewLocator.Instance.ResolveView<TViewModel>();

        if (viewFor is not WindowBase<TViewModel> window)
        {
            return Task.FromResult(false);
        }

        window.Owner = owner;
        window.ViewModel = vm;

        var result = window.ShowDialog();

        return Task.FromResult(result ?? false);
    }
}
