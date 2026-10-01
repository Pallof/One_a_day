namespace OneADay.Tests;

/// <summary>
/// What <c>fly deploy</c> uploads. Everything not listed in .dockerignore goes to Fly's builder,
/// so a private file missing from that list leaves the author's machine on every deploy.
/// </summary>
public class DeployFilesTests
{
    [Theory]
    [InlineData("OneADay/App_Data/")]                  // the bank, subscribers and keys
    [InlineData("OneADay/BrainTeaserQuestions.txt")]   // the early draft, answers included
    public void The_deploy_upload_leaves_out_every_private_file(string path)
    {
        // Found missing in the security review of 2026-09-28: the draft was gitignored but
        // not dockerignored, so it went to Fly's builder with every deploy.
        var excluded = File.ReadAllLines(Path.Combine(FindRepoRoot(), ".dockerignore"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'));

        Assert.Contains(path, excluded);
    }

    [Fact]
    public void The_build_restores_only_once_the_razor_files_are_in()
    {
        // The SDK decides whether to fetch Blazor's own script by looking for .razor files at
        // restore time. The first deploy restored from the .csproj alone, so the live site had
        // no blazor.web.js and none of its buttons worked.
        var lines = File.ReadAllLines(Path.Combine(FindRepoRoot(), "Dockerfile"))
            .Select(line => line.Trim())
            .ToList();

        var sourceCopied = lines.IndexOf("COPY OneADay/ OneADay/");
        var firstDotnet = lines.FindIndex(line => line.StartsWith("RUN dotnet "));

        Assert.True(sourceCopied >= 0, "The Dockerfile no longer copies the OneADay folder.");
        Assert.True(firstDotnet > sourceCopied,
            $"Line {firstDotnet + 1} runs dotnet before the source is copied in on line {sourceCopied + 1}.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, ".gitignore")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Couldn't find the repo root.");
    }
}
