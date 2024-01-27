using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ReactiveUI.Validation.Helpers;
using Splat;
using System.IO;
using System.Reactive;
using System.Windows;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.ViewModels
{
    public class AddServer2ViewModel : ReactiveValidationObject
    {
        private static Config _config;
        private NoticeHandler? _noticeHandler;
        private Window _view;

        [Reactive]
        public ProfileItem SelectedSource { get; set; }

        public ReactiveCommand<Unit, Unit> BrowseServerCmd { get; }
        public ReactiveCommand<Unit, Unit> EditServerCmd { get; }
        public ReactiveCommand<Unit, Unit> SaveServerCmd { get; }
        public bool IsModified { get; set; }

        public AddServer2ViewModel(ProfileItem profileItem, Window view)
        {
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            _config = LazyConfig.Instance.GetConfig();

            if (profileItem.indexId.IsNullOrEmpty())
            {
                SelectedSource = profileItem;
            }
            else
            {
                SelectedSource = JsonUtils.DeepCopy(profileItem);
            }

            _view = view;

            BrowseServerCmd = ReactiveCommand.Create(() =>
            {
                BrowseServer();
            });

            EditServerCmd = ReactiveCommand.Create(() =>
            {
                EditServer();
            });

            SaveServerCmd = ReactiveCommand.Create(() =>
            {
                SaveServer();
            });

            Utils.SetDarkBorder(view, _config.uiItem.colorModeDark);
        }

        private void SaveServer()
        {
            string remarks = SelectedSource.remarks;
            if (Utils.IsNullOrEmpty(remarks))
            {
                UI.Show(ResUI.PleaseFillRemarks);
                return;
            }

            if (Utils.IsNullOrEmpty(SelectedSource.address))
            {
                UI.Show(ResUI.FillServerAddressCustom);
                return;
            }

            var item = LazyConfig.Instance.GetProfileItem(SelectedSource.indexId);
            if (item is null)
            {
                item = SelectedSource;
            }
            else
            {
                item.remarks = SelectedSource.remarks;
                item.address = SelectedSource.address;
                item.coreType = SelectedSource.coreType;
                item.displayLog = SelectedSource.displayLog;
                item.preSocksPort = SelectedSource.preSocksPort;
            }

            if (ConfigHandler.EditCustomServer(_config, item) == 0)
            {
                _noticeHandler?.Enqueue(ResUI.OperationSuccess);
                _view.DialogResult = true;
            }
            else
            {
                UI.Show(ResUI.OperationFailed);
            }
        }

        private void BrowseServer()
        {
            //UI.Show(ResUI.CustomServerTips);

            if (UI.OpenFileDialog(out string fileName,
                "Config|*.json|YAML|*.yaml;*.yml|All|*.*") != true)
            {
                return;
            }
            if (Utils.IsNullOrEmpty(fileName))
            {
                return;
            }

            var item = LazyConfig.Instance.GetProfileItem(SelectedSource.indexId);
            item ??= SelectedSource;
            item.address = fileName;
            if (ConfigHandler.AddCustomServer(_config, item, false) == 0)
            {
                _noticeHandler?.Enqueue(ResUI.SuccessfullyImportedCustomServer);
                if (!Utils.IsNullOrEmpty(item.indexId))
                {
                    SelectedSource = JsonUtils.DeepCopy(item);
                }
                IsModified = true;
            }
            else
            {
                UI.ShowWarning(ResUI.FailedImportedCustomServer);
            }
        }

        private void EditServer()
        {
            var address = SelectedSource.address;
            if (Utils.IsNullOrEmpty(address))
            {
                UI.Show(ResUI.FillServerAddressCustom);
                return;
            }

            address = Utils.GetConfigPath(address);
            if (File.Exists(address))
            {
                Utils.ProcessStart(address);
            }
            else
            {
                _noticeHandler?.Enqueue(ResUI.FailedReadConfiguration);
            }
        }
    }
}