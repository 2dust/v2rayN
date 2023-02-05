using SQLite;
using v2rayN.Base;

namespace v2rayN.Mode
{
    [Serializable]
    public class ProfileItem
    {
        public ProfileItem()
        {
            indexId = string.Empty;
            configType = EConfigType.VMess;
            configVersion = 2;
            sort = 0;
            address = string.Empty;
            port = 0;
            id = string.Empty;
            alterId = 0;
            security = string.Empty;
            network = string.Empty;
            remarks = string.Empty;
            headerType = string.Empty;
            requestHost = string.Empty;
            path = string.Empty;
            streamSecurity = string.Empty;
            allowInsecure = string.Empty;
            subid = string.Empty;
            flow = string.Empty;
        }

        #region function
        public string GetSummary()
        {
            string summary = string.Format("[{0}] ", (configType).ToString());
            string[] arrAddr = address.Split('.');
            string addr;
            if (arrAddr.Length > 2)
            {
                addr = $"{arrAddr[0]}***{arrAddr[arrAddr.Length - 1]}";
            }
            else if (arrAddr.Length > 1)
            {
                addr = $"***{arrAddr[arrAddr.Length - 1]}";
            }
            else
            {
                addr = address;
            }
            switch (configType)
            {
                case EConfigType.VMess:
                case EConfigType.Shadowsocks:
                case EConfigType.Socks:
                case EConfigType.VLESS:
                case EConfigType.Trojan:
                    summary += string.Format("{0}({1}:{2})", remarks, addr, port);
                    break;
                default:
                    summary += string.Format("{0}", remarks);
                    break;
            }
            return summary;
        }

        public List<string> GetAlpn()
        {
            if (Utils.IsNullOrEmpty(alpn))
            {
                return null;
            }
            else
            {
                return Utils.String2List(alpn);
            }
        }

        public string GetNetwork()
        {
            if (Utils.IsNullOrEmpty(network) || !Global.networks.Contains(network))
            {
                return Global.DefaultNetwork;
            }
            return network.TrimEx();
        }

        #endregion

        [PrimaryKey]
        public string indexId
        {
            get; set;
        }

        /// <summary>
        /// config type(1=normal,2=custom)
        /// </summary>
        public EConfigType configType
        {
            get; set;
        }

        /// <summary>
        /// 版本(现在=2)
        /// </summary>
        public int configVersion
        {
            get; set;
        }

        public int sort
        {
            get; set;
        }

        /// <summary>
        /// 远程服务器地址
        /// </summary>
        public string address
        {
            get; set;
        }
        /// <summary>
        /// 远程服务器端口
        /// </summary>
        public int port
        {
            get; set;
        }
        /// <summary>
        /// 远程服务器ID
        /// </summary>
        public string id
        {
            get; set;
        }
        /// <summary>
        /// 远程服务器额外ID
        /// </summary>
        public int alterId
        {
            get; set;
        }
        /// <summary>
        /// 本地安全策略
        /// </summary>
        public string security
        {
            get; set;
        }
        /// <summary>
        /// tcp,kcp,ws,h2,quic
        /// </summary>
        public string network
        {
            get; set;
        }
        /// <summary>
        /// 备注或别名
        /// </summary>
        public string remarks
        {
            get; set;
        }

        /// <summary>
        /// 伪装类型
        /// </summary>
        public string headerType
        {
            get; set;
        }

        /// <summary>
        /// 伪装的域名
        /// </summary>
        public string requestHost
        {
            get; set;
        }

        /// <summary>
        /// ws h2 path
        /// </summary>
        public string path
        {
            get; set;
        }

        /// <summary>
        /// 传输层安全
        /// </summary>
        public string streamSecurity
        {
            get; set;
        }

        /// <summary>
        /// 是否允许不安全连接（用于客户端）
        /// </summary>
        public string allowInsecure
        {
            get; set;
        }

        public int delay { get; set; }
        public decimal speed { get; set; }

        /// <summary>
        /// SubItem id
        /// </summary>
        public string subid
        {
            get; set;
        }
        public bool isSub { get; set; } = true;

        /// <summary>
        /// VLESS flow
        /// </summary>
        public string flow
        {
            get; set;
        }
        /// <summary>
        /// tls sni
        /// </summary>
        public string sni
        {
            get; set;
        }
        /// <summary>
        /// tls alpn
        /// </summary>
        public string alpn { get; set; } = string.Empty;


        public ECoreType? coreType
        {
            get; set;
        }

        public int preSocksPort
        {
            get; set;
        }

        public string fingerprint { get; set; }

        public bool displayLog { get; set; } = true;

        // TODO: change ProfileItem and other models to C# records
        public override bool Equals(object? obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ProfileItem)obj);
        }

        protected bool Equals(ProfileItem other) {
            return indexId == other.indexId && configType == other.configType && configVersion == other.configVersion &&
                   sort == other.sort && address == other.address && port == other.port && id == other.id &&
                   alterId == other.alterId && security == other.security && network == other.network &&
                   remarks == other.remarks && headerType == other.headerType && requestHost == other.requestHost &&
                   path == other.path && streamSecurity == other.streamSecurity && allowInsecure == other.allowInsecure &&
                   delay == other.delay && speed == other.speed && subid == other.subid && isSub == other.isSub &&
                   flow == other.flow && sni == other.sni && alpn == other.alpn && coreType == other.coreType &&
                   preSocksPort == other.preSocksPort && fingerprint == other.fingerprint && displayLog == other.displayLog;
        }

        public override int GetHashCode() {
            var hashCode = new HashCode();
            hashCode.Add(indexId);
            hashCode.Add((int)configType);
            hashCode.Add(configVersion);
            hashCode.Add(sort);
            hashCode.Add(address);
            hashCode.Add(port);
            hashCode.Add(id);
            hashCode.Add(alterId);
            hashCode.Add(security);
            hashCode.Add(network);
            hashCode.Add(remarks);
            hashCode.Add(headerType);
            hashCode.Add(requestHost);
            hashCode.Add(path);
            hashCode.Add(streamSecurity);
            hashCode.Add(allowInsecure);
            hashCode.Add(delay);
            hashCode.Add(speed);
            hashCode.Add(subid);
            hashCode.Add(isSub);
            hashCode.Add(flow);
            hashCode.Add(sni);
            hashCode.Add(alpn);
            hashCode.Add(coreType);
            hashCode.Add(preSocksPort);
            hashCode.Add(fingerprint);
            hashCode.Add(displayLog);
            return hashCode.ToHashCode();
        }
    }
}
