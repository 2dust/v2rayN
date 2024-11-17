namespace AmazTool
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine(LocalizationHelper.GetLocalizedValue("Guidelines"));
                Thread.Sleep(5000);
                return;
            }

            var fileName = Uri.UnescapeDataString(string.Join(" ", args));
            UpgradeApp.Upgrade(fileName);
        }
    }
}