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

            SaveCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await SaveSubAsync();
            });

            SelectedSource = subItem.id.IsNullOrEmpty() ? subItem : JsonUtils.DeepCopy(subItem);
        }

        private async Task SaveSubAsync()
        {
            var remarks = SelectedSource.remarks;
            if (Utils.IsNullOrEmpty(remarks))
            {
                NoticeHandler.Instance.Enqueue(ResUI.PleaseFillRemarks);
                return;
            }

            if (await ConfigHandler.AddSubItem(_config, SelectedSource) == 0)
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