namespace ServiceLib.Models.Dto;

public class RetResult(bool success, string? msg, object? data)
{
    public RetResult(bool success = false) : this(success, null, null)
    {
    }

    public RetResult(bool success, string? msg) : this(success, msg, null)
    {
    }

    public bool Success { get; set; } = success;
    public string? Msg { get; set; } = msg;
    public object? Data { get; set; } = data;
}
