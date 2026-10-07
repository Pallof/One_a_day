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

    [Fact]
    public void The_live_data_download_fetches_exactly_its_allowlist()
    {
        // This machine holds the real Gmail password, so a real subscriber list here could mail
        // real people; keys/ decrypts every visitor's stored note. Neither may ever come down
        // (PRD 16). Runs the real script against a stand-in `fly` that records what it was asked
        // for. The first version only read the FILES= line, so an extra copy line elsewhere in
        // the script passed (code review, 2026-10-07).
        using var run = new DownloadRun(siteRunningHere: false);

        var exit = run.Start(out _);

        Assert.Equal(0, exit);
        Assert.Equal(
            new[] { "/app/App_Data/metrics.json", "/app/App_Data/stats.json", "/app/App_Data/rotation.json" },
            run.Requested());
        Assert.Equal(new[] { "metrics.json", "rotation.json", "stats.json" }, run.Landed());
    }

    [Fact]
    public void The_live_data_download_refuses_while_the_site_runs_here()
    {
        // The running site holds stats and rotation in memory; its next save would write those
        // old copies straight back over the download.
        using var run = new DownloadRun(siteRunningHere: true);

        var exit = run.Start(out var error);

        Assert.NotEqual(0, exit);
        Assert.Contains("Stop the site", error);
        Assert.Empty(run.Requested());
        Assert.Empty(run.Landed());
    }

    /// <summary>
    /// The download script run in a scratch copy of the repo's layout, with `fly` and `pgrep`
    /// replaced, so nothing touches the network, the live site or this machine's App_Data.
    /// </summary>
    private sealed class DownloadRun : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "oneaday-tests", Guid.NewGuid().ToString("N"));
        private string Bin => Path.Combine(_root, "fakebin");
        private string Log => Path.Combine(_root, "fly-requests.log");
        private string AppData => Path.Combine(_root, "OneADay", "App_Data");

        public DownloadRun(bool siteRunningHere)
        {
            Directory.CreateDirectory(Path.Combine(_root, "deploy"));
            Directory.CreateDirectory(AppData);
            Directory.CreateDirectory(Bin);
            File.WriteAllText(Path.Combine(_root, "fly.toml"), "");
            File.Copy(Path.Combine(FindRepoRoot(), "deploy", "pull-live-data.sh"),
                      Path.Combine(_root, "deploy", "pull-live-data.sh"));

            // `fly ssh sftp get REMOTE LOCAL`: note what was asked for, and hand back a file.
            Script("fly", $"echo \"$4\" >> '{Log}'\necho live > \"$5\"\n");
            Script("pgrep", siteRunningHere ? "exit 0\n" : "exit 1\n");
        }

        private void Script(string name, string body)
        {
            var path = Path.Combine(Bin, name);
            File.WriteAllText(path, "#!/bin/sh\n" + body);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        public int Start(out string error)
        {
            var start = new System.Diagnostics.ProcessStartInfo("/bin/sh", "deploy/pull-live-data.sh")
            {
                WorkingDirectory = _root,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            start.Environment["PATH"] = $"{Bin}:/usr/bin:/bin";
            using var process = System.Diagnostics.Process.Start(start)!;
            error = process.StandardError.ReadToEnd();
            process.StandardOutput.ReadToEnd();
            Assert.True(process.WaitForExit(10_000), "The download script didn't finish.");
            return process.ExitCode;
        }

        public string[] Requested() => File.Exists(Log) ? File.ReadAllLines(Log) : [];

        public string[] Landed() => Directory.GetFiles(AppData).Select(Path.GetFileName).Order().ToArray()!;

        public void Dispose() => Directory.Delete(_root, recursive: true);
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
