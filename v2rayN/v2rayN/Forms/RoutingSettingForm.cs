using System;
using System.Collections.Generic;
using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class RoutingSettingForm : BaseForm
    {
        List<RoutingSettingControl> lstControls = new List<RoutingSettingControl>();

        public RoutingSettingForm()
        {
            InitializeComponent();
        }

        private void RoutingSettingForm_Load(object sender, EventArgs e)
        {
            cmbdomainStrategy.Text = config.domainStrategy;

            if (config.routingItem == null)
            {
                config.routingItem = new List<RoutingItem>();
            }

            RefreshSubsView();
        }

        /// <summary>
        /// 刷新列表
        /// </summary>
        private void RefreshSubsView()
        {
            panCon.Controls.Clear();
            lstControls.Clear();

            for (int k = config.routingItem.Count - 1; k >= 0; k--)
            {
                RoutingItem item = config.routingItem[k];
                if (Utils.IsNullOrEmpty(item.remarks))
                {
                    config.routingItem.RemoveAt(k);
                }
            }

            foreach (RoutingItem item in config.routingItem)
            {
                RoutingSettingControl control = new RoutingSettingControl();
                control.OnButtonClicked += Control_OnButtonClicked;
                control.routingItem = item;
                control.Dock = DockStyle.Top;

                panCon.Controls.Add(control);
                panCon.Controls.SetChildIndex(control, 0);

                lstControls.Add(control);
            }
        }

        private void Control_OnButtonClicked(object sender, EventArgs e)
        {
            RefreshSubsView();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (config.routingItem.Count <= 0)
            {
                AddSub("proxy", "");
            }
            if (ConfigHandler.SaveRoutingItem(ref config) == 0)
            {
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                UI.ShowWarning(UIRes.I18N("OperationFailed"));
            }

        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            AddSub("proxy", "");

            RefreshSubsView();
        }


        private void AddSub(string outboundTag, string userRule, string routingMode = "0")
        {
            RoutingItem RoutingItem = new RoutingItem
            {
                remarks = outboundTag,
                routingMode = routingMode,
                outboundTag = outboundTag,
                userRules = Utils.String2List(userRule)

            };
            config.routingItem.Add(RoutingItem);
        }


        private void btnSetDefRountingRule_Click(object sender, EventArgs e)
        {
            config.routingItem.Clear();

            List<string> lstTag = new List<string>
            {
                Global.agentTag,
                Global.directTag,
                Global.blockTag
            };
            for (int k = 0; k < lstTag.Count; k++)
            {
                DownloadHandle downloadHandle = new DownloadHandle();

                string result = downloadHandle.WebDownloadStringSync(Global.CustomRoutingListUrl + lstTag[k]);
                if (Utils.IsNullOrEmpty(result))
                {
                    result = Utils.GetEmbedText(Global.CustomRoutingFileName + lstTag[k]);
                }
                AddSub(lstTag[k], result);
            }

            AddSub(Global.directTag, "", "4");
            AddSub(Global.agentTag, "", "0");

            RefreshSubsView();
        }

        private void linkLabelRoutingDoc_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.v2fly.org/config/routing.html");
        }
    }
}
