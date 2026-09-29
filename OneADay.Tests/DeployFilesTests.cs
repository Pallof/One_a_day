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
