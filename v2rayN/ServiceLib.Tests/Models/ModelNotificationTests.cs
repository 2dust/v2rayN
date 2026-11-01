using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace ServiceLib.Tests.Models;

public class ModelNotificationTests
{
    [Test]
    public Task ProfileProperties_NotifyAfterChangesAndIgnoreEqualAssignments() => VerifyNotifications<ProfileItem>();

    [Test]
    public Task SubscriptionProperties_NotifyAfterChangesAndIgnoreEqualAssignments() => VerifyNotifications<SubItem>();

    [Test]
    public Task RoutingProperties_NotifyAfterChangesAndIgnoreEqualAssignments() => VerifyNotifications<RoutingItem>();

    [Test]
    public Task WebDavProperties_NotifyAfterChangesAndIgnoreEqualAssignments() => VerifyNotifications<WebDavItem>();

    private static async Task VerifyNotifications<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>() where T : new()
    {
        var modelType = typeof(T);
        var model = new T();
        await (model is INotifyPropertyChanged).Should().BeTrue();
        if (model is not INotifyPropertyChanged observable)
        {
            return;
        }

        var changes = new List<string?>();
        object? observedValue = null;
        PropertyInfo? currentProperty = null;
        observable.PropertyChanged += (_, args) =>
        {
            changes.Add(args.PropertyName);
            observedValue = currentProperty!.GetValue(model);
        };

        foreach (var property in modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            currentProperty = property;
            var previous = property.GetValue(model);
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            object updated = type == typeof(string) ? "updated"
                : type == typeof(bool) ? !Equals(previous, true)
                : type == typeof(int) ? (previous is int integer ? integer + 1 : 1)
                : type == typeof(long) ? (previous is long number ? number + 1 : 1L)
                : type.IsEnum ? Enum.ToObject(type, Equals(previous, Enum.ToObject(type, 1)) ? 2 : 1)
                : throw new InvalidOperationException($"No test value for {property.Name}.");

            changes.Clear();
            property.SetValue(model, updated);
            await changes.Should().HaveCount(1);
            await changes[0].Should().BeEqualTo(property.Name);
            await Equals(observedValue, updated).Should().BeTrue();

            property.SetValue(model, updated);
            await changes.Should().HaveCount(1);

            if (!property.PropertyType.IsValueType || Nullable.GetUnderlyingType(property.PropertyType) is not null)
            {
                changes.Clear();
                property.SetValue(model, null);
                await changes.Should().HaveCount(1);
                await changes[0].Should().BeEqualTo(property.Name);
                await observedValue.Should().BeNull();
                property.SetValue(model, null);
                await changes.Should().HaveCount(1);
            }
        }
    }

    [Test]
    public async Task NestedObservation_FollowsEditsAndReplacementAndStopsAfterDisposal()
    {
        var original = new ProfileItem { Remarks = "Original" };
        var editor = new ProfileEditor { SelectedSource = original };
        var observed = new List<string?>();
        using var subscription = editor.WhenAnyValue(x => x.SelectedSource.Remarks).Subscribe(new RecordingObserver(observed));

        original.Remarks = "Edited";
        original.Remarks = "Edited";
        editor.SelectedSource = new ProfileItem { Remarks = "Replacement" };
        original.Remarks = "No longer selected";
        editor.SelectedSource.Remarks = null!;
        subscription.Dispose();
        editor.SelectedSource.Remarks = "Disposed";

        await observed.SequenceEqual(["Original", "Edited", "Replacement", null]).Should().BeTrue();
    }

    private sealed class RecordingObserver(List<string?> values) : IObserver<string>
    {
        public void OnNext(string value) => values.Add(value);

        public void OnError(Exception error) => throw new InvalidOperationException("Unexpected observation failure.", error);

        public void OnCompleted()
        {
        }
    }
    public sealed class ProfileEditor : ReactiveObject
    {
        private ProfileItem _selectedSource = new();

        public ProfileItem SelectedSource
        {
            get => _selectedSource;
            set => this.RaiseAndSetIfChanged(ref _selectedSource, value);
        }
    }

    [Test]
    public async Task Notifications_DoNotChangeJsonOrSqliteColumns()
    {
        var item = new SubItem { Id = "subscription", Remarks = "Original", Url = "https://example.com" };
        var json = JsonUtils.Serialize(item, false);
        var count = 0;
        if (item is INotifyPropertyChanged observable)
        {
            observable.PropertyChanged += (_, _) => count++;
        }

        using var database = new SQLiteConnection(":memory:", false);
        database.CreateTable<SubItem>();
        database.Insert(item, typeof(SubItem));
        item.Remarks = "Edited";
        database.Update(item, typeof(SubItem));

        await count.Should().BeEqualTo(1);
        await database.Find<SubItem>(item.Id).Remarks.Should().BeEqualTo("Edited");
        var restored = JsonUtils.Deserialize<SubItem>(json)!;
        await restored.Remarks.Should().BeEqualTo("Original");
        await database.GetTableInfo(nameof(SubItem)).Count.Should().BeEqualTo(17);
        await JsonUtils.ParseJson(json)!.AsObject().Count.Should().BeEqualTo(8);
    }
}
