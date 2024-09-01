using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceLib.Models
{
    public class CheckUpdateItem
    {
        public bool? isSelected { get; set; }
        public string coreType { get; set; }
        public string? remarks { get; set; }
        public string? fileName { get; set; }
        public bool? isFinished { get; set; }
    }
}
