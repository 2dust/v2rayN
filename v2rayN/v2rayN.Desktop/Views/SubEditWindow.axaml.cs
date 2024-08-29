using Avalonia;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System.Reactive.Disposables;

namespace v2rayN.Desktop.Views
{
    public partial class SubEditWindow : ReactiveWindow<SubEditViewModel>
    {
        public SubEditWindow()
        {
            InitializeComponent();
        }

        public SubEditWindow(SubItem subItem)
        {
            InitializeComponent();

            Loaded += Window_Loaded;
            btnCancel.Click += (s, e) => this.Close();

            ViewModel = new SubEditViewModel(subItem, UpdateViewHandler);

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
                this.Bind(ViewModel, vm => vm.SelectedSource.convertTarget, v => v.cmbConvertTarget.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.prevProfile, v => v.txtPrevProfile.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.nextProfile, v => v.txtNextProfile.Text).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
            });
        }

        private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
        {
            switch (action)
            {
                case EViewAction.CloseWindow:
                    this.Close(true);
                    break;
            }
            return await Task.FromResult(true);
        }

        private void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            txtRemarks.Focus();
        }
    }
}