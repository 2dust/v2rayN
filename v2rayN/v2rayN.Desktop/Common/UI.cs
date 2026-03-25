using Avalonia.Platform.Storage;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;

namespace v2rayN.Desktop.Common;

internal class UI
{
    private static readonly string caption = Global.AppName;

    public static async Task<ButtonResult> ShowYesNo(Window owner, string msg)
    {
        var messageBoxParams = new MessageBoxStandardParams
        {
            ContentTitle = caption,
            ContentMessage = msg,
            ButtonDefinitions = ButtonEnum.YesNo,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Topmost = false,
            SystemDecorations = Utils.IsLinux() ? SystemDecorations.BorderOnly : SystemDecorations.Full
        };
        var box = MessageBoxManager.GetMessageBoxStandard(messageBoxParams);
        return await box.ShowWindowDialogAsync(owner);
    }

    public static async Task<string?> OpenFileDialog(Window owner, FilePickerFileType? filter)
    {
        var sp = GetStorageProvider(owner);
        if (sp is null)
        {
            return null;
        }

        // Start async operation to open the dialog.
        var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = filter is null ? [FilePickerFileTypes.All, FilePickerFileTypes.ImagePng] : [filter]
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public static async Task<string?> SaveFileDialog(Window owner, string filter)
    {
        var sp = GetStorageProvider(owner);
        if (sp is null)
        {
            return null;
        }

        // Start async operation to open the dialog.
        var files = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
        });

        return files?.TryGetLocalPath();
    }

    private static IStorageProvider? GetStorageProvider(Window owner)
    {
        var topLevel = TopLevel.GetTopLevel(owner);
        return topLevel?.StorageProvider;
    }
}
