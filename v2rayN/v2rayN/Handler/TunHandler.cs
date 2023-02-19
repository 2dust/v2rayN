using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Base
{
    public sealed class TunHandler
    {
        private static readonly Lazy<TunHandler> _instance = new(() => new());
        public static TunHandler Instance => _instance.Value;
        private string _tunConfigName = "tunConfig.json";
        private static Config _config;
        private CoreInfo coreInfo;
        private Process? _process;
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

            if (socksPort == _socksPort
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
                CoreStartTest();
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
            //Template
            string configStr = Utils.GetEmbedText(Global.TunSingboxFileName);
            if (!Utils.IsNullOrEmpty(_config.tunModeItem.customTemplate) && File.Exists(_config.tunModeItem.customTemplate))
            {
                var customTemplate = File.ReadAllText(_config.tunModeItem.customTemplate);
                if (!Utils.IsNullOrEmpty(customTemplate))
                {
                    configStr = customTemplate;
                }
            }
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

            //logs
            configStr = configStr.Replace("$log_disabled$", $"{(!_config.tunModeItem.enabledLog).ToString().ToLower()}");
            if (_config.tunModeItem.showWindow)
            {
                configStr = configStr.Replace("$log_output$", $"");
            }
            else
            {
                var dtNow = DateTime.Now;
                var log_output = $"\"output\": \"{Utils.GetLogPath($"singbox_{dtNow:yyyy-MM-dd}.txt")}\", ";
                configStr = configStr.Replace("$log_output$", $"{log_output.Replace(@"\", @"\\")}");
            }

            //port
            configStr = configStr.Replace("$socksPort$", $"{_socksPort}");

            //exe
            List<string> lstDnsExe = new();
            List<string> lstDirectExe = new();
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
                        //lstDnsExe.Add(it2);
                        lstDnsExe.Add($"{it2}.exe");
                    }

                    if (!lstDirectExe.Contains(it2))
                    {
                        //lstDirectExe.Add(it2);
                        lstDirectExe.Add($"{it2}.exe");
                    }
                }
            }
            string strDns = string.Join("\",\"", lstDnsExe.ToArray());
            configStr = configStr.Replace("$dnsProcessName$", $"\"{strDns}\"");

            string strDirect = string.Join("\",\"", lstDirectExe.ToArray());
            configStr = configStr.Replace("$directProcessName$", $"\"{strDirect}\"");

            if (_config.tunModeItem.bypassMode)
            {
                //direct ips
                if (_config.tunModeItem.directIP != null && _config.tunModeItem.directIP.Count > 0)
                {
                    var ips = new { outbound = "direct", ip_cidr = _config.tunModeItem.directIP };
                    configStr = configStr.Replace("$ruleDirectIPs$", "," + Utils.ToJson(ips));
                }
                //direct process
                if (_config.tunModeItem.directProcess != null && _config.tunModeItem.directProcess.Count > 0)
                {
                    var process = new { outbound = "direct", process_name = _config.tunModeItem.directProcess };
                    configStr = configStr.Replace("$ruleDirectProcess$", "," + Utils.ToJson(process));
                }
            }
            else
            {
                //proxy ips
                if (_config.tunModeItem.proxyIP != null && _config.tunModeItem.proxyIP.Count > 0)
                {
                    var ips = new { outbound = "proxy", ip_cidr = _config.tunModeItem.proxyIP };
                    configStr = configStr.Replace("$ruleProxyIPs$", "," + Utils.ToJson(ips));
                }
                //proxy process
                if (_config.tunModeItem.proxyProcess != null && _config.tunModeItem.proxyProcess.Count > 0)
                {
                    var process = new { outbound = "proxy", process_name = _config.tunModeItem.proxyProcess };
                    configStr = configStr.Replace("$ruleProxyProcess$", "," + Utils.ToJson(process));
                }

                var final = new { outbound = "direct", inbound = "tun-in" };
                configStr = configStr.Replace("$ruleFinally$", "," + Utils.ToJson(final));
            }
            configStr = configStr.Replace("$ruleDirectIPs$", "");
            configStr = configStr.Replace("$ruleDirectProcess$", "");
            configStr = configStr.Replace("$ruleProxyIPs$", "");
            configStr = configStr.Replace("$ruleProxyProcess$", "");
            configStr = configStr.Replace("$ruleFinally$", "");


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
                    _needRestart = true;
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
                string msg = string.Format(ResUI.NotFoundCore, Utils.GetBinPath("", coreInfo.coreType), string.Join(", ", coreInfo.coreExes.ToArray()), coreInfo.coreUrl);
                Utils.SaveLog(msg);
            }
            return fileName;
        }

        private void CoreStart()
        {
            try
            {
                string fileName = CoreFindexe();
                if (Utils.IsNullOrEmpty(fileName))
                {
                    return;
                }
                var showWindow = _config.tunModeItem.showWindow;
                Process p = new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = $"run -c \"{Utils.GetConfigPath(_tunConfigName)}\"",
                        WorkingDirectory = Utils.GetConfigPath(),
                        UseShellExecute = showWindow,
                        CreateNoWindow = !showWindow,
                        //RedirectStandardError = !showWindow,
                        Verb = "runas",
                    }
                };
                p.Start();
                _process = p;
                _isRunning = true;
                if (p.WaitForExit(1000))
                {
                    //if (showWindow)
                    //{
                    throw new Exception("start tun mode fail");
                    //}
                    //else
                    //{
                    //    throw new Exception(p.StandardError.ReadToEnd());
                    //}
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

        private int CoreStartTest()
        {
            Utils.SaveLog("Tun mode configuration file test start");
            try
            {
                string fileName = CoreFindexe();
                if (fileName == "")
                {
                    return -1;
                }
                Process p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = $"run -c \"{Utils.GetConfigPath(_tunConfigName)}\"",
                        WorkingDirectory = Utils.GetConfigPath(),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        Verb = "runas",
                    }
                };
                p.Start();
                if (p.WaitForExit(2000))
                {
                    throw new Exception(p.StandardError.ReadToEnd());
                }
                KillProcess(p);
                return 0;
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                return -1;
            }
            finally
            {
                Utils.SaveLog("Tun mode configuration file test end");
            }
        }
    }
}