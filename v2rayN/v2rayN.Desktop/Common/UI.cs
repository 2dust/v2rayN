using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace v2rayN.Desktop.Common
{
    internal class UI
    {
        private static readonly string caption = Global.AppName;

        public static async Task<ButtonResult> ShowYesNo(Window owner, string msg)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(caption, msg, ButtonEnum.YesNo);
            return await box.ShowWindowDialogAsync(owner);
        }

        public static async Task<string?> OpenFileDialog(Window owner, FilePickerFileType? filter)
        {
            var topLevel = TopLevel.GetTopLevel(owner);
            if (topLevel == null)
            {
                return null;
            }
            // Start async operation to open the dialog.
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = filter is null ? [FilePickerFileTypes.All, FilePickerFileTypes.ImagePng] : [filter]
            });

            return files.FirstOrDefault()?.TryGetLocalPath();
        }

        public static async Task<string?> SaveFileDialog(Window owner, string filter)
        {
            var topLevel = TopLevel.GetTopLevel(owner);
            if (topLevel == null)
            {
                return null;
            }
            // Start async operation to open the dialog.
            var files = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
            });

            return files?.TryGetLocalPath();
        }
    }
}