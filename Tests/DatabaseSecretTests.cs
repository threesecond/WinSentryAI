using Microsoft.Data.Sqlite;
using WinSentryAI.Services;

namespace WinSentryAI.Tests;

public class DatabaseSecretTests
{
    [Fact]
    public async Task GetSecretAsync_returns_saved_secret_for_current_windows_user()
    {
        string dbPath = Path.Combine(TestPaths.CreateTempDirectory(), "secrets.db");
        var service = new DatabaseService(dbPath);
        service.Initialize(retentionDays: 30);

        await service.SaveSecretAsync("GeminiApiKey", "test-key");

        Assert.Equal("test-key", await service.GetSecretAsync("GeminiApiKey"));
    }

    [Fact]
    public async Task GetSecretAsync_throws_when_encrypted_secret_cannot_be_decrypted()
    {
        string dbPath = Path.Combine(TestPaths.CreateTempDirectory(), "broken-secret.db");
        var service = new DatabaseService(dbPath);
        service.Initialize(retentionDays: 30);

        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO AppSettings (Key, Value, IsEncrypted)
                VALUES ('OpenAiApiKey', 'not-dpapi-ciphertext', 1);
            ";
            command.ExecuteNonQuery();
        }

        var ex = await Assert.ThrowsAsync<SecretDecryptionException>(
            () => service.GetSecretAsync("OpenAiApiKey"));

        Assert.Equal("OpenAiApiKey", ex.Key);
    }
}
