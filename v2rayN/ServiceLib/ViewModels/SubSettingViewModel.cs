using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;

namespace ServiceLib.ViewModels
{
    public class SubSettingViewModel : MyReactiveObject
    {
        private IObservableCollection<SubItem> _subItems = new ObservableCollectionExtended<SubItem>();
        public IObservableCollection<SubItem> SubItems => _subItems;

        [Reactive]
        public SubItem SelectedSource { get; set; }

        public IList<SubItem> SelectedSources { get; set; }

        public ReactiveCommand<Unit, Unit> SubAddCmd { get; }
        public ReactiveCommand<Unit, Unit> SubDeleteCmd { get; }
        public ReactiveCommand<Unit, Unit> SubEditCmd { get; }
        public ReactiveCommand<Unit, Unit> SubShareCmd { get; }
        public bool IsModified { get; set; }

        public SubSettingViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _config = AppHandler.Instance.Config;

            _updateView = updateView;

            SelectedSource = new();

            RefreshSubItems();

            var canEditRemove = this.WhenAnyValue(
               x => x.SelectedSource,
               selectedSource => selectedSource != null && !selectedSource.id.IsNullOrEmpty());

            SubAddCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await EditSubAsync(true);
            });
            SubDeleteCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await DeleteSubAsync();
            }, canEditRemove);
            SubEditCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await EditSubAsync(false);
            }, canEditRemove);
            SubShareCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await _updateView?.Invoke(EViewAction.ShareSub, SelectedSource?.url);
            }, canEditRemove);
        }

        public void RefreshSubItems()
        {
            _subItems.Clear();
            _subItems.AddRange(AppHandler.Instance.SubItems().OrderBy(t => t.sort));
        }

        public async Task EditSubAsync(bool blNew)
        {
            SubItem item;
            if (blNew)
            {
                item = new();
            }
            else
            {
                item = AppHandler.Instance.GetSubItem(SelectedSource?.id);
                if (item is null)
                {
                    return;
                }
            }
            if (await _updateView?.Invoke(EViewAction.SubEditWindow, item) == true)
            {
                RefreshSubItems();
                IsModified = true;
            }
        }

        private async Task DeleteSubAsync()
        {
            if (await _updateView?.Invoke(EViewAction.ShowYesNo, null) == false)
            {
                return;
            }

            foreach (var it in SelectedSources ?? [SelectedSource])
            {
                ConfigHandler.DeleteSubItem(_config, it.id);
            }
            RefreshSubItems();
            NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
            IsModified = true;
        }
    }
}