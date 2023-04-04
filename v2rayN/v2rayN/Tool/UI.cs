using System.Windows;

namespace v2rayN
{
    class UI
    {
        private readonly static string caption = "v2rayN";

        public static void Show(string msg)
        {
            MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
        }
        public static void ShowWarning(string msg)
        {
            MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK);
        }
        public static MessageBoxResult ShowYesNo(string msg)
        {
            return MessageBox.Show(msg, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
        }
    }
}
