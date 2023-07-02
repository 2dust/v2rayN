using ReactiveUI;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Input;
using v2rayN.Mode;
using v2rayN.ViewModels;
using Application = System.Windows.Application;

namespace v2rayN.Views
{
    public partial class SubSettingWindow
    {
        public SubSettingWindow()
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            this.Loaded += Window_Loaded;

            ViewModel = new SubSettingViewModel(this);
            this.Closing += SubSettingWindow_Closing;
            lstSubscription.MouseDoubleClick += LstSubscription_MouseDoubleClick;
            lstSubscription.SelectionChanged += LstSubscription_SelectionChanged;

            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm.SubItems, v => v.lstSubscription.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource, v => v.lstSubscription.SelectedItem).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.SubAddCmd, v => v.menuSubAdd).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SubDeleteCmd, v => v.menuSubDelete).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SubEditCmd, v => v.menuSubEdit).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SubShareCmd, v => v.menuSubShare).DisposeWith(disposables);
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 获取屏幕的 DPI 缩放因素
            double dpiFactor = 1;
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null)
            {
                dpiFactor = source.CompositionTarget.TransformToDevice.M11;
            }

            // 获取当前屏幕的尺寸
            var screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            var screenWidth = screen.WorkingArea.Width / dpiFactor;
            var screenHeight = screen.WorkingArea.Height / dpiFactor;
            var screenTop = screen.WorkingArea.Top / dpiFactor;
            var screenLeft = screen.WorkingArea.Left / dpiFactor;
            var screenBottom = screen.WorkingArea.Bottom / dpiFactor;
            var screenRight = screen.WorkingArea.Right / dpiFactor;

            // 设置窗口尺寸不超过当前屏幕的尺寸
            if (this.Width > screenWidth)
            {
                this.Width = screenWidth;
            }
            if (this.Height > screenHeight)
            {
                this.Height = screenHeight;
            }

            // 设置窗口不要显示在屏幕外面
            if (this.Top < screenTop)
            {
                this.Top = screenTop;
            }
            if (this.Left < screenLeft)
            {
                this.Left = screenLeft;
            }
            if (this.Top + this.Height > screenBottom)
            {
                this.Top = screenBottom - this.Height;
            }
            if (this.Left + this.Width > screenRight)
            {
                this.Left = screenRight - this.Width;
            }
        }

        private void SubSettingWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (ViewModel?.IsModified == true)
            {
                this.DialogResult = true;
            }
        }

        private void LstSubscription_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.EditSub(false);
        }

        private void LstSubscription_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ViewModel.SelectedSources = lstSubscription.SelectedItems.Cast<SubItem>().ToList();
        }

        private void menuClose_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ViewModel?.IsModified == true)
            {
                this.DialogResult = true;
            }
            else
            {
                this.Close();
            }
        }
    }
}