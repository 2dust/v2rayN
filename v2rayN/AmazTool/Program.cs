namespace AmazTool
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Utils.WriteLine(Resx.Resource.Guidelines);
                Utils.Waiting(5);
                return;
            }

            var argData = Uri.UnescapeDataString(string.Join(" ", args));
            if (argData.Equals("rebootas"))
            {
                Utils.Waiting(1);
                Utils.StartV2RayN();
                return;
            }
           
            var tryTimes = 0;
            UpgradeApp.Init();
            while (tryTimes++ < 3)
            {
                if (!UpgradeApp.Upgrade(argData))
                {
                    continue;
                }

                Utils.WriteLine(Resx.Resource.Restartv2rayN);
                Utils.Waiting(3);
                Utils.StartV2RayN();
                break;
            }
        }
    }
}
