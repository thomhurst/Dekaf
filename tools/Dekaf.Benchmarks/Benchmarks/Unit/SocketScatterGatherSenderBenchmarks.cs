using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks.Sources;
using BenchmarkDotNet.Attributes;
using Dekaf.Networking;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures one asynchronous scatter/gather produce write through
/// <see cref="KafkaConnection.SocketScatterGatherSender"/>: a 64 KB frame in 16 segments,
/// the shape <c>KafkaConnection.WriteSegmentedFrameHoldingLockAsync</c> sends per zero-copy
/// produce request. The loopback pair is configured so the send cannot complete synchronously
/// (zero send buffer: the kernel only completes once the peer has acknowledged the data, on
/// Windows as well as Linux; an 8 KB receive buffer keeps the peer's window small) while a
/// dedicated thread drains the peer, so every operation exercises the
/// <see cref="SocketAsyncEventArgs.Completed"/> path whose continuation policy is under test.
/// <c>RunContinuationsAsynchronously</c> is flipped on the sender's
/// <see cref="ManualResetValueTaskSourceCore{TResult}"/> so both policies run in the same
/// process; the production sender constructs with <c>false</c>. The <c>Completed Work Items</c>
/// column (ThreadingDiagnoser) counts ThreadPool dispatches per send: the redundant hop shows up
/// as one extra work item per operation.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class SocketScatterGatherSenderBenchmarks
{
    private const int SegmentCount = KafkaConnection.SocketScatterGatherSender.MaximumSegmentsPerSend;
    private const int SegmentBytes = 4 * 1024;
    private const int FrameBytes = SegmentCount * SegmentBytes;
    private const int ReceiveBufferSize = 8 * 1024;

    private TcpListener _listener = null!;
    private Socket _client = null!;
    private Socket _server = null!;
    private Thread _drainThread = null!;
    private KafkaConnection.SocketScatterGatherSender _sender = null!;
    private long _sends;
    private long _synchronousCompletions;

    [Params(true, false)]
    public bool RunContinuationsAsynchronously { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
            SendBufferSize = 0,
        };
        _client.Connect((IPEndPoint)_listener.LocalEndpoint);
        _server = _listener.AcceptSocket();
        _server.ReceiveBufferSize = ReceiveBufferSize;

        _drainThread = new Thread(Drain) { IsBackground = true, Name = "scatter-gather-drain" };
        _drainThread.Start(_server);

        _sender = new KafkaConnection.SocketScatterGatherSender(SegmentCount);
        SetRunContinuationsAsynchronously(_sender, RunContinuationsAsynchronously);
        for (var i = 0; i < SegmentCount; i++)
        {
            var segment = new byte[SegmentBytes];
            segment.AsSpan().Fill((byte)i);
            _sender.PendingSegments.Add(new ArraySegment<byte>(segment));
        }
    }

    /// <summary>
    /// One 16-segment window, i.e. one <see cref="Socket.SendAsync(SocketAsyncEventArgs)"/>: the
    /// loop <c>KafkaConnection.WriteSegmentedFrameHoldingLockAsync</c> runs per zero-copy produce
    /// request has exactly one iteration for frames of up to 16 segments. The sender's
    /// <see cref="ValueTask{TResult}"/> is returned directly so the only continuation is
    /// BenchmarkDotNet's allocation-free awaiter and the Allocated column reflects the sender alone.
    /// </summary>
    [Benchmark]
    public ValueTask<int> SendFrame()
    {
        var sender = _sender;
        sender.BeginPendingSend();
        sender.LoadSendWindow();
        var send = sender.SendAsync(_client);
        _sends++;
        if (send.IsCompleted)
            _synchronousCompletions++;

        return send;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // Validity check for the run log: the hop under test only exists on asynchronous
        // completions, so nearly every send must have gone asynchronous.
        Console.WriteLine(
            $"// SocketScatterGatherSenderBenchmarks RunContinuationsAsynchronously={RunContinuationsAsynchronously}: "
            + $"{_synchronousCompletions}/{_sends} sends completed synchronously");

        _client.Shutdown(SocketShutdown.Both);
        _client.Dispose();
        _drainThread.Join();
        _server.Dispose();
        _listener.Stop();
        _sender.Dispose();
    }

    private static void Drain(object? state)
    {
        var socket = (Socket)state!;
        // Smaller than a frame: Windows can hand a send straight to a pending receive that has
        // room for all of it, which would let that send complete synchronously.
        var buffer = new byte[FrameBytes / 4];
        try
        {
            while (socket.Receive(buffer) > 0)
            {
            }
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void SetRunContinuationsAsynchronously(
        KafkaConnection.SocketScatterGatherSender sender,
        bool value)
    {
        var coreField = typeof(KafkaConnection.SocketScatterGatherSender).GetField(
                "_core",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SocketScatterGatherSender._core field not found.");
        var core = (ManualResetValueTaskSourceCore<int>)coreField.GetValue(sender)!;
        core.RunContinuationsAsynchronously = value;
        coreField.SetValue(sender, core);
    }
}
