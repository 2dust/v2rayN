using Avalonia.Controls.Templates;
using v2rayN.Desktop.ViewModels;
using v2rayN.Desktop.Views;

namespace v2rayN.Desktop.Common;

public class SimpleViewLocator : IDataTemplate
{
    private static readonly Lazy<SimpleViewLocator> _instance = new(() => new SimpleViewLocator());

    private readonly Dictionary<Type, Func<Control?>> _locator = new();

    private SimpleViewLocator()
    {
        RegisterViewFactory<AddGroupServerViewModel, AddGroupServerWindow>();
        RegisterViewFactory<AddServer2ViewModel, AddServer2Window>();
        RegisterViewFactory<AddServerViewModel, AddServerWindow>();
        RegisterViewFactory<BackupAndRestoreViewModel, BackupAndRestoreView>();
        RegisterViewFactory<CheckUpdateViewModel, CheckUpdateView>();
        RegisterViewFactory<ClashConnectionsViewModel, ClashConnectionsView>();
        RegisterViewFactory<ClashProxiesViewModel, ClashProxiesView>();
        RegisterViewFactory<DNSSettingViewModel, DNSSettingWindow>();
        RegisterViewFactory<FullConfigTemplateViewModel, FullConfigTemplateWindow>();
        RegisterViewFactory<GlobalHotkeySettingViewModel, GlobalHotkeySettingWindow>();
        RegisterViewFactory<MainWindowViewModel, MainWindow>();
        RegisterViewFactory<MsgViewModel, MsgView>();
        RegisterViewFactory<OptionSettingViewModel, OptionSettingWindow>();
        RegisterViewFactory<ProfilesSelectViewModel, ProfilesSelectWindow>();
        RegisterViewFactory<ProfilesViewModel, ProfilesView>();
        RegisterViewFactory<RoutingRuleDetailsViewModel, RoutingRuleDetailsWindow>();
        RegisterViewFactory<RoutingRuleSettingViewModel, RoutingRuleSettingWindow>();
        RegisterViewFactory<RoutingSettingViewModel, RoutingSettingWindow>();
        RegisterViewFactory<StatusBarViewModel, StatusBarView>();
        RegisterViewFactory<SubEditViewModel, SubEditWindow>();
        RegisterViewFactory<SubSettingViewModel, SubSettingWindow>();
        RegisterViewFactory<ThemeSettingViewModel, ThemeSettingView>();
    }

    public static SimpleViewLocator Instance => _instance.Value;

    public Control Build(object? data)
    {
        if (data is null)
        {
            return new TextBlock { Text = "No VM provided" };
        }

        _locator.TryGetValue(data.GetType(), out var factory);

        return factory?.Invoke() ?? new TextBlock { Text = $"VM Not Registered: {data.GetType()}" };
    }

    public bool Match(object? data)
    {
        return data is MyReactiveObject;
    }

    public void RegisterViewFactory<TViewModel>(Func<Control> factory) where TViewModel : class
    {
        _locator.Add(typeof(TViewModel), factory);
    }

    public void RegisterViewFactory<TViewModel, TView>()
        where TViewModel : class
        where TView : Control, new()
    {
        _locator.Add(typeof(TViewModel), () => new TView());
    }
}
