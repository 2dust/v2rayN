using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using v2rayN.Base;
using v2rayN.Enums;
using v2rayN.Handler;
using v2rayN.Models;
using v2rayN.Resx;

namespace v2rayN.ViewModels
{
    public class SubEditViewModel : MyReactiveObject
    {
        [Reactive]
        public SubItem SelectedSource { get; set; }

        public ReactiveCommand<Unit, Unit> SaveCmd { get; }

        public SubEditViewModel(SubItem subItem, Func<EViewAction, bool>? updateView)
        {
            _config = LazyConfig.Instance.GetConfig();
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            _updateView = updateView;

            if (subItem.id.IsNullOrEmpty())
            {
                SelectedSource = subItem;
            }
            else
            {
                SelectedSource = JsonUtils.DeepCopy(subItem);
            }

            SaveCmd = ReactiveCommand.Create(() =>
            {
                SaveSub();
            });
        }

        private void SaveSub()
        {
            string remarks = SelectedSource.remarks;
            if (Utils.IsNullOrEmpty(remarks))
            {
                _noticeHandler?.Enqueue(ResUI.PleaseFillRemarks);
                return;
            }

            var item = LazyConfig.Instance.GetSubItem(SelectedSource.id);
            if (item is null)
            {
                item = SelectedSource;
            }
            else
            {
                item.remarks = SelectedSource.remarks;
                item.url = SelectedSource.url;
                item.moreUrl = SelectedSource.moreUrl;
                item.enabled = SelectedSource.enabled;
                item.autoUpdateInterval = SelectedSource.autoUpdateInterval;
                item.userAgent = SelectedSource.userAgent;
                item.sort = SelectedSource.sort;
                item.filter = SelectedSource.filter;
                item.convertTarget = SelectedSource.convertTarget;
                item.prevProfile = SelectedSource.prevProfile;
                item.nextProfile = SelectedSource.nextProfile;
            }

            if (ConfigHandler.AddSubItem(_config, item) == 0)
            {
                _noticeHandler?.Enqueue(ResUI.OperationSuccess);
                _updateView?.Invoke(EViewAction.CloseWindow);
            }
            else
            {
                _noticeHandler?.Enqueue(ResUI.OperationFailed);
            }
        }
    }
}