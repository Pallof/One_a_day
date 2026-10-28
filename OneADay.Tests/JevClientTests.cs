using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The request Jev gets through OpenRouter, and what comes of each kind of reply (PRD 02). No
/// real call is made: a stand-in answers in OpenRouter's place.
/// </summary>
public class JevClientTests
{
    private sealed class StandIn(HttpStatusCode status, string reply, TimeSpan delay = default) : HttpMessageHandler, IHttpClientFactory
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            await Task.Delay(delay, cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(reply, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly JevOptions Settings = GradingTestServices.On();

    private static Task<double?> Ask(StandIn openRouter, JevOptions? settings = null) =>
        new JevClient(openRouter, Options.Create(settings ?? Settings), NullLogger<JevClient>.Instance)
            .ChanceSameAnswerAsync("Where are you now?", ["second", "2nd"], "behind the leader", CancellationToken.None);

    [Fact]
    public async Task Asks_one_yes_no_question_and_reads_the_chance_of_yes()
    {
        var openRouter = new StandIn(HttpStatusCode.OK,
            """{"id":"gen-dec-1","answers":{"same_answer":{"type":"noul","noul":0.96}},"usage":{"input_tokens":120}}""");

        Assert.Equal(0.96, await Ask(openRouter));

        Assert.Equal(HttpMethod.Post, openRouter.Request!.Method);
        Assert.Equal(Settings.Endpoint, openRouter.Request.RequestUri!.ToString());
        Assert.Equal("Bearer test-key-not-real", openRouter.Request.Headers.Authorization!.ToString());

        using var sent = JsonDocument.Parse(openRouter.Body!);
        var root = sent.RootElement;
        Assert.Equal(Settings.Model, root.GetProperty("model").GetString());
        var state = root.GetProperty("state");
        Assert.Equal("Where are you now?", state.GetProperty("question").GetString());
        Assert.Equal(["second", "2nd"], state.GetProperty("accepted_answers").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal("behind the leader", state.GetProperty("submission").GetString());
        var question = root.GetProperty("questions").GetProperty("same_answer");
        Assert.Equal("noul", question.GetProperty("type").GetString());
        Assert.Equal(JevClient.Instructions, question.GetProperty("instructions").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, """{"answers":{"same_answer":{"noul":0.96}}}""")]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":{"message":"No auth credentials found"}}""")]
    [InlineData(HttpStatusCode.OK, """{"answers":{}}""")]
    [InlineData(HttpStatusCode.OK, """{"answers":{"same_answer":{"noul":"0.96"}}}""")]
    [InlineData(HttpStatusCode.OK, """{"answers":{"same_answer":{"noul":1.7}}}""")]
    [InlineData(HttpStatusCode.OK, "not json at all")]
    public async Task An_error_or_an_odd_reply_gives_no_verdict(HttpStatusCode status, string reply) =>
        Assert.Null(await Ask(new StandIn(status, reply)));

    [Fact]
    public async Task A_slow_reply_gives_no_verdict_rather_than_keep_the_visitor_waiting()
    {
        var settings = GradingTestServices.On();
        settings.TimeoutSeconds = 0.05;
        var slow = new StandIn(HttpStatusCode.OK, """{"answers":{"same_answer":{"noul":0.96}}}""", TimeSpan.FromSeconds(5));

        var waited = System.Diagnostics.Stopwatch.StartNew();
        Assert.Null(await Ask(slow, settings));
        Assert.True(waited.Elapsed < TimeSpan.FromSeconds(2), $"waited {waited.Elapsed}");
    }
}
