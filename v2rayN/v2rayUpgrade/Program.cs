namespace v2rayUpgrade
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
                Console.WriteLine("Please run it from the main application.(请从主应用运行)");
                Thread.Sleep(5000);
                return;
            }

            var fileName = Uri.UnescapeDataString(string.Join(" ", args));
            Upgrade.UpgradeApp(fileName);
        }
    }
}