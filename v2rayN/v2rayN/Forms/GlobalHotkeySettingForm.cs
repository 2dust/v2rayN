using System;
using System.Collections.Generic;
using System.Windows.Forms;
using v2rayN.Base;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Forms
{
    public partial class GlobalHotkeySettingForm : BaseForm
    {
        List<KeyEventItem> lstKey;
        public GlobalHotkeySettingForm()
        {
            InitializeComponent();
        }

        private void GlobalHotkeySettingForm_Load(object sender, EventArgs e)
        {
            if (config.globalHotkeys == null)
            {
                config.globalHotkeys = new List<KeyEventItem>();
            }

            foreach (EGlobalHotkey it in Enum.GetValues(typeof(EGlobalHotkey)))
            {
                if (config.globalHotkeys.FindIndex(t => t.eGlobalHotkey == it) >= 0)
                {
                    continue;
                }

                config.globalHotkeys.Add(new KeyEventItem()
                {
                    eGlobalHotkey = it,
                    Alt = false,
                    Control = false,
                    Shift = false,
                    KeyCode = null
                });
            }

            lstKey = Utils.DeepCopy(config.globalHotkeys);

            txtGlobalHotkey0.KeyDown += TxtGlobalHotkey_KeyDown;
            txtGlobalHotkey1.KeyDown += TxtGlobalHotkey_KeyDown;
            txtGlobalHotkey2.KeyDown += TxtGlobalHotkey_KeyDown;
            txtGlobalHotkey3.KeyDown += TxtGlobalHotkey_KeyDown;

            BindingData(-1);
        }

        private void TxtGlobalHotkey_KeyDown(object sender, KeyEventArgs e)
        {
            var txt = ((TextBox)sender);
            var index = Utils.ToInt(txt.Name.Substring(txt.Name.Length - 1, 1));

            lstKey[index].KeyCode = e.KeyCode;
            lstKey[index].Alt = e.Alt;
            lstKey[index].Control = e.Control;
            lstKey[index].Shift = e.Shift;

            BindingData(index);
        }

        private void BindingData(int index)
        {
            for (int k = 0; k < lstKey.Count; k++)
            {
                if (index >= 0 && index != k)
                {
                    continue;
                }
                var item = lstKey[k];
                var keys = string.Empty;

                if (item.Control)
                {
                    keys += $"{Keys.Control.ToString()} + ";
                }
                if (item.Alt)
                {
                    keys += $"{Keys.Alt.ToString()} + ";
                }
                if (item.Shift)
                {
                    keys += $"{Keys.Shift.ToString()} + ";
                }
                if (item.KeyCode != null)
                {
                    keys += $"{item.KeyCode.ToString()}";
                }

                panel1.Controls[$"txtGlobalHotkey{k}"].Text = keys;
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            config.globalHotkeys = lstKey;

            if (ConfigHandler.SaveConfig(ref config, false) == 0)
            {
                DialogResult = DialogResult.OK;
            }
            else
            {
                UI.ShowWarning(ResUI.OperationFailed);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            lstKey.Clear();
            foreach (EGlobalHotkey it in Enum.GetValues(typeof(EGlobalHotkey)))
            {
                if (lstKey.FindIndex(t => t.eGlobalHotkey == it) >= 0)
                {
                    continue;
                }

                lstKey.Add(new KeyEventItem()
                {
                    eGlobalHotkey = it,
                    Alt = false,
                    Control = false,
                    Shift = false,
                    KeyCode = null
                });
            }
            BindingData(-1);
        }
    }
}
