using Avalonia.Input;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using DynamicData;
using ReactiveUI;
using Splat;
using System.Reactive.Disposables;

namespace v2rayN.Desktop.Views
{
	public partial class ClashProxiesView : ReactiveUserControl<ClashProxiesViewModel>
	{
		public ClashProxiesView()
		{
			InitializeComponent();
			ViewModel = new ClashProxiesViewModel(UpdateViewHandler);
			Locator.CurrentMutable.RegisterLazySingleton(() => ViewModel, typeof(ClashProxiesViewModel));
			lstProxyDetails.DoubleTapped += LstProxyDetails_DoubleTapped;
			this.KeyDown += ClashProxiesView_KeyDown;

			this.WhenActivated(disposables =>
			{
				this.OneWayBind(ViewModel, vm => vm.ProxyGroups, v => v.lstProxyGroups.ItemsSource).DisposeWith(disposables);
				this.Bind(ViewModel, vm => vm.SelectedGroup, v => v.lstProxyGroups.SelectedItem).DisposeWith(disposables);

				this.OneWayBind(ViewModel, vm => vm.ProxyDetails, v => v.lstProxyDetails.ItemsSource).DisposeWith(disposables);
				this.Bind(ViewModel, vm => vm.SelectedDetail, v => v.lstProxyDetails.SelectedItem).DisposeWith(disposables);

				this.BindCommand(ViewModel, vm => vm.ProxiesReloadCmd, v => v.menuProxiesReload).DisposeWith(disposables);
				this.BindCommand(ViewModel, vm => vm.ProxiesDelaytestCmd, v => v.menuProxiesDelaytest).DisposeWith(disposables);

				this.BindCommand(ViewModel, vm => vm.ProxiesDelaytestPartCmd, v => v.menuProxiesDelaytestPart).DisposeWith(disposables);
				this.BindCommand(ViewModel, vm => vm.ProxiesSelectActivityCmd, v => v.menuProxiesSelectActivity).DisposeWith(disposables);

				this.Bind(ViewModel, vm => vm.RuleModeSelected, v => v.cmbRulemode.SelectedIndex).DisposeWith(disposables);
				this.Bind(ViewModel, vm => vm.SortingSelected, v => v.cmbSorting.SelectedIndex).DisposeWith(disposables);
				this.Bind(ViewModel, vm => vm.AutoRefresh, v => v.togAutoRefresh.IsChecked).DisposeWith(disposables);
			});
		}

		private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
		{
			switch (action)
			{
				case EViewAction.DispatcherRefreshProxyGroups:
					Dispatcher.UIThread.Post(() =>
						ViewModel?.RefreshProxyGroups(),
						DispatcherPriority.Default);
					break;

				case EViewAction.DispatcherProxiesDelayTest:
					if (obj is null)
						return false;
					Dispatcher.UIThread.Post(() =>
						ViewModel?.ProxiesDelayTestResult((SpeedTestResult)obj),
						DispatcherPriority.Default);
					break;
			}

			return await Task.FromResult(true);
		}

		private void ClashProxiesView_KeyDown(object? sender, KeyEventArgs e)
		{
			switch (e.Key)
			{
				case Key.F5:
					ViewModel?.ProxiesReload();
					break;

				case Key.Enter:
					ViewModel?.SetActiveProxy();
					break;
			}
		}

		private void LstProxyDetails_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
		{
			ViewModel?.SetActiveProxy();
		}
	}
}