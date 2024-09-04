using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;

namespace ServiceLib.ViewModels
{
    public class AddServer2ViewModel : MyReactiveObject
    {
        [Reactive]
        public ProfileItem SelectedSource { get; set; }

        [Reactive]
        public string? CoreType { get; set; }

        public ReactiveCommand<Unit, Unit> BrowseServerCmd { get; }
        public ReactiveCommand<Unit, Unit> EditServerCmd { get; }
        public ReactiveCommand<Unit, Unit> SaveServerCmd { get; }
        public bool IsModified { get; set; }

        public AddServer2ViewModel(ProfileItem profileItem, Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            _config = LazyConfig.Instance.Config;
            _updateView = updateView;

            if (profileItem.indexId.IsNullOrEmpty())
            {
                SelectedSource = profileItem;
            }
            else
            {
                SelectedSource = JsonUtils.DeepCopy(profileItem);
            }
            CoreType = SelectedSource?.coreType?.ToString();

            BrowseServerCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await _updateView?.Invoke(EViewAction.BrowseServer, null);
            });

            EditServerCmd = ReactiveCommand.Create(() =>
            {
                EditServer();
            });

            SaveServerCmd = ReactiveCommand.Create(() =>
            {
                SaveServerAsync();
            });
        }

        private async Task SaveServerAsync()
        {
            string remarks = SelectedSource.remarks;
            if (Utils.IsNullOrEmpty(remarks))
            {
                _noticeHandler?.Enqueue(ResUI.PleaseFillRemarks);
                return;
            }

            if (Utils.IsNullOrEmpty(SelectedSource.address))
            {
                _noticeHandler?.Enqueue(ResUI.FillServerAddressCustom);
                return;
            }
            SelectedSource.coreType = CoreType.IsNullOrEmpty() ? null : (ECoreType)Enum.Parse(typeof(ECoreType), CoreType);

            if (ConfigHandler.EditCustomServer(_config, SelectedSource) == 0)
            {
                _noticeHandler?.Enqueue(ResUI.OperationSuccess);
                await _updateView?.Invoke(EViewAction.CloseWindow, null);
            }
            else
            {
                _noticeHandler?.Enqueue(ResUI.OperationFailed);
            }
        }

        public void BrowseServer(string fileName)
        {
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
                _noticeHandler?.Enqueue(ResUI.FailedImportedCustomServer);
            }
        }

        private void EditServer()
        {
            var address = SelectedSource.address;
            if (Utils.IsNullOrEmpty(address))
            {
                _noticeHandler?.Enqueue(ResUI.FillServerAddressCustom);
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