using SQLite;

namespace v2rayN.Web.Services;

internal static class BackupDatabaseCompatibility
{
    private static readonly IReadOnlyDictionary<string, string> RequiredPrimaryKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["DNSItem"] = "Id",
        ["FullConfigTemplateItem"] = "Id",
        ["ProfileExItem"] = "IndexId",
        ["ProfileGroupItem"] = "IndexId",
        ["ProfileItem"] = "IndexId",
        ["RoutingItem"] = "Id",
        ["ServerStatItem"] = "IndexId",
        ["SubItem"] = "Id",
    };

    public static bool IsCompatible(string databasePath, out string error)
    {
        try
        {
            using var database = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadOnly, false);
            if (!string.Equals(database.ExecuteScalar<string>("PRAGMA quick_check;"), "ok", StringComparison.OrdinalIgnoreCase))
            {
                error = "SQLite quick_check did not return ok.";
                return false;
            }

            var tableNames = database.Query<TableName>(
                    "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';")
                .Select(item => item.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!tableNames.Any(name => RequiredPrimaryKeys.ContainsKey(name)))
            {
                error = "The database does not contain any known v2rayN data tables.";
                return false;
            }

            foreach (var (tableName, primaryKey) in RequiredPrimaryKeys)
            {
                // Older backups may not yet have every optional ServiceLib table; AppManager
                // creates missing tables during initialization. Existing tables must retain
                // their stable primary key so that SQLite-net can add newer columns safely.
                if (!tableNames.Contains(tableName))
                {
                    continue;
                }

                var columns = database.Query<TableColumn>($"PRAGMA table_info(\"{tableName}\");");
                if (!columns.Any(column => string.Equals(column.Name, primaryKey, StringComparison.OrdinalIgnoreCase)
                    && column.PrimaryKey == 1))
                {
                    error = $"Table {tableName} is missing its expected primary key {primaryKey}.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private sealed class TableName
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TableColumn
    {
        public string Name { get; set; } = string.Empty;

        [Column("pk")]
        public int PrimaryKey { get; set; }
    }
}
