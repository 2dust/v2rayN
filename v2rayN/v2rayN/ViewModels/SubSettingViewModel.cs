﻿using DynamicData;
using DynamicData.Binding;
using MaterialDesignThemes.Wpf;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using System.Windows;
using v2rayN.Handler;
using v2rayN.Model;
using v2rayN.Resx;
using v2rayN.Views;

namespace v2rayN.ViewModels
{
    public class SubSettingViewModel : ReactiveObject
    {
        private static Config _config;
        private NoticeHandler? _noticeHandler;

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

        public SubSettingViewModel(Window view)
        {
            _config = LazyConfig.Instance.GetConfig();
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();

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
                SubShare();
            }, canEditRemove);

            Utile.SetDarkBorder(view, _config.uiItem.colorModeDark);
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
            var ret = (new SubEditWindow(item)).ShowDialog();
            if (ret == true)
            {
                RefreshSubItems();
                IsModified = true;
            }
        }

        private void DeleteSub()
        {
            if (UI.ShowYesNo(ResUI.RemoveServer) == MessageBoxResult.No)
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

        private async void SubShare()
        {
            if (Utile.IsNullOrEmpty(SelectedSource?.url))
            {
                return;
            }
            var img = QRCodeHelper.GetQRCode(SelectedSource?.url);
            var dialog = new QrcodeView()
            {
                imgQrcode = { Source = img },
                txtContent = { Text = SelectedSource?.url },
            };

            await DialogHost.Show(dialog, "SubDialog");
        }
    }
}