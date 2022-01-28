using System;

namespace v2rayN.Mode
{
    [Serializable]
    class TestItem
    {
        public int selected
        {
            get; set;
        }
        public string indexId
        {
            get; set;
        }
        public string address
        {
            get; set;
        }
        public int port
        {
            get; set;
        }
        public int configType
        {
            get; set;
        }
    }
}
