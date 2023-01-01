namespace v2rayN.Mode
{
    [Serializable]
    class ServerTestItem
    {
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
        public EConfigType configType
        {
            get; set;
        }
        public bool allowTest
        {
            get; set;
        }
    }
}
