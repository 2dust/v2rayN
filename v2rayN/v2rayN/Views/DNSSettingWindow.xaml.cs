using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.ViewModels;
using Application = System.Windows.Application;

namespace v2rayN.Views
{
    public partial class DNSSettingWindow
    {
        private static Config _config;

        public DNSSettingWindow()
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;

            this.Loaded += Window_Loaded;

            _config = LazyConfig.Instance.GetConfig();
            ViewModel = new DNSSettingViewModel(this);

            Global.domainStrategy4Freedoms.ForEach(it =>
            {
                cmbdomainStrategy4Freedom.Items.Add(it);
            });

            this.WhenActivated(disposables =>
            {
                this.Bind(ViewModel, vm => vm.domainStrategy4Freedom, v => v.cmbdomainStrategy4Freedom.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.normalDNS, v => v.txtnormalDNS.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.normalDNS2, v => v.txtnormalDNS2.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.tunDNS2, v => v.txttunDNS2.Text).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.ImportDefConfig4V2rayCmd, v => v.btnImportDefConfig4V2ray).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.ImportDefConfig4SingboxCmd, v => v.btnImportDefConfig4Singbox).DisposeWith(disposables);
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
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

        private void linkDnsObjectDoc_Click(object sender, RoutedEventArgs e)
        {
            Utils.ProcessStart("https://www.v2fly.org/config/dns.html#dnsobject");
        }

        private void linkDnsSingboxObjectDoc_Click(object sender, RoutedEventArgs e)
        {
            Utils.ProcessStart("https://sing-box.sagernet.org/zh/configuration/dns/");
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}