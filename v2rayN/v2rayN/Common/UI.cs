using Microsoft.Win32;
using System.Windows;

namespace v2rayN
{
    internal class UI
    {
        private static readonly string caption = Global.AppName;

        public static void Show(string msg)
        {
            MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
        }

        public static MessageBoxResult ShowYesNo(string msg)
        {
            return MessageBox.Show(msg, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
        }

        public static bool? OpenFileDialog(out string fileName, string filter)
        {
            fileName = string.Empty;

            var fileDialog = new OpenFileDialog
            {
                Multiselect = false,
                Filter = filter
            };

            if (fileDialog.ShowDialog() != true)
            {
                return false;
            }
            fileName = fileDialog.FileName;

            return true;
        }

        public static bool? SaveFileDialog(out string fileName, string filter)
        {
            fileName = string.Empty;

            SaveFileDialog fileDialog = new()
            {
                Filter = filter,
                FilterIndex = 2,
                RestoreDirectory = true
            };
            if (fileDialog.ShowDialog() != true)
            {
                return false;
            }

            fileName = fileDialog.FileName;

            return true;
        }
    }
}