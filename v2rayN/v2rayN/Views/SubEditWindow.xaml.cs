using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;
using v2rayN.Mode;
using v2rayN.ViewModels;
using Application = System.Windows.Application;

namespace v2rayN.Views
{
    public partial class SubEditWindow
    {
        public SubEditWindow(SubItem subItem)
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            this.Loaded += Window_Loaded;

            ViewModel = new SubEditViewModel(subItem, this);

            Global.SubConvertTargets.ForEach(it =>
            {
                cmbConvertTarget.Items.Add(it);
            });

            this.WhenActivated(disposables =>
            {
                this.Bind(ViewModel, vm => vm.SelectedSource.remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.url, v => v.txtUrl.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.moreUrl, v => v.txtMoreUrl.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.enabled, v => v.togEnable.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.autoUpdateInterval, v => v.txtAutoUpdateInterval.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.userAgent, v => v.txtUserAgent.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.sort, v => v.txtSort.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.filter, v => v.txtFilter.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.convertTarget, v => v.cmbConvertTarget.Text).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtRemarks.Focus();

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

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}