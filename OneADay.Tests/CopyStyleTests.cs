using System.Text;
using System.Text.RegularExpressions;

namespace OneADay.Tests;

/// <summary>
/// No em-dashes and no emoji in anything the site shows or sends (author's decision,
/// 2026-10-27; PRD 09): both read as machine-written. Checked in the source, where every one
/// of those strings is written. Comments are left out, since only the strings reach anyone.
/// </summary>
public class CopyStyleTests
{
    /// <summary>Symbols that do a job: the close buttons' cross and the suits on the 24 cards.</summary>
    private const string Allowed = "✕♠♥♣♦";

    private static readonly Regex BlockComments =
        new(@"@\*.*?\*@|/\*.*?\*/|<!--.*?-->", RegexOptions.Singleline);

    /// <summary>A comment at the end of a line of code: "x = 1;   // like this".</summary>
    private static readonly Regex TrailingComment = new(@"\s//.*$");

    [Fact]
    public void The_sites_strings_have_no_em_dash_or_emoji()
    {
        var app = Path.Combine(RepoFiles.Root, "OneADay");
        var offenders = Directory.EnumerateFiles(app, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".razor") || f.EndsWith(".cs") || f.EndsWith(".js"))
            .Where(f => !Path.GetRelativePath(app, f).Split(Path.DirectorySeparatorChar)
                .Any(part => part is "bin" or "obj" or "lib"))
            .SelectMany(f => Offending(File.ReadAllText(f))
                .Select(hit => $"{Path.GetRelativePath(app, f)}:{hit}"))
            .ToList();

        Assert.True(offenders.Count == 0, "Em-dash or emoji in:\n" + string.Join("\n", offenders));
    }

    private static IEnumerable<string> Offending(string source)
    {
        // Blanked out line for line, so the line numbers in a failure still match the file.
        var code = BlockComments.Replace(source, m => new string('\n', m.Value.Count(c => c == '\n')));
        var lines = code.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith("//"))
            {
                continue;
            }
            line = TrailingComment.Replace(line, "");
            if (line.EnumerateRunes().Any(IsOdd))
            {
                yield return $"{i + 1}: {line.Trim()}";
            }
        }
    }

    private static bool IsOdd(Rune r) =>
        !Allowed.Contains(r.ToString()) &&
        (r.Value == 0x2014                              // em-dash
         || r.Value is >= 0x1F000 and <= 0x1FAFF        // emoji, most of them
         || r.Value is >= 0x2600 and <= 0x27BF          // older symbols that render as emoji
         || r.Value is >= 0x2B00 and <= 0x2BFF          // stars, arrows and squares
         || r.Value == 0xFE0F);                         // asks for the emoji style
}
