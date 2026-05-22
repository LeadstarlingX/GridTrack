using System.Data;
using Dapper;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GridTrack.Infrastructure.Data;

internal sealed class PointTypeHandler : SqlMapper.TypeHandler<Point>
{
    private static readonly WKBReader Reader = new();
 
    public override Point Parse(object value)
    {
        return value switch
        {
            byte[] bytes => (Point)Reader.Read(bytes),
            string hex   => (Point)Reader.Read(Convert.FromHexString(hex)),
            _            => throw new DataException($"Cannot convert {value.GetType()} to Point.")
        };
    }
 
    public override void SetValue(IDbDataParameter parameter, Point value)
    {
        var writer = new WKBWriter();
        parameter.Value = writer.Write(value);
        parameter.DbType = DbType.Binary;
    }
}