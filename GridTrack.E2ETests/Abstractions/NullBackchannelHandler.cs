using System.Net;

namespace GridTrack.E2ETests.Abstractions;

internal sealed class NullBackchannelHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
}
