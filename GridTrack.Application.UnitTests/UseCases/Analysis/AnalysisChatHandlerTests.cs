using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Analysis;

namespace GridTrack.Application.UnitTests.UseCases.Analysis;

public class AnalysisChatHandlerTests
{
    [Test]
    public async Task Handle_Returns_Reply_From_Chat_Service()
    {
        var handler = new AnalysisChatHandler();
        var chat = new FakeAnalysisChatService("Mezzeh had the most anomalies.");

        var result = await handler.Handle(
            new AnalysisChatQuery(
                [new ChatMessageDto("user", "Which district had the most anomalies?")],
                "id,districtId\n1,mezzeh"),
            chat,
            CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Reply).IsEqualTo("Mezzeh had the most anomalies.");
    }

    [Test]
    public async Task Handle_Returns_Null_When_Chat_Service_Unavailable()
    {
        var handler = new AnalysisChatHandler();
        var chat = new FakeAnalysisChatService(null); // null = service down

        var result = await handler.Handle(
            new AnalysisChatQuery(
                [new ChatMessageDto("user", "What is the anomaly rate?")],
                "csv-data"),
            chat,
            CancellationToken.None);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Handle_Uses_Last_User_Message_As_Question()
    {
        var handler = new AnalysisChatHandler();
        var chat = new FakeAnalysisChatService("Answer to the last question.");

        await handler.Handle(
            new AnalysisChatQuery(
                [
                    new ChatMessageDto("user", "First question"),
                    new ChatMessageDto("assistant", "First answer"),
                    new ChatMessageDto("user", "Second question"),
                ],
                "csv"),
            chat,
            CancellationToken.None);

        await Assert.That(chat.LastQuestion).IsEqualTo("Second question");
    }

    [Test]
    public async Task Handle_Returns_Prompt_When_No_Messages_Provided()
    {
        var handler = new AnalysisChatHandler();
        var chat = new FakeAnalysisChatService("ignored");

        var result = await handler.Handle(
            new AnalysisChatQuery([], "csv"),
            chat,
            CancellationToken.None);

        // No question → returns a fallback reply without calling the service
        await Assert.That(result).IsNotNull();
        await Assert.That(chat.LastQuestion).IsNull(); // service was not called
    }

    [Test]
    public async Task Handle_Passes_CsvData_As_Context()
    {
        var handler = new AnalysisChatHandler();
        var chat = new FakeAnalysisChatService("ok");
        const string csv = "id,status\n1,Delivered";

        await handler.Handle(
            new AnalysisChatQuery([new ChatMessageDto("user", "q")], csv),
            chat,
            CancellationToken.None);

        await Assert.That(chat.LastCsvContext).IsEqualTo(csv);
    }

    // ── Fake ──────────────────────────────────────────────────────────────

    private sealed class FakeAnalysisChatService(string? answer) : IAnalysisChatService
    {
        public string? LastQuestion { get; private set; }
        public string? LastCsvContext { get; private set; }

        public Task<string?> AskAsync(string question, string csvContext, CancellationToken ct)
        {
            LastQuestion = question;
            LastCsvContext = csvContext;
            return Task.FromResult(answer);
        }

#pragma warning disable CS1998
        public async IAsyncEnumerable<string> StreamAsync(
            string question, string csvContext, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            if (answer is not null) yield return answer;
        }
#pragma warning restore CS1998

        public Task<string?> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken ct)
            => Task.FromResult<string?>(null);
    }
}
