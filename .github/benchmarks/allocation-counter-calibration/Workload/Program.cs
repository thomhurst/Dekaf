using System.Diagnostics.Tracing;
using System.Text.Json;

if (args.Length is not (2 or 3)) throw new ArgumentException("object-size output.json [serial-tail-bytes]");
var size = int.Parse(args[0]);
const int workerCount = 4;
var iterations = Math.Max(1000, 512_000_000 / size);
var workers = new Worker[workerCount];
using var ready = new CountdownEvent(workerCount);
using var start = new ManualResetEventSlim();
var threads = new Thread[workerCount];
for (var i = 0; i < workerCount; i++)
{
    workers[i] = new Worker(size, iterations, ready, start);
    threads[i] = new Thread(workers[i].Run);
    threads[i].Start();
}
ready.Wait();
GC.Collect();
GC.WaitForPendingFinalizers();
Events.Log.Phase("start");
var allocatedStart = GC.GetTotalAllocatedBytes(true);
start.Set();
foreach (var thread in threads) thread.Join();
var allocatedHotEnd = GC.GetTotalAllocatedBytes(true);
long serialAllocated = 0;
if (args.Length == 3)
{
    Events.Log.Phase("serial");
    var before = GC.GetAllocatedBytesForCurrentThread();
    var count = long.Parse(args[2]) / size;
    for (long i = 0; i < count; i++)
    {
        var data = new byte[size];
        data[0] = (byte)i;
        GC.KeepAlive(data);
        if ((i & 8191) == 0) Thread.Sleep(1);
    }
    serialAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
}
var allocatedEnd = GC.GetTotalAllocatedBytes(true);
Events.Log.Phase("end");
var known = workers.Sum(worker => worker.Allocated) + serialAllocated;
File.WriteAllText(args[1], JsonSerializer.Serialize(new
{
    Scope = "Allocation counter calibration only; not product performance acceptance",
    Size = size, WorkerCount = workerCount, IterationsPerWorker = iterations,
    KnownWorkerAllocatedBytes = known, GlobalAllocatedStart = allocatedStart,
    GlobalAllocatedEnd = allocatedEnd, GlobalAllocatedDelta = allocatedEnd - allocatedStart,
    GlobalAllocatedHotEnd = allocatedHotEnd, SerialAllocatedBytes = serialAllocated,
    GlobalSerialDelta = allocatedEnd - allocatedHotEnd,
    ServerGC = System.Runtime.GCSettings.IsServerGC,
    DynamicAdaptationMode = Environment.GetEnvironmentVariable("DOTNET_GCDynamicAdaptationMode"),
    RuntimeVersion = Environment.Version.ToString()
}, new JsonSerializerOptions { WriteIndented = true }));

sealed class Worker(int size, int iterations, CountdownEvent ready, ManualResetEventSlim start)
{
    internal long Allocated;
    internal void Run()
    {
        ready.Signal();
        start.Wait();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            var data = new byte[size];
            data[0] = (byte)i;
            GC.KeepAlive(data);
        }
        Allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    }
}

[EventSource(Name = "Dekaf-AllocationCounterCalibration")]
sealed class Events : EventSource
{
    internal static readonly Events Log = new();
    [Event(1, Level = EventLevel.Informational)]
    public void Phase(string name) => WriteEvent(1, name);
}
