using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using v2rayN.Mode;

namespace v2rayN.Handler
{

    /// <summary>
    /// 消息委托
    /// </summary>
    /// <param name="notify">是否显示在托盘区</param>
    /// <param name="msg">内容</param>
    public delegate void ProcessDelegate(bool notify, string msg);

    /// <summary>
    /// v2ray进程处理类
    /// </summary>
    class V2rayHandler
    {
        private static string v2rayConfigRes = Global.v2rayConfigFileName;
        private List<string> lstV2ray;
        public event ProcessDelegate ProcessEvent;
        //private int processId = 0;
        private Process _process;

        public V2rayHandler()
        {
            lstV2ray = new List<string>
            {
                "wv2ray",
                "v2ray"
            };
        }

        /// <summary>
        /// 载入V2ray
        /// </summary>
        public void LoadV2ray(Config config)
        {
            if (Global.reloadV2ray)
            {
                string fileName = Utils.GetPath(v2rayConfigRes);
                string msg;
                if (V2rayConfigHandler.GenerateClientConfig(config, fileName, false, out msg) != 0)
                {
                    ShowMsg(false, msg);
                }
                else
                {
                    ShowMsg(true, msg);
                    V2rayRestart();
                }
            }
        }

        /// <summary>
        /// 载入V2ray
        /// </summary>
        public void LoadV2ray(Config config, List<int> _selecteds)
        {
            if (Global.reloadV2ray)
            {
                string fileName = Utils.GetPath(v2rayConfigRes);
                string msg;
                if (V2rayConfigHandler.GenerateClientSpeedtestConfig(config, _selecteds, fileName, out msg) != 0)
                {
                    ShowMsg(false, msg);
                }
                else
                {
                    ShowMsg(true, msg);
                    V2rayRestart();
                }
            }
        }

        /// <summary>
        /// V2ray重启
        /// </summary>
        private void V2rayRestart()
        {
            V2rayStop();
            V2rayStart();
        }

        /// <summary>
        /// V2ray停止
        /// </summary>
        public void V2rayStop()
        {
            try
            {
                if (_process != null)
                {
                    KillProcess(_process);
                    _process.Dispose();
                    _process = null;
                }
                else
                {
                    foreach (string vName in lstV2ray)
                    {
                        Process[] existing = Process.GetProcessesByName(vName);
                        foreach (Process p in existing)
                        {
                            string path = p.MainModule.FileName;
                            if (path == $"{Utils.GetPath(vName)}.exe")
                            {
                                KillProcess(p);
                            }
                        }
                    }
                }

                //bool blExist = true;
                //if (processId > 0)
                //{
                //    Process p1 = Process.GetProcessById(processId);
                //    if (p1 != null)
                //    {
                //        p1.Kill();
                //        blExist = false;
                //    }
                //}
                //if (blExist)
                //{
                //    foreach (string vName in lstV2ray)
                //    {
                //        Process[] killPro = Process.GetProcessesByName(vName);
                //        foreach (Process p in killPro)
                //        {
                //            p.Kill();
                //        }
                //    }
                //}
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        /// <summary>
        /// V2ray启动
        /// </summary>
        private void V2rayStart()
        {
            ShowMsg(false, string.Format(UIRes.I18N("StartService"), DateTime.Now.ToString()));

            try
            {
                //查找v2ray文件是否存在
                string fileName = string.Empty;
                foreach (string name in lstV2ray)
                {
                    string vName = string.Format("{0}.exe", name);
                    vName = Utils.GetPath(vName);
                    if (File.Exists(vName))
                    {
                        fileName = vName;
                        break;
                    }
                }
                if (Utils.IsNullOrEmpty(fileName))
                {
                    string msg = string.Format(UIRes.I18N("NotFoundCore"), @"https://github.com/v2ray/v2ray-core/releases");
                    ShowMsg(true, msg);
                    return;
                }

                Process p = new Process();
                p.StartInfo.FileName = fileName;
                p.StartInfo.WorkingDirectory = Utils.StartupPath();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                p.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (!String.IsNullOrEmpty(e.Data))
                    {
                        string msg = e.Data + Environment.NewLine;
                        ShowMsg(false, msg);
                    }
                });
                p.Start();
                p.BeginOutputReadLine();
                //processId = p.Id;
                _process = p;

                Global.processJob.AddProcess(p.Handle);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                string msg = ex.Message;
                ShowMsg(true, msg);
            }
        }

        /// <summary>
        /// 消息委托
        /// </summary>
        /// <param name="updateToTrayTooltip">是否更新托盘图标的工具提示</param>
        /// <param name="msg">输出到日志框</param>
        private void ShowMsg(bool updateToTrayTooltip, string msg)
        {
            ProcessEvent?.Invoke(updateToTrayTooltip, msg);
        }

        private void KillProcess(Process p)
        {
            try
            {
                p.CloseMainWindow();
                p.WaitForExit(100);
                if (!p.HasExited)
                {
                    p.Kill();
                    p.WaitForExit(100);
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }         
    }
}
