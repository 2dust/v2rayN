namespace v2rayN.Web.Api;

public sealed class DeleteAfterFileResult(string filePath) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await Results.File(stream, "application/zip", Path.GetFileName(filePath), enableRangeProcessing: false)
                .ExecuteAsync(httpContext);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
