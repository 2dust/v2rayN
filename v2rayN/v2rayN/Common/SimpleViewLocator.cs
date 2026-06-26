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

    public IViewFor<TViewModel>? ResolveView<TViewModel>(string? contract = null) where TViewModel : class
    {
        if (_mappings.TryGetValue(typeof(TViewModel), out var factory))
        {
            return factory() as IViewFor<TViewModel>;
        }
        return null;
    }

    public IViewFor? ResolveView(object? instance, string? contract = null)
    {
        if (instance == null)
        {
            return null;
        }
        var viewModelType = instance.GetType();
        if (_mappings.TryGetValue(viewModelType, out var factory))
        {
            return factory();
        }
        return null;
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
