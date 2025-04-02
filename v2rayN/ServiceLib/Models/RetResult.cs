namespace ServiceLib.Models;

public class RetResult
{
    public bool Success { get; set; }
    public string? Msg { get; set; }
    public object? Data { get; set; }

    public RetResult(bool success = false)
    {
        Success = success;
    }

    public RetResult(bool success, string? msg)
    {
        Success = success;
        Msg = msg;
    }

    public RetResult(bool success, string? msg, object? data)
    {
        Success = success;
        Msg = msg;
        Data = data;
    }
}
