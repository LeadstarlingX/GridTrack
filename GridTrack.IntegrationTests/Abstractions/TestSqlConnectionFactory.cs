using System.Data;
using GridTrack.Application.Abstractions.Data;
using Npgsql;

namespace GridTrack.IntegrationTests.Abstractions;

public sealed class TestSqlConnectionFactory : ISqlConnectionFactory, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
 
    public TestSqlConnectionFactory(string connectionString)
    {
        NtsTypeHandlers.Register();
 
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseNetTopologySuite();
        _dataSource = builder.Build();
    }
 
    public IDbConnection CreateConnection()
    {
        return _dataSource.OpenConnection();
    }
 
    public async ValueTask DisposeAsync()
        => await _dataSource.DisposeAsync();
}
