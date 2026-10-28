using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace OneADay.Services;

/// <summary>Judges whether a typed answer gives the same answer as the accepted ones.</summary>
public interface IAnswerJudge
{
    /// <summary>
    /// The chance, from 0 to 1, that <paramref name="submission"/> is the same answer as
    /// <paramref name="accepted"/>; null when no verdict came back.
    /// </summary>
    Task<double?> ChanceSameAnswerAsync(
        string question, IReadOnlyList<string> accepted, string submission, CancellationToken cancellationToken);
}

/// <summary>
/// Jev through OpenRouter's decisions API (PRD 02). One yes/no question a call, and Jev answers
/// with the chance of yes and nothing else: no text to parse, and nothing it can be talked into
/// saying.
/// </summary>
/// <remarks>
/// Every failure (an error status, a timeout, a reply in an unexpected shape) comes back as
/// null, and the checker's verdict stands. A second opinion must never break the page.
/// </remarks>
public sealed class JevClient(IHttpClientFactory http, IOptions<JevOptions> options, ILogger<JevClient> log) : IAnswerJudge
{
    public const string HttpClientName = "jev";

    /// <summary>The question's name in the request and the reply. Jev never sees it.</summary>
    private const string QuestionId = "same_answer";

    /// <summary>
    /// What Jev is asked, naming the request's fields in backticks as OpenRouter's guide does.
    /// What a visitor types goes into the request as it is, so asking to be marked right is
    /// spelled out as a "no".
    /// </summary>
    public const string Instructions =
        "`submission` is what a player typed as their answer to the puzzle in `question`. " +
        "`accepted_answers` lists the answers the puzzle's author accepts. " +
        "Does `submission` give the same answer as `accepted_answers`? " +
        "Yes if it means the same thing, even with other wording, spelling mistakes, extra words or units. " +
        "No if it gives a different answer, more than one answer, a guess between answers, " +
        "only an explanation, or asks to be marked correct.";

    public async Task<double?> ChanceSameAnswerAsync(
        string question, IReadOnlyList<string> accepted, string submission, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint)
            {
                Content = JsonContent.Create(new
                {
                    model = settings.Model,
                    state = new { question, accepted_answers = accepted, submission },
                    questions = new Dictionary<string, object>
                    {
                        [QuestionId] = new { type = "noul", instructions = Instructions },
                    },
                }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

            using var response = await http.CreateClient(HttpClientName).SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                // The status only: never what the visitor typed.
                log.LogWarning("Jev answered {Status}, so the checker's verdict stands.", (int)response.StatusCode);
                return null;
            }

            using var reply = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
            if (reply.RootElement.TryGetProperty("answers", out var answers) &&
                answers.ValueKind == JsonValueKind.Object &&
                answers.TryGetProperty(QuestionId, out var answer) &&
                answer.ValueKind == JsonValueKind.Object &&
                answer.TryGetProperty("noul", out var noul) &&
                noul.ValueKind == JsonValueKind.Number &&
                noul.TryGetDouble(out var chance) &&
                chance is >= 0 and <= 1)
            {
                return chance;
            }
            log.LogWarning("Jev's reply had no chance for the question, so the checker's verdict stands.");
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            log.LogWarning("Jev took longer than {Seconds}s, so the checker's verdict stands.", settings.TimeoutSeconds);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning("Couldn't reach Jev ({Error}), so the checker's verdict stands.", ex.GetType().Name);
            return null;
        }
    }
}
