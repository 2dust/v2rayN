using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using v2rayN.Handler;
using v2rayN.Mode;

namespace v2rayN.Base
{
    public sealed class TunHandler
    {
        private static readonly Lazy<TunHandler> _instance = new Lazy<TunHandler>(() => new());
        public static TunHandler Instance => _instance.Value;
        private string _tunConfigName = "tunConfig.json";
        private static Config _config;
        private CoreInfo coreInfo;
        private Process _process;
        private static int _socksPort;
        private static bool _needRestart = true;
        private static bool _isRunning = false;

        public TunHandler()
        {
            _config = LazyConfig.Instance.GetConfig();

            Observable.Interval(TimeSpan.FromSeconds(10))
                 .Subscribe(x =>
                 {
                     if (_isRunning && _config.tunModeItem.enableTun)
                     {
                         if (_process == null || _process.HasExited)
                         {
                             if (Init() == false)
                             {
                                 return;
                             }
                             CoreStart();
                             Utils.SaveLog("Tun mode monitors restart");
                         }
                     }
                 });
        }

        public void Start()
        {
            var socksPort = LazyConfig.Instance.GetLocalPort(Global.InboundSocks);

            if (socksPort.Equals(_socksPort)
                && _process != null
                && !_process.HasExited)
            {
                _needRestart = false;
            }

            _socksPort = socksPort;

            if (_needRestart)
            {
                CoreStop();
                if (Init() == false)
                {
                    return;
                }
                CoreStart();
            }
        }

        public void Stop()
        {
            CoreStop();
        }

        private bool Init()
        {
            coreInfo = LazyConfig.Instance.GetCoreInfo(ECoreType.sing_box);
            string configStr = Utils.GetEmbedText(Global.TunSingboxFileName);
            if (Utils.IsNullOrEmpty(configStr))
            {
                return false;
            }

            //settings
            if (_config.tunModeItem.mtu <= 0)
            {
                _config.tunModeItem.mtu = Convert.ToInt32(Global.TunMtus[0]);
            }
            if (Utils.IsNullOrEmpty(_config.tunModeItem.stack))
            {
                _config.tunModeItem.stack = Global.TunStacks[0];
            }
            configStr = configStr.Replace("$mtu$", $"{_config.tunModeItem.mtu}");
            configStr = configStr.Replace("$strict_route$", $"{_config.tunModeItem.strictRoute.ToString().ToLower()}");
            configStr = configStr.Replace("$stack$", $"{_config.tunModeItem.stack}");


            //port
            configStr = configStr.Replace("$socksPort$", $"{_socksPort}");

            //exe
            List<string> lstDnsExe = new List<string>();
            List<string> lstDirectExe = new List<string>();
            var coreInfos = LazyConfig.Instance.GetCoreInfos();
            foreach (var it in coreInfos)
            {
                if (it.coreType == ECoreType.v2rayN)
                {
                    continue;
                }
                foreach (var it2 in it.coreExes)
                {
                    if (!lstDnsExe.Contains(it2) && it.coreType != ECoreType.sing_box)
                    {
                        lstDnsExe.Add(it2);
                        lstDnsExe.Add($"{it2}.exe");
                    }

                    if (!lstDirectExe.Contains(it2))
                    {
                        lstDirectExe.Add(it2);
                        lstDirectExe.Add($"{it2}.exe");
                    }
                }
            }
            string strDns = string.Join("\",\"", lstDnsExe.ToArray());
            configStr = configStr.Replace("$dnsProcessName$", $"\"{strDns}\"");

            string strDirect = string.Join("\",\"", lstDirectExe.ToArray());
            configStr = configStr.Replace("$directProcessName$", $"\"{strDirect}\"");


            //ips
            if (_config.tunModeItem.directIP != null && _config.tunModeItem.directIP.Count > 0)
            {
                var ips = new { outbound = "direct", ip_cidr = _config.tunModeItem.directIP };
                configStr = configStr.Replace("$ruleDirectIPs$", "," + Utils.ToJson(ips));
            }
            else
            {
                configStr = configStr.Replace("$ruleDirectIPs$", "");
            }
            //process
            if (_config.tunModeItem.directProcess != null && _config.tunModeItem.directProcess.Count > 0)
            {
                var process = new { outbound = "direct", process_name = _config.tunModeItem.directProcess };
                configStr = configStr.Replace("$ruleDirectProcess$", "," + Utils.ToJson(process));
            }
            else
            {
                configStr = configStr.Replace("$ruleDirectProcess$", "");
            }

            File.WriteAllText(Utils.GetConfigPath(_tunConfigName), configStr);

            return true;
        }

        private void CoreStop()
        {
            try
            {
                _isRunning = false;
                if (_process != null)
                {
                    KillProcess(_process);
                    _process.Dispose();
                    _process = null;
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        private string CoreFindexe()
        {
            string fileName = string.Empty;
            foreach (string name in coreInfo.coreExes)
            {
                string vName = $"{name}.exe";
                vName = Utils.GetBinPath(vName, coreInfo.coreType);
                if (File.Exists(vName))
                {
                    fileName = vName;
                    break;
                }
            }
            if (Utils.IsNullOrEmpty(fileName))
            {

            }
            return fileName;
        }

        private void CoreStart()
        {
            try
            {
                string fileName = CoreFindexe();
                if (fileName == "")
                {
                    return;
                }
                var showWindow = _config.tunModeItem.showWindow;
                Process p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = $"run -c \"{Utils.GetConfigPath(_tunConfigName)}\"",
                        WorkingDirectory = Utils.GetConfigPath(),
                        UseShellExecute = showWindow,
                        CreateNoWindow = !showWindow,
                        RedirectStandardError = !showWindow,
                        Verb = "runas",
                    }
                };
                p.Start();
                _process = p;
                _isRunning = true;
                if (p.WaitForExit(1000))
                {
                    if (showWindow)
                    {
                        throw new Exception("start tun mode fail");
                    }
                    else
                    {
                        throw new Exception(p.StandardError.ReadToEnd());
                    }
                }

                Global.processJob.AddProcess(p.Handle);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
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