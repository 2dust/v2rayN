using System;
using System.Collections.Generic;
using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.Base;
using v2rayN.HttpProxyHandler;
using v2rayN.Mode;
using System.Diagnostics;

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

            InitKCP();

            InitGUI();
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

            chkdefAllowInsecure.Checked = config.defAllowInsecure;

            txtsystemProxyExceptions.Text = config.systemProxyExceptions;
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

            chkIgnoreGeoUpdateCore.Checked = config.ignoreGeoUpdateCore;
            cmbCoreType.SelectedIndex = (int)config.coreType;
            txtautoUpdateInterval.Text = config.autoUpdateInterval.ToString();
            chkEnableAutoAdjustMainLvColWidth.Checked = config.uiItem.enableAutoAdjustMainLvColWidth;
        }
        private void btnOK_Click(object sender, EventArgs e)
        {
            if (SaveBase() != 0)
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

            var remoteDNS = txtremoteDNS.Text.TrimEx();
            var obj = Utils.ParseJson(remoteDNS);
            if (obj != null && obj.ContainsKey("servers"))
            {
            }
            else
            {
                if (remoteDNS.Contains("{") || remoteDNS.Contains("}"))
                {
                    UI.Show(UIRes.I18N("FillCorrectDNSText"));
                    return -1;
                }
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

            config.defAllowInsecure = chkdefAllowInsecure.Checked;

            config.systemProxyExceptions = txtsystemProxyExceptions.Text.TrimEx();

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

            config.allowLANConn = chkAllowLANConn.Checked;

            bool lastEnableStatistics = config.enableStatistics;
            config.enableStatistics = chkEnableStatistics.Checked;
            config.statisticsFreshRate = (int)cbFreshrate.SelectedValue;
            config.keepOlderDedupl = chkKeepOlderDedupl.Checked;

            config.ignoreGeoUpdateCore = chkIgnoreGeoUpdateCore.Checked;
            config.coreType = (ECoreType)cmbCoreType.SelectedIndex;
            config.autoUpdateInterval = Utils.ToInt(txtautoUpdateInterval.Text);
            config.uiItem.enableAutoAdjustMainLvColWidth = chkEnableAutoAdjustMainLvColWidth.Checked;

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

        private void linkDnsObjectDoc_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://www.v2fly.org/config/dns.html#dnsobject");
        }

        private void btnSetLoopback_Click(object sender, EventArgs e)
        {
            Process.Start(Utils.GetPath("EnableLoopback.exe"));
        }
    }
}
