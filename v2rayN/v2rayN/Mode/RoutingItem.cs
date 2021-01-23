using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace v2rayN.Mode
{
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

    }
}
