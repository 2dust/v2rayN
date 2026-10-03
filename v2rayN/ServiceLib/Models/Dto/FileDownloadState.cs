namespace ServiceLib.Models.Dto;

public record FileDownloadState
{
    public required FileDownloadRequest Request { get; init; }
    public long DownloadedBytes { get; init; } = 0;
    public long TotalBytes { get; init; } = 0;
    public double SpeedBytesPerSecond { get; init; } = 0;
    public bool Completed { get; init; } = false;

    public Exception? Error { get; init; }
    public bool IsFailed => Error != null;
}

public record FileDownloadRequest
{
    public required string FileUrl { get; init; }
    public required string FilePath { get; init; }
    public string? DisplayFileName { get; init; }

    public string FileName => DisplayFileName ?? Path.GetFileName(FilePath);
}
