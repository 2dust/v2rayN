using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using v2rayN.Base;
using v2rayN.Handler;

namespace v2rayN.ViewModels
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

        public SubSettingViewModel(Func<EViewAction, object?, bool>? updateView)
        {
            _config = LazyConfig.Instance.Config;
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            _updateView = updateView;

            SelectedSource = new();

            RefreshSubItems();

            var canEditRemove = this.WhenAnyValue(
               x => x.SelectedSource,
               selectedSource => selectedSource != null && !selectedSource.id.IsNullOrEmpty());

            SubAddCmd = ReactiveCommand.Create(() =>
            {
                EditSub(true);
            });
            SubDeleteCmd = ReactiveCommand.Create(() =>
            {
                DeleteSub();
            }, canEditRemove);
            SubEditCmd = ReactiveCommand.Create(() =>
            {
                EditSub(false);
            }, canEditRemove);
            SubShareCmd = ReactiveCommand.Create(() =>
            {
                _updateView?.Invoke(EViewAction.ShareSub, SelectedSource?.url);
            }, canEditRemove);
        }

        public void RefreshSubItems()
        {
            _subItems.Clear();
            _subItems.AddRange(LazyConfig.Instance.SubItems().OrderBy(t => t.sort));
        }

        public void EditSub(bool blNew)
        {
            SubItem item;
            if (blNew)
            {
                item = new();
            }
            else
            {
                item = LazyConfig.Instance.GetSubItem(SelectedSource?.id);
                if (item is null)
                {
                    return;
                }
            }
            if (_updateView?.Invoke(EViewAction.SubEditWindow, item) == true)
            {
                RefreshSubItems();
                IsModified = true;
            }
        }

        private void DeleteSub()
        {
            if (_updateView?.Invoke(EViewAction.ShowYesNo, null) == false)
            {
                return;
            }

            foreach (var it in SelectedSources)
            {
                ConfigHandler.DeleteSubItem(_config, it.id);
            }
            RefreshSubItems();
            _noticeHandler?.Enqueue(ResUI.OperationSuccess);
            IsModified = true;
        }
    }
}