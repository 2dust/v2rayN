﻿using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;
using v2rayN.Mode;
using v2rayN.ViewModels;
using Application = System.Windows.Application;

namespace v2rayN.Views
{
    public partial class AddServer2Window
    {
        public AddServer2Window(ProfileItem profileItem)
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            this.Loaded += Window_Loaded;
            ViewModel = new AddServer2ViewModel(profileItem, this);

            foreach (ECoreType it in Enum.GetValues(typeof(ECoreType)))
            {
                if (it == ECoreType.v2rayN)
                    continue;
                cmbCoreType.Items.Add(it.ToString());
            }
            cmbCoreType.Items.Add(string.Empty);

            this.WhenActivated(disposables =>
            {
                this.Bind(ViewModel, vm => vm.SelectedSource.remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.address, v => v.txtAddress.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.coreType, v => v.cmbCoreType.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.displayLog, v => v.togDisplayLog.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.preSocksPort, v => v.txtPreSocksPort.Text).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.BrowseServerCmd, v => v.btnBrowse).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.EditServerCmd, v => v.btnEdit).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SaveServerCmd, v => v.btnSave).DisposeWith(disposables);
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

        private void btnCancel_Click(object sender, System.Windows.RoutedEventArgs e)
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