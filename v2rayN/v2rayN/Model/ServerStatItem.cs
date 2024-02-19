using SQLite;

namespace v2rayN.Model
{
    [Serializable]
    public class ServerStatItem
    {
        [PrimaryKey]
        public string indexId
        {
            get; set;
        }

        public long totalUp
        {
            get; set;
        }

        public long totalDown
        {
            get; set;
        }

        public long todayUp
        {
            get; set;
        }

        public long todayDown
        {
            get; set;
        }

        public long dateNow
        {
            get; set;
        }
    }
}