using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;

namespace ServiceLib.ViewModels
{
    public class AddServerViewModel : MyReactiveObject
    {
        [Reactive]
        public ProfileItem SelectedSource { get; set; }

        [Reactive]
        public string? CoreType { get; set; }

        public ReactiveCommand<Unit, Unit> SaveCmd { get; }

        public AddServerViewModel(ProfileItem profileItem, Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _config = AppHandler.Instance.Config;

            _updateView = updateView;

            if (profileItem.indexId.IsNullOrEmpty())
            {
                profileItem.network = Global.DefaultNetwork;
                profileItem.headerType = Global.None;
                profileItem.requestHost = "";
                profileItem.streamSecurity = "";
                SelectedSource = profileItem;
            }
            else
            {
                SelectedSource = JsonUtils.DeepCopy(profileItem);
            }
            CoreType = SelectedSource?.coreType?.ToString();

            SaveCmd = ReactiveCommand.Create(() =>
            {
                SaveServerAsync();
            });
        }

        private async Task SaveServerAsync()
        {
            if (Utils.IsNullOrEmpty(SelectedSource.remarks))
            {
                NoticeHandler.Instance.Enqueue(ResUI.PleaseFillRemarks);
                return;
            }

            if (Utils.IsNullOrEmpty(SelectedSource.address))
            {
                NoticeHandler.Instance.Enqueue(ResUI.FillServerAddress);
                return;
            }
            var port = SelectedSource.port.ToString();
            if (Utils.IsNullOrEmpty(port) || !Utils.IsNumeric(port)
                || SelectedSource.port <= 0 || SelectedSource.port >= Global.MaxPort)
            {
                NoticeHandler.Instance.Enqueue(ResUI.FillCorrectServerPort);
                return;
            }
            if (SelectedSource.configType == EConfigType.Shadowsocks)
            {
                if (Utils.IsNullOrEmpty(SelectedSource.id))
                {
                    NoticeHandler.Instance.Enqueue(ResUI.FillPassword);
                    return;
                }
                if (Utils.IsNullOrEmpty(SelectedSource.security))
                {
                    NoticeHandler.Instance.Enqueue(ResUI.PleaseSelectEncryption);
                    return;
                }
            }
            if (SelectedSource.configType != EConfigType.SOCKS
                && SelectedSource.configType != EConfigType.HTTP)
            {
                if (Utils.IsNullOrEmpty(SelectedSource.id))
                {
                    NoticeHandler.Instance.Enqueue(ResUI.FillUUID);
                    return;
                }
            }
            SelectedSource.coreType = CoreType.IsNullOrEmpty() ? null : (ECoreType)Enum.Parse(typeof(ECoreType), CoreType);

            if (ConfigHandler.AddServer(_config, SelectedSource) == 0)
            {
                NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
                _updateView?.Invoke(EViewAction.CloseWindow, null);
            }
            else
            {
                NoticeHandler.Instance.Enqueue(ResUI.OperationFailed);
            }
        }
    }
}