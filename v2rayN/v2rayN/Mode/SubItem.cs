using SQLite;

namespace v2rayN.Mode
{
    [Serializable]
    public class SubItem
    {
        [PrimaryKey]
        public string id
        {
            get; set;
        }

        /// <summary>
        /// 备注
        /// </summary>
        public string remarks
        {
            get; set;
        }

        /// <summary>
        /// url
        /// </summary>
        public string url
        {
            get; set;
        }

        /// <summary>
        /// enable
        /// </summary>
        public bool enabled { get; set; } = true;

        /// <summary>
        /// 
        /// </summary>
        public string userAgent
        {
            get; set;
        } = string.Empty;


        public int sort
        {
            get; set;
        }
        public string filter { get; set; }

    }
}
