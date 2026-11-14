using System.Globalization;

namespace Dekaf.Streams.Internal.Runtime;

/// <summary>Lays out persistent state as <c>root/application/subtopology_partition/store</c>.</summary>
internal sealed class StateDirectory(string root, string applicationId)
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Locks a task's directory and takes its checkpoint. Without a checkpoint the directory holds state
    /// that may include uncommitted writes, so it is emptied and the task restores in full.
    /// </summary>
    public async ValueTask<TaskDirectory> OpenAsync(TaskId id, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, applicationId, id.ToString());
        Directory.CreateDirectory(path);

        // The previous owner releases the lock before it gives the task up, so waiting is short unless
        // another process on this machine still runs the task.
        var lockPath = Path.Combine(path, TaskDirectory.LockFile);
        var deadline = DateTime.UtcNow + LockTimeout;
        FileStream lockFile;
        while (true)
        {
            try
            {
                lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                break;
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(LockRetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                throw new StreamsException(
                    $"State directory '{path}' is locked by another process. Two instances that run on one machine need separate state directories.",
                    exception);
            }
        }

        try
        {
            var checkpointPath = Path.Combine(path, TaskDirectory.CheckpointFile);
            Dictionary<string, long>? checkpoint = null;
            if (File.Exists(checkpointPath))
            {
                checkpoint = ReadCheckpoint(checkpointPath);

                // Taken, not copied: from here until the next clean close the state is ahead of it.
                File.Delete(checkpointPath);
            }

            if (checkpoint is null)
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(path))
                {
                    if (Path.GetFileName(entry) == TaskDirectory.LockFile)
                    {
                        continue;
                    }

                    if (Directory.Exists(entry))
                    {
                        Directory.Delete(entry, recursive: true);
                    }
                    else
                    {
                        File.Delete(entry);
                    }
                }
            }

            return new TaskDirectory(path, lockFile, checkpoint ?? []);
        }
        catch
        {
            lockFile.Dispose();
            throw;
        }
    }

    // One "offset topic" line per restore topic. A file that does not parse is treated as no checkpoint.
    private static Dictionary<string, long>? ReadCheckpoint(string path)
    {
        var checkpoint = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(path))
        {
            var separator = line.IndexOf(' ', StringComparison.Ordinal);
            if (separator <= 0
                || !long.TryParse(line.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var offset))
            {
                return null;
            }

            checkpoint[line[(separator + 1)..]] = offset;
        }

        return checkpoint;
    }
}

/// <summary>The locked directory of one task.</summary>
internal sealed class TaskDirectory(string path, FileStream lockFile, Dictionary<string, long> checkpoint)
{
    public const string LockFile = ".lock";
    public const string CheckpointFile = ".checkpoint";

    public string Path { get; } = path;

    /// <summary>The offset to resume restoring <paramref name="topic"/> from, or -1 to restore from the start.</summary>
    public long CheckpointOf(string topic) => checkpoint.GetValueOrDefault(topic, -1);

    /// <summary>
    /// Releases the directory. Call after the stores are closed. A non-null <paramref name="offsets"/>
    /// records that the stores match their restore topics up to those offsets.
    /// </summary>
    public void Close(Dictionary<string, long>? offsets)
    {
        try
        {
            if (offsets is not null)
            {
                var lines = new List<string>(offsets.Count);
                foreach (var entry in offsets)
                {
                    lines.Add(string.Create(CultureInfo.InvariantCulture, $"{entry.Value} {entry.Key}"));
                }

                // Written beside the target and renamed, so a crash never leaves half a checkpoint.
                var target = System.IO.Path.Combine(Path, CheckpointFile);
                var temporary = target + ".tmp";
                File.WriteAllLines(temporary, lines);
                File.Move(temporary, target, overwrite: true);
            }
        }
        finally
        {
            lockFile.Dispose();
        }
    }
}
