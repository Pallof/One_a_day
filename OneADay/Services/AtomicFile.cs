namespace OneADay.Services;

/// <summary>
/// Writes a file so that a crash can never leave it half-written.
///
/// <para><b>Why this exists.</b> <see cref="File.WriteAllText(string,string)"/> is two
/// operations: truncate the file to zero, then stream the new bytes in. Between them
/// the data exists nowhere. A deploy restart, an OOM kill, or a power cut inside that
/// window leaves a truncated file — and a truncated JSON file is not "slightly
/// damaged", it is <i>unparseable</i>. Losing half of <c>teasers.json</c> loses all of
/// it, and the app then throws on startup rather than booting with partial data.</para>
///
/// <para>That window opens on every write, and <c>StatsStore</c> writes on every
/// answer a visitor submits. <c>App_Data/</c> is also gitignored, so version control
/// is not a fallback.</para>
///
/// <para><b>How it avoids it.</b> Write to a sibling temp file, then rename it over the
/// target. Renaming within one filesystem is atomic — the kernel guarantees the name
/// points at either the old file or the new one, never at something in between. A
/// crash during the write leaves only the temp file dirty; a crash during the rename
/// is not observable. The window doesn't shrink, it stops existing.</para>
/// </summary>
public static class AtomicFile
{
    /// <summary>
    /// Writes <paramref name="contents"/> to <paramref name="path"/>, replacing it
    /// atomically.
    /// </summary>
    /// <param name="flushToDisk">
    /// Force the bytes out of the OS page cache before the rename. Off by default: the
    /// rename alone already survives everything short of losing the machine (process
    /// crash, OOM, restart, deploy), because the OS still holds the data and flushes
    /// it. Turning this on additionally survives power loss and kernel panic, at the
    /// cost of a real disk sync per write — which matters for the stats file, written
    /// on every submission.
    /// </param>
    public static void WriteAllText(string path, string contents, bool flushToDisk = false)
    {
        // Sibling, not the system temp directory: a rename is only atomic within one
        // filesystem. Across a mount boundary it silently degrades into copy-then-
        // delete, which reintroduces exactly the window this exists to close.
        var temp = path + ".tmp";

        try
        {
            if (flushToDisk)
            {
                using var stream = new FileStream(
                    temp, FileMode.Create, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream);
                writer.Write(contents);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            else
            {
                File.WriteAllText(temp, contents);
            }

            // The atomic step. Everything above touched only the temp file, so the
            // real file has been intact and complete this whole time.
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            // Leave the target alone and clean up after ourselves. A stale temp file is
            // harmless — the next write overwrites it, and nothing ever reads it — but
            // there is no reason to leave litter in App_Data.
            TryDelete(temp);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best effort. Failing to remove a temp file must never mask the real error.
        }
    }
}
