using System.Data;
using Microsoft.EntityFrameworkCore;

namespace SneakerShop.BuildingBlocks.Data;

public static class LegacyDatabaseMigrationAdoption
{
    public static async Task AdoptEnsureCreatedDatabaseAsync(
        DbContext db,
        string legacyTable,
        string historyTable,
        string initialMigrationId,
        CancellationToken cancellationToken = default)
    {
        var productVersion = typeof(DbContext).Assembly.GetName().Version
            ?? throw new InvalidOperationException("Could not determine the EF Core product version.");
        var version = $"{productVersion.Major}.{productVersion.Minor}.{productVersion.Build}";

        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = $"""
                IF OBJECT_ID(N'[dbo].[{EscapeIdentifier(historyTable)}]', N'U') IS NULL
                    CREATE TABLE [dbo].[{EscapeIdentifier(historyTable)}] (
                        [MigrationId] nvarchar(150) NOT NULL,
                        [ProductVersion] nvarchar(32) NOT NULL,
                        CONSTRAINT [PK_{EscapeIdentifier(historyTable)}] PRIMARY KEY ([MigrationId])
                    );

                IF OBJECT_ID(N'[dbo].[{EscapeIdentifier(legacyTable)}]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM [dbo].[{EscapeIdentifier(historyTable)}]
                       WHERE [MigrationId] = @migrationId)
                    INSERT INTO [dbo].[{EscapeIdentifier(historyTable)}] ([MigrationId], [ProductVersion])
                    VALUES (@migrationId, @productVersion);
                """;
            var migrationParameter = command.CreateParameter();
            migrationParameter.ParameterName = "@migrationId";
            migrationParameter.Value = initialMigrationId;
            command.Parameters.Add(migrationParameter);
            var versionParameter = command.CreateParameter();
            versionParameter.ParameterName = "@productVersion";
            versionParameter.Value = version;
            command.Parameters.Add(versionParameter);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static string EscapeIdentifier(string identifier) =>
        identifier.Replace("]", "]]", StringComparison.Ordinal);
}
