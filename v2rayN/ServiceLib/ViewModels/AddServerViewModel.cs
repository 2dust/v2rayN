using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
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
            _config = LazyConfig.Instance.Config;
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
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
                _noticeHandler?.Enqueue(ResUI.PleaseFillRemarks);
                return;
            }

            if (Utils.IsNullOrEmpty(SelectedSource.address))
            {
                _noticeHandler?.Enqueue(ResUI.FillServerAddress);
                return;
            }
            var port = SelectedSource.port.ToString();
            if (Utils.IsNullOrEmpty(port) || !Utils.IsNumeric(port)
                || SelectedSource.port <= 0 || SelectedSource.port >= Global.MaxPort)
            {
                _noticeHandler?.Enqueue(ResUI.FillCorrectServerPort);
                return;
            }
            if (SelectedSource.configType == EConfigType.Shadowsocks)
            {
                if (Utils.IsNullOrEmpty(SelectedSource.id))
                {
                    _noticeHandler?.Enqueue(ResUI.FillPassword);
                    return;
                }
                if (Utils.IsNullOrEmpty(SelectedSource.security))
                {
                    _noticeHandler?.Enqueue(ResUI.PleaseSelectEncryption);
                    return;
                }
            }
            if (SelectedSource.configType != EConfigType.Socks
                && SelectedSource.configType != EConfigType.Http)
            {
                if (Utils.IsNullOrEmpty(SelectedSource.id))
                {
                    _noticeHandler?.Enqueue(ResUI.FillUUID);
                    return;
                }
            }
            SelectedSource.coreType = CoreType.IsNullOrEmpty() ? null : (ECoreType)Enum.Parse(typeof(ECoreType), CoreType);

            if (ConfigHandler.AddServer(_config, SelectedSource) == 0)
            {
                _noticeHandler?.Enqueue(ResUI.OperationSuccess);
                await _updateView?.Invoke(EViewAction.CloseWindow, null);
            }
            else
            {
                _noticeHandler?.Enqueue(ResUI.OperationFailed);
            }
        }
    }
}