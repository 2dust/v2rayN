using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using System.Windows;
using v2rayN.Handler;
using v2rayN.Models;
using v2rayN.Resx;

namespace v2rayN.ViewModels
{
    public class SubEditViewModel : ReactiveObject
    {
        private static Config _config;
        private NoticeHandler? _noticeHandler;
        private Window _view;

        [Reactive]
        public SubItem SelectedSource { get; set; }

        public ReactiveCommand<Unit, Unit> SaveCmd { get; }

        public SubEditViewModel(SubItem subItem, Window view)
        {
            _config = LazyConfig.Instance.GetConfig();
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            _view = view;

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

            Utils.SetDarkBorder(view, _config.uiItem.colorModeDark);
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
                _view.DialogResult = true;
                //_view?.Close();
            }
            else
            {
                _noticeHandler?.Enqueue(ResUI.OperationFailed);
            }
        }
    }
}