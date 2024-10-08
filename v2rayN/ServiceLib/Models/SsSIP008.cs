namespace ServiceLib.Models
{
    public class SsSIP008
    {
        public List<SsServer>? servers { get; set; }
    }

    [Serializable]
    public class SsServer
    {
        public string? remarks { get; set; }
        public string? server { get; set; }
        public string? server_port { get; set; }
        public string? method { get; set; }
        public string? password { get; set; }
        public string? plugin { get; set; }
    }
}