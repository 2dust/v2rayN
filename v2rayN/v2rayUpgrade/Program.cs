using System;
using System.Windows.Forms;

namespace v2rayUpgrade
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length <= 0)
            {
                MessageBox.Show("Please use v2rayN to upgrade(请用v2rayN升级)");
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args));
        }
    }
}
