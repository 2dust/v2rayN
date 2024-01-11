﻿using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.ViewModels;

namespace v2rayN.Views
{
    public partial class RoutingRuleDetailsWindow
    {
        public RoutingRuleDetailsWindow(RulesItem rulesItem)
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
            this.Loaded += Window_Loaded;
            clbProtocol.SelectionChanged += ClbProtocol_SelectionChanged;
            clbInboundTag.SelectionChanged += ClbInboundTag_SelectionChanged;

            ViewModel = new RoutingRuleDetailsViewModel(rulesItem, this);
            cmbOutboundTag.Items.Add(Global.ProxyTag);
            cmbOutboundTag.Items.Add(Global.DirectTag);
            cmbOutboundTag.Items.Add(Global.BlockTag);

            // 分流实现
            cmbOutboundIndexId.Items.Add(Global.ProxyTag);
            cmbOutboundIndexId.Items.Add(Global.DirectTag);
            cmbOutboundIndexId.Items.Add(Global.BlockTag);

            var config = LazyConfig.Instance.GetConfig();
            var allServerItems = LazyConfig.Instance.ProfileItems(null, "");

            string tagName = null;
            int index = 1;
            foreach (var serverItem in allServerItems)
            {
                if (serverItem == null || serverItem.configType == EConfigType.Custom)
                {
                    continue;
                }
                tagName = $"[{(serverItem.indexId.Equals(config.indexId) ? "proxy" : index++)}]{serverItem.remarks}";
                cmbOutboundTag.Items.Add(tagName);
                cmbOutboundIndexId.Items.Add(serverItem.indexId);
            }
            // end

            Global.RuleProtocols.ForEach(it =>
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
                // 分流
                cmbOutboundIndexId.SelectedItem = cmbOutboundIndexId.Items.Cast<object>().FirstOrDefault(item => item.ToString() == ViewModel.SelectedSource.bingServerIndexId);
                cmbOutboundTag.SelectedIndex = cmbOutboundIndexId.SelectedIndex;
                this.WhenAnyValue(v => v.cmbOutboundTag.SelectedItem)
                    .Where(selectedItem => selectedItem != null)
                    .Subscribe(selectedItem =>
                    {
                        cmbOutboundIndexId.SelectedIndex = cmbOutboundTag.SelectedIndex;
                        ViewModel.SelectedSource.bingServerIndexId = cmbOutboundIndexId.SelectedItem.ToString();
                        ViewModel.SelectedSource.outboundTag = cmbOutboundTag.Text;
                    })
                    .DisposeWith(disposables);
                // end
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