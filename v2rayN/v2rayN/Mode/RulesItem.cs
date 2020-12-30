using System;
using System.Collections.Generic;

namespace v2rayN.Mode
{
    [Serializable]
    public class RulesItem
    {
         public string remarks { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string type { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string port { get; set; }

        public List<string> inboundTag { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string outboundTag { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public List<string> ip { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public List<string> domain { get; set; }
    }

}
