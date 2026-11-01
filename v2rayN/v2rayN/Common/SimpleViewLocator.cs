using v2rayN.ViewModels;
using v2rayN.Views;

namespace v2rayN.Common;

public class SimpleViewLocator : IViewLocator
{
    private static readonly Lazy<SimpleViewLocator> _instance = new(() => new SimpleViewLocator());
    private readonly Dictionary<Type, Func<IViewFor>> _mappings = new();

    private SimpleViewLocator()
    {
        Register<AddGroupServerViewModel, AddGroupServerWindow>();
        Register<AddServer2ViewModel, AddServer2Window>();
        Register<AddServerViewModel, AddServerWindow>();
        Register<BackupAndRestoreViewModel, BackupAndRestoreView>();
        Register<CheckUpdateViewModel, CheckUpdateView>();
        Register<ClashConnectionsViewModel, ClashConnectionsView>();
        Register<ClashProxiesViewModel, ClashProxiesView>();
        Register<DNSSettingViewModel, DNSSettingWindow>();
        Register<FullConfigTemplateViewModel, FullConfigTemplateWindow>();
        Register<GlobalHotkeySettingViewModel, GlobalHotkeySettingWindow>();
        Register<MainWindowViewModel, MainWindow>();
        Register<MsgViewModel, MsgView>();
        Register<OptionSettingViewModel, OptionSettingWindow>();
        Register<ProfilesSelectViewModel, ProfilesSelectWindow>();
        Register<ProfilesViewModel, ProfilesView>();
        Register<RoutingRuleDetailsViewModel, RoutingRuleDetailsWindow>();
        Register<RoutingRuleSettingViewModel, RoutingRuleSettingWindow>();
        Register<RoutingSettingViewModel, RoutingSettingWindow>();
        Register<StatusBarViewModel, StatusBarView>();
        Register<SubEditViewModel, SubEditWindow>();
        Register<SubSettingViewModel, SubSettingWindow>();
        Register<ThemeSettingViewModel, ThemeSettingView>();
    }

    public static SimpleViewLocator Instance => _instance.Value;

    public IViewFor<TViewModel>? ResolveView<TViewModel>() where TViewModel : class
    {
        if (_mappings.TryGetValue(typeof(TViewModel), out var factory))
        {
            var view = factory() as IViewFor<TViewModel>;
            return view;
        }
        return null;
    }

    public IViewFor? ResolveView<TViewModel>(TViewModel viewModel, string? contract) where TViewModel : class
    {
        return ResolveView<TViewModel>();
    }

    public IViewFor? ResolveView(object? viewModel, string? contract)
    {
        if (viewModel == null)
        {
            return null;
        }
        var viewModelType = viewModel.GetType();
        if (_mappings.TryGetValue(viewModelType, out var factory))
        {
            return factory();
        }
        return null;
    }

    public IViewFor? ResolveViewUnsafe(object? viewModel, string? contract)
    {
        return ResolveView(viewModel, contract);
    }

    public IViewFor? ResolveView<TViewModel>(TViewModel instance) where TViewModel : class
    {
        return ResolveView(instance, null);
    }

    public void Register<TViewModel, TView>(Func<TView> factory)
        where TViewModel : class
        where TView : class, IViewFor<TViewModel>
    {
        _mappings[typeof(TViewModel)] = factory;
    }

    public void Register<TViewModel, TView>()
        where TViewModel : class
        where TView : class, IViewFor<TViewModel>, new()
    {
        _mappings[typeof(TViewModel)] = () => new TView();
    }
}
