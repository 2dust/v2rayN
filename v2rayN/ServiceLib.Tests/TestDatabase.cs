namespace ServiceLib.Tests;

internal static class TestDatabase
{
    private static readonly Lazy<SQLiteHelper> Database = new(() =>
    {
        var database = SQLiteHelper.Instance;
        database.CreateTable<SubItem>();
        database.CreateTable<ProfileItem>();
        database.CreateTable<ProfileExItem>();
        return database;
    });

    public static void Initialize() => _ = Database.Value;
}
