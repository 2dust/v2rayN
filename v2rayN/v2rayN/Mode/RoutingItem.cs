using System;
using System.Collections.Generic;

namespace v2rayN.Mode
{
    [Serializable]
    public class RoutingItem
    {
        public string remarks
        {
            get; set;
        }
        public string url
        {
            get; set;
        }
        public List<RulesItem> rules
        {
            get; set;
        }
        public bool enabled { get; set; } = true;

        public bool locked
        {
            get; set;
        }
        public string customIcon
        {
            get; set;
        }
    }
}
