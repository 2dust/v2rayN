using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;

namespace ServiceLib.ViewModels
{
    public class SubEditViewModel : MyReactiveObject
    {
        [Reactive]
        public SubItem SelectedSource { get; set; }

        public ReactiveCommand<Unit, Unit> SaveCmd { get; }

        public SubEditViewModel(SubItem subItem, Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _config = AppHandler.Instance.Config;

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
                SaveSubAsync();
            });
        }

        private async Task SaveSubAsync()
        {
            string remarks = SelectedSource.remarks;
            if (Utils.IsNullOrEmpty(remarks))
            {
                NoticeHandler.Instance.Enqueue(ResUI.PleaseFillRemarks);
                return;
            }

            if (ConfigHandler.AddSubItem(_config, SelectedSource) == 0)
            {
                NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
                await _updateView?.Invoke(EViewAction.CloseWindow, null);
            }
            else
            {
                NoticeHandler.Instance.Enqueue(ResUI.OperationFailed);
            }
        }
    }
}