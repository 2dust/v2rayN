using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using v2rayN.Forms;

namespace v2rayN
{
    static class Program
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (Environment.OSVersion.Version.Major >= 6)
            {
                SetProcessDPIAware();
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(Application_ThreadException);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;


            var args = Environment.GetCommandLineArgs();
            bool _isRestart = args.Length > 1 && args[1] == "/restart";
            if (_isRestart)
            {
                try
                {
                    // get old process and wait UP TO 5 secs then give up!
                    int _restartProcessId = int.Parse(args[2]);
                    Process oldProcess = Process.GetProcessById(_restartProcessId);
                    oldProcess.WaitForExit();
                }
                catch 
                {
                    // the process did not exist - probably already closed!
                    //TODO: --> LOG
                }
            }

            Process instance = RunningInstance();
            if (instance == null)
            {
                Utils.SaveLog("v2rayN start up");

                //设置语言环境
                string lang = Utils.RegReadValue(Global.MyRegPath, Global.MyRegKeyLanguage, "zh-Hans");
                System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(lang);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            else
            {
                UI.Show($"v2rayN is already running(v2rayN已经运行){args[1]} {args[2]}");
            }
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                string resourceName = "v2rayN." + new AssemblyName(args.Name).Name + ".dll";
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        return null;
                    }
                    byte[] assemblyData = new byte[stream.Length];
                    stream.Read(assemblyData, 0, assemblyData.Length);
                    return Assembly.Load(assemblyData);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary> 
        /// 获取正在运行的实例，没有运行的实例返回null; 
        /// </summary> 
        public static Process RunningInstance()
        {
            Process current = Process.GetCurrentProcess();
            Process[] processes = Process.GetProcessesByName(current.ProcessName);
            foreach (Process process in processes)
            {
                if (process.Id != current.Id)
                {
                    if (Assembly.GetExecutingAssembly().Location.Replace("/", "\\") == process.MainModule.FileName)
                    {
                        return process;
                    }
                }
            }
            return null;
        }

        static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            Utils.SaveLog("Application_ThreadException", e.Exception);
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Utils.SaveLog("CurrentDomain_UnhandledException", (Exception)e.ExceptionObject);
        }

    }
}
