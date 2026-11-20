using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Streams;

/// <summary>
/// Delivers task-assignment changes to an <see cref="IStreamsGroupAssignmentListener"/> one at a time, in the
/// order they were published, on a loop of its own, so a slow callback never delays a heartbeat and a callback
/// can call back into its member.
/// </summary>
/// <remarks>
/// Publishers must serialize their calls (the member's worker, or the in-memory group lock). Publishing an
/// assignment that is reference-equal to the last one does no work and allocates nothing.
/// </remarks>
internal sealed partial class StreamsGroupAssignmentNotifier
{
    private static readonly AsyncLocal<StreamsGroupAssignmentNotifier?> Dispatching = new();

    private readonly IStreamsGroupAssignmentListener _listener;
    private readonly ILogger _logger;
    private readonly Channel<Notification> _notifications = Channel.CreateUnbounded<Notification>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _dispatchTask;
    private StreamsGroupAssignment _published = Empty;

    internal static readonly StreamsGroupAssignment Empty = new()
    {
        ActiveTasks = [],
        StandbyTasks = [],
        WarmupTasks = []
    };

    internal StreamsGroupAssignmentNotifier(IStreamsGroupAssignmentListener listener, ILogger? logger = null)
    {
        _listener = listener;
        _logger = logger ?? NullLogger.Instance;
        // Callbacks run on the thread pool: never on the publisher's thread, which holds the member's worker or
        // the group lock, and never on a context captured from the thread that created the member.
        _dispatchTask = Task.Run(DispatchAsync);
    }

    /// <summary>Queues the revoked and newly assigned tasks between the last published assignment and this one.</summary>
    internal void PublishAssignment(StreamsGroupAssignment assignment)
    {
        var previous = _published;
        if (ReferenceEquals(previous, assignment))
        {
            return;
        }

        _published = assignment;
        var revoked = Subtract(previous, assignment);
        var assigned = Subtract(assignment, previous);
        if (revoked is not null)
        {
            _notifications.Writer.TryWrite(new Notification(NotificationKind.Revoked, revoked));
        }
        if (assigned is not null)
        {
            _notifications.Writer.TryWrite(new Notification(NotificationKind.Assigned, assigned));
        }
    }

    /// <summary>
    /// Queues every task the member held as lost and starts again from an empty assignment. The member held the
    /// last published assignment and the tasks it last reported as owned, which still include revoked tasks the
    /// caller has not yet acknowledged through <see cref="IStreamsGroupMember.UpdateAsync"/>.
    /// </summary>
    internal void PublishLost(StreamsGroupAssignment reported)
    {
        var previous = _published;
        _published = Empty;
        var lost = Union(previous, reported);
        if (!IsEmpty(lost))
        {
            _notifications.Writer.TryWrite(new Notification(NotificationKind.Lost, lost));
        }
    }

    /// <summary>
    /// Stops accepting changes but keeps delivering the queued ones, for a member whose worker stopped without
    /// being closed. A later <see cref="StopAsync"/> still drops what is left.
    /// </summary>
    internal void Complete() => _notifications.Writer.TryComplete();

    /// <summary>
    /// Stops delivery: queued notifications are dropped and the running callback's token is cancelled. Waits for
    /// that callback to return unless called from inside one.
    /// </summary>
    internal Task StopAsync()
    {
        _stopping.Cancel();
        _notifications.Writer.TryComplete();

        return ReferenceEquals(Dispatching.Value, this) ? Task.CompletedTask : _dispatchTask;
    }

