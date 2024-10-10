using ReactiveUI;
using ReactiveUI.Fody.Helpers;
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
            _config = AppHandler.Instance.Config;
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
                _updateView?.Invoke(EViewAction.BrowseServer, null);
            });

            EditServerCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await EditServer();
            });

            SaveServerCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await SaveServerAsync();
            });
        }

        private async Task SaveServerAsync()
        {
            string remarks = SelectedSource.remarks;
            if (Utils.IsNullOrEmpty(remarks))
            {
                NoticeHandler.Instance.Enqueue(ResUI.PleaseFillRemarks);
                return;
            }

            if (Utils.IsNullOrEmpty(SelectedSource.address))
            {
                NoticeHandler.Instance.Enqueue(ResUI.FillServerAddressCustom);
                return;
            }
            SelectedSource.coreType = CoreType.IsNullOrEmpty() ? null : (ECoreType)Enum.Parse(typeof(ECoreType), CoreType);

            if (ConfigHandler.EditCustomServer(_config, SelectedSource) == 0)
            {
                NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
                _updateView?.Invoke(EViewAction.CloseWindow, null);
            }
            else
            {
                NoticeHandler.Instance.Enqueue(ResUI.OperationFailed);
            }
        }

        public void BrowseServer(string fileName)
        {
            if (Utils.IsNullOrEmpty(fileName))
            {
                return;
            }

            var item = AppHandler.Instance.GetProfileItem(SelectedSource.indexId);
            item ??= SelectedSource;
            item.address = fileName;
            if (ConfigHandler.AddCustomServer(_config, item, false) == 0)
            {
                NoticeHandler.Instance.Enqueue(ResUI.SuccessfullyImportedCustomServer);
                if (Utils.IsNotEmpty(item.indexId))
                {
                    SelectedSource = JsonUtils.DeepCopy(item);
                }
                IsModified = true;
            }
            else
            {
                NoticeHandler.Instance.Enqueue(ResUI.FailedImportedCustomServer);
            }
        }

        private async Task EditServer()
        {
            var address = SelectedSource.address;
            if (Utils.IsNullOrEmpty(address))
            {
                NoticeHandler.Instance.Enqueue(ResUI.FillServerAddressCustom);
                return;
            }

            address = Utils.GetConfigPath(address);
            if (File.Exists(address))
            {
                Utils.ProcessStart(address);
            }
            else
            {
                NoticeHandler.Instance.Enqueue(ResUI.FailedReadConfiguration);
            }
        }
    }
}