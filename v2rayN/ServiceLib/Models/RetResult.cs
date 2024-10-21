namespace ServiceLib.Models
{
    public class RetResult
    {
        public int Code { get; set; }
        public string? Msg { get; set; }
        public object? Data { get; set; }

        public RetResult(int code)
        {
            Code = code;
        }
    }
}