namespace ServiceLib.Tests.Services;

public class SpeedTestProgressTests
{
    [Test]
    [Arguments(0, 10, 0)]
    [Arguments(1, 10, 10)]
    [Arguments(5, 10, 50)]
    [Arguments(10, 10, 100)]
    public async Task Percent_ShouldReflectCompletedShare(int completed, int total, double expected)
    {
        await new SpeedTestProgress { Completed = completed, Total = total }.Percent.Should().BeEqualTo(expected);
    }

    [Test]
    [Arguments(0, 0)]
    [Arguments(3, 0)]
    public async Task Percent_ShouldBeZeroWithoutProfiles(int completed, int total)
    {
        await new SpeedTestProgress { Completed = completed, Total = total }.Percent.Should().BeEqualTo(0);
    }

    [Test]
    public async Task Percent_ShouldNeverExceedHundred()
    {
        await new SpeedTestProgress { Completed = 12, Total = 10 }.Percent.Should().BeEqualTo(100);
        await new SpeedTestProgress { Completed = -3, Total = 10 }.Percent.Should().BeEqualTo(0);
    }
}