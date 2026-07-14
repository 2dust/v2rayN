using System.Windows.Controls;

namespace v2rayN.Base;

public static class ViewHost
{
    public static void Show(
        ContentControl host,
        object? viewModel)
    {
        if (viewModel == null)
        {
            host.Content = null;
            return;
        }

        var view = SimpleViewLocator.Instance.ResolveView(viewModel);
        view?.ViewModel = viewModel;

        host.Content = view;
    }
}
