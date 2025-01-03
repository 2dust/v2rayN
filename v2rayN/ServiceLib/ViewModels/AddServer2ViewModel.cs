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

            SelectedSource = profileItem.IndexId.IsNullOrEmpty() ? profileItem : JsonUtils.DeepCopy(profileItem);
            CoreType = SelectedSource?.CoreType?.ToString();
        }

        private async Task SaveServerAsync()
        {
            string remarks = SelectedSource.Remarks;
            if (Utils.IsNullOrEmpty(remarks))
            {
                NoticeHandler.Instance.Enqueue(ResUI.PleaseFillRemarks);
                return;
            }

            if (Utils.IsNullOrEmpty(SelectedSource.Address))
            {
                NoticeHandler.Instance.Enqueue(ResUI.FillServerAddressCustom);
                return;
            }
            SelectedSource.CoreType = CoreType.IsNullOrEmpty() ? null : (ECoreType)Enum.Parse(typeof(ECoreType), CoreType);

            if (await ConfigHandler.EditCustomServer(_config, SelectedSource) == 0)
            {
                NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
                _updateView?.Invoke(EViewAction.CloseWindow, null);
            }
            else
            {
                NoticeHandler.Instance.Enqueue(ResUI.OperationFailed);
            }
        }

        public async Task BrowseServer(string fileName)
        {
            if (Utils.IsNullOrEmpty(fileName))
            {
                return;
            }

            var item = await AppHandler.Instance.GetProfileItem(SelectedSource.IndexId);
            item ??= SelectedSource;
            item.Address = fileName;
            if (await ConfigHandler.AddCustomServer(_config, item, false) == 0)
            {
                NoticeHandler.Instance.Enqueue(ResUI.SuccessfullyImportedCustomServer);
                if (Utils.IsNotEmpty(item.IndexId))
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
            var address = SelectedSource.Address;
            if (Utils.IsNullOrEmpty(address))
            {
                NoticeHandler.Instance.Enqueue(ResUI.FillServerAddressCustom);
                return;
            }

            address = Utils.GetConfigPath(address);
            if (File.Exists(address))
            {
                ProcUtils.ProcessStart(address);
            }
            else
            {
                NoticeHandler.Instance.Enqueue(ResUI.FailedReadConfiguration);
            }
        }
    }
}