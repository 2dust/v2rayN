using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace v2rayN.Mode {
  public class GithubRemoteStorageConfig {
    /// <summary>
    /// 用户名
    /// </summary>
    public string userName { get; set; }
    /// <summary>
    /// 仓库名称
    /// </summary>
    public string repoName { get; set; }
    /// <summary>
    /// 远端配置文件路径
    /// </summary>
    public string path { get; set; }
    /// <summary>
    /// 令牌
    /// </summary>
    public string token { get; set; }

    /// <summary>
    /// 配置验证通过
    /// </summary>
    public bool IsConfigVerificationPassed {
      get {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(repoName) || string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(token)) {
          return false;
        }
        return true;
      }
    }
  }
}
