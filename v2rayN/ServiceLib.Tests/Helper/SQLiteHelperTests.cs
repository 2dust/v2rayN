namespace ServiceLib.Tests.Helper;

[NotInParallel]
public class SQLiteHelperTests
{
    private static readonly Lazy<SQLiteHelper> Database = new(() =>
    {
        var database = SQLiteHelper.Instance;
        database.CreateTable<HelperRow>();
        database.CreateTable<DeleteAllRow>();
        database.CreateTable<NoPrimaryKeyRow>();
        return database;
    });

    [Test]
    public async Task SingleWrites_ShouldPreserveValuesAndDeleteByPrimaryKey()
    {
        var database = Database.Value;
        var id = Guid.NewGuid().ToString("N");
        var row = new HelperRow { Id = id, Value = $"{id}-original" };
        try
        {
            await (await database.InsertAsync(row)).Should().BeEqualTo(1);
            await (await database.ReplaceAsync(new HelperRow { Id = id, Value = $"{id}-replacement" })).Should().BeEqualTo(1);
            row.Value = $"{id}-updated";
            await (await database.UpdateAsync(row)).Should().BeEqualTo(1);

            var rows = await database.QueryAsync<HelperRow>($"SELECT * FROM SQLiteHelperTestRows WHERE Id = '{id}'");
            await rows.Count.Should().BeEqualTo(1);
            await rows[0].Value.Should().BeEqualTo($"{id}-updated");
            await (await database.TableAsync<HelperRow>().Where(item => item.Id == id).CountAsync()).Should().BeEqualTo(1);

            row.Value = "not-the-stored-value";
            await (await database.DeleteAsync(row)).Should().BeEqualTo(1);
            await (await database.DeleteAsync(row)).Should().BeEqualTo(0);
            await (await database.TableAsync<HelperRow>().Where(item => item.Id == id).CountAsync()).Should().BeEqualTo(0);
        }
        finally
        {
            await database.ExecuteAsync($"DELETE FROM SQLiteHelperTestRows WHERE Id = '{id}'");
        }
    }

    [Test]
    public async Task BulkWrites_ShouldCommitEveryRowAndReturnAffectedCounts()
    {
        var database = Database.Value;
        var prefix = Guid.NewGuid().ToString("N");
        var rows = new[]
        {
            new HelperRow { Id = $"{prefix}-first", Value = $"{prefix}-original-first" },
            new HelperRow { Id = $"{prefix}-second", Value = $"{prefix}-original-second" },
        };
        try
        {
            await (await database.InsertAllAsync(rows)).Should().BeEqualTo(2);
            rows[0].Value = $"{prefix}-updated-first";
            rows[1].Value = $"{prefix}-updated-second";
            await (await database.UpdateAllAsync(rows)).Should().BeEqualTo(2);

            var saved = await database.QueryAsync<HelperRow>($"SELECT * FROM SQLiteHelperTestRows WHERE Id LIKE '{prefix}%' ORDER BY Id");
            await saved.Count.Should().BeEqualTo(2);
            await saved[0].Value.Should().BeEqualTo($"{prefix}-updated-first");
            await saved[1].Value.Should().BeEqualTo($"{prefix}-updated-second");
        }
        finally
        {
            await database.ExecuteAsync($"DELETE FROM SQLiteHelperTestRows WHERE Id LIKE '{prefix}%'");
        }
    }

    [Test]
    public async Task InsertAllAsync_ShouldRollBackEarlierRowsWhenALaterInsertFails()
    {
        var database = Database.Value;
        var id = Guid.NewGuid().ToString("N");
        try
        {
            SQLiteException? error = null;
            try
            {
                await database.InsertAllAsync(new[]
                {
                    new HelperRow { Id = id, Value = $"{id}-first" },
                    new HelperRow { Id = id, Value = $"{id}-duplicate" },
                });
            }
            catch (SQLiteException exception)
            {
                error = exception;
            }

            await error.Should().NotBeNull();
            await (await database.TableAsync<HelperRow>().Where(item => item.Id == id).CountAsync()).Should().BeEqualTo(0);
        }
        finally
        {
            await database.ExecuteAsync($"DELETE FROM SQLiteHelperTestRows WHERE Id = '{id}'");
        }
    }