    private async Task DispatchAsync()
    {
        Dispatching.Value = this;
        var token = _stopping.Token;
        while (!token.IsCancellationRequested
               && await _notifications.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            while (!token.IsCancellationRequested && _notifications.Reader.TryRead(out var notification))
            {
                await InvokeAsync(notification, token).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask InvokeAsync(Notification notification, CancellationToken cancellationToken)
    {
        try
        {
            var task = notification.Kind switch
            {
                NotificationKind.Assigned => _listener.OnTasksAssignedAsync(notification.Tasks, cancellationToken),
                NotificationKind.Revoked => _listener.OnTasksRevokedAsync(notification.Tasks, cancellationToken),
                _ => _listener.OnTasksLostAsync(notification.Tasks, cancellationToken)
            };
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LogListenerFailed(exception, notification.Kind);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Streams group assignment listener failed handling {Kind} tasks")]
    private partial void LogListenerFailed(Exception exception, NotificationKind kind);

    private static bool IsEmpty(StreamsGroupAssignment assignment) =>
        assignment.ActiveTasks.Count == 0 && assignment.StandbyTasks.Count == 0 && assignment.WarmupTasks.Count == 0;

    private static StreamsGroupAssignment Union(StreamsGroupAssignment left, StreamsGroupAssignment right)
    {
        var extra = Subtract(right, left);
        return extra is null
            ? left
            : new StreamsGroupAssignment
            {
                ActiveTasks = Concat(left.ActiveTasks, extra.ActiveTasks),
                StandbyTasks = Concat(left.StandbyTasks, extra.StandbyTasks),
                WarmupTasks = Concat(left.WarmupTasks, extra.WarmupTasks)
            };
    }

    /// <summary>Merges task sets that share no task, keeping one entry per subtopology.</summary>
    private static IReadOnlyList<StreamsGroupTaskSet> Concat(
        IReadOnlyList<StreamsGroupTaskSet> left,
        IReadOnlyList<StreamsGroupTaskSet> right)
    {
        if (right.Count == 0)
        {
            return left;
        }

        var result = new List<StreamsGroupTaskSet>(left.Count + right.Count);
        foreach (var taskSet in left)
        {
            List<int>? partitions = null;
            foreach (var other in right)
            {
                if (other.SubtopologyId == taskSet.SubtopologyId)
                {
                    (partitions ??= [.. taskSet.Partitions]).AddRange(other.Partitions);
                }
            }

            result.Add(partitions is null
                ? taskSet
                : new StreamsGroupTaskSet { SubtopologyId = taskSet.SubtopologyId, Partitions = partitions.ToArray() });
        }

        foreach (var other in right)
        {
            if (!ContainsSubtopology(left, other.SubtopologyId))
            {
                result.Add(other);
            }
        }

        return result.ToArray();
    }

    private static bool ContainsSubtopology(IReadOnlyList<StreamsGroupTaskSet> taskSets, string subtopologyId)
    {
        foreach (var taskSet in taskSets)
        {
            if (taskSet.SubtopologyId == subtopologyId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the tasks in <paramref name="left"/> missing from <paramref name="right"/>, or null when none are.</summary>
    private static StreamsGroupAssignment? Subtract(StreamsGroupAssignment left, StreamsGroupAssignment right)
    {
        var active = Subtract(left.ActiveTasks, right.ActiveTasks);
        var standby = Subtract(left.StandbyTasks, right.StandbyTasks);
        var warmup = Subtract(left.WarmupTasks, right.WarmupTasks);
        return active.Count == 0 && standby.Count == 0 && warmup.Count == 0
            ? null
            : new StreamsGroupAssignment { ActiveTasks = active, StandbyTasks = standby, WarmupTasks = warmup };
    }

    private static IReadOnlyList<StreamsGroupTaskSet> Subtract(
        IReadOnlyList<StreamsGroupTaskSet> left,
        IReadOnlyList<StreamsGroupTaskSet> right)
    {
        if (left.Count == 0)
        {
            return [];
        }

        HashSet<(string SubtopologyId, int Partition)>? remaining = null;
        if (right.Count > 0)
        {
            remaining = [];
            foreach (var taskSet in right)
            {
                foreach (var partition in taskSet.Partitions)
                {
                    remaining.Add((taskSet.SubtopologyId, partition));
                }
            }
        }

        List<StreamsGroupTaskSet>? result = null;
        foreach (var taskSet in left)
        {
            List<int>? partitions = null;
            foreach (var partition in taskSet.Partitions)
            {
                if (remaining is null || !remaining.Contains((taskSet.SubtopologyId, partition)))
                {
                    (partitions ??= []).Add(partition);
                }
            }

            if (partitions is not null)
            {
                (result ??= []).Add(new StreamsGroupTaskSet
                {
                    SubtopologyId = taskSet.SubtopologyId,
                    Partitions = partitions.ToArray()
                });
            }
        }

        return result is null ? [] : result.ToArray();
    }

    private enum NotificationKind : byte
    {
        Assigned,
        Revoked,
        Lost
    }

    private readonly record struct Notification(NotificationKind Kind, StreamsGroupAssignment Tasks);
}
