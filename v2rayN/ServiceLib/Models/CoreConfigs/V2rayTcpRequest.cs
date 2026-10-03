namespace ServiceLib.Models.CoreConfigs;

public class V2rayTcpRequest
{
    /// <summary>
    ///
    /// </summary>
    public RequestHeaders headers { get; set; }
}

public class RequestHeaders
{
    /// <summary>
    ///
    /// </summary>
    public List<string> Host { get; set; }
}
