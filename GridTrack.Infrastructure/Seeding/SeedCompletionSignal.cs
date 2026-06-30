namespace GridTrack.Infrastructure.Seeding;

// Lets other startup hosted services (PositionSimulatorService, AnomalySimulatorService) wait
// until seeding has actually settled, instead of guessing how long it takes with a fixed delay.
// Now that SeedService always reseeds on startup (~20s, OSRM calls included), every single boot
// would otherwise race a service that reads Drivers/Deliveries before the reseed has finished
// deleting/re-inserting them.
public sealed class SeedCompletionSignal
{
    private readonly TaskCompletionSource _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Completed => _tcs.Task;

    public void MarkComplete() => _tcs.TrySetResult();
}
