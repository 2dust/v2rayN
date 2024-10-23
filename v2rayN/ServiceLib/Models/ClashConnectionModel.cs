namespace ServiceLib.Models
{
    public class ClashConnectionModel
    {
        public string? Id { get; set; }
        public string? Network { get; set; }
        public string? Type { get; set; }
        public string? Host { get; set; }
        public ulong Upload { get; set; }
        public ulong Download { get; set; }
        public string? UploadTraffic { get; set; }
        public string? DownloadTraffic { get; set; }
        public double Time { get; set; }
        public string? Elapsed { get; set; }
        public string? Chain { get; set; }
    }
}