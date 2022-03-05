﻿using System.Collections.Generic;

namespace v2rayN.Mode
{
    /// <summary>
    /// Tcp伪装http的Request，只要Host
    /// </summary>
    public class V2rayTcpRequest
    {
        /// <summary>
        /// 
        /// </summary>
        public RequestHeaders headers { get; set; }
    }

    public class RequestHeaders
    {
        /// <summary>
        /// 
        /// </summary>
        public List<string> Host { get; set; }
    }


}
