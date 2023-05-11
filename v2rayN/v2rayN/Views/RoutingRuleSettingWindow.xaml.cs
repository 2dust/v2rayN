using Microsoft.Win32;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Input;
using v2rayN.Mode;
using v2rayN.ViewModels;

namespace v2rayN.Views
{
    public partial class RoutingRuleSettingWindow
    {
        public RoutingRuleSettingWindow(RoutingItem routingItem)
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            this.Loaded += Window_Loaded;
            this.PreviewKeyDown += RoutingRuleSettingWindow_PreviewKeyDown;
            lstRules.SelectionChanged += lstRules_SelectionChanged;
            lstRules.MouseDoubleClick += LstRules_MouseDoubleClick;

            ViewModel = new RoutingRuleSettingViewModel(routingItem, this);
            Global.domainStrategys.ForEach(it =>
            {
                cmbdomainStrategy.Items.Add(it);
            });
            cmbdomainStrategy.Items.Add(string.Empty);
            Global.domainStrategys4Singbox.ForEach(it =>
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

        private void btnBrowse_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Filter = "PNG|*.png";
            openFileDialog1.ShowDialog();

            txtCustomIcon.Text = openFileDialog1.FileName;
        }
    }
}