using System;
using System.Collections.Generic;
using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.Base;
using v2rayN.HttpProxyHandler;

namespace v2rayN.Forms
{
    public partial class OptionSettingForm : BaseForm
    {
        public OptionSettingForm()
        {
            InitializeComponent();
        }

        private void OptionSettingForm_Load(object sender, EventArgs e)
        {
            InitBase();

            InitRouting();

            InitKCP();

            InitGUI();

            InitUserPAC();
        }

        /// <summary>
        /// 初始化基础设置
        /// </summary>
        private void InitBase()
        {
            //日志
            chklogEnabled.Checked = config.logEnabled;
            cmbloglevel.Text = config.loglevel;

            //Mux
            chkmuxEnabled.Checked = config.muxEnabled;

            //本地监听
            if (config.inbound.Count > 0)
            {
                txtlocalPort.Text = config.inbound[0].localPort.ToString();
                cmbprotocol.Text = config.inbound[0].protocol.ToString();
                chkudpEnabled.Checked = config.inbound[0].udpEnabled;
                chksniffingEnabled.Checked = config.inbound[0].sniffingEnabled;

                txtlocalPort2.Text = $"{config.inbound[0].localPort + 1}";
                cmbprotocol2.Text = Global.InboundHttp;

                if (config.inbound.Count > 1)
                {
                    txtlocalPort2.Text = config.inbound[1].localPort.ToString();
                    cmbprotocol2.Text = config.inbound[1].protocol.ToString();
                    chkudpEnabled2.Checked = config.inbound[1].udpEnabled;
                    chksniffingEnabled2.Checked = config.inbound[1].sniffingEnabled;
                    chkAllowIn2.Checked = true;
                }
                else
                {
                    chkAllowIn2.Checked = false;
                }
                chkAllowIn2State();
            }

            //remoteDNS
            txtremoteDNS.Text = config.remoteDNS;

            cmblistenerType.SelectedIndex = (int)config.listenerType;
        }

        /// <summary>
        /// 初始化路由设置
        /// </summary>
        private void InitRouting()
        {
            //路由
            cmbdomainStrategy.Text = config.domainStrategy;
            int.TryParse(config.routingMode, out int routingMode);
            cmbroutingMode.SelectedIndex = routingMode;

            txtUseragent.Text = Utils.List2String(config.useragent, true);
            txtUserdirect.Text = Utils.List2String(config.userdirect, true);
            txtUserblock.Text = Utils.List2String(config.userblock, true);
        }

        /// <summary>
        /// 初始化KCP设置
        /// </summary>
        private void InitKCP()
        {
            txtKcpmtu.Text = config.kcpItem.mtu.ToString();
            txtKcptti.Text = config.kcpItem.tti.ToString();
            txtKcpuplinkCapacity.Text = config.kcpItem.uplinkCapacity.ToString();
            txtKcpdownlinkCapacity.Text = config.kcpItem.downlinkCapacity.ToString();
            txtKcpreadBufferSize.Text = config.kcpItem.readBufferSize.ToString();
            txtKcpwriteBufferSize.Text = config.kcpItem.writeBufferSize.ToString();
            chkKcpcongestion.Checked = config.kcpItem.congestion;
        }

        /// <summary>
        /// 初始化v2rayN GUI设置
        /// </summary>
        private void InitGUI()
        {
            //开机自动启动
            chkAutoRun.Checked = Utils.IsAutoRun();

            //自定义GFWList
            txturlGFWList.Text = config.urlGFWList;

            chkAllowLANConn.Checked = config.allowLANConn;
            chkEnableStatistics.Checked = config.enableStatistics;
            chkKeepOlderDedupl.Checked = config.keepOlderDedupl;




            ComboItem[] cbSource = new ComboItem[]
            {
                new ComboItem{ID = (int)Global.StatisticsFreshRate.quick, Text = UIRes.I18N("QuickFresh")},
                new ComboItem{ID = (int)Global.StatisticsFreshRate.medium, Text = UIRes.I18N("MediumFresh")},
                new ComboItem{ID = (int)Global.StatisticsFreshRate.slow, Text = UIRes.I18N("SlowFresh")},
            };
            cbFreshrate.DataSource = cbSource;

            cbFreshrate.DisplayMember = "Text";
            cbFreshrate.ValueMember = "ID";

            switch (config.statisticsFreshRate)
            {
                case (int)Global.StatisticsFreshRate.quick:
                    cbFreshrate.SelectedItem = cbSource[0];
                    break;
                case (int)Global.StatisticsFreshRate.medium:
                    cbFreshrate.SelectedItem = cbSource[1];
                    break;
                case (int)Global.StatisticsFreshRate.slow:
                    cbFreshrate.SelectedItem = cbSource[2];
                    break;
            }

        }

        private void InitUserPAC()
        {
            txtuserPacRule.Text = Utils.List2String(config.userPacRule, true);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (SaveBase() != 0)
            {
                return;
            }

            if (SaveRouting() != 0)
            {
                return;
            }

            if (SaveKCP() != 0)
            {
                return;
            }

            if (SaveGUI() != 0)
            {
                return;
            }

            if (SaveUserPAC() != 0)
            {
                return;
            }

            if (config.listenerType == ListenerType.HttpOpenAndClearOnce || config.listenerType == ListenerType.PacOpenAndClearOnce) {
                SysProxyHandle.ResetIEProxy();
            }
            if (ConfigHandler.SaveConfig(ref config) == 0)
            {
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                UI.ShowWarning(UIRes.I18N("OperationFailed"));
            }
        }

        /// <summary>
        /// 保存基础设置
        /// </summary>
        /// <returns></returns>
        private int SaveBase()
        {
            //日志
            bool logEnabled = chklogEnabled.Checked;
            string loglevel = cmbloglevel.Text.TrimEx();

            //Mux
            bool muxEnabled = chkmuxEnabled.Checked;

            //本地监听
            string localPort = txtlocalPort.Text.TrimEx();
            string protocol = cmbprotocol.Text.TrimEx();
            bool udpEnabled = chkudpEnabled.Checked;
            bool sniffingEnabled = chksniffingEnabled.Checked;
            if (Utils.IsNullOrEmpty(localPort) || !Utils.IsNumberic(localPort))
            {
                UI.Show(UIRes.I18N("FillLocalListeningPort"));
                return -1;
            }
            if (Utils.IsNullOrEmpty(protocol))
            {
                UI.Show(UIRes.I18N("PleaseSelectProtocol"));
                return -1;
            }
            config.inbound[0].localPort = Utils.ToInt(localPort);
            config.inbound[0].protocol = protocol;
            config.inbound[0].udpEnabled = udpEnabled;
            config.inbound[0].sniffingEnabled = sniffingEnabled;

            //本地监听2
            string localPort2 = txtlocalPort2.Text.TrimEx();
            string protocol2 = cmbprotocol2.Text.TrimEx();
            bool udpEnabled2 = chkudpEnabled2.Checked;
            bool sniffingEnabled2 = chksniffingEnabled2.Checked;
            if (chkAllowIn2.Checked)
            {
                if (Utils.IsNullOrEmpty(localPort2) || !Utils.IsNumberic(localPort2))
                {
                    UI.Show(UIRes.I18N("FillLocalListeningPort"));
                    return -1;
                }
                if (Utils.IsNullOrEmpty(protocol2))
                {
                    UI.Show(UIRes.I18N("PleaseSelectProtocol"));
                    return -1;
                }
                if (config.inbound.Count < 2)
                {
                    config.inbound.Add(new Mode.InItem());
                }
                config.inbound[1].localPort = Utils.ToInt(localPort2);
                config.inbound[1].protocol = protocol2;
                config.inbound[1].udpEnabled = udpEnabled2;
                config.inbound[1].sniffingEnabled = sniffingEnabled2;
            }
            else
            {
                if (config.inbound.Count > 1)
                {
                    config.inbound.RemoveAt(1);
                }
            }

            //日志     
            config.logEnabled = logEnabled;
            config.loglevel = loglevel;

            //Mux
            config.muxEnabled = muxEnabled;

            //remoteDNS
            config.remoteDNS = txtremoteDNS.Text.TrimEx();

            config.listenerType = (ListenerType)Enum.ToObject(typeof(ListenerType), cmblistenerType.SelectedIndex);

            return 0;
        }

        /// <summary>
        /// 保存路由设置
        /// </summary>
        /// <returns></returns>
        private int SaveRouting()
        {
            //路由            
            string domainStrategy = cmbdomainStrategy.Text;
            string routingMode = cmbroutingMode.SelectedIndex.ToString();

            string useragent = txtUseragent.Text.TrimEx();
            string userdirect = txtUserdirect.Text.TrimEx();
            string userblock = txtUserblock.Text.TrimEx();

            config.domainStrategy = domainStrategy;
            config.routingMode = routingMode;

            config.useragent = Utils.String2List(useragent);
            config.userdirect = Utils.String2List(userdirect);
            config.userblock = Utils.String2List(userblock);

            return 0;
        }

