using System.Windows;

namespace v2rayN.Views
{
    public partial class PrivacyNoticeWindow : Window
    {
        public PrivacyNoticeWindow()
        {
            InitializeComponent();
        }

        private void btnAccept_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
