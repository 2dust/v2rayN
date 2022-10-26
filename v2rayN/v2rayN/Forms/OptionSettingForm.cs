using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using v2rayN.Base;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.Resx;

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
            cmbSystemProxyAdvancedProtocol.Items.AddRange(Global.IEProxyProtocols.ToArray());
           
            InitBase();

            InitKCP();

            InitGUI();

            InitCoreType();
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
                chkAllowLANConn.Checked = config.inbound[0].allowLANConn;
                txtuser.Text = config.inbound[0].user;
                txtpass.Text = config.inbound[0].pass;

            }

            //remoteDNS
            txtremoteDNS.Text = config.remoteDNS;
            cmbdomainStrategy4Freedom.Text = config.domainStrategy4Freedom;

            chkdefAllowInsecure.Checked = config.defAllowInsecure;

            txtsystemProxyExceptions.Text = config.systemProxyExceptions;

            cmbSystemProxyAdvancedProtocol.Text = config.systemProxyAdvancedProtocol;
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

            chkEnableStatistics.Checked = config.enableStatistics;
            numStatisticsFreshRate.Value = config.statisticsFreshRate;
            chkKeepOlderDedupl.Checked = config.keepOlderDedupl;

            chkIgnoreGeoUpdateCore.Checked = config.ignoreGeoUpdateCore;
            chkEnableAutoAdjustMainLvColWidth.Checked = config.uiItem.enableAutoAdjustMainLvColWidth;
            chkEnableSecurityProtocolTls13.Checked = config.enableSecurityProtocolTls13;

            txtautoUpdateInterval.Text = config.autoUpdateInterval.ToString();
            txtautoUpdateSubInterval.Text = config.autoUpdateSubInterval.ToString();
            chkEnableCheckPreReleaseUpdate.Checked = config.checkPreReleaseUpdate;
            txttrayMenuServersLimit.Text = config.trayMenuServersLimit.ToString();
        }

        private void InitCoreType()
        {
            if (config.coreTypeItem == null)
            {
                config.coreTypeItem = new List<CoreTypeItem>();
            }

            foreach (EConfigType it in Enum.GetValues(typeof(EConfigType)))
            {
                if (config.coreTypeItem.FindIndex(t => t.configType == it) >= 0)
                {
                    continue;
                }

                config.coreTypeItem.Add(new CoreTypeItem()
                {
                    configType = it,
                    coreType = ECoreType.Xray
                });
            }
            for (int k = 1; k <= config.coreTypeItem.Count; k++)
            {
                var item = config.coreTypeItem[k - 1];
                ((ComboBox)tabPageCoreType.Controls[$"cmbCoreType{k}"]).Items.AddRange(Global.coreTypes.ToArray());
                tabPageCoreType.Controls[$"labCoreType{k}"].Text = item.configType.ToString();
                tabPageCoreType.Controls[$"cmbCoreType{k}"].Text = item.coreType.ToString();
            }
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

            if (SaveCoreType() != 0)
            {
                return;
            }

            if (ConfigHandler.SaveConfig(ref config) == 0)
            {
                DialogResult = DialogResult.OK;
            }
            else
            {
                UI.ShowWarning(ResUI.OperationFailed);
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
            bool allowLANConn = chkAllowLANConn.Checked;
            if (Utils.IsNullOrEmpty(localPort) || !Utils.IsNumberic(localPort))
            {
                UI.Show(ResUI.FillLocalListeningPort);
                return -1;
            }
            if (Utils.IsNullOrEmpty(protocol))
            {
                UI.Show(ResUI.PleaseSelectProtocol);
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
                    UI.Show(ResUI.FillCorrectDNSText);
                    return -1;
                }
            }

            config.inbound[0].localPort = Utils.ToInt(localPort);
            config.inbound[0].protocol = protocol;
            config.inbound[0].udpEnabled = udpEnabled;
            config.inbound[0].sniffingEnabled = sniffingEnabled;
            config.inbound[0].allowLANConn = allowLANConn;
            config.inbound[0].user = txtuser.Text;
            config.inbound[0].pass = txtpass.Text;

            if (config.inbound.Count > 1)
            {
                config.inbound.RemoveAt(1);
            }


            //日志     
            config.logEnabled = logEnabled;
            config.loglevel = loglevel;

            //Mux
            config.muxEnabled = muxEnabled;

            //remoteDNS          
            config.remoteDNS = txtremoteDNS.Text.TrimEx();
            config.domainStrategy4Freedom = cmbdomainStrategy4Freedom.Text;

            config.defAllowInsecure = chkdefAllowInsecure.Checked;

            config.systemProxyExceptions = txtsystemProxyExceptions.Text.TrimEx();

            config.systemProxyAdvancedProtocol = cmbSystemProxyAdvancedProtocol.Text.TrimEx();

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
                UI.Show(ResUI.FillKcpParameters);
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

            bool lastEnableStatistics = config.enableStatistics;
            config.enableStatistics = chkEnableStatistics.Checked;
            config.statisticsFreshRate = Convert.ToInt32(numStatisticsFreshRate.Value);
            if (config.statisticsFreshRate > 100 || config.statisticsFreshRate < 1)
            {
                config.statisticsFreshRate = 1;
            }

            config.keepOlderDedupl = chkKeepOlderDedupl.Checked;

            config.ignoreGeoUpdateCore = chkIgnoreGeoUpdateCore.Checked;
            config.uiItem.enableAutoAdjustMainLvColWidth = chkEnableAutoAdjustMainLvColWidth.Checked;
            config.enableSecurityProtocolTls13 = chkEnableSecurityProtocolTls13.Checked;

            config.autoUpdateInterval = Utils.ToInt(txtautoUpdateInterval.Text);
            config.autoUpdateSubInterval = Utils.ToInt(txtautoUpdateSubInterval.Text);
            config.checkPreReleaseUpdate = chkEnableCheckPreReleaseUpdate.Checked;
            config.trayMenuServersLimit = Utils.ToInt(txttrayMenuServersLimit.Text);
            return 0;
        }

        private int SaveCoreType()
        {
            for (int k = 1; k <= config.coreTypeItem.Count; k++)
            {
                var item = config.coreTypeItem[k - 1];
                item.coreType = (ECoreType)Enum.Parse(typeof(ECoreType), tabPageCoreType.Controls[$"cmbCoreType{k}"].Text);
            }

            return 0;
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
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