    [Test]
    public async Task UpdateAllAsync_ShouldRollBackEarlierChangesWhenALaterUpdateFails()
    {
        var database = Database.Value;
        var prefix = Guid.NewGuid().ToString("N");
        var first = new HelperRow { Id = $"{prefix}-first", Value = $"{prefix}-original-first" };
        var second = new HelperRow { Id = $"{prefix}-second", Value = $"{prefix}-original-second" };
        try
        {
            await database.InsertAllAsync(new[] { first, second });
            first.Value = $"{prefix}-updated-first";
            second.Value = first.Value;
            SQLiteException? error = null;
            try
            {
                await database.UpdateAllAsync(new[] { first, second });
            }
            catch (SQLiteException exception)
            {
                error = exception;
            }

            await error.Should().NotBeNull();
            var saved = await database.QueryAsync<HelperRow>($"SELECT * FROM SQLiteHelperTestRows WHERE Id LIKE '{prefix}%' ORDER BY Id");
            await saved.Count.Should().BeEqualTo(2);
            await saved[0].Value.Should().BeEqualTo($"{prefix}-original-first");
            await saved[1].Value.Should().BeEqualTo($"{prefix}-original-second");
        }
        finally
        {
            await database.ExecuteAsync($"DELETE FROM SQLiteHelperTestRows WHERE Id LIKE '{prefix}%'");
        }
    }

    [Test]
    public async Task EmptyBatches_ShouldReturnZero()
    {
        var database = Database.Value;
        await (await database.InsertAllAsync(Array.Empty<HelperRow>())).Should().BeEqualTo(0);
        await (await database.UpdateAllAsync(Array.Empty<HelperRow>())).Should().BeEqualTo(0);
    }

    [Test]
    public async Task DeleteAsync_ShouldRejectRowsWithoutAPrimaryKey()
    {
        var database = Database.Value;
        var value = Guid.NewGuid().ToString("N");
        try
        {
            var row = new NoPrimaryKeyRow { Value = value };
            await database.InsertAsync(row);
            NotSupportedException? error = null;
            try
            {
                await database.DeleteAsync(row);
            }
            catch (NotSupportedException exception)
            {
                error = exception;
            }

            await error.Should().NotBeNull();
            await (await database.TableAsync<NoPrimaryKeyRow>().Where(item => item.Value == value).CountAsync()).Should().BeEqualTo(1);
        }
        finally
        {
            await database.ExecuteAsync($"DELETE FROM SQLiteHelperNoPrimaryKeyTestRows WHERE Value = '{value}'");
        }
    }

    [Test]
    public async Task DeleteAllAsync_ShouldReturnTheNumberOfDeletedRows()
    {
        var database = Database.Value;
        await database.DeleteAllAsync<DeleteAllRow>();
        await database.InsertAllAsync(new[] { new DeleteAllRow { Id = "first" }, new DeleteAllRow { Id = "second" } });

        await (await database.DeleteAllAsync<DeleteAllRow>()).Should().BeEqualTo(2);
        await (await database.TableAsync<DeleteAllRow>().CountAsync()).Should().BeEqualTo(0);
    }

    [Table("SQLiteHelperTestRows")]
    public class HelperRow
    {
        [PrimaryKey]
        public string Id { get; set; } = string.Empty;

        [Unique]
        public string Value { get; set; } = string.Empty;
    }

    [Table("SQLiteHelperDeleteAllTestRows")]
    public class DeleteAllRow
    {
        [PrimaryKey]
        public string Id { get; set; } = string.Empty;
    }

    [Table("SQLiteHelperNoPrimaryKeyTestRows")]
    public class NoPrimaryKeyRow
    {
        public string Value { get; set; } = string.Empty;
    }
}
