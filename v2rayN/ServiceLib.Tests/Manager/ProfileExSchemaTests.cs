using AwesomeAssertions;
using ServiceLib.Models.Entities;
using SQLite;
using Xunit;

namespace ServiceLib.Tests.Manager;

public class ProfileExSchemaTests
{
    [Fact]
    public void CreateTable_ShouldAddIpInfoCountryCodeToExistingProfileExTable()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        try
        {
            using var database = new SQLiteConnection(databasePath);
            database.Execute("""
                create table ProfileExItem (
                    IndexId varchar primary key,
                    Delay integer,
                    Speed decimal,
                    Sort integer,
                    Message varchar,
                    IpInfo varchar
                )
                """);

            database.CreateTable<ProfileExItem>();

            database.GetTableInfo(nameof(ProfileExItem))
                .Should()
                .Contain(column => column.Name == nameof(ProfileExItem.IpInfoCountryCode));
        }
        finally
        {
            File.Delete(databasePath);
        }
    }
}