        /// <summary>
        /// 保存KCP设置
        /// </summary>
        /// <returns></returns>
        private int SaveKCP()
        {
            string mtu = txtKcpmtu.Text.TrimEx();
            string tti = txtKcptti.Text.TrimEx();
            string uplinkCapacity = txtKcpuplinkCapacity.Text.TrimEx();
            string downlinkCapacity = txtKcpdownlinkCapacity.Text.TrimEx();
            string readBufferSize = txtKcpreadBufferSize.Text.TrimEx();
            string writeBufferSize = txtKcpwriteBufferSize.Text.TrimEx();
            bool congestion = chkKcpcongestion.Checked;

            if (Utils.IsNullOrEmpty(mtu) || !Utils.IsNumberic(mtu)
                || Utils.IsNullOrEmpty(tti) || !Utils.IsNumberic(tti)
                || Utils.IsNullOrEmpty(uplinkCapacity) || !Utils.IsNumberic(uplinkCapacity)
                || Utils.IsNullOrEmpty(downlinkCapacity) || !Utils.IsNumberic(downlinkCapacity)
                || Utils.IsNullOrEmpty(readBufferSize) || !Utils.IsNumberic(readBufferSize)
                || Utils.IsNullOrEmpty(writeBufferSize) || !Utils.IsNumberic(writeBufferSize))
            {
                UI.Show(UIRes.I18N("FillKcpParameters"));
                return -1;
            }
            config.kcpItem.mtu = Utils.ToInt(mtu);
            config.kcpItem.tti = Utils.ToInt(tti);
            config.kcpItem.uplinkCapacity = Utils.ToInt(uplinkCapacity);
            config.kcpItem.downlinkCapacity = Utils.ToInt(downlinkCapacity);
            config.kcpItem.readBufferSize = Utils.ToInt(readBufferSize);
            config.kcpItem.writeBufferSize = Utils.ToInt(writeBufferSize);
            config.kcpItem.congestion = congestion;

            return 0;
        }

        /// <summary>
        /// 保存GUI设置
        /// </summary>
        /// <returns></returns>
        private int SaveGUI()
        {
            //开机自动启动
            Utils.SetAutoRun(chkAutoRun.Checked);

            //自定义GFWList
            config.urlGFWList = txturlGFWList.Text.TrimEx();

            config.allowLANConn = chkAllowLANConn.Checked;

            bool lastEnableStatistics = config.enableStatistics;
            config.enableStatistics = chkEnableStatistics.Checked;
            config.statisticsFreshRate = (int)cbFreshrate.SelectedValue;
            config.keepOlderDedupl = chkKeepOlderDedupl.Checked;

            //if(lastEnableStatistics != config.enableStatistics)
            //{
            //    /// https://stackoverflow.com/questions/779405/how-do-i-restart-my-c-sharp-winform-application
            //    // Shut down the current app instance.
            //    Application.Exit();

            //    // Restart the app passing "/restart [processId]" as cmd line args
            //    Process.Start(Application.ExecutablePath, "/restart " + Process.GetCurrentProcess().Id);
            //}
            return 0;
        }

        private int SaveUserPAC()
        {
            string userPacRule = txtuserPacRule.Text.TrimEx();
            userPacRule = userPacRule.Replace("\"", "");

            config.userPacRule = Utils.String2List(userPacRule);

            return 0;
        }
        private void btnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }

        private void chkAllowIn2_CheckedChanged(object sender, EventArgs e)
        {
            chkAllowIn2State();
        }
        private void chkAllowIn2State()
        {
            bool blAllow2 = chkAllowIn2.Checked;
            txtlocalPort2.Enabled =
            cmbprotocol2.Enabled =
            chkudpEnabled2.Enabled = blAllow2;
        }

        private void btnSetDefRountingRule_Click(object sender, EventArgs e)
        {
            txtUseragent.Text = Utils.GetEmbedText(Global.CustomRoutingFileName + Global.agentTag);
            txtUserdirect.Text = Utils.GetEmbedText(Global.CustomRoutingFileName + Global.directTag);
            txtUserblock.Text = Utils.GetEmbedText(Global.CustomRoutingFileName + Global.blockTag);
            cmbroutingMode.SelectedIndex = 3;

            List<string> lstUrl = new List<string>
            {
                Global.CustomRoutingListUrl + Global.agentTag,
                Global.CustomRoutingListUrl + Global.directTag,
                Global.CustomRoutingListUrl + Global.blockTag
            };

            List<TextBox> lstTxt = new List<TextBox>
            {
                txtUseragent,
                txtUserdirect,
                txtUserblock
            };

            for (int k = 0; k < lstUrl.Count; k++)
            {
                TextBox txt = lstTxt[k];
                DownloadHandle downloadHandle = new DownloadHandle();
                downloadHandle.UpdateCompleted += (sender2, args) =>
                {
                    if (args.Success)
                    {
                        string result = args.Msg;
                        if (Utils.IsNullOrEmpty(result))
                        {
                            return;
                        }
                        txt.Text = result;
                    }
                    else
                    {
                        AppendText(false, args.Msg);
                    }
                };
                downloadHandle.Error += (sender2, args) =>
                {
                    AppendText(true, args.GetException().Message);
                };

                downloadHandle.WebDownloadString(lstUrl[k]);
            }
        }
        void AppendText(bool notify, string text)
        {
            labRoutingTips.Text = text;
        }
    }

    class ComboItem
    {
        public int ID
        {
            get; set;
        }
        public string Text
        {
            get; set;
        }
    }
}
