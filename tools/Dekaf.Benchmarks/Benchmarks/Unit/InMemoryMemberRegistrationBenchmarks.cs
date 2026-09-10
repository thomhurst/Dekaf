using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures registration and removal with retained dictionary capacity and increasing membership.</summary>
[MemoryDiagnoser]
public class InMemoryMemberRegistrationBenchmarks
{
    private InMemoryKafkaCluster _cluster = null!;
    private string[] _memberIds = null!;
    private string[] _instanceIds = null!;
    private ConsumerGroupMemberIdentity[] _identities = null!;

    [Params(32, 1024)]
    public int Members { get; set; }

    [Params(false, true)]
    public bool Static { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _cluster = new InMemoryKafkaCluster();
        _memberIds = new string[Members];
        _instanceIds = new string[Members];
        _identities = new ConsumerGroupMemberIdentity[Members];
        for (var index = 0; index < Members; index++)
        {
            _memberIds[index] = "member-" + index;
            _instanceIds[index] = "instance-" + index;
            _identities[index] = Static
                ? new ConsumerGroupMemberIdentity { GroupInstanceId = _instanceIds[index] }
                : new ConsumerGroupMemberIdentity { MemberId = _memberIds[index] };
        }
        RegisterAndRemove();
    }

    [Benchmark]
    public int RegisterAndRemove()
    {
        for (var index = 0; index < Members; index++)
            _cluster.RegisterConsumerGroupMember("group", _memberIds[index], [], out _,
                Static ? _instanceIds[index] : null);
        var result = _cluster.RemoveConsumerGroupMembers("group", _identities);
        if (!result.Succeeded || result.Members.Count != Members)
            throw new InvalidOperationException("The fixture must remove every registered member.");
        return result.Members.Count;
    }
}
