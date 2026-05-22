using System.Data;
using Dapper;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.Abstractions;


public sealed class NtsPointTypeHandler : SqlMapper.TypeHandler<Point>
{
    public override Point Parse(object value)
    {
        return value switch
        {
            Point p => p,
            // Guard against unexpected geometry subtypes rather than silently returning null
            _ => throw new InvalidCastException(
                $"Expected NetTopologySuite.Geometries.Point but received {value?.GetType().FullName ?? "null"}. " +
                "Ensure UseNetTopologySuite() is called on the NpgsqlDataSource used by the connection.")
        };
    }
 
    public override void SetValue(IDbDataParameter parameter, Point value)
    {
        parameter.Value = value;
    }
}
 
public static class NtsTypeHandlers
{
    private static bool _registered;
    private static readonly Lock Lock = new();
 
    public static void Register()
    {
        if (_registered) return;
        lock (Lock)
        {
            if (_registered) return;
            SqlMapper.AddTypeHandler(new NtsPointTypeHandler());
            _registered = true;
        }
    }
}