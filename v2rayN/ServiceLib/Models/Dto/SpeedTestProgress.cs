namespace ServiceLib.Models.Dto;

/// <summary>
/// Progress of a speed test running over multiple profiles.
/// </summary>
public class SpeedTestProgress
{
    public int Total { get; set; }
    public int Completed { get; set; }
    public bool IsRunning { get; set; }

    public double Percent => Total > 0 ? Math.Clamp(Completed * 100.0 / Total, 0, 100) : 0;
}