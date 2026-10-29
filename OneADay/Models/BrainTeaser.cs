using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OneADay.Models;

public enum Difficulty
{
    Easy,
    Medium,
    Hard,
}

public class BrainTeaser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The day this teaser is shown as the "Challenge of the day".</summary>
    public DateOnly Date { get; set; }

    public Difficulty Difficulty { get; set; } = Difficulty.Medium;

    /// <summary>Classification labels (e.g. "logic", "wordplay", "math").</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Filename of an optional support image, served from /teaser-images.</summary>
    public string? ImageFileName { get; set; }

    /// <summary>
    /// What the picture shows, in words (PRD 05): read out by screen readers, and shown by an
    /// email app that holds pictures back. A diagram's description carries every number on it.
    /// </summary>
    public string? ImageAlt { get; set; }

    /// <summary>The picture's description, or a plain stand-in for one written before there was a field for it.</summary>
    public string ImageDescription() =>
        string.IsNullOrWhiteSpace(ImageAlt) ? "A picture that goes with the question" : ImageAlt.Trim();

    public string Question { get; set; } = string.Empty;

    /// <summary>Accepted answer. Multiple accepted answers can be separated with ';'.</summary>
    public string Answer { get; set; } = string.Empty;

    public string? Hint { get; set; }

    /// <summary>Optional worked explanation, revealed on past questions.</summary>
    public string? Solution { get; set; }

    /// <summary>
    /// Answers the author has ruled wrong, separated with ';' (PRD 02). Checked before Jev is
    /// asked or an earlier verdict of Jev's is reused, so the author's ruling always wins.
    /// </summary>
    public string? NotAccepted { get; set; }

    public bool AcceptsAnswer(string submission) => Accepts(Answer, submission);

    /// <summary>Whether the author has ruled <paramref name="submission"/> wrong.</summary>
    public bool RulesOut(string submission) => Accepts(NotAccepted, submission);

    /// <summary>Each accepted answer on its own, as written.</summary>
    public IReadOnlyList<string> AcceptedAnswers() =>
        Answer.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// A short fingerprint of what Jev is shown: the question and its accepted answers. A kept
    /// verdict stands only while this is unchanged, so editing either means Jev is asked again
    /// (PRD 02). The picture, solution, date and rulings don't count.
    /// </summary>
    public string Fingerprint() =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            Question + "\n" + string.Join("\n", AcceptedAnswers()))))[..16];

    /// <summary>
    /// Whether Jev may give a second opinion on <paramref name="submission"/> (PRD 02).
    /// <list type="bullet">
    /// <item>Never on a formula question: the checker works formulas out, so it is right by definition.</item>
    /// <item>A submission that names a number must name an accepted one: "it's 48" can be asked
    /// about, "50" can't, so Jev never turns a wrong number into a right one.</item>
    /// <item>Where every accepted answer is a number, the submission must name one.</item>
    /// <item>Never on a submission with no letters or digits ("?", "...", an emoji). Its
    /// <see cref="AnswerKey"/> would be empty, the same for every such submission, so one
    /// verdict would answer for them all.</item>
    /// </list>
    /// </summary>
    public bool AllowsSecondOpinion(string submission)
    {
        if (!submission.Any(char.IsLetterOrDigit))
        {
            return false;
        }

        var accepted = AcceptedAnswers().SelectMany(Variants).ToList();
        if (accepted.Any(a => Arithmetic.TryEvaluate(a, out _) && !TryParseNumber(a, out _)))
        {
            return false;
        }

        var acceptedNumbers = accepted.Select(NumberIn).OfType<decimal>().ToList();
        if (acceptedNumbers.Count == 0)
        {
            return true;
        }

        var typedNumbers = NumbersIn(submission).ToList();
        if (typedNumbers.Count == 0)
        {
            return accepted.Any(a => NumberIn(a) is null);
        }
        return typedNumbers.Any(t => acceptedNumbers.Any(a => Arithmetic.ValuesClose(a, t)));
    }

    /// <summary>The number an accepted answer gives, as "48" or "48 mph" does; null for a word answer.</summary>
    private static decimal? NumberIn(string accepted) =>
        TryParseNumber(accepted, out var value) || TrySplitNumberAndUnit(accepted, out value, out _)
            ? value
            : null;

    /// <summary>Every number in free text, in digits ("48", "1,000", the 2 of "2nd") or in words ("forty-eight").</summary>
    private static IEnumerable<decimal> NumbersIn(string text)
    {
        foreach (Match digits in Regex.Matches(text, @"\d[\d,]*(?:\.\d+)?"))
        {
            if (decimal.TryParse(digits.Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                yield return value;
            }
        }

        var run = new List<string>();
        foreach (var word in Regex.Matches(text.ToLowerInvariant(), "[a-z]+").Select(m => m.Value).Append(""))
        {
            if (SmallNumberWords.ContainsKey(word) || MultiplierWords.ContainsKey(word) || (run.Count > 0 && word == "and"))
            {
                run.Add(word);
                continue;
            }
            if (run.Count > 0 && TryParseNumberWords(string.Join(' ', run), out var value))
            {
                yield return value;
            }
            run.Clear();
        }
    }

    /// <summary>Whether <paramref name="submission"/> matches one of the ';'-separated <paramref name="answers"/>.</summary>
    private static bool Accepts(string? answers, string submission)
    {
        if (string.IsNullOrWhiteSpace(submission) || string.IsNullOrWhiteSpace(answers))
        {
            return false;
        }
        var accepted = answers
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(Variants)
            .ToList();
        return WithoutLeadIns(submission).Any(typed => accepted.Any(answer => Matches(answer, typed)));
    }

    /// <summary>
    /// Words people type before the answer itself: "the answer is", "I think", "it's". Only at
    /// the start, and as whole words, so "itself" keeps its "its".
    /// </summary>
    private static readonly Regex LeadIn = new(
        @"^\s*(?:(?:the|my)\s+answer\s+is|answer\s+is|answer|i\s+think(?:\s+that)?|i\s+believe(?:\s+that)?|i[’']?d\s+say|it[’']?s|it\s+is)\b\s*[:=,]?\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// What was typed, then the same with each lead-in taken off in turn: "I think it's 9", "it's
    /// 9", "9". The rest still has to be an accepted answer on its own, so a lead-in never makes
    /// a wrong answer right (PRD 02).
    /// </summary>
    private static IEnumerable<string> WithoutLeadIns(string submission)
    {
        yield return submission;
        var text = submission;
        for (var i = 0; i < 3; i++)
        {
            var shorter = LeadIn.Replace(text, "");
            if (shorter == text || string.IsNullOrWhiteSpace(shorter))
            {
                yield break;
            }
            yield return text = shorter;
        }
    }

    /// <summary>
    /// Two numbers either side of a colon: a ratio like "2:1", or a time like "3:15". Left to
    /// the text check, it would lose its colon and read "21", and accept a typed "21".
    /// </summary>
    private static readonly Regex ColonPair = new(@"^\s*(\d+(?:\.\d+)?)\s*:\s*(\d+(?:\.\d+)?)\s*$");

    /// <summary>
    /// "2:1" against "2 : 1", part by part, so "1:2" or "1:5" for "3:15" don't pass. A part too
    /// big to read, thirty nines say, is simply no match: a parse that threw would end the
    /// visitor's page (PR 74 review).
    /// </summary>
    private static bool SameColonPair(Match accepted, string submission)
    {
        var typed = ColonPair.Match(submission);
        return typed.Success &&
               Enumerable.Range(1, 2).All(g =>
                   decimal.TryParse(accepted.Groups[g].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var want) &&
                   decimal.TryParse(typed.Groups[g].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var got) &&
                   want == got);
    }

    /// <summary>
    /// An accepted answer like "12 (a dozen)" also accepts "12", "a dozen",
    /// and the full "12 (a dozen)". Only a single parenthetical group is split
    /// this way — formula answers such as "5*(5-(1/5))" have multiple/nested
    /// parentheses and are left intact so they aren't matched by a fragment.
    /// </summary>
    private static IEnumerable<string> Variants(string accepted)
    {
        yield return accepted;

        if (accepted.Count(c => c == '(') != 1 || accepted.Count(c => c == ')') != 1)
        {
            yield break;
        }

        var open = accepted.IndexOf('(');
        var close = accepted.IndexOf(')');
        if (close > open)
        {
            yield return (accepted[..open] + accepted[(close + 1)..]).Trim();
            yield return accepted[(open + 1)..close].Trim();
        }
    }

    private static bool Matches(string accepted, string submission)
    {
        if (ColonPair.Match(accepted) is { Success: true } pair)
        {
            return SameColonPair(pair, submission);
        }

        // Numeric answers compare by value, so "9", "9.0", "1,000" vs "1000",
        // and spelled-out words like "eighty" all line up.
        if (TryParseNumber(accepted, out var acceptedNumber) &&
            TryParseNumber(submission, out var submittedNumber))
        {
            return Arithmetic.ValuesClose(acceptedNumber, submittedNumber);
        }

        // Formula answers are evaluated, so any arithmetically equivalent way of
        // writing the same expression counts: "5*(5-1/5)" and "(5-1/5)*5" are
        // the same answer.
        var acceptedEvaluates = Arithmetic.TryEvaluate(accepted, out var acceptedResult);
        if (acceptedEvaluates &&
            Arithmetic.TryEvaluate(submission, out var submittedResult) &&
            Arithmetic.ValuesClose(acceptedResult, submittedResult))
        {
            var acceptedIsFormula = !TryParseNumber(accepted, out _);
            if (!acceptedIsFormula)
            {
                // Plain-number answer: any arithmetic reaching it counts as showing work.
                return true;
            }

            // Formula puzzles ("make 24 from 5,5,5,1") are only solved by an
            // expression built from exactly the numbers the question supplies —
            // so a bare "24", or an unrelated "12+12", is not a solution.
            var submissionIsFormula = !TryParseNumber(submission, out _);
            if (submissionIsFormula &&
                Arithmetic.Operands(accepted).SequenceEqual(Arithmetic.Operands(submission)))
            {
                return true;
            }
        }

        // A formula answer is judged by the arithmetic above and nothing else. The checks
        // below compare text with its symbols stripped, so they would pass "5+5+1+5" (16)
        // or a bare "5515" for a stored "5*(5-(1/5))" on its digits alone. Found by the
        // test audit of 2026-09-28.
        if (acceptedEvaluates && !TryParseNumber(accepted, out _))
        {
            return false;
        }

        // Same, but for a number followed by a unit ("80 degrees" vs "eighty degrees").
        if (TrySplitNumberAndUnit(accepted, out var acceptedValue, out var acceptedUnit) &&
            TrySplitNumberAndUnit(submission, out var submittedValue, out var submittedUnit) &&
            acceptedValue == submittedValue &&
            Normalize(acceptedUnit) == Normalize(submittedUnit))
        {
            return true;
        }

        var normalizedAccepted = Normalize(accepted);
        return normalizedAccepted.Length > 0 && normalizedAccepted == Normalize(submission);
    }

    /// <summary>
    /// Splits "80 degrees" / "eighty degrees" / "48 mph" into value + unit,
    /// taking the longest leading run of words that parses as a number.
    /// </summary>
    private static bool TrySplitNumberAndUnit(string text, out decimal value, out string unit)
    {
        value = 0;
        unit = string.Empty;

        var words = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2)
        {
            return false;
        }

        for (var take = words.Length - 1; take >= 1; take--)
        {
            if (TryParseNumber(string.Join(' ', words[..take]), out var parsed))
            {
                value = parsed;
                unit = string.Join(' ', words[take..]);
                return unit.Length > 0;
            }
        }
        return false;
    }

    /// <summary>
    /// Parses a number written as digits ("80", "1,000", "-5.5") or as English
    /// words ("eighty", "forty-eight", "five thousand", "one hundred and one").
    /// </summary>
    private static bool TryParseNumber(string text, out decimal value)
    {
        if (decimal.TryParse(
                text.Trim().TrimEnd('.').Replace(",", ""),
                NumberStyles.Number | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value))
        {
            return true;
        }
        return TryParseNumberWords(text, out value);
    }

    private static readonly Dictionary<string, long> SmallNumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"] = 0, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4,
        ["five"] = 5, ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9,
        ["ten"] = 10, ["eleven"] = 11, ["twelve"] = 12, ["thirteen"] = 13,
        ["fourteen"] = 14, ["fifteen"] = 15, ["sixteen"] = 16, ["seventeen"] = 17,
        ["eighteen"] = 18, ["nineteen"] = 19, ["twenty"] = 20, ["thirty"] = 30,
        ["forty"] = 40, ["fourty"] = 40, ["fifty"] = 50, ["sixty"] = 60,
        ["seventy"] = 70, ["eighty"] = 80, ["ninety"] = 90,
    };

    private static readonly Dictionary<string, long> MultiplierWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hundred"] = 100, ["thousand"] = 1_000, ["million"] = 1_000_000, ["billion"] = 1_000_000_000,
    };

    private static bool TryParseNumberWords(string text, out decimal value)
    {
        value = 0;
        var words = text.ToLowerInvariant()
            .Replace('-', ' ')
            .Split([' ', '\t', ',', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w is not ("and" or "a"))
            .ToArray();

        if (words.Length == 0)
        {
            return false;
        }

        long total = 0, current = 0;
        var sawNumberWord = false;

        foreach (var word in words)
        {
            if (SmallNumberWords.TryGetValue(word, out var small))
            {
                current += small;
                sawNumberWord = true;
            }
            else if (MultiplierWords.TryGetValue(word, out var multiplier))
            {
                if (!sawNumberWord && multiplier >= 1000)
                {
                    return false;   // "thousand" alone isn't a number
                }
                if (current == 0)
                {
                    current = 1;    // "hundred" → 100
                }
                if (multiplier == 100)
                {
                    current *= multiplier;
                }
                else
                {
                    total += current * multiplier;
                    current = 0;
                }
                sawNumberWord = true;
            }
            else
            {
                return false;   // any non-number word ⇒ not a spelled-out number
            }
        }

        if (!sawNumberWord)
        {
            return false;
        }
        value = total + current;
        return true;
    }

    /// <summary>
    /// An answer reduced for telling two typings of it apart: lower case, letters and digits, and
    /// what is part of a number: the symbols in "2.5", "2:1", "2-1", "-3" or "1/2", and the
    /// space in "2 1". So "Right behind the leader!" and "right behind the leader" share a key,
    /// but "2-1" and "21" don't: stripped like the rest, they would, and a verdict on one would
    /// be reused for the other (PR 74 review).
    /// </summary>
    public static string AnswerKey(string text)
    {
        var compact = new StringBuilder(text.Length);
        var afterSpace = false;
        foreach (var c in text.ToLowerInvariant())
        {
            if (char.IsWhiteSpace(c))
            {
                afterSpace = true;
                continue;
            }
            if (afterSpace && compact.Length > 0 && char.IsDigit(compact[^1]) && char.IsDigit(c))
            {
                compact.Append(' ');
            }
            afterSpace = false;
            compact.Append(c);
        }

        var key = new StringBuilder(compact.Length);
        for (var i = 0; i < compact.Length; i++)
        {
            var c = compact[i];
            var startsOrJoinsNumber = i + 1 < compact.Length && char.IsDigit(compact[i + 1]) &&
                                      (i == 0 || !char.IsLetter(compact[i - 1]));
            if (char.IsLetterOrDigit(c) || startsOrJoinsNumber)
            {
                key.Append(c);
            }
        }
        return key.ToString();
    }

    private static string Normalize(string text) =>
        new(text.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
