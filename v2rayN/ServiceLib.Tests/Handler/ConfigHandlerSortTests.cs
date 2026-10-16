namespace ServiceLib.Tests.Handler;

public class ConfigHandlerSortTests
{
    private static readonly List<ProfileItemModel> _profiles =
    [
        new() { IndexId = "a", GoogleRegion = "FI" },
        new() { IndexId = "b", GoogleRegion = Global.None },
        new() { IndexId = "c", GoogleRegion = "DE" },
        new() { IndexId = "d", GoogleRegion = "" },
        new() { IndexId = "e", GoogleRegion = ResUI.SpeedtestingSkip },
        new() { IndexId = "f", GoogleRegion = "US" },
    ];

    [Test]
    public async Task SortByTestResult_Ascending_ShouldPutEmptyResultsLast()
    {
        var result = Sort(asc: true);

        await Order(result).Should().BeEqualTo("c,a,f,b,d,e");
    }

    [Test]
    public async Task SortByTestResult_Descending_ShouldPutEmptyResultsLast()
    {
        var result = Sort(asc: false);

        await Order(result).Should().BeEqualTo("f,a,c,b,d,e");
    }

    [Test]
    [Arguments("")]
    [Arguments(Global.None)]
    [Arguments("  ")]
    public async Task IsEmptyTestResult_ShouldDetectMissingResults(string value)
    {
        await IsEmpty(value).Should().BeTrue();
    }

    [Test]
    public async Task IsEmptyTestResult_ShouldTreatSkipMessageAsNoResult()
    {
        await IsEmpty(ResUI.SpeedtestingSkip).Should().BeTrue();
    }

    [Test]
    public async Task IsEmptyTestResult_ShouldKeepRealResults()
    {
        await IsEmpty("FI").Should().BeFalse();
    }

    private static string Order(List<ProfileItemModel> items) => string.Join(",", items.Select(t => t.IndexId));

    private static List<ProfileItemModel> Sort(bool asc)
    {
        var method = typeof(ConfigHandler).GetMethod("SortByTestResult", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (List<ProfileItemModel>)method.Invoke(null, [_profiles, (Func<ProfileItemModel, string>)(t => t.GoogleRegion), asc])!;
    }

    private static bool IsEmpty(string value)
    {
        var method = typeof(ConfigHandler).GetMethod("IsEmptyTestResult", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (bool)method.Invoke(null, [value])!;
    }
}