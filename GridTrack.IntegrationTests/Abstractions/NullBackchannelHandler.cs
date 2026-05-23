using System.Net;

namespace GridTrack.IntegrationTests.Abstractions;

internal sealed class NullBackchannelHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
}