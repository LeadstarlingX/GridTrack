using GridTrack.Application.Abstractions.Clock;

namespace GridTrack.Infrastructure.Clock;

internal sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
