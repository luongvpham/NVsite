using Testcontainers.PostgreSql;

namespace Identity.IntegrationTests;

/// <summary>
/// Postgres thật qua Testcontainers (backend/CLAUDE.md — "Integration test cho endpoint, chạy trên
/// database thật"), thay EF InMemory đã dùng cho Sample throwaway. Dùng chung cho mọi test class
/// trong project này qua <see cref="ICollectionFixture{TFixture}"/>.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("vsite_test")
        .WithUsername("vsite")
        .WithPassword("vsite_test_only")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
