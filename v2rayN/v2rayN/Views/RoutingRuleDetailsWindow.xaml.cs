using System.Reactive.Disposables;
using System.Windows;
using ReactiveUI;

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

			if (!rulesItem.Id.IsNullOrEmpty())
			{
				rulesItem.Protocol?.ForEach(it =>
				{
					clbProtocol.SelectedItems.Add(it);
				});
				rulesItem.InboundTag?.ForEach(it =>
				{
					clbInboundTag.SelectedItems.Add(it);
				});
			}

			this.WhenActivated(disposables =>
			{
				this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
				this.Bind(ViewModel, vm => vm.SelectedSource.OutboundTag, v => v.cmbOutboundTag.Text).DisposeWith(disposables);
				this.Bind(ViewModel, vm => vm.SelectedSource.Port, v => v.txtPort.Text).DisposeWith(disposables);
				this.Bind(ViewModel, vm => vm.SelectedSource.Network, v => v.cmbNetwork.Text).DisposeWith(disposables);
				this.Bind(ViewModel, vm => vm.SelectedSource.Enabled, v => v.togEnabled.IsChecked).DisposeWith(disposables);
				this.Bind(ViewModel, vm => vm.Domain, v => v.txtDomain.Text).DisposeWith(disposables);
				this.Bind(ViewModel, vm => vm.IP, v => v.txtIP.Text).DisposeWith(disposables);
				this.Bind(ViewModel, vm => vm.Process, v => v.txtProcess.Text).DisposeWith(disposables);
				this.Bind(ViewModel, vm => vm.AutoSort, v => v.chkAutoSort.IsChecked).DisposeWith(disposables);

				this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
			});
			WindowsUtils.SetDarkBorder(this, AppHandler.Instance.Config.UiItem.CurrentTheme);
		}

		private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
		{
			switch (action)
			{
				case EViewAction.CloseWindow:
					this.DialogResult = true;
					break;
			}
			return await Task.FromResult(true);
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
			ProcUtils.ProcessStart("https://xtls.github.io/config/routing.html#ruleobject");
		}
	}
}
