using System;
using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.Mode;

namespace v2rayN.Forms {
    public partial class GithubSettingsForm : BaseForm {
        public GithubSettingsForm() {
            InitializeComponent();
            BindingConfig();
        }

        /// <summary>
        /// 绑定数据
        /// </summary>
        private void BindingConfig() {
            var config = githubRemoteStorageConfig;
            txtUsername.Text = config?.userName ?? "";
            txtRepoName.Text = config?.repoName ?? "";
            txtRemotePath.Text = config?.path ?? "";
            txtToken.Text = config?.token ?? "";
        }

        /// <summary>
        /// 清除设置
        /// </summary>
        private void ClearServer() {
            txtUsername.Text = "";
        }

        private void btnOK_Click(object sender, EventArgs e) {
            //vmessItem.remarks = remarks;
            githubRemoteStorageConfig.userName = txtUsername.Text.Trim();
            githubRemoteStorageConfig.repoName = txtRepoName.Text.Trim();
            githubRemoteStorageConfig.path = txtRemotePath.Text.Trim();
            githubRemoteStorageConfig.token = txtToken.Text.Trim();
            if (ConfigHandler.SaveGithubRemoteStorageConfig(githubRemoteStorageConfig) == 0) {
                this.DialogResult = DialogResult.OK;
            }
            else {
                UI.ShowWarning(UIRes.I18N("OperationFailed"));
            }
        }

        private void btnClose_Click(object sender, EventArgs e) {
            this.DialogResult = DialogResult.Cancel;
        }
    }
}
