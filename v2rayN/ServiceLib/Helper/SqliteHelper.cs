using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using SQLite.Framework;
using SQLite.Framework.Extensions;
using SQLite.Framework.Generated;
using SQLitePCL;

namespace ServiceLib.Helper;

public sealed class SQLiteHelper
{
    private static readonly Lazy<SQLiteHelper> _instance = new(() => new());
    public static SQLiteHelper Instance => _instance.Value;
    private readonly string _connstr;
    private SQLiteDatabase _db;
    private readonly string _configDB = "guiNDB.db";

    public SQLiteHelper()
    {
        raw.SetProvider(new SQLite3Provider_e_sqlite3());
        _connstr = Utils.GetConfigPath(_configDB);
        var options = new SQLiteOptionsBuilder(_connstr)
            .UseGeneratedMaterializers()
            .Build();
        _db = new SQLiteDatabase(options);
    }

    public int CreateTable<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>()
    {
        return _db.Schema.CreateTable<T>();
    }

    public Task<int> CreateTableAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>()
    {
        return _db.Schema.CreateTableAsync<T>();
    }

    public Task<int> InsertAllAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>(IEnumerable<T> models)
    {
        return _db.Table<T>().AddRangeAsync(models, runInTransaction: true);
    }

    public Task<int> InsertAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>(T model)
    {
        return _db.Table<T>().AddAsync(model);
    }

    public Task<int> ReplaceAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>(T model)
    {
        return _db.Table<T>().AddOrUpdateAsync(model);
    }

    public Task<int> ReplaceAllAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>(IEnumerable<T> models)
    {
        return _db.Table<T>().AddOrUpdateRangeAsync(models, runInTransaction: true);
    }

    public Task<int> UpdateAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>(T model)
    {
        return _db.Table<T>().UpdateAsync(model);
    }

    public Task<int> UpdateAllAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>(IEnumerable<T> models)
    {
        return _db.Table<T>().UpdateRangeAsync(models, runInTransaction: true);
    }

    public Task<int> DeleteAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>(T model)
    {
        return _db.Table<T>().RemoveAsync(model);
    }

    public Task<int> DeleteAllAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>()
    {
        return _db.Table<T>().ClearAsync();
        // return _db.Schema.DropTableAsync<T>();
    }

    /// <summary>
    /// Projects the profile list together with its subscription remarks.
    /// Written as raw SQL rather than a LINQ projection: <c>select new ProfileItemModel { ... }</c>
    /// makes the compiler emit <c>Expression.New</c>/<c>Expression.Bind</c>, which are
    /// <c>[RequiresUnreferencedCode]</c> and warn IL2026 under AOT.
    /// </summary>
    public Task<List<ProfileItemModel>> FetchProfileModelsAsync(string subid, string filter)
    {
        const string sql = """
            select a.IndexId
                  ,a.ConfigType
                  ,a.Remarks
                  ,a.Address
                  ,a.Port
                  ,a.Network
                  ,a.StreamSecurity
                  ,a.Subid
                  ,b.Remarks as SubRemarks
              from ProfileItem a
              left join SubItem b on a.Subid = b.Id
             where (@subid is null or a.Subid = @subid)
               and (@filter is null or instr(a.Remarks, @filter) > 0 or instr(a.Address, @filter) > 0)
            """;

        var parameters = new[]
        {
            new SQLiteParameter { Name = "@subid", Value = subid.IsNullOrEmpty() ? null : subid },
            new SQLiteParameter { Name = "@filter", Value = filter.IsNullOrEmpty() ? null : filter },
        };

        return _db.QueryAsync<ProfileItemModel>(sql, parameters);
    }

    public Task<int> ResetServerStatTodayAsync(long dateNow)
    {
        const string sql = @"UPDATE ServerStatItem 
        SET TodayUp = 0, TodayDown = 0, DateNow = @dateNow
        WHERE DateNow <> @dateNow";

        return _db.ExecuteAsync(sql, new[] { new SQLiteParameter { Name = "@dateNow", Value = dateNow } });
    }

    public Task<T?> FirstOrDefaultAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>()
    {
        return _db.Table<T>().FirstOrDefaultAsync();
    }

    public Task<T?> FirstOrDefaultAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>(Expression<Func<T, bool>> predicate)
    {
        return _db.Table<T>().FirstOrDefaultAsync(predicate);
    }

    public Task<List<T>> FetchAllAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>()
    {
        return _db.Table<T>().ToListAsync();
    }

    public Task<List<T>> FetchOrderedAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>(Expression<Func<T, object>> orderBy, bool ascending = true)
    {
        return _db.Table<T>().OrderBy(orderBy).ToListAsync();
    }

    public Task<List<T>> FetchAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>(Expression<Func<T, bool>> predicate)
    {
        return _db.Table<T>().Where(predicate).ToListAsync();
    }

    public Task<List<T>> FetchPagedAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        Expression<Func<T, bool>> predicate,
        int? take = null,
        int? offset = null)
    {
        var query = _db.Table<T>().Where(predicate);
        if (offset.HasValue)
        {
            query = query.Skip(offset.Value);
        }
        if (take.HasValue)
        {
            query = query.Take(take.Value);
        }
        return query.ToListAsync();
    }

    public Task<int> DeleteWhereAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>(Expression<Func<T, bool>> predicate)
    {
        return _db.Table<T>()
            .Where(predicate)
            .ExecuteDeleteAsync();
    }

    public Task<int> DeleteOrphanServerStatsAsync()
    {
        const string sql = @"
        DELETE FROM ServerStatItem 
        WHERE IndexId NOT IN (SELECT IndexId FROM ProfileItem)";

        return _db.ExecuteAsync(sql, Array.Empty<object>());
    }

    public Task<int> CountAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>()
    {
        return _db.Table<T>().CountAsync();
    }

    public Task<int> CountAsync<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>(Expression<Func<T, bool>> predicate)
    {
        return _db.Table<T>().Where(predicate).CountAsync();
    }

    public async Task DisposeDbConnectionAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                //_db?.Close();
                _db?.Dispose();
            }
            finally
            {
                _db = null;
            }
        });
    }
}
