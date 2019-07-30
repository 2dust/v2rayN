using System;

namespace v2rayN.Mode
{
    [Serializable]
    class VmessQRCode
    {
        /// <summary>
        /// 版本
        /// </summary>
        public string v { get; set; }
        /// <summary>
        /// 备注
        /// </summary>
        public string ps { get; set; }
        /// <summary>
        /// 远程服务器地址
        /// </summary>
        public string add { get; set; }
        /// <summary>
        /// 远程服务器端口
        /// </summary>
        public string port { get; set; }
        /// <summary>
        /// 远程服务器ID
        /// </summary>
        public string id { get; set; }
        /// <summary>
        /// 远程服务器额外ID
        /// </summary>
        public string aid { get; set; }
        /// <summary>
        /// 传输协议tcp,kcp,ws
        /// </summary>
        public string net { get; set; }
        /// <summary>
        /// 伪装类型
        /// </summary>
        public string type { get; set; }
        /// <summary>
        /// 伪装的域名
        /// </summary>
        public string host { get; set; }
        /// <summary>
        /// path
        /// </summary>
        public string path { get; set; }
        /// <summary>
        /// 底层传输安全
        /// </summary>
        public string tls { get; set; }
    }
}
