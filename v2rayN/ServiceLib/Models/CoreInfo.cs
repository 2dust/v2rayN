namespace ServiceLib.Models;

[Serializable]
public class CoreInfo
{
    public ECoreType CoreType { get; set; }
    public List<string>? CoreExes { get; set; }
    public string? Arguments { get; set; }
    public string? Url { get; set; }
    public string? ReleaseApiUrl { get; set; }
    public string? DownloadUrlWin64 { get; set; }
    public string? DownloadUrlWinArm64 { get; set; }
    public string? DownloadUrlLinux64 { get; set; }
    public string? DownloadUrlLinuxArm64 { get; set; }
    public string? DownloadUrlOSX64 { get; set; }
    public string? DownloadUrlOSXArm64 { get; set; }
    public string? Match { get; set; }
    public string? VersionArg { get; set; }
    public bool AbsolutePath { get; set; }
}
