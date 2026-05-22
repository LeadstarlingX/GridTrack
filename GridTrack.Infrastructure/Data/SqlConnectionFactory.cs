using System.Data;
using GridTrack.Application.Abstractions.Data;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace GridTrack.Infrastructure.Data;

internal sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly NpgsqlDataSource _dataSource;

    public SqlConnectionFactory(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public IDbConnection CreateConnection()
    {
        var connection = _dataSource.CreateConnection();
        connection.Open();
        return connection;
    }
    
}