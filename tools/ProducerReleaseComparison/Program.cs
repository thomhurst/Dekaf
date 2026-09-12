using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Runtime;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Dekaf;
using Dekaf.Producer;

// Investigation fixture: identical source, independent package closures, no product edits.
var seconds = int.Parse(args[0]);
var warmup = int.Parse(args[1]);
var output = args[2];
Directory.CreateDirectory(output);
var keys = Enumerable.Range(0, 10000).Select(i => $"key-{i}").ToArray();
var value = new string('x', 1000);
using var errors = new MeterListener();
long deliveryErrors = 0;
errors.InstrumentPublished = (instrument, listener) =>
{
    if (instrument.Name == "dekaf.producer.send.errors") listener.EnableMeasurementEvents(instrument);
};
errors.SetMeasurementEventCallback<long>((_, n, _, _) => Interlocked.Add(ref deliveryErrors, n));
errors.Start();
await using var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092").WithClientId("release-comparison")
    .WithIdempotence(false).WithAcks(Acks.Leader)
    .WithLinger(TimeSpan.FromMilliseconds(5)).WithBatchSize(1048576)
    .WithBufferMemory(512UL * 1024 * 1024).WithConnectionsPerBroker(1)
    .WithoutAdaptiveConnections().WithDeliveryLatencyTarget(TimeSpan.FromMilliseconds(10))
    .WithDeliveryDiagnostics().BuildAsync();
var phases = new List<object>();
await RunPhase("warmup", warmup);
await RunPhase("measure", seconds);
var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic)
    .Select(a => new { name = a.GetName().Name, version = a.GetName().Version?.ToString(),
        info = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        path = a.Location }).ToArray();
File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new {
    phases, deliveryErrors, assemblies, runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    processors = Environment.ProcessorCount, serverGc = GCSettings.IsServerGC
}, new JsonSerializerOptions { WriteIndented = true }));
if (deliveryErrors != 0) throw new Exception($"Delivery errors: {deliveryErrors}");

async Task RunPhase(string phase, int duration)
{
    var stats = new Counters();
    using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(duration));
    using var sampling = new CancellationTokenSource();
    using var process = Process.GetCurrentProcess();
    using var writer = new StreamWriter(Path.Combine(output, phase + ".jsonl")) { AutoFlush = true };
    var startOffset = BrokerOffsets.Read();
    var beginUtc = DateTimeOffset.UtcNow;
    var begin = Stopwatch.GetTimestamp();
    var cpu = process.TotalProcessorTime.TotalSeconds;
    var allocated = GC.GetTotalAllocatedBytes(false);
    Console.WriteLine($"{phase} started {beginUtc:O}, {duration}s");
    Snapshot();
    var sampler = SampleLoop();
    long index = 0;
    while (!stop.IsCancellationRequested)
    {
        if (index % 1000 == 0) { Interlocked.Increment(ref stats.PendingSamples); _ = Sample(keys[index % keys.Length]); }
        else await producer.FireAsync("comparison", keys[index % keys.Length], value).ConfigureAwait(false);
        Interlocked.Increment(ref stats.Accepted);
        Interlocked.Add(ref stats.Bytes, 1000);
        index++;
        if (index % 100000 == 0) await Task.Yield();
    }
    var ingressEndUtc = DateTimeOffset.UtcNow;
    await producer.FlushAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
    while (Interlocked.Read(ref stats.PendingSamples) != 0) await Task.Delay(1);
    var end = Stopwatch.GetTimestamp();
    var endUtc = DateTimeOffset.UtcNow;
    var elapsed = Stopwatch.GetElapsedTime(begin, end).TotalSeconds;
    var totalCpu = process.TotalProcessorTime.TotalSeconds - cpu;
    var totalAlloc = GC.GetTotalAllocatedBytes(false) - allocated;
    var delivered = BrokerOffsets.Read() - startOffset;
    sampling.Cancel();
    await sampler;
    Snapshot();
    var result = new { phase, beginUtc, ingressEndUtc, endUtc, elapsed, accepted = stats.Accepted, delivered,
        messagesPerSecond = stats.Accepted / elapsed, cpuUsPerMessage = totalCpu * 1e6 / stats.Accepted,
        allocatedBytesPerMessage = (double)totalAlloc / stats.Accepted, errors = stats.Errors,
        latency = stats.Total.Snapshot(false) };
    phases.Add(result);
    Console.WriteLine(JsonSerializer.Serialize(result));
    if (stats.Errors != 0) throw new Exception("Sampled delivery failed");
    if (delivered != stats.Accepted) throw new Exception($"Delivery mismatch: {delivered} != {stats.Accepted}");

    async Task Sample(string key)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            await producer.ProduceAsync("comparison", key, value).ConfigureAwait(false);
            var us = Stopwatch.GetElapsedTime(start).TotalMicroseconds;
            stats.Interval.Record(us); stats.Total.Record(us);
        }
        catch { Interlocked.Increment(ref stats.Errors); }
        finally { Interlocked.Decrement(ref stats.PendingSamples); }
    }
    async Task SampleLoop()
    {
        try { while (true) { await Task.Delay(5000, sampling.Token); Snapshot(); } }
        catch (OperationCanceledException) when (sampling.IsCancellationRequested) { }
    }
    void Snapshot()
    {
        process.Refresh();
        writer.WriteLine(JsonSerializer.Serialize(new {
            utc = DateTimeOffset.UtcNow, elapsed = Stopwatch.GetElapsedTime(begin).TotalSeconds,
            accepted = Interlocked.Read(ref stats.Accepted), cpuSeconds = process.TotalProcessorTime.TotalSeconds,
            allocated = GC.GetTotalAllocatedBytes(false), heap = GC.GetTotalMemory(false),
            workingSet = process.WorkingSet64, gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2),
            gcPauseMs = GC.GetTotalPauseDuration().TotalMilliseconds,
            threads = ThreadPool.ThreadCount, pendingWork = ThreadPool.PendingWorkItemCount,
            pendingSamples = Interlocked.Read(ref stats.PendingSamples), errors = Interlocked.Read(ref stats.Errors),
            latency = stats.Interval.Snapshot(true)
        }));
    }
}

static class BrokerOffsets
{
    public static long Read()
    {
        using var request = new MemoryStream();
        void Short(short n) { Span<byte> b = stackalloc byte[2]; BinaryPrimitives.WriteInt16BigEndian(b, n); request.Write(b); }
        void Int(int n) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(b, n); request.Write(b); }
        void Long(long n) { Span<byte> b = stackalloc byte[8]; BinaryPrimitives.WriteInt64BigEndian(b, n); request.Write(b); }
        void Text(string text) { var b = Encoding.UTF8.GetBytes(text); Short((short)b.Length); request.Write(b); }
        Short(2); Short(1); Int(1); Text("phase-offsets"); Int(-1); Int(1); Text("comparison"); Int(6);
        for (int p = 0; p < 6; p++) { Int(p); Long(-1); }
        using var client = new TcpClient();
        client.Connect("localhost", 9092); client.ReceiveTimeout = 10000; client.SendTimeout = 10000;
        using var stream = client.GetStream();
        Span<byte> size = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(size, (int)request.Length);
        stream.Write(size); stream.Write(request.ToArray()); stream.ReadExactly(size);
        var response = new byte[BinaryPrimitives.ReadInt32BigEndian(size)]; stream.ReadExactly(response);
        int pos = 4;
        int GetInt() { var n = BinaryPrimitives.ReadInt32BigEndian(response.AsSpan(pos)); pos += 4; return n; }
        short GetShort() { var n = BinaryPrimitives.ReadInt16BigEndian(response.AsSpan(pos)); pos += 2; return n; }
        long GetLong() { var n = BinaryPrimitives.ReadInt64BigEndian(response.AsSpan(pos)); pos += 8; return n; }
        if (GetInt() != 1) throw new Exception("Unexpected ListOffsets topic count");
        var length = GetShort(); pos += length;
        if (GetInt() != 6) throw new Exception("Unexpected ListOffsets partition count");
        long total = 0;
        for (int p = 0; p < 6; p++) { GetInt(); var error = GetShort(); GetLong(); total += GetLong(); if (error != 0) throw new Exception($"ListOffsets error {error}"); }
        return total;
    }
}

sealed class Counters
{
    public long Accepted, Bytes, PendingSamples, Errors;
    public readonly Histogram Interval = new(), Total = new();
}
sealed class Histogram
{
    // 100us buckets, including a separate overflow count; no tail samples discarded.
    readonly long[] buckets = new long[300001];
    long count; double max;
    public void Record(double us)
    {
        lock (buckets) { buckets[Math.Min(buckets.Length - 1, (int)(us / 100))]++; count++; max = Math.Max(max, us); }
    }
    public object Snapshot(bool reset)
    {
        lock (buckets)
        {
            double Quantile(double q)
            {
                long n = 0;
                for (int i = 0; i < buckets.Length; i++) { n += buckets[i]; if (n >= Math.Max(1, (long)Math.Ceiling(count * q))) return (i + 0.5) / 10; }
                return 0;
            }
            var result = new { count, p50Ms = Quantile(.5), p95Ms = Quantile(.95), p99Ms = Quantile(.99), maxMs = max / 1000, overflow = buckets[^1] };
            if (reset) { Array.Clear(buckets); count = 0; max = 0; }
            return result;
        }
    }
}
