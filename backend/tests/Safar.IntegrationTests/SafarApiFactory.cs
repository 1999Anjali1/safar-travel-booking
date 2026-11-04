using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Safar.Application.Auth;
using Testcontainers.PostgreSql;

namespace Safar.IntegrationTests;

/// <summary>
/// Starts the real API in memory against a real PostgreSQL database.
/// - In CI (or anywhere with Docker) a throw-away postgres:17 container is started with Testcontainers.
/// - Set SAFAR_TEST_POSTGRES to a connection string to use an existing server instead (no Docker needed).
/// The database is created fresh, migrated and seeded with demo data.
/// </summary>
public class SafarApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string _connectionString = string.Empty;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("SAFAR_TEST_POSTGRES");
        if (!string.IsNullOrEmpty(external))
        {
            var csb = new NpgsqlConnectionStringBuilder(external) { Database = $"safar_test_{Guid.NewGuid():N}"[..30] };
            _connectionString = csb.ConnectionString;
        }
        else
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        // Environment variables are read by WebApplication.CreateBuilder before services are registered
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _connectionString);
        Environment.SetEnvironmentVariable("RateLimiting__AuthPerMinute", "10000");
        Environment.SetEnvironmentVariable("RateLimiting__AssistantPerMinute", "10000");

        _ = Server; // boots the app: runs migrations + seed
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
        else
        {
            // drop the temporary database created on the external server
            NpgsqlConnection.ClearAllPools();
            var csb = new NpgsqlConnectionStringBuilder(_connectionString);
            var dbName = csb.Database;
            csb.Database = "postgres";
            await using var conn = new NpgsqlConnection(csb.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{dbName}\" WITH (FORCE)", conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<HttpClient> ClientForAsync(string email, string password = "Password@123")
    {
        var client = CreateClient();
        var auth = await LoginAsync(client, email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    public static async Task<AuthResponse> LoginAsync(HttpClient client, string email, string password = "Password@123")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<SafarApiFactory>
{
    public const string Name = "api";
}
