using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Retry;

namespace Dekaf.Streams;

internal sealed class StreamsGroupMember : IStreamsGroupMember
{
    private const int CommandCapacity = 64;

    private static readonly StreamsGroupAssignment EmptyAssignment = new()
    {
        ActiveTasks = [],
        StandbyTasks = [],
        WarmupTasks = []
    };

    private readonly StreamsGroupMemberOptions _options;
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
    private readonly Channel<MemberCommand> _commands = Channel.CreateBounded<MemberCommand>(
        new BoundedChannelOptions(CommandCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    private readonly object _commandWriteGate = new();
    private readonly CancellationTokenSource _commandAdmissionCancellation = new();
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private readonly StreamsGroupHeartbeatRequestCache _steadyRequestCache = new();
    private readonly Timer _heartbeatTimer;
    private readonly Task _workerTask;
    private readonly int _retryBackoffMs;
    private readonly int _retryBackoffMaxMs;
    private string _memberId = Guid.NewGuid().ToString();

    private volatile StreamsGroupMemberSnapshot _snapshot = new()
    {
        Assignment = EmptyAssignment,
        PartitionsByUserEndpoint = [],
        Status = []
    };
    private Exception? _backgroundFailure;
    private volatile bool _initialized;
    private int _closeRequested;
    private int _disposed;
    private int _coordinatorId = -1;
    private int _memberEpoch;
    private int _heartbeatIntervalMs = 5_000;
    private bool _ambiguousMembership;

    private StreamsGroupHeartbeatTopology? _topology;
    private IReadOnlyList<StreamsGroupHeartbeatTaskIds>? _activeTasks;
    private IReadOnlyList<StreamsGroupHeartbeatTaskIds>? _standbyTasks;
    private IReadOnlyList<StreamsGroupHeartbeatTaskIds>? _warmupTasks;
    private string? _processId;
    private StreamsGroupHeartbeatEndpoint? _userEndpoint;
    private IReadOnlyList<StreamsGroupHeartbeatKeyValue>? _clientTags;
    private IReadOnlyList<StreamsGroupHeartbeatTaskOffset>? _taskOffsets;
    private IReadOnlyList<StreamsGroupHeartbeatTaskOffset>? _taskEndOffsets;
    private int _endpointInformationEpoch;
    private MemberCommand? _closeCommand;

    internal StreamsGroupMember(
        StreamsGroupMemberOptions options,
        IConnectionPool connectionPool,
        MetadataManager metadataManager,
        int retryBackoffMs = 100,
        int retryBackoffMaxMs = 1_000)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connectionPool);
        ArgumentNullException.ThrowIfNull(metadataManager);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.GroupId);
        if (options.RebalanceTimeout.TotalMilliseconds < 1
            || options.RebalanceTimeout.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "RebalanceTimeout must be greater than zero and no greater than Int32.MaxValue milliseconds.");
        }

        _options = options;
        _connectionPool = connectionPool;
        _metadataManager = metadataManager;
        ExponentialRetryBackoff.Validate(retryBackoffMs, retryBackoffMaxMs);
        _retryBackoffMs = retryBackoffMs;
        _retryBackoffMaxMs = retryBackoffMaxMs;
        _heartbeatTimer = new Timer(
            static state => ((StreamsGroupMember)state!).QueueHeartbeat(),
            this,
            Timeout.Infinite,
            Timeout.Infinite);
        _workerTask = RunAsync();
    }

    public string GroupId => _options.GroupId;
    public string? InstanceId => _options.InstanceId;
    public StreamsGroupMemberSnapshot Snapshot => _snapshot;

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfClosed();
        if (_initialized)
            return;

        await _initializeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfClosed();
            if (_initialized)
                return;

            await _metadataManager.InitializeAsync(cancellationToken).ConfigureAwait(false);
            ThrowIfClosed();
            _initialized = true;
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    internal void MarkInitializedForTesting() => _initialized = true;

    public ValueTask<StreamsGroupHeartbeatResult> JoinAsync(
        StreamsGroupMemberUpdate initialState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        if (initialState.Topology is null)
            throw new ArgumentException("The initial Streams group state must include a topology.", nameof(initialState));

        return QueueOperationAsync(MemberCommand.Join(initialState), cancellationToken);
    }

    public ValueTask<StreamsGroupHeartbeatResult> UpdateAsync(
        StreamsGroupMemberUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        return QueueOperationAsync(MemberCommand.ForUpdate(update), cancellationToken);
    }

    public ValueTask<StreamsGroupHeartbeatResult> ReportTaskOffsetsAsync(
        StreamsGroupTaskOffsetReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        return QueueOperationAsync(MemberCommand.ReportOffsets(report), cancellationToken);
    }

    public ValueTask CloseAsync(CancellationToken cancellationToken = default) =>
        CloseAsync(new StreamsGroupCloseOptions(), cancellationToken);

    public async ValueTask CloseAsync(
        StreamsGroupCloseOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(options.GroupMembershipOperation))
            throw new ArgumentOutOfRangeException(nameof(options));
        cancellationToken.ThrowIfCancellationRequested();
        MemberCommand command;
        var enqueueClose = false;
        lock (_commandWriteGate)
        {
            if (Volatile.Read(ref _closeRequested) == 0)
            {
                Volatile.Write(ref _closeRequested, 1);
                command = MemberCommand.Close(options);
                Volatile.Write(ref _closeCommand, command);
                enqueueClose = true;
            }
            else
            {
                command = Volatile.Read(ref _closeCommand)!;
            }
        }

        if (enqueueClose)
        {
            _commandAdmissionCancellation.Cancel();
            if (!_commands.Writer.TryWrite(command))
            {
                try
                {
                    await _workerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    ObserveFault(command.Completion.Task);
                    throw;
                }
                CompleteRejectedClose();
            }
        }

        try
        {
            await WaitForInitializationDrainAsync(cancellationToken).ConfigureAwait(false);
            await command.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ObserveFault(command.Completion.Task);
            throw;
        }
        await _workerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask WaitForInitializationDrainAsync(CancellationToken cancellationToken)
    {
        await _initializeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        _initializeLock.Release();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            await CloseAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Explicit CloseAsync callers receive terminal-heartbeat failures. Disposal is
            // best-effort cleanup and must still release the timer after observing the fault.
        }
        finally
        {
            _heartbeatTimer.Dispose();
            _commandAdmissionCancellation.Dispose();
            _initializeLock.Dispose();
        }
    }

    private void CompleteRejectedClose() =>
        Volatile.Read(ref _closeCommand)?.Completion.TrySetException(
            Volatile.Read(ref _backgroundFailure) ??
            new ObjectDisposedException(nameof(StreamsGroupMember)));

    private async ValueTask<StreamsGroupHeartbeatResult> QueueOperationAsync(
        MemberCommand command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfNotInitialized();

        var backgroundFailure = Volatile.Read(ref _backgroundFailure);
        if (backgroundFailure is not null && command.Kind != MemberCommandKind.Join)
            throw CreateBackgroundFailureException(backgroundFailure);

        command.CancellationToken = cancellationToken;
        await EnqueueCommandAsync(command, cancellationToken).ConfigureAwait(false);

        try
        {
            return (await command.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false))!;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ObserveFault(command.Completion.Task);
            throw;
        }
    }

    private ValueTask EnqueueCommandAsync(MemberCommand command, CancellationToken cancellationToken)
    {
        lock (_commandWriteGate)
        {
            ThrowIfClosed();
            if (_commands.Writer.TryWrite(command))
                return ValueTask.CompletedTask;
        }

        return EnqueueCommandSlowAsync(command, cancellationToken);
    }

    private async ValueTask EnqueueCommandSlowAsync(
        MemberCommand command,
        CancellationToken cancellationToken)
    {
        using var admissionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _commandAdmissionCancellation.Token);
        try
        {
            await _commands.Writer.WriteAsync(command, admissionCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            _commandAdmissionCancellation.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(StreamsGroupMember));
        }
        catch (ChannelClosedException)
        {
            var backgroundFailure = Volatile.Read(ref _backgroundFailure);
            throw backgroundFailure is null
                ? new ObjectDisposedException(nameof(StreamsGroupMember))
                : CreateBackgroundFailureException(backgroundFailure);
        }
    }

    private async Task RunAsync()
    {
        try
        {
            while (await _commands.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (true)
                {
                    if (Volatile.Read(ref _closeCommand) is { } closeCommand)
                    {
                        await CompleteCloseAsync(closeCommand).ConfigureAwait(false);
                        return;
                    }

                    if (!_commands.Reader.TryRead(out var command))
                        break;

                    if (Volatile.Read(ref _closeCommand) is { } racedCloseCommand)
                    {
                        if (!ReferenceEquals(command, racedCloseCommand) &&
                            command.Kind != MemberCommandKind.Heartbeat)
                        {
                            CompleteAbandonedCommand(
                                command,
                                new ObjectDisposedException(nameof(StreamsGroupMember)));
                        }
                        await CompleteCloseAsync(racedCloseCommand).ConfigureAwait(false);
                        return;
                    }

                    if (command.Kind == MemberCommandKind.Heartbeat)
                    {
                        await ProcessBackgroundHeartbeatAsync().ConfigureAwait(false);
                        continue;
                    }

                    if (command.Kind == MemberCommandKind.Close)
                    {
                        await CompleteCloseAsync(command).ConfigureAwait(false);
                        return;
                    }

                    if (command.CancellationToken.IsCancellationRequested)
                    {
                        command.Completion.TrySetCanceled(command.CancellationToken);
                        continue;
                    }

                    var backgroundFailure = Volatile.Read(ref _backgroundFailure);
                    if (backgroundFailure is not null && command.Kind != MemberCommandKind.Join)
                    {
                        command.Completion.TrySetException(
                            CreateBackgroundFailureException(backgroundFailure));
                        continue;
                    }

                    try
                    {
                        var result = await ProcessOperationAsync(command).ConfigureAwait(false);
                        command.Completion.TrySetResult(result);
                    }
                    catch (Exception exception)
                    {
                        command.Completion.TrySetException(exception);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            MarkUnjoined();
            Volatile.Write(ref _backgroundFailure, exception);
            _commands.Writer.TryComplete(exception);
            Volatile.Read(ref _closeCommand)?.Completion.TrySetException(exception);
            while (_commands.Reader.TryRead(out var command))
                command.Completion.TrySetException(exception);
        }
        finally
        {
            _heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    private async ValueTask CompleteCloseAsync(MemberCommand command)
    {
        await ProcessCloseAsync(command.CloseOptions!).AsTask().ContinueWith(
            static (task, state) => CompleteCloseCommand(task, (MemberCommand)state!),
            command,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default).ConfigureAwait(false);

        _commands.Writer.TryComplete();
        var disposed = new ObjectDisposedException(nameof(StreamsGroupMember));
        while (_commands.Reader.TryRead(out var pending))
        {
            if (ReferenceEquals(pending, command) || pending.Kind == MemberCommandKind.Heartbeat)
                continue;
            CompleteAbandonedCommand(pending, disposed);
        }
    }

    private static void CompleteCloseCommand(Task task, MemberCommand command)
    {
        if (task.Status == TaskStatus.RanToCompletion)
        {
            command.Completion.TrySetResult(null);
            return;
        }

        if (task.IsCanceled)
        {
            command.Completion.TrySetCanceled();
            return;
        }

        var aggregate = task.Exception!;
        command.Completion.TrySetException(
            aggregate.InnerExceptions.Count == 1 ? aggregate.InnerException! : aggregate);
    }

    private static void CompleteAbandonedCommand(
        MemberCommand command,
        ObjectDisposedException disposed)
    {
        if (command.CancellationToken.IsCancellationRequested)
            command.Completion.TrySetCanceled(command.CancellationToken);
        else
            command.Completion.TrySetException(disposed);
    }

    private async ValueTask<StreamsGroupHeartbeatResult> ProcessOperationAsync(MemberCommand command)
    {
        if (command.Kind != MemberCommandKind.Join && _memberEpoch <= 0)
            throw new InvalidOperationException("JoinAsync must complete before updating a Streams group member.");
        if (command.Kind == MemberCommandKind.Join && _memberEpoch > 0)
            throw new InvalidOperationException("The Streams group member has already joined.");

        var previousState = CaptureOperationState();
        StreamsGroupHeartbeatResponse response;
        try
        {
            StreamsGroupHeartbeatRequest request;
            int? recoveryJoinEpoch = command.Kind == MemberCommandKind.Join ? 0 : null;
            switch (command.Kind)
            {
                case MemberCommandKind.Join:
                    var initialState = command.Update!;
                    ApplyUpdate(initialState);
                    request = CreateJoinRequest(shutdownApplication: initialState.ShutdownApplication);
                    break;
                case MemberCommandKind.Update:
                    var update = command.Update!;
                    ApplyUpdate(update);
                    request = update.Topology is null
                        ? CreateDeltaRequest(update)
                        : CreateJoinRequest(shutdownApplication: update.ShutdownApplication);
                    recoveryJoinEpoch = update.Topology is not null ? 0 : null;
                    break;
                case MemberCommandKind.ReportOffsets:
                    _taskOffsets = MapTaskOffsets(command.OffsetReport!.TaskOffsets);
                    _taskEndOffsets = MapTaskOffsets(command.OffsetReport.TaskEndOffsets);
                    request = CreateOffsetRequest();
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported command {command.Kind}.");
            }

            response = await SendWithRecoveryAsync(
                    request,
                    command.CancellationToken,
                    recoveryJoinEpoch: recoveryJoinEpoch,
                    trackAmbiguousRequestFailure: true)
                .ConfigureAwait(false);
        }
        catch (AmbiguousHeartbeatException exception)
        {
            MarkUnjoined();
            _ambiguousMembership = true;
            ExceptionDispatchInfo.Capture(exception.InnerException!).Throw();
            throw;
        }
        catch
        {
            RestoreOperationState(previousState);
            throw;
        }

        var clearOmittedAssignment = command is
        {
            Kind: MemberCommandKind.Update,
            Update.Topology: not null
        };
        var result = ApplyResponse(response, createResult: true, clearOmittedAssignment)!;
        if (command.Kind == MemberCommandKind.Join)
            Volatile.Write(ref _backgroundFailure, null);
        ScheduleHeartbeat();
        return result;
    }

    private async ValueTask ProcessBackgroundHeartbeatAsync()
    {
        if (_memberEpoch <= 0 || Volatile.Read(ref _closeRequested) != 0)
            return;

        try
        {
            var request = GetOrCreateSteadyRequest();
            var response = await SendWithRecoveryAsync(request, CancellationToken.None)
                .ConfigureAwait(false);
            ApplyResponse(response, createResult: false, clearOmittedAssignment: false);
            Volatile.Write(ref _backgroundFailure, null);
        }
        catch (KafkaException exception)
        {
            HandleBackgroundHeartbeatFailure(exception);
        }
        catch (IOException exception)
        {
            HandleBackgroundHeartbeatFailure(exception);
        }
        catch (System.Net.Sockets.SocketException exception)
        {
            HandleBackgroundHeartbeatFailure(exception);
        }
        catch (TimeoutException exception)
        {
            HandleBackgroundHeartbeatFailure(exception);
        }
        catch (DnsResolutionException exception)
        {
            HandleBackgroundHeartbeatFailure(exception);
        }

        if (Volatile.Read(ref _closeRequested) == 0 && Volatile.Read(ref _backgroundFailure) is null)
            ScheduleHeartbeat();
    }

    private void HandleBackgroundHeartbeatFailure(Exception exception)
    {
        var fatal = IsFatalHeartbeatFailure(exception);
        if (fatal)
            MarkUnjoined();
        Volatile.Write(
            ref _backgroundFailure,
            fatal ? exception : null);
    }

    private async ValueTask ProcessCloseAsync(StreamsGroupCloseOptions options)
    {
        _heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
        int? terminalEpoch = options.GroupMembershipOperation switch
        {
            StreamsGroupMembershipOperation.RemainInGroup => null,
            StreamsGroupMembershipOperation.LeaveGroup => -1,
            StreamsGroupMembershipOperation.Default => InstanceId is null ? -1 : -2,
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
        var retainMembership = terminalEpoch is null or -2;
        var heartbeatEpoch = terminalEpoch ?? (options.ShutdownApplication ? _memberEpoch : null);
        try
        {
            if (heartbeatEpoch is not null
                && (_memberEpoch > 0 || (_ambiguousMembership && terminalEpoch is not null)))
            {
                var request = new StreamsGroupHeartbeatRequest
                {
                    GroupId = GroupId,
                    MemberId = _memberId,
                    MemberEpoch = heartbeatEpoch.Value,
                    EndpointInformationEpoch = _endpointInformationEpoch,
                    InstanceId = InstanceId,
                    ShutdownApplication = options.ShutdownApplication
                };
                await SendWithRecoveryAsync(
                        request,
                        CancellationToken.None,
                        recoverFencing: false,
                        trackAmbiguousRequestFailure: !retainMembership)
                    .ConfigureAwait(false);
            }
        }
        catch (AmbiguousHeartbeatException exception)
            when (!retainMembership
                && exception.InnerException is GroupException
                {
                    ErrorCode: ErrorCode.FencedMemberEpoch or ErrorCode.UnknownMemberId
                })
        {
            // A prior leave attempt may have succeeded before its response was lost.
        }
        catch (AmbiguousHeartbeatException exception)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException!).Throw();
            throw;
        }
        catch (GroupException exception)
            when (exception.ErrorCode is ErrorCode.FencedMemberEpoch or ErrorCode.UnknownMemberId)
        {
            retainMembership = false;
            throw;
        }
        finally
        {
            _memberEpoch = 0;
            _ambiguousMembership = false;
            var current = _snapshot;
            _snapshot = new StreamsGroupMemberSnapshot
            {
                IsJoined = retainMembership && current.IsJoined,
                IsClosed = true,
                MemberId = retainMembership ? current.MemberId : null,
                MemberEpoch = retainMembership ? current.MemberEpoch : 0,
                Assignment = current.Assignment,
                EndpointInformationEpoch = current.EndpointInformationEpoch,
                PartitionsByUserEndpoint = current.PartitionsByUserEndpoint,
                Status = current.Status,
                HeartbeatInterval = current.HeartbeatInterval,
                AcceptableRecoveryLag = current.AcceptableRecoveryLag,
                TaskOffsetInterval = current.TaskOffsetInterval
            };
        }
    }

    private async ValueTask<StreamsGroupHeartbeatResponse> SendWithRecoveryAsync(
        StreamsGroupHeartbeatRequest request,
        CancellationToken cancellationToken,
        int? recoveryJoinEpoch = null,
        bool recoverFencing = true,
        bool trackAmbiguousRequestFailure = false)
    {
        var fencedRecoveryAttempted = false;
        var shutdownApplication = request.ShutdownApplication;
        var unreleasedFailureCount = 0;
        long unreleasedStartedAt = -1;
        var attemptLimit = 5;
        var fencingObserved = false;
        var ambiguousCompletionObserved = false;
        try
        {
            for (var attempt = 0; attempt < attemptLimit; attempt++)
            {
                if (_coordinatorId < 0)
                    await FindCoordinatorAsync(cancellationToken).ConfigureAwait(false);

                StreamsGroupHeartbeatResponse response;
                var requestAttempted = false;
                try
                {
                    using var lease = await _connectionPool.LeaseConnectionAsync(_coordinatorId, cancellationToken)
                        .ConfigureAwait(false);
                    var connection = lease.Connection;
                    if (!_metadataManager.HasApiKey(connection, ApiKey.StreamsGroupHeartbeat))
                    {
                        throw new BrokerVersionException(
                            "The target Kafka broker does not support StreamsGroupHeartbeat " +
                            "(KIP-1071). Streams group membership requires Kafka 4.2 or later.");
                    }

                    var version = _metadataManager.GetNegotiatedApiVersion(
                        connection,
                        ApiKey.StreamsGroupHeartbeat,
                        StreamsGroupHeartbeatRequest.LowestSupportedVersion,
                        StreamsGroupHeartbeatRequest.HighestSupportedVersion);
                    requestAttempted = true;
                    response = await connection.SendAsync<StreamsGroupHeartbeatRequest, StreamsGroupHeartbeatResponse>(
                        request,
                        version,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (
                    trackAmbiguousRequestFailure
                    && requestAttempted
                    && exception is OperationCanceledException
                    && cancellationToken.IsCancellationRequested)
                {
                    ambiguousCompletionObserved = true;
                    throw;
                }
                catch (Exception exception) when (
                    IsRetriableConnectionFailure(exception)
                    && (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested))
                {
                    _coordinatorId = -1;
                    if (trackAmbiguousRequestFailure && requestAttempted)
                    {
                        // Later retry responses cannot prove whether an earlier request
                        // reached the broker when its completion was lost.
                        ambiguousCompletionObserved = true;
                    }
                    if (attempt == attemptLimit - 1)
                        throw;
                    if (recoverFencing)
                        request = CreateRecoveryRequest(recoveryJoinEpoch, shutdownApplication);
                    await DelayForRetryAsync(attempt + 1, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                if (response.ErrorCode == ErrorCode.None)
                    return response;

                if (response.ErrorCode == ErrorCode.UnsupportedVersion)
                {
                    throw new BrokerVersionException(
                        "The broker rejected StreamsGroupHeartbeat although it advertised API 88. " +
                        "Enable the Streams rebalance protocol (streams.version >= 1 and " +
                        "group.coordinator.rebalance.protocols containing 'streams').");
                }

                if (response.ErrorCode == ErrorCode.UnreleasedInstanceId
                    && InstanceId is not null
                    && request.MemberEpoch == 0)
                {
                    if (unreleasedStartedAt < 0)
                        unreleasedStartedAt = Stopwatch.GetTimestamp();
                    var elapsed = Stopwatch.GetElapsedTime(unreleasedStartedAt);
                    if (elapsed >= _options.RebalanceTimeout)
                        throw CreateGroupException(response);

                    var remainingMs = Math.Max(
                        1,
                        (int)Math.Ceiling((_options.RebalanceTimeout - elapsed).TotalMilliseconds));
                    var delayMs = Math.Min(
                        remainingMs,
                        Math.Max(1, ExponentialRetryBackoff.CalculateDelayMilliseconds(
                            _retryBackoffMs,
                            _retryBackoffMaxMs,
                            ++unreleasedFailureCount)));
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                    attempt--;
                    continue;
                }

                if (response.ErrorCode is ErrorCode.NotCoordinator
                    or ErrorCode.CoordinatorNotAvailable
                    or ErrorCode.CoordinatorLoadInProgress)
                {
                    if (attempt == attemptLimit - 1)
                        throw CreateGroupException(response);

                    if (response.ErrorCode != ErrorCode.CoordinatorLoadInProgress)
                        _coordinatorId = -1;
                    if (recoverFencing)
                        request = CreateRecoveryRequest(recoveryJoinEpoch, shutdownApplication);
                    await DelayForRetryAsync(attempt + 1, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (recoverFencing
                    && !fencedRecoveryAttempted
                    && response.ErrorCode is ErrorCode.FencedMemberEpoch or ErrorCode.UnknownMemberId)
                {
                    fencingObserved = true;
                    fencedRecoveryAttempted = true;
                    attemptLimit++;
                    recoveryJoinEpoch = InstanceId is null ? 0 : -2;
                    ResetTaskAssignment(markUnjoined: false);
                    request = CreateJoinRequest(recoveryJoinEpoch.Value, shutdownApplication);
                    continue;
                }

                throw CreateGroupException(response);
            }

            throw new GroupException(
                ErrorCode.CoordinatorNotAvailable,
                $"StreamsGroupHeartbeat failed after {attemptLimit} coordinator attempts.")
            { GroupId = GroupId };
        }
        catch (Exception exception) when (trackAmbiguousRequestFailure && ambiguousCompletionObserved)
        {
            throw new AmbiguousHeartbeatException(exception);
        }
        catch
        {
            if (fencingObserved)
                MarkUnjoined();
            throw;
        }
    }

    private void MarkUnjoined()
    {
        _memberEpoch = 0;
        _ambiguousMembership = false;
        ResetTaskAssignment(markUnjoined: true);
    }

    private void ResetTaskAssignment(bool markUnjoined)
    {
        _activeTasks = [];
        _standbyTasks = [];
        _warmupTasks = [];
        _steadyRequestCache.Invalidate();
        var current = _snapshot;
        _snapshot = new StreamsGroupMemberSnapshot
        {
            IsJoined = !markUnjoined && current.IsJoined,
            MemberId = current.MemberId,
            MemberEpoch = markUnjoined ? 0 : current.MemberEpoch,
            Assignment = EmptyAssignment,
            EndpointInformationEpoch = current.EndpointInformationEpoch,
            PartitionsByUserEndpoint = markUnjoined ? [] : current.PartitionsByUserEndpoint,
            Status = current.Status,
            HeartbeatInterval = current.HeartbeatInterval,
            AcceptableRecoveryLag = current.AcceptableRecoveryLag,
            TaskOffsetInterval = current.TaskOffsetInterval
        };
    }

    private async ValueTask FindCoordinatorAsync(CancellationToken cancellationToken)
    {
        var brokers = _metadataManager.Metadata.GetBrokers();
        if (brokers.Count == 0)
        {
            throw new GroupException(
                ErrorCode.CoordinatorNotAvailable,
                "No brokers available for Streams group coordinator discovery.")
            { GroupId = GroupId };
        }

        var request = new FindCoordinatorRequest { Key = GroupId, KeyType = CoordinatorType.Group };
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var broker = brokers[attempt % brokers.Count];
                using var lease = await _connectionPool.LeaseConnectionAsync(broker.NodeId, cancellationToken)
                    .ConfigureAwait(false);
                var connection = lease.Connection;
                var version = _metadataManager.GetNegotiatedApiVersion(
                    connection,
                    ApiKey.FindCoordinator,
                    FindCoordinatorRequest.LowestSupportedVersion,
                    FindCoordinatorRequest.HighestSupportedVersion);
                var response = await connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                    request,
                    version,
                    cancellationToken).ConfigureAwait(false);
                if (response.Coordinators.Count == 0)
                {
                    throw new GroupException(
                        ErrorCode.CoordinatorNotAvailable,
                        "FindCoordinator returned no Streams group coordinator.")
                    { GroupId = GroupId };
                }

                var coordinator = response.Coordinators[0];
                if (coordinator.ErrorCode is ErrorCode.CoordinatorNotAvailable
                    or ErrorCode.CoordinatorLoadInProgress)
                {
                    if (attempt == 4)
                        break;
                    await DelayForRetryAsync(attempt + 1, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                if (coordinator.ErrorCode != ErrorCode.None)
                {
                    throw new GroupException(
                        coordinator.ErrorCode,
                        $"FindCoordinator failed for Streams group '{GroupId}': {coordinator.ErrorCode}.")
                    { GroupId = GroupId };
                }

                _connectionPool.RegisterBroker(coordinator.NodeId, coordinator.Host, coordinator.Port);
                _coordinatorId = coordinator.NodeId;
                return;
            }
            catch (Exception exception) when (
                IsRetriableConnectionFailure(exception)
                && !cancellationToken.IsCancellationRequested)
            {
                if (attempt == 4)
                {
                    throw new GroupException(
                        ErrorCode.CoordinatorNotAvailable,
                        $"FindCoordinator failed after 5 attempts: {exception.Message}",
                        exception)
                    { GroupId = GroupId };
                }
                await DelayForRetryAsync(attempt + 1, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new GroupException(
            ErrorCode.CoordinatorNotAvailable,
            "FindCoordinator failed after 5 attempts.")
        { GroupId = GroupId };
    }

    private static bool IsRetriableConnectionFailure(Exception exception) =>
        RetryHelper.IsRetriableRequestFailure(exception)
        || exception is ObjectDisposedException;

    private static void ObserveFault(Task task)
    {
        if (task.IsCompleted)
        {
            _ = task.Exception;
            return;
        }

        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private Task DelayForRetryAsync(int failureCount, CancellationToken cancellationToken) =>
        Task.Delay(
            ExponentialRetryBackoff.CalculateDelayMilliseconds(
                _retryBackoffMs,
                _retryBackoffMaxMs,
                failureCount),
            cancellationToken);

    private static bool IsFatalHeartbeatFailure(Exception? exception) => exception switch
    {
        BrokerVersionException => true,
        AuthenticationException or AuthorizationException => true,
        GroupException { ErrorCode: ErrorCode.UnknownServerError } => false,
        GroupException { ErrorCode: { } errorCode } => !errorCode.IsRetriable(),
        _ => false
    };

    private Exception CreateBackgroundFailureException(Exception exception) => exception switch
    {
        BrokerVersionException => new BrokerVersionException(
            "The Streams group heartbeat loop stopped because the broker does not support the protocol.",
            exception),
        GroupException { ErrorCode: { } errorCode } => new GroupException(
            errorCode,
            "The Streams group heartbeat loop failed.",
            exception)
            { GroupId = GroupId },
        _ => new GroupException("The Streams group heartbeat loop failed.", exception)
            { GroupId = GroupId }
    };

    private GroupException CreateGroupException(StreamsGroupHeartbeatResponse response) => new(
        response.ErrorCode,
        $"StreamsGroupHeartbeat failed: {response.ErrorCode} - {response.ErrorMessage}")
        { GroupId = GroupId };

    private StreamsGroupHeartbeatResult? ApplyResponse(
        StreamsGroupHeartbeatResponse response,
        bool createResult,
        bool clearOmittedAssignment)
    {
        _ambiguousMembership = false;
        var heartbeatRequestChanged = response.MemberEpoch != _memberEpoch
            || response.EndpointInformationEpoch != _endpointInformationEpoch
            || (!string.IsNullOrEmpty(response.MemberId) && response.MemberId != _memberId);
        if (!string.IsNullOrEmpty(response.MemberId))
            _memberId = response.MemberId;

        _memberEpoch = response.MemberEpoch;
        if (response.HeartbeatIntervalMs > 0)
            _heartbeatIntervalMs = response.HeartbeatIntervalMs;

        var current = _snapshot;
        var fallbackAssignment = clearOmittedAssignment ? EmptyAssignment : current.Assignment;
        var active = response.ActiveTasks is null
            ? fallbackAssignment.ActiveTasks
            : MapTaskSets(response.ActiveTasks);
        var standby = response.StandbyTasks is null
            ? fallbackAssignment.StandbyTasks
            : MapTaskSets(response.StandbyTasks);
        var warmup = response.WarmupTasks is null
            ? fallbackAssignment.WarmupTasks
            : MapTaskSets(response.WarmupTasks);
        var status = response.Status is null ? current.Status : MapStatus(response.Status);
        var endpoints = response.PartitionsByUserEndpoint is null
            ? current.PartitionsByUserEndpoint
            : MapEndpointAssignments(response.PartitionsByUserEndpoint);
        var heartbeatInterval = response.HeartbeatIntervalMs > 0
            ? TimeSpan.FromMilliseconds(response.HeartbeatIntervalMs)
            : current.HeartbeatInterval;
        var taskOffsetInterval = response.TaskOffsetIntervalMs > 0
            ? TimeSpan.FromMilliseconds(response.TaskOffsetIntervalMs)
            : current.TaskOffsetInterval;

        _endpointInformationEpoch = response.EndpointInformationEpoch;
        if (heartbeatRequestChanged)
            _steadyRequestCache.Invalidate();
        var assignmentReturned = response.ActiveTasks is not null
            || response.StandbyTasks is not null
            || response.WarmupTasks is not null;
        var assignmentChanged = clearOmittedAssignment || assignmentReturned;
        var assignment = current.Assignment;
        if (clearOmittedAssignment && !assignmentReturned)
        {
            assignment = EmptyAssignment;
        }
        else if (assignmentReturned)
        {
            assignment = new StreamsGroupAssignment
            {
                ActiveTasks = active,
                StandbyTasks = standby,
                WarmupTasks = warmup
            };
        }
        var snapshotChanged = current.IsJoined != (response.MemberEpoch > 0)
            || current.MemberId != _memberId
            || current.MemberEpoch != response.MemberEpoch
            || assignmentChanged
            || response.Status is not null
            || response.PartitionsByUserEndpoint is not null
            || current.EndpointInformationEpoch != response.EndpointInformationEpoch
            || current.HeartbeatInterval != heartbeatInterval
            || current.AcceptableRecoveryLag != response.AcceptableRecoveryLag
            || current.TaskOffsetInterval != taskOffsetInterval;
        if (snapshotChanged)
        {
            _snapshot = new StreamsGroupMemberSnapshot
            {
                IsJoined = response.MemberEpoch > 0,
                MemberId = _memberId,
                MemberEpoch = response.MemberEpoch,
                Assignment = assignment,
                EndpointInformationEpoch = response.EndpointInformationEpoch,
                PartitionsByUserEndpoint = endpoints,
                Status = status,
                HeartbeatInterval = heartbeatInterval,
                AcceptableRecoveryLag = response.AcceptableRecoveryLag,
                TaskOffsetInterval = taskOffsetInterval
            };
        }

        if (!createResult)
            return null;

        return new StreamsGroupHeartbeatResult
        {
            ThrottleTime = TimeSpan.FromMilliseconds(response.ThrottleTimeMs),
            MemberId = _memberId,
            MemberEpoch = response.MemberEpoch,
            HeartbeatInterval = heartbeatInterval,
            AcceptableRecoveryLag = response.AcceptableRecoveryLag,
            TaskOffsetInterval = taskOffsetInterval,
            Status = response.Status is null ? null : status,
            ActiveTasks = response.ActiveTasks is null ? null : active,
            StandbyTasks = response.StandbyTasks is null ? null : standby,
            WarmupTasks = response.WarmupTasks is null ? null : warmup,
            EndpointInformationEpoch = response.EndpointInformationEpoch,
            PartitionsByUserEndpoint = response.PartitionsByUserEndpoint is null ? null : endpoints
        };
    }

    private void ApplyUpdate(StreamsGroupMemberUpdate update)
    {
        _endpointInformationEpoch = update.EndpointInformationEpoch;
        if (update.Topology is not null)
        {
            _topology = MapTopology(update.Topology);
            _activeTasks = [];
            _standbyTasks = [];
            _warmupTasks = [];
        }
        else
        {
            if (update.ActiveTasks is not null)
                _activeTasks = MapTaskSets(update.ActiveTasks);
            if (update.StandbyTasks is not null)
                _standbyTasks = MapTaskSets(update.StandbyTasks);
            if (update.WarmupTasks is not null)
                _warmupTasks = MapTaskSets(update.WarmupTasks);
        }
        if (update.ProcessId is not null)
            _processId = update.ProcessId;
        if (update.UserEndpoint is not null)
            _userEndpoint = MapEndpoint(update.UserEndpoint);
        if (update.ClientTags is not null)
            _clientTags = MapKeyValues(update.ClientTags);
        if (update.TaskOffsets is not null)
            _taskOffsets = MapTaskOffsets(update.TaskOffsets);
        if (update.TaskEndOffsets is not null)
            _taskEndOffsets = MapTaskOffsets(update.TaskEndOffsets);
        _steadyRequestCache.Invalidate();
    }

    private OperationState CaptureOperationState() => new(
        _topology,
        _activeTasks,
        _standbyTasks,
        _warmupTasks,
        _processId,
        _userEndpoint,
        _clientTags,
        _taskOffsets,
        _taskEndOffsets,
        _endpointInformationEpoch);

    private void RestoreOperationState(in OperationState state)
    {
        _topology = state.Topology;
        _activeTasks = state.ActiveTasks;
        _standbyTasks = state.StandbyTasks;
        _warmupTasks = state.WarmupTasks;
        _processId = state.ProcessId;
        _userEndpoint = state.UserEndpoint;
        _clientTags = state.ClientTags;
        _taskOffsets = state.TaskOffsets;
        _taskEndOffsets = state.TaskEndOffsets;
        _endpointInformationEpoch = state.EndpointInformationEpoch;
        _steadyRequestCache.Invalidate();
    }

    private StreamsGroupHeartbeatRequest CreateJoinRequest(
        int memberEpoch = 0,
        bool shutdownApplication = false) => new()
    {
        GroupId = GroupId,
        MemberId = _memberId,
        MemberEpoch = memberEpoch,
        EndpointInformationEpoch = _endpointInformationEpoch,
        InstanceId = InstanceId,
        RackId = _options.RackId,
        RebalanceTimeoutMs = checked((int)_options.RebalanceTimeout.TotalMilliseconds),
        Topology = _topology,
        ActiveTasks = [],
        StandbyTasks = [],
        WarmupTasks = [],
        ProcessId = _processId,
        UserEndpoint = _userEndpoint,
        ClientTags = _clientTags,
        TaskOffsets = _taskOffsets,
        TaskEndOffsets = _taskEndOffsets,
        ShutdownApplication = shutdownApplication
    };

    private StreamsGroupHeartbeatRequest CreateRecoveryRequest(
        int? recoveryJoinEpoch,
        bool shutdownApplication) =>
        recoveryJoinEpoch is not null || _memberEpoch <= 0
            ? CreateJoinRequest(recoveryJoinEpoch ?? _memberEpoch, shutdownApplication)
            : CreateFullRequest(shutdownApplication);

    private StreamsGroupHeartbeatRequest CreateDeltaRequest(StreamsGroupMemberUpdate update) => new()
    {
        GroupId = GroupId,
        MemberId = _memberId,
        MemberEpoch = _memberEpoch,
        EndpointInformationEpoch = update.EndpointInformationEpoch,
        InstanceId = InstanceId,
        ActiveTasks = update.ActiveTasks is null ? null : _activeTasks,
        StandbyTasks = update.StandbyTasks is null ? null : _standbyTasks,
        WarmupTasks = update.WarmupTasks is null ? null : _warmupTasks,
        ProcessId = update.ProcessId,
        UserEndpoint = update.UserEndpoint is null ? null : _userEndpoint,
        ClientTags = update.ClientTags is null ? null : _clientTags,
        TaskOffsets = update.TaskOffsets is null ? null : _taskOffsets,
        TaskEndOffsets = update.TaskEndOffsets is null ? null : _taskEndOffsets,
        ShutdownApplication = update.ShutdownApplication
    };

    private StreamsGroupHeartbeatRequest CreateOffsetRequest() => new()
    {
        GroupId = GroupId,
        MemberId = _memberId,
        MemberEpoch = _memberEpoch,
        EndpointInformationEpoch = _endpointInformationEpoch,
        InstanceId = InstanceId,
        TaskOffsets = _taskOffsets,
        TaskEndOffsets = _taskEndOffsets
    };

    private StreamsGroupHeartbeatRequest CreateFullRequest(bool shutdownApplication) => new()
    {
        GroupId = GroupId,
        MemberId = _memberId,
        MemberEpoch = _memberEpoch,
        EndpointInformationEpoch = _endpointInformationEpoch,
        InstanceId = InstanceId,
        RackId = _options.RackId,
        RebalanceTimeoutMs = checked((int)_options.RebalanceTimeout.TotalMilliseconds),
        ActiveTasks = _activeTasks,
        StandbyTasks = _standbyTasks,
        WarmupTasks = _warmupTasks,
        ProcessId = _processId,
        UserEndpoint = _userEndpoint,
        ClientTags = _clientTags,
        TaskOffsets = _taskOffsets,
        TaskEndOffsets = _taskEndOffsets,
        ShutdownApplication = shutdownApplication
    };

    internal StreamsGroupHeartbeatRequest GetOrCreateSteadyRequest() =>
        _steadyRequestCache.Get(
            GroupId,
            _memberId,
            _memberEpoch,
            _endpointInformationEpoch,
            InstanceId);

    private bool QueueHeartbeat()
    {
        if (Volatile.Read(ref _closeRequested) != 0)
            return false;
        if (_commands.Writer.TryWrite(MemberCommand.Heartbeat))
            return true;

        try
        {
            if (Volatile.Read(ref _closeRequested) == 0)
                ScheduleHeartbeat();
        }
        catch (ObjectDisposedException) when (Volatile.Read(ref _disposed) != 0)
        {
            // DisposeAsync can release the timer after the callback's close check.
        }

        return false;
    }

    private void ScheduleHeartbeat() =>
        _heartbeatTimer.Change(Math.Max(_heartbeatIntervalMs, 1), Timeout.Infinite);

    private void ThrowIfClosed()
    {
        if (Volatile.Read(ref _closeRequested) != 0)
            throw new ObjectDisposedException(nameof(StreamsGroupMember));
    }

    private void ThrowIfNotInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException(
                "The Streams group member is not initialized. Call InitializeAsync() before joining.");
        }
    }

    private static StreamsGroupHeartbeatTopology MapTopology(StreamsGroupTopology source)
    {
        var subtopologies = new StreamsGroupHeartbeatSubtopology[source.Subtopologies.Count];
        for (var i = 0; i < subtopologies.Length; i++)
        {
            var item = source.Subtopologies[i];
            subtopologies[i] = new StreamsGroupHeartbeatSubtopology
            {
                SubtopologyId = item.SubtopologyId,
                SourceTopics = Copy(item.SourceTopics),
                SourceTopicRegex = Copy(item.SourceTopicRegex),
                StateChangelogTopics = MapTopicInfo(item.StateChangelogTopics),
                RepartitionSinkTopics = Copy(item.RepartitionSinkTopics),
                RepartitionSourceTopics = MapTopicInfo(item.RepartitionSourceTopics),
                CopartitionGroups = MapCopartitionGroups(item.CopartitionGroups)
            };
        }

        return new StreamsGroupHeartbeatTopology { Epoch = source.Epoch, Subtopologies = subtopologies };
    }

    private static StreamsGroupHeartbeatTopicInfo[] MapTopicInfo(IReadOnlyList<StreamsGroupTopicInfo> source)
    {
        var result = new StreamsGroupHeartbeatTopicInfo[source.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var item = source[i];
            result[i] = new StreamsGroupHeartbeatTopicInfo
            {
                Name = item.Name,
                Partitions = item.Partitions,
                ReplicationFactor = item.ReplicationFactor,
                TopicConfigs = MapKeyValues(item.TopicConfigs)
            };
        }
        return result;
    }

    private static StreamsGroupHeartbeatCopartitionGroup[] MapCopartitionGroups(
        IReadOnlyList<StreamsGroupCopartitionGroup> source)
    {
        var result = new StreamsGroupHeartbeatCopartitionGroup[source.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var item = source[i];
            result[i] = new StreamsGroupHeartbeatCopartitionGroup
            {
                SourceTopics = Copy(item.SourceTopics),
                SourceTopicRegex = Copy(item.SourceTopicRegex),
                RepartitionSourceTopics = Copy(item.RepartitionSourceTopics)
            };
        }
        return result;
    }

    private static StreamsGroupHeartbeatTaskIds[] MapTaskSets(IReadOnlyList<StreamsGroupTaskSet> source)
    {
        var result = new StreamsGroupHeartbeatTaskIds[source.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var item = source[i];
            result[i] = new StreamsGroupHeartbeatTaskIds
            {
                SubtopologyId = item.SubtopologyId,
                Partitions = Copy(item.Partitions)
            };
        }
        return result;
    }

    private static StreamsGroupTaskSet[] MapTaskSets(IReadOnlyList<StreamsGroupHeartbeatTaskIds> source)
    {
        var result = new StreamsGroupTaskSet[source.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var item = source[i];
            result[i] = new StreamsGroupTaskSet
            {
                SubtopologyId = item.SubtopologyId,
                Partitions = Copy(item.Partitions)
            };
        }
        return result;
    }

    private static StreamsGroupHeartbeatTaskOffset[] MapTaskOffsets(
        IReadOnlyList<StreamsGroupTaskOffset> source)
    {
        var result = new StreamsGroupHeartbeatTaskOffset[source.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var item = source[i];
            result[i] = new StreamsGroupHeartbeatTaskOffset
            {
                SubtopologyId = item.SubtopologyId,
                Partition = item.Partition,
                Offset = item.Offset
            };
        }
        return result;
    }

    private static StreamsGroupHeartbeatKeyValue[] MapKeyValues(IReadOnlyList<StreamsGroupKeyValue> source)
    {
        var result = new StreamsGroupHeartbeatKeyValue[source.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var item = source[i];
            result[i] = new StreamsGroupHeartbeatKeyValue { Key = item.Key, Value = item.Value };
        }
        return result;
    }

    private static StreamsGroupHeartbeatEndpoint MapEndpoint(StreamsGroupEndpoint source) => new()
    {
        Host = source.Host,
        Port = source.Port
    };

    private static StreamsGroupStatus[] MapStatus(IReadOnlyList<StreamsGroupHeartbeatStatus> source)
    {
        var result = new StreamsGroupStatus[source.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var item = source[i];
            result[i] = new StreamsGroupStatus
            {
                StatusCode = item.StatusCode,
                StatusDetail = item.StatusDetail
            };
        }
        return result;
    }

    private static StreamsGroupEndpointAssignment[] MapEndpointAssignments(
        IReadOnlyList<StreamsGroupHeartbeatEndpointPartitions> source)
    {
        var result = new StreamsGroupEndpointAssignment[source.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var item = source[i];
            result[i] = new StreamsGroupEndpointAssignment
            {
                UserEndpoint = new StreamsGroupEndpoint
                {
                    Host = item.UserEndpoint.Host,
                    Port = item.UserEndpoint.Port
                },
                ActivePartitions = MapTopicPartitions(item.ActivePartitions),
                StandbyPartitions = MapTopicPartitions(item.StandbyPartitions)
            };
        }
        return result;
    }

    private static StreamsGroupTopicPartitions[] MapTopicPartitions(
        IReadOnlyList<StreamsGroupHeartbeatTopicPartitions> source)
    {
        var result = new StreamsGroupTopicPartitions[source.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var item = source[i];
            result[i] = new StreamsGroupTopicPartitions
            {
                Topic = item.Topic,
                Partitions = Copy(item.Partitions)
            };
        }
        return result;
    }

    private static T[] Copy<T>(IReadOnlyList<T> source)
    {
        if (source.Count == 0)
            return [];
        var result = new T[source.Count];
        for (var i = 0; i < result.Length; i++)
            result[i] = source[i];
        return result;
    }

    private readonly record struct OperationState(
        StreamsGroupHeartbeatTopology? Topology,
        IReadOnlyList<StreamsGroupHeartbeatTaskIds>? ActiveTasks,
        IReadOnlyList<StreamsGroupHeartbeatTaskIds>? StandbyTasks,
        IReadOnlyList<StreamsGroupHeartbeatTaskIds>? WarmupTasks,
        string? ProcessId,
        StreamsGroupHeartbeatEndpoint? UserEndpoint,
        IReadOnlyList<StreamsGroupHeartbeatKeyValue>? ClientTags,
        IReadOnlyList<StreamsGroupHeartbeatTaskOffset>? TaskOffsets,
        IReadOnlyList<StreamsGroupHeartbeatTaskOffset>? TaskEndOffsets,
        int EndpointInformationEpoch);

    private sealed class AmbiguousHeartbeatException(Exception innerException) : Exception(
        "StreamsGroupHeartbeat completion is ambiguous.",
        innerException);

    private enum MemberCommandKind : byte
    {
        Heartbeat,
        Join,
        Update,
        ReportOffsets,
        Close
    }

    private sealed class MemberCommand
    {
        internal static readonly MemberCommand Heartbeat = new(MemberCommandKind.Heartbeat);

        private MemberCommand(MemberCommandKind kind)
        {
            Kind = kind;
        }

        internal MemberCommandKind Kind { get; }
        internal StreamsGroupMemberUpdate? Update { get; init; }
        internal StreamsGroupTaskOffsetReport? OffsetReport { get; init; }
        internal StreamsGroupCloseOptions? CloseOptions { get; init; }
        internal CancellationToken CancellationToken { get; set; }
        internal TaskCompletionSource<StreamsGroupHeartbeatResult?> Completion { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal static MemberCommand Join(StreamsGroupMemberUpdate update) => new(MemberCommandKind.Join)
        {
            Update = update
        };

        internal static MemberCommand ForUpdate(StreamsGroupMemberUpdate update) => new(MemberCommandKind.Update)
        {
            Update = update
        };

        internal static MemberCommand ReportOffsets(StreamsGroupTaskOffsetReport report) =>
            new(MemberCommandKind.ReportOffsets) { OffsetReport = report };

        internal static MemberCommand Close(StreamsGroupCloseOptions options) => new(MemberCommandKind.Close)
        {
            CloseOptions = options
        };
    }
}
