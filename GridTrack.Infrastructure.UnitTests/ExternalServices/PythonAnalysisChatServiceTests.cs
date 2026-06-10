using System.Net;
using System.Net.Http.Json;
using GridTrack.Infrastructure.ExternalServices;

namespace GridTrack.Infrastructure.UnitTests.ExternalServices;

public class PythonAnalysisChatServiceTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public string? LastBody { get; private set; }
        public Uri? LastUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastUri = request.RequestUri;
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(ct);
            return responder(request);
        }
    }

    private static PythonAnalysisChatService Create(StubHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://python.test") });

    [Test]
    public async Task AskAsync_Should_Post_To_Chat_And_Return_Answer_On_Success()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { answer = "Mezzeh had the most anomalies." }),
        });
        var sut = Create(handler);

        var result = await sut.AskAsync("Which district?", "id,status\n1,Delivered", CancellationToken.None);

        await Assert.That(result).IsEqualTo("Mezzeh had the most anomalies.");
        await Assert.That(handler.LastUri!.AbsolutePath).IsEqualTo("/chat");
        await Assert.That(handler.LastBody!).Contains("Which district?");
        await Assert.That(handler.LastBody!).Contains("csv");
    }

    [Test]
    public async Task AskAsync_Should_Return_Null_On_NonSuccess_Status()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var sut = Create(handler);

        var result = await sut.AskAsync("q", "csv", CancellationToken.None);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task AskAsync_Should_Return_Null_When_Http_Throws()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));
        var sut = Create(handler);

        var result = await sut.AskAsync("q", "csv", CancellationToken.None);

        await Assert.That(result).IsNull();
    }
}
