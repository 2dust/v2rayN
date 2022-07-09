﻿using System;
using System.Collections.Generic;

namespace v2rayN.Mode
{
    [Serializable]
    public class CoreInfo
    {
        public ECoreType coreType { get; set; }

        public List<string> coreExes { get; set; }

        public string arguments { get; set; }

        public string coreUrl { get; set; }

        public string coreLatestUrl { get; set; }

        public string coreDownloadUrl32 { get; set; }

        public string coreDownloadUrl64 { get; set; }

        public string match { get; set; }
    }
}
