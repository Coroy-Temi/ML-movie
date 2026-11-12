using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using ML_MOVIE.Entities;

namespace ML_MOVIE.Data;

public static class SchemaBootstrapper
{
    public static async Task EnsureLatestAsync(AppDbContext dbContext)
    {
        await dbContext.Database.EnsureCreatedAsync();

        await EnsureColumnAsync(dbContext, "Users", "IsEmailVerified", "ALTER TABLE Users ADD COLUMN IsEmailVerified INTEGER NOT NULL DEFAULT 0;");
        await EnsureColumnAsync(dbContext, "Users", "ThemePreference", "ALTER TABLE Users ADD COLUMN ThemePreference TEXT NOT NULL DEFAULT 'dark';");
        await EnsureColumnAsync(dbContext, "Users", "VerificationCode", "ALTER TABLE Users ADD COLUMN VerificationCode TEXT NOT NULL DEFAULT '';");
        await EnsureColumnAsync(dbContext, "Users", "VerificationCodeExpiresUtc", "ALTER TABLE Users ADD COLUMN VerificationCodeExpiresUtc TEXT NULL;");
        await EnsureColumnAsync(dbContext, "Users", "PendingEmail", "ALTER TABLE Users ADD COLUMN PendingEmail TEXT NOT NULL DEFAULT '';");
        await EnsureColumnAsync(dbContext, "Users", "IsAdmin", "ALTER TABLE Users ADD COLUMN IsAdmin INTEGER NOT NULL DEFAULT 0;");
        await EnsureColumnAsync(dbContext, "RecommendationSessions", "CaptureDataUrl", "ALTER TABLE RecommendationSessions ADD COLUMN CaptureDataUrl TEXT NOT NULL DEFAULT '';");

        if (!await TableExistsAsync(dbContext, "ContactMessages"))
        {
            await dbContext.Database.ExecuteSqlRawAsync("""
                CREATE TABLE ContactMessages (
                    Id INTEGER NOT NULL CONSTRAINT PK_ContactMessages PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    Subject TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    CreatedUtc TEXT NOT NULL,
                CONSTRAINT FK_ContactMessages_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
                );
                """);
        }

        if (!await TableExistsAsync(dbContext, "HomepageUpdates"))
        {
            await dbContext.Database.ExecuteSqlRawAsync("""
                CREATE TABLE HomepageUpdates (
                    Id INTEGER NOT NULL CONSTRAINT PK_HomepageUpdates PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Body TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    IsPublished INTEGER NOT NULL DEFAULT 1,
                    CreatedUtc TEXT NOT NULL
                );
                """);
        }

        await SeedHomepageUpdatesAsync(dbContext);
        await EnsureAdminUserAsync(dbContext);
    }

    private static async Task EnsureColumnAsync(AppDbContext dbContext, string tableName, string columnName, string sql)
    {
        if (await ColumnExistsAsync(dbContext, tableName, columnName))
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(sql);
    }

    private static async Task<bool> TableExistsAsync(AppDbContext dbContext, string tableName)
    {
        var connection = dbContext.Database.GetDbConnection();
        await using var _ = await OpenIfNeededAsync(connection);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync();
        return result is not null;
    }

    private static async Task<bool> ColumnExistsAsync(AppDbContext dbContext, string tableName, string columnName)
    {
        var connection = dbContext.Database.GetDbConnection();
        await using var _ = await OpenIfNeededAsync(connection);
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task SeedHomepageUpdatesAsync(AppDbContext dbContext)
    {
        if (await dbContext.HomepageUpdates.AnyAsync())
        {
            return;
        }

        dbContext.HomepageUpdates.AddRange(
            new HomepageUpdate
            {
                Title = "AI recommendation assistant is coming soon",
                Body = "We are preparing a smarter assistant that will explain why each title fits your mood and help you discover hidden gems faster.",
                Category = "Coming Soon"
            },
            new HomepageUpdate
            {
                Title = "MoviePulse keeps growing every week",
                Body = "More users are saving mood sessions, testing manual mood selection, and helping us improve recommendation quality across more genres.",
                Category = "News"
            },
            new HomepageUpdate
            {
                Title = "Manual mood plus facial detection now work together",
                Body = "Users can choose a mood themselves or let the camera detect it automatically, then save recommendation runs straight into the library.",
                Category = "Latest Feature"
            });

        await dbContext.SaveChangesAsync();
    }

    private static async Task EnsureAdminUserAsync(AppDbContext dbContext)
    {
        if (await dbContext.Users.AnyAsync(item => item.IsAdmin))
        {
            return;
        }

        var firstUser = await dbContext.Users.OrderBy(item => item.Id).FirstOrDefaultAsync();
        if (firstUser is null)
        {
            return;
        }

        firstUser.IsAdmin = true;
        await dbContext.SaveChangesAsync();
    }

    private static async Task<IAsyncDisposable?> OpenIfNeededAsync(DbConnection connection)
    {
        if (connection.State == System.Data.ConnectionState.Open)
        {
            return null;
        }

        await connection.OpenAsync();
        return new ConnectionScope(connection);
    }

    private sealed class ConnectionScope : IAsyncDisposable
    {
        private readonly DbConnection _connection;

        public ConnectionScope(DbConnection connection)
        {
            _connection = connection;
        }

        public ValueTask DisposeAsync()
        {
            _connection.Close();
            return ValueTask.CompletedTask;
        }
    }
}
