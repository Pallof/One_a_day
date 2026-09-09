using System.Text.Json;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// Writes that a crash cannot leave half-finished.
///
/// <para>The failure this prevents is not partial data, it is <i>no</i> data:
/// <c>File.WriteAllText</c> truncates before it writes, and a truncated JSON file
/// doesn't parse at all. Losing half of <c>teasers.json</c> loses the whole bank, and
/// the app then throws on startup instead of booting.</para>
///
/// <para>True atomicity can only be proven by killing the process mid-write, which a
/// unit test can't do. What is tested here is everything that makes it hold: the real
/// file is never the thing being written to, a failed write leaves the original
/// untouched, and no litter is left behind.</para>
/// </summary>
public class AtomicFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "oneaday-atomic", Guid.NewGuid().ToString("N"));

    public AtomicFileTests() => Directory.CreateDirectory(_dir);

    private string Path_(string name) => System.IO.Path.Combine(_dir, name);

    // ---- the basics ------------------------------------------------------------

    [Fact]
    public void Writes_a_new_file()
    {
        var path = Path_("new.json");

        AtomicFile.WriteAllText(path, "{\"hello\":\"world\"}");

        Assert.Equal("{\"hello\":\"world\"}", File.ReadAllText(path));
    }

    [Fact]
    public void Replaces_an_existing_file_completely()
    {
        // Not append, not merge — the old content must be entirely gone, including
        // when the new content is shorter than the old.
        var path = Path_("existing.json");
        File.WriteAllText(path, new string('x', 5_000));

        AtomicFile.WriteAllText(path, "short");

        Assert.Equal("short", File.ReadAllText(path));
    }

    [Fact]
    public void Leaves_no_temp_file_behind()
    {
        var path = Path_("clean.json");

        AtomicFile.WriteAllText(path, "content");

        Assert.False(File.Exists(path + ".tmp"));
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void Round_trips_a_realistic_payload()
    {
        // Large and multi-line, like the real teaser bank — the case where a torn
        // write would actually be observable.
        var path = Path_("teasers.json");
        var payload = JsonSerializer.Serialize(
            Enumerable.Range(0, 500).Select(i => new { Id = i, Text = $"teaser number {i}" }),
            new JsonSerializerOptions { WriteIndented = true });

        AtomicFile.WriteAllText(path, payload);

        Assert.Equal(payload, File.ReadAllText(path));
        Assert.True(File.ReadAllText(path).Length > 10_000);
    }

    // ---- the point of the exercise ---------------------------------------------

    [Fact]
    public void A_failed_write_leaves_the_original_intact()
    {
        // The property that matters. Under File.WriteAllText the original is already
        // destroyed by the time anything can go wrong; here the real file isn't
        // touched until the rename, so a failure mid-write costs nothing.
        //
        // The failure is induced by parking a *directory* at the temp path, so
        // creating the temp file throws.
        var path = Path_("precious.json");
        File.WriteAllText(path, "the original content");
        Directory.CreateDirectory(path + ".tmp");

        Assert.ThrowsAny<Exception>(() => AtomicFile.WriteAllText(path, "replacement"));

        Assert.Equal("the original content", File.ReadAllText(path));
    }

    [Fact]
    public void A_stale_temp_file_from_an_earlier_crash_does_not_block_the_next_write()
    {
        // If the process died between writing the temp file and renaming it, a stale
        // .tmp is left over. The next write must overwrite it rather than fail.
        var path = Path_("recovered.json");
        File.WriteAllText(path, "old");
        File.WriteAllText(path + ".tmp", "garbage from a crash");

        AtomicFile.WriteAllText(path, "new");

        Assert.Equal("new", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void The_temp_file_is_a_sibling_so_the_rename_stays_atomic()
    {
        // A rename is only atomic within one filesystem. Putting the temp file in the
        // system temp directory would silently degrade it to copy-then-delete across a
        // mount boundary, reopening the window this exists to close. Proven by
        // catching the temp file in the act.
        var path = Path_("sibling.json");
        string? observedDirectory = null;

        var watcher = new FileSystemWatcher(_dir, "*.tmp") { EnableRaisingEvents = true };
        watcher.Created += (_, e) => observedDirectory = System.IO.Path.GetDirectoryName(e.FullPath);

        AtomicFile.WriteAllText(path, new string('y', 200_000));   // big enough to observe
        SpinWait.SpinUntil(() => observedDirectory is not null, TimeSpan.FromSeconds(2));
        watcher.Dispose();

        Assert.Equal(_dir, observedDirectory);
    }

    // ---- durability option ------------------------------------------------------

    [Fact]
    public void Flushing_to_disk_writes_the_same_bytes()
    {
        // The flushToDisk path is a different code branch (FileStream rather than
        // File.WriteAllText), so it needs its own coverage — it would be easy for it
        // to work but produce a different encoding.
        var path = Path_("durable.json");
        const string content = "{\"durable\":true,\"unicode\":\"café ♠\"}";

        AtomicFile.WriteAllText(path, content, flushToDisk: true);

        Assert.Equal(content, File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Both_paths_produce_identical_files()
    {
        var quick = Path_("quick.json");
        var durable = Path_("durable2.json");
        const string content = "line one\nline two\nline three";

        AtomicFile.WriteAllText(quick, content, flushToDisk: false);
        AtomicFile.WriteAllText(durable, content, flushToDisk: true);

        Assert.Equal(File.ReadAllBytes(quick), File.ReadAllBytes(durable));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test over.
        }
    }
}
