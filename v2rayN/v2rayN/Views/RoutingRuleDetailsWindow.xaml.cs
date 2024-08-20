using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;

namespace v2rayN.Views
{
    public partial class RoutingRuleDetailsWindow
    {
        public RoutingRuleDetailsWindow(RulesItem rulesItem)
        {
            InitializeComponent();

            this.Owner = Application.Current.MainWindow;
            this.Loaded += Window_Loaded;
            clbProtocol.SelectionChanged += ClbProtocol_SelectionChanged;
            clbInboundTag.SelectionChanged += ClbInboundTag_SelectionChanged;

            ViewModel = new RoutingRuleDetailsViewModel(rulesItem, UpdateViewHandler);
            cmbOutboundTag.Items.Add(Global.ProxyTag);
            cmbOutboundTag.Items.Add(Global.DirectTag);
            cmbOutboundTag.Items.Add(Global.BlockTag);
            Global.RuleProtocols.ForEach(it =>
            {
                clbProtocol.Items.Add(it);
            });
            Global.InboundTags.ForEach(it =>
            {
                clbInboundTag.Items.Add(it);
            });
            Global.RuleNetworks.ForEach(it =>
            {
                cmbNetwork.Items.Add(it);
            });

            if (!rulesItem.id.IsNullOrEmpty())
            {
                rulesItem.protocol?.ForEach(it =>
                {
                    clbProtocol.SelectedItems.Add(it);
                });
                rulesItem.inboundTag?.ForEach(it =>
                {
                    clbInboundTag.SelectedItems.Add(it);
                });
            }

            this.WhenActivated(disposables =>
            {
                this.Bind(ViewModel, vm => vm.SelectedSource.outboundTag, v => v.cmbOutboundTag.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.port, v => v.txtPort.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.network, v => v.cmbNetwork.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.enabled, v => v.togEnabled.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.Domain, v => v.txtDomain.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.IP, v => v.txtIP.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.Process, v => v.txtProcess.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.AutoSort, v => v.chkAutoSort.IsChecked).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
            });
            WindowsUtils.SetDarkBorder(this, LazyConfig.Instance.Config.uiItem.followSystemTheme ? !WindowsUtils.IsLightTheme() : LazyConfig.Instance.Config.uiItem.colorModeDark);
        }

        private bool UpdateViewHandler(EViewAction action, object? obj)
        {
            switch (action)
            {
                case EViewAction.CloseWindow:
                    this.DialogResult = true;
                    break;
            }
            return true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            cmbOutboundTag.Focus();
        }

        private void ClbProtocol_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ViewModel.ProtocolItems = clbProtocol.SelectedItems.Cast<string>().ToList();
        }

        private void ClbInboundTag_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ViewModel.InboundTagItems = clbInboundTag.SelectedItems.Cast<string>().ToList();
        }

        private void linkRuleobjectDoc_Click(object sender, RoutedEventArgs e)
        {
            Utils.ProcessStart("https://xtls.github.io/config/routing.html#ruleobject");
        }
    }
}