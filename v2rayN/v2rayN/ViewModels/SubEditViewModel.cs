using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using v2rayN.Base;
using v2rayN.Handler;

namespace v2rayN.ViewModels
{
    public class SubEditViewModel : MyReactiveObject
    {
        [Reactive]
        public SubItem SelectedSource { get; set; }

        public ReactiveCommand<Unit, Unit> SaveCmd { get; }

        public SubEditViewModel(SubItem subItem, Func<EViewAction, object?, bool>? updateView)
        {
            _config = LazyConfig.Instance.Config;
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

            if (ConfigHandler.AddSubItem(_config, SelectedSource) == 0)
            {
                _noticeHandler?.Enqueue(ResUI.OperationSuccess);
                _updateView?.Invoke(EViewAction.CloseWindow, null);
            }
            else
            {
                _noticeHandler?.Enqueue(ResUI.OperationFailed);
            }
        }
    }
}