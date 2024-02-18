﻿using ReactiveUI;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Input;
using v2rayN.Model;
using v2rayN.ViewModels;

namespace v2rayN.Views
{
    public partial class SubSettingWindow
    {
        public SubSettingWindow()
        {
            InitializeComponent();

            // 设置窗口的尺寸不大于屏幕的尺寸
            if (this.Width > SystemParameters.WorkArea.Width)
            {
                this.Width = SystemParameters.WorkArea.Width;
            }
            if (this.Height > SystemParameters.WorkArea.Height)
            {
                this.Height = SystemParameters.WorkArea.Height;
            }

            this.Owner = Application.Current.MainWindow;

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