using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;
using v2rayN.Base;
using v2rayN.Mode;
using v2rayN.ViewModels;
using Application = System.Windows.Application;

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

            ViewModel = new RoutingRuleDetailsViewModel(rulesItem, this);
            cmbOutboundTag.Items.Add(Global.agentTag);
            cmbOutboundTag.Items.Add(Global.directTag);
            cmbOutboundTag.Items.Add(Global.blockTag);
            Global.Protocols.ForEach(it =>
            {
                clbProtocol.Items.Add(it);
            });
            Global.InboundTags.ForEach(it =>
            {
                clbInboundTag.Items.Add(it);
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
                this.Bind(ViewModel, vm => vm.SelectedSource.enabled, v => v.togEnabled.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.Domain, v => v.txtDomain.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.IP, v => v.txtIP.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.Process, v => v.txtProcess.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.AutoSort, v => v.chkAutoSort.IsChecked).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            cmbOutboundTag.Focus();

            // 获取当前屏幕的尺寸
            var screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            var screenWidth = screen.WorkingArea.Width;
            var screenHeight = screen.WorkingArea.Height;
            var screenTop = screen.WorkingArea.Top;

            // 获取屏幕的 DPI 缩放因素
            double dpiFactor = 1;
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null)
            {
                dpiFactor = source.CompositionTarget.TransformToDevice.M11;
            }

            // 设置窗口尺寸不超过当前屏幕的尺寸
            if (this.Width > screenWidth / dpiFactor)
            {
                this.Width = screenWidth / dpiFactor;
            }
            if (this.Height > screenHeight / dpiFactor)
            {
                this.Height = screenHeight / dpiFactor;
            }

            // 设置窗口不要显示在屏幕外面
            if (this.Top < screenTop / dpiFactor)
            {
                this.Top = screenTop / dpiFactor;
            }
        }

        private void ClbProtocol_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ViewModel.ProtocolItems = clbProtocol.SelectedItems.Cast<string>().ToList();
        }

        private void ClbInboundTag_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ViewModel.InboundTagItems = clbInboundTag.SelectedItems.Cast<string>().ToList();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void linkRuleobjectDoc_Click(object sender, RoutedEventArgs e)
        {
            Utils.ProcessStart("https://www.v2fly.org/config/routing.html#ruleobject");
        }
    }
}