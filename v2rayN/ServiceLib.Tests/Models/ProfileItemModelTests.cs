namespace ServiceLib.Tests.Models;

public class ProfileItemModelTests
{
    [Test]
    public async Task Delay_ShouldNotifyOnlyWhenTheDisplayedValueChanges()
    {
        var model = new ProfileItemModel();
        var changes = new List<(string? Name, int Value)>();
        model.PropertyChanged += (_, args) => changes.Add((args.PropertyName, model.Delay));

        model.Delay = 123;
        model.Delay = 123;
        model.Delay = 0;

        await changes.SequenceEqual(new[]
        {
            (nameof(ProfileItemModel.Delay), 123),
            (nameof(ProfileItemModel.Delay), 0),
        }).Should().BeTrue();
    }

    [Test]
    public async Task SpeedVal_ShouldNotifyWhenTheDisplayedValueIsCleared()
    {
        var model = new ProfileItemModel();
        var changes = new List<(string? Name, string? Value)>();
        model.PropertyChanged += (_, args) => changes.Add((args.PropertyName, model.SpeedVal));

        model.SpeedVal = "1 MB/s";
        model.SpeedVal = "1 MB/s";
        model.SpeedVal = null;
        model.SpeedVal = null;

        await changes.SequenceEqual(new (string?, string?)[]
        {
            (nameof(ProfileItemModel.SpeedVal), "1 MB/s"),
            (nameof(ProfileItemModel.SpeedVal), null),
        }).Should().BeTrue();
    }
}
