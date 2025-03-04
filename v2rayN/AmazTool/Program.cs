namespace AmazTool
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine(Resx.Resource.Guidelines);
                Thread.Sleep(5000);
                return;
            }

            var argData = Uri.UnescapeDataString(string.Join(" ", args));
            if (argData.Equals("rebootas"))
            {
                Thread.Sleep(1000);
                Utils.StartV2RayN();
                return;
            }

            UpgradeApp.Upgrade(argData);
        }
    }
}
