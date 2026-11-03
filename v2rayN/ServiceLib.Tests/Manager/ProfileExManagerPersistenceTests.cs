namespace ServiceLib.Tests.Manager;

public class ProfileExManagerPersistenceTests
{
    [Test]
    public async Task SaveTo_NewProfilesWithRepeatedResults_ShouldPersistEveryProfile()
    {
        SQLiteHelper.Instance.CreateTable<ProfileExItem>();
        var manager = new ProfileExManager();
        var ids = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid().ToString("N")).ToArray();

        try
        {
            foreach (var id in ids)
            {
                manager.SetTestDelay(id, 42);
                manager.SetTestSpeed(id, 12.5m);
                manager.SetTestMessage(id, "completed");
                manager.SetTestIpInfo(id, "192.0.2.1");
                manager.SetSort(id, 7);
            }

            await manager.SaveTo();

            foreach (var id in ids)
            {
                var saved = await SQLiteHelper.Instance.TableAsync<ProfileExItem>()
                    .Where(t => t.IndexId == id).ToListAsync();
                await saved.Count.Should().BeEqualTo(1);
                await saved[0].Delay.Should().BeEqualTo(42);
                await saved[0].Speed.Should().BeEqualTo(12.5m);
                await saved[0].Message.Should().BeEqualTo("completed");
                await saved[0].IpInfo.Should().BeEqualTo("192.0.2.1");
                await saved[0].Sort.Should().BeEqualTo(7);
            }
        }
        finally
        {
            foreach (var id in ids)
            {
                await SQLiteHelper.Instance.DeleteAsync(new ProfileExItem { IndexId = id });
            }
        }
    }

    [Test]
    public async Task SaveTo_MixedNewAndExistingProfiles_ShouldInsertAndUpdateResults()
    {
        SQLiteHelper.Instance.CreateTable<ProfileExItem>();
        var manager = new ProfileExManager();
        var existingId = Guid.NewGuid().ToString("N");
        var newId = Guid.NewGuid().ToString("N");

        try
        {
            await SQLiteHelper.Instance.InsertAsync(new ProfileExItem
            {
                IndexId = existingId,
                Delay = 1,
                Speed = 2,
            });
            manager.SetTestDelay(existingId, 100);
            manager.SetTestSpeed(existingId, 20);
            manager.SetTestDelay(newId, 200);
            manager.SetTestIpInfo(newId, "192.0.2.2");

            await manager.SaveTo();

            var existing = await SQLiteHelper.Instance.TableAsync<ProfileExItem>()
                .Where(t => t.IndexId == existingId).FirstAsync();
            await existing.Delay.Should().BeEqualTo(100);
            await existing.Speed.Should().BeEqualTo(20m);
            var inserted = await SQLiteHelper.Instance.TableAsync<ProfileExItem>()
                .Where(t => t.IndexId == newId).ToListAsync();
            await inserted.Count.Should().BeEqualTo(1);
            await inserted[0].Delay.Should().BeEqualTo(200);
            await inserted[0].IpInfo.Should().BeEqualTo("192.0.2.2");

            manager.SetTestDelay(newId, 300);
            await manager.SaveTo();
            var updated = await SQLiteHelper.Instance.TableAsync<ProfileExItem>()
                .Where(t => t.IndexId == newId).FirstAsync();
            await updated.Delay.Should().BeEqualTo(300);
            await updated.IpInfo.Should().BeEqualTo("192.0.2.2");
        }
        finally
        {
            await SQLiteHelper.Instance.DeleteAsync(new ProfileExItem { IndexId = existingId });
            await SQLiteHelper.Instance.DeleteAsync(new ProfileExItem { IndexId = newId });
        }
    }
}
