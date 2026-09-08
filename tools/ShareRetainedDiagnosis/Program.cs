using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Dekaf.Benchmarks.Benchmarks.Unit;
using Dekaf.ShareConsumer;

var benchmark = new ShareConsumerParsingBenchmarks { RecordCount = 1024, HeaderCount = 0 };
await benchmark.Setup();
try
{
    var records = (List<ShareConsumeResult<int, int>>)typeof(ShareConsumerParsingBenchmarks)
        .GetField("_retained", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(benchmark)!;
    var checksum = benchmark.TraverseRetainedBatch();
    var expected = Enumerable.Range(0, 1024).Sum(index => 1000L + index + index + index + 1 + 3 + 1700000000000 + index);
    if (records.Count != 1024 || checksum != expected || records.Any(record => record.Headers.Count != 0))
        throw new InvalidOperationException("Retained fixture validation failed.");
    long repeated = 0;
    for (var index = 0; index < 1000; index++) repeated += benchmark.TraverseRetainedBatch();
    if (repeated != checksum * 1000) throw new InvalidOperationException("Traversal checksum changed.");

    var fields = new List<object>();
    foreach (var field in typeof(ShareConsumeResult<int, int>).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
    {
        var method = new DynamicMethod("FieldOffset", typeof(nint), [typeof(ShareConsumeResult<int, int>)], typeof(Program).Module, true);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldflda, field); il.Emit(OpCodes.Conv_I);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Conv_I); il.Emit(OpCodes.Sub); il.Emit(OpCodes.Ret);
        var offset = method.CreateDelegate<Func<ShareConsumeResult<int, int>, nint>>()(records[0]);
        fields.Add(new { field.Name, Type = field.FieldType.FullName, Offset = (long)offset });
    }
    var addresses = new long[records.Count];
    var noGc = GC.TryStartNoGCRegion(1024 * 1024);
    if (!noGc) throw new InvalidOperationException("Cannot safely inspect reference spacing.");
    try
    {
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            addresses[index] = Unsafe.As<ShareConsumeResult<int, int>, nint>(ref record);
        }
    }
    finally { GC.EndNoGCRegion(); }
    var assembly = typeof(ShareConsumeResult<int, int>).Assembly;
    var result = new
    {
        Checksum = checksum, RepeatedChecksum = repeated, Count = records.Count,
        Product = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
        ProductMvid = assembly.ManifestModule.ModuleVersionId,
        Fields = fields,
        AddressRemainders64 = addresses.GroupBy(address => address % 64).ToDictionary(group => group.Key, group => group.Count()),
        AdjacentStrides = addresses.Zip(addresses.Skip(1), (left, right) => right - left).GroupBy(stride => stride).ToDictionary(group => group.Key, group => group.Count()),
        RelativeAddresses = addresses.Select(address => address - addresses[0]).ToArray()
    };
    File.WriteAllText(args[0], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Validated {records.Count} retained records; checksum {checksum}.");
}
finally { await benchmark.Cleanup(); }
