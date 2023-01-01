
using SQLite;
using System.Collections;

namespace v2rayN.Base
{
    public sealed class SqliteHelper
    {
        private static readonly Lazy<SqliteHelper> _instance = new Lazy<SqliteHelper>(() => new());
        public static SqliteHelper Instance => _instance.Value;
        private string _connstr;
        public SQLiteConnection _db;
        public SQLiteAsyncConnection _dbAsync;

        public SqliteHelper()
        {
            _connstr = Utils.GetConfigPath(Global.ConfigDB);
            _db = new SQLiteConnection(_connstr, false);
            _dbAsync = new SQLiteAsyncConnection(_connstr, false);
        }

        public CreateTableResult CreateTable<T>()
        {
            return _db.CreateTable<T>();
        }

        public int Add(object model)
        {
            return _db.Insert(model);
        }
        public async Task<int> AddAsync(object model)
        {
            return await _dbAsync.InsertAsync(model);
        }
        public int Replace(object model)
        {
            return _db.InsertOrReplace(model);
        }
        public async Task<int> Replacesync(object model)
        {
            return await _dbAsync.InsertOrReplaceAsync(model);
        }

        public int Update(object model)
        {
            return _db.Update(model);
        }
        public async Task<int> UpdateAsync(object model)
        {
            return await _dbAsync.UpdateAsync(model);
        }
        public int UpdateAll(IEnumerable models)
        {
            return _db.UpdateAll(models);
        }

        public int Delete(object model)
        {
            return _db.Delete(model);
        }
        public async Task<int> DeleteAsync(object model)
        {
            return await _dbAsync.DeleteAsync(model);
        }
        public List<T> Query<T>(string sql) where T : new()
        {
            return _db.Query<T>(sql);
        }
        public async Task<List<T>> QueryAsync<T>(string sql) where T : new()
        {
            return await _dbAsync.QueryAsync<T>(sql);
        }
        public int Execute(string sql)
        {
            return _db.Execute(sql);
        }
        public async Task<int> ExecuteAsync(string sql)
        {
            return await _dbAsync.ExecuteAsync(sql);
        }

        public TableQuery<T> Table<T>() where T : new()
        {
            return _db.Table<T>();
        }
        public AsyncTableQuery<T> TableAsync<T>() where T : new()
        {
            return _dbAsync.Table<T>();
        }
    }
}