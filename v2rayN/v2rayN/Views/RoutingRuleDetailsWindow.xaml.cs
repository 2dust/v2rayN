﻿using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;
using v2rayN.Base;
using v2rayN.Mode;
using v2rayN.ViewModels;

namespace v2rayN.Views
{
    public partial class RoutingRuleDetailsWindow
    {
        public RoutingRuleDetailsWindow(RulesItem rulesItem)
        {
            InitializeComponent();
            this.Loaded += Window_Loaded;
            clbProtocol.SelectionChanged += ClbProtocol_SelectionChanged;
            clbInboundTag.SelectionChanged += ClbInboundTag_SelectionChanged;

            ViewModel = new RoutingRuleDetailsViewModel(rulesItem, this);
            cmbOutboundTag.Items.Add(Global.agentTag);
            cmbOutboundTag.Items.Add(Global.directTag);
            cmbOutboundTag.Items.Add(Global.blockTag);
            foreach (var profileItem in SqliteHelper.Instance.Table<ProfileItem>())
            {
                cmbOutboundTag.Items.Add(profileItem.remarks);
            }
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
