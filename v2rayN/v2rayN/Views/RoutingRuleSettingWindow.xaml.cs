using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Input;
using v2rayN.Enums;
using v2rayN.Models;
using v2rayN.ViewModels;

namespace v2rayN.Views
{
    public partial class RoutingRuleSettingWindow
    {
        public RoutingRuleSettingWindow(RoutingItem routingItem)
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
            this.PreviewKeyDown += RoutingRuleSettingWindow_PreviewKeyDown;
            lstRules.SelectionChanged += lstRules_SelectionChanged;
            lstRules.MouseDoubleClick += LstRules_MouseDoubleClick;

            ViewModel = new RoutingRuleSettingViewModel(routingItem, this);
            Global.DomainStrategies.ForEach(it =>
            {
                cmbdomainStrategy.Items.Add(it);
            });
            cmbdomainStrategy.Items.Add(string.Empty);
            Global.DomainStrategies4Singbox.ForEach(it =>
            {
                cmbdomainStrategy4Singbox.Items.Add(it);
            });

            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm.RulesItems, v => v.lstRules.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource, v => v.lstRules.SelectedItem).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.SelectedRouting.remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedRouting.domainStrategy, v => v.cmbdomainStrategy.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedRouting.domainStrategy4Singbox, v => v.cmbdomainStrategy4Singbox.Text).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.SelectedRouting.url, v => v.txtUrl.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedRouting.customIcon, v => v.txtCustomIcon.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedRouting.customRulesetPath4Singbox, v => v.txtCustomRulesetPath4Singbox.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedRouting.sort, v => v.txtSort.Text).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.RuleAddCmd, v => v.menuRuleAdd).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.ImportRulesFromFileCmd, v => v.menuImportRulesFromFile).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.ImportRulesFromClipboardCmd, v => v.menuImportRulesFromClipboard).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.ImportRulesFromUrlCmd, v => v.menuImportRulesFromUrl).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.RuleAddCmd, v => v.menuRuleAdd2).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RuleRemoveCmd, v => v.menuRuleRemove).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RuleExportSelectedCmd, v => v.menuRuleExportSelected).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.MoveTopCmd, v => v.menuMoveTop).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.MoveUpCmd, v => v.menuMoveUp).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.MoveDownCmd, v => v.menuMoveDown).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.MoveBottomCmd, v => v.menuMoveBottom).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtRemarks.Focus();
        }

        private void RoutingRuleSettingWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Key == Key.A)
                {
                    lstRules.SelectAll();
                }
                else if (e.Key == Key.C)
                {
                    ViewModel?.RuleExportSelected();
                }
            }
            else
            {
                if (e.Key == Key.T)
                {
                    ViewModel?.MoveRule(EMove.Top);
                }
                else if (e.Key == Key.U)
                {
                    ViewModel?.MoveRule(EMove.Up);
                }
                else if (e.Key == Key.D)
                {
                    ViewModel?.MoveRule(EMove.Down);
                }
                else if (e.Key == Key.B)
                {
                    ViewModel?.MoveRule(EMove.Bottom);
                }
                else if (e.Key == Key.Delete)
                {
                    ViewModel?.RuleRemove();
                }
            }
        }

        private void lstRules_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ViewModel.SelectedSources = lstRules.SelectedItems.Cast<RulesItemModel>().ToList();
        }

        private void LstRules_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.RuleEdit(false);
        }

        private void menuRuleSelectAll_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            lstRules.SelectAll();
        }

        private void btnBrowseCustomIcon_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (UI.OpenFileDialog(out string fileName,
                "PNG,ICO|*.png;*.ico") != true)
            {
                return;
            }

            txtCustomIcon.Text = fileName;
        }

        private void btnBrowseCustomRulesetPath4Singbox_Click(object sender, RoutedEventArgs e)
        {
            if (UI.OpenFileDialog(out string fileName,
                  "Config|*.json|All|*.*") != true)
            {
                return;
            }

            txtCustomRulesetPath4Singbox.Text = fileName;
        }

        private void linkCustomRulesetPath4Singbox(object sender, RoutedEventArgs e)
        {
            Utils.ProcessStart("https://github.com/2dust/v2rayCustomRoutingList/blob/master/singbox_custom_ruleset_example.json");
        }
    }
}