using SQLite;
using System.Collections;

namespace ServiceLib.Common
{
    public sealed class SQLiteHelper
    {
        private static readonly Lazy<SQLiteHelper> _instance = new(() => new());
        public static SQLiteHelper Instance => _instance.Value;
        private string _connstr;
        private SQLiteConnection _db;
        private SQLiteAsyncConnection _dbAsync;
        private readonly string _configDB = "guiNDB.db";

        public SQLiteHelper()
        {
            _connstr = Utils.GetConfigPath(_configDB);
            _db = new SQLiteConnection(_connstr, false);
            _dbAsync = new SQLiteAsyncConnection(_connstr, false);
        }

        public CreateTableResult CreateTable<T>()
        {
            return _db.CreateTable<T>();
        }

        public async Task<int> InsertAllAsync(IEnumerable models)
        {
            return await _dbAsync.InsertAllAsync(models);
        }

        public async Task<int> InsertAsync(object model)
        {
            return await _dbAsync.InsertAsync(model);
        }

        public async Task<int> ReplaceAsync(object model)
        {
            return await _dbAsync.InsertOrReplaceAsync(model);
        }

        public async Task<int> UpdateAsync(object model)
        {
            return await _dbAsync.UpdateAsync(model);
        }

        public async Task<int> UpdateAllAsync(IEnumerable models)
        {
            return await _dbAsync.UpdateAllAsync(models);
        }

        public async Task<int> DeleteAsync(object model)
        {
            return await _dbAsync.DeleteAsync(model);
        }

        public async Task<int> DeleteAllAsync<T>()
        {
            return await _dbAsync.DeleteAllAsync<T>();
        }

        public async Task<int> ExecuteAsync(string sql)
        {
            return await _dbAsync.ExecuteAsync(sql);
        }

        public async Task<List<T>> QueryAsync<T>(string sql) where T : new()
        {
            return await _dbAsync.QueryAsync<T>(sql);
        }

        public AsyncTableQuery<T> TableAsync<T>() where T : new()
        {
            return _dbAsync.Table<T>();
        }

        public async Task DisposeDbConnectionAsync()
        {
            await Task.Factory.StartNew(() =>
            {
                _db?.Close();
                _db?.Dispose();
                _db = null;

                _dbAsync?.GetConnection()?.Close();
                _dbAsync?.GetConnection()?.Dispose();
                _dbAsync = null;
            });
        }
    }
}