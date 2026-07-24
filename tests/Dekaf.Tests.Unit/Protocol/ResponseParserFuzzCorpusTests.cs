using System.Buffers.Binary;
using Dekaf.Fuzzing;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Protocol;

public class ResponseParserFuzzCorpusTests
{
    [Test]
    public async Task Registry_CoversEveryConcreteResponseVersion()
    {
        var expected = typeof(IKafkaMessage).Assembly
            .GetTypes()
            .Where(static type => !type.IsAbstract && typeof(IKafkaResponse).IsAssignableFrom(type))
            .SelectMany(static type => SupportedVersions(type)
                .Select(version => $"{type.Name}.v{version}"))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var actual = ResponseParserRegistry.Registrations
            .SelectMany(static registration => registration.SupportedVersions
                .Select(version => $"{registration.ResponseType.Name}.v{version}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        await Assert.That(actual).IsEquivalentTo(expected);
    }

    [Test]
    public async Task InputHeader_SelectsEveryRegisteredResponseVersion()
    {
        foreach (var registration in ResponseParserRegistry.Registrations)
        {
            foreach (var version in registration.SupportedVersions)
            {
                var input = ResponseParserFuzzTarget.CreateInput(registration.ApiKey, version, []);

                await Assert.That(ResponseParserFuzzTarget.TryGetSelection(
                    input,
                    out var selected,
                    out var segmented)).IsTrue();
                await Assert.That(selected).IsSameReferenceAs(registration);
                await Assert.That(segmented).IsFalse();
            }
        }
    }

    [Test]
    public async Task CheckedInCorpus_CoversEveryResponseVersion()
    {
        var seeds = ResponseParserCorpus.LoadEmbedded();
        var expectedNames = ResponseParserRegistry.Registrations
            .SelectMany(static registration => registration.SupportedVersions
                .Select(version => $"{registration.ResponseType.Name}.v{version}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        await Assert.That(seeds.Select(static seed => seed.Name).Order(StringComparer.Ordinal))
            .IsEquivalentTo(expectedNames);

        foreach (var seed in seeds)
        {
            await Assert.That(ResponseParserFuzzTarget.TryGetSelection(
                seed.Data,
                out var registration,
                out var segmented)).IsTrue();
            await Assert.That(seed.Name).IsEqualTo(
                $"{registration.ResponseType.Name}.v{ResponseParserFuzzTarget.GetVersion(seed.Data)}");
            await Assert.That(segmented).IsFalse();
        }
    }

    [Test]
    public async Task CheckedInCorpus_IncludesPopulatedNestedResponseFamilies()
    {
        var seedsByName = ResponseParserCorpus.LoadEmbedded()
            .ToDictionary(static seed => seed.Name, StringComparer.Ordinal);
        string[] populatedFamilies =
        [
            "DescribeConfigsResponse.v4",
            "DescribeGroupsResponse.v5",
            "FetchResponse.v18",
            "ListOffsetsResponse.v8",
            "MetadataResponse.v13",
            "OffsetFetchResponse.v9",
            "ProduceResponse.v12"
        ];

        foreach (var name in populatedFamilies)
        {
            var seed = seedsByName[name];
            var containsNonZeroPayload = seed.Data
                .Skip(ResponseParserFuzzTarget.HeaderLength)
                .Any(static value => value != 0);

            await Assert.That(seed.Data.Length)
                .IsGreaterThan(ResponseParserFuzzTarget.HeaderLength + 40);
            await Assert.That(containsNonZeroPayload).IsTrue();
        }
    }

    [Test]
    public async Task CheckedInCorpus_ReplaysContiguousAndSegmented()
    {
        foreach (var seed in ResponseParserCorpus.LoadEmbedded())
        {
            await Assert.That(ResponseParserFuzzTarget.ParseValidInput(seed.Data)).IsEqualTo(0);

            var segmented = seed.Data.ToArray();
            segmented[ResponseParserFuzzTarget.FlagsOffset] |= ResponseParserFuzzTarget.SegmentedInputFlag;
            await Assert.That(ResponseParserFuzzTarget.ParseValidInput(segmented)).IsEqualTo(0);
        }
    }

    [Test]
    public async Task CheckedInRegressionCorpus_RemainsExpectedParseFailure()
    {
        var seeds = ResponseParserCorpus.LoadEmbeddedRegressions();

        await Assert.That(seeds).IsNotEmpty();

        foreach (var seed in seeds)
        {
            await Assert.That(ResponseParserFuzzTarget.TryGetSelection(
                seed.Data,
                out _,
                out var segmented)).IsTrue();
            await Assert.That(segmented).IsFalse();

            // Promoted crash inputs are intentionally malformed: they must be rejected
            // with an expected parse exception (never an OOM, hang, or unexpected type),
            // both contiguous and segmented.
            AssertExpectedParseFailure(seed.Data.ToArray());

            var segmentedInput = seed.Data.ToArray();
            segmentedInput[ResponseParserFuzzTarget.FlagsOffset] |= ResponseParserFuzzTarget.SegmentedInputFlag;
            AssertExpectedParseFailure(segmentedInput);
        }
    }

    private static void AssertExpectedParseFailure(byte[] input)
    {
        try
        {
            ResponseParserFuzzTarget.ParseValidInput(input);
        }
        catch (MalformedProtocolDataException)
        {
            return;
        }
        catch (InsufficientDataException)
        {
            return;
        }

        throw new InvalidOperationException(
            "Regression corpus input parsed successfully but must fail with an expected parse exception.");
    }

    [Test]
    public void CheckedInCorpus_TruncationAndCorruptionRemainExpectedParseFailures()
    {
        foreach (var seed in ResponseParserCorpus.LoadEmbedded().Concat(ResponseParserCorpus.LoadEmbeddedRegressions()))
        {
            for (var length = ResponseParserFuzzTarget.HeaderLength; length < seed.Data.Length; length++)
            {
                RunContiguousAndSegmented(seed.Data.AsSpan(0, length).ToArray());
            }

            for (var index = ResponseParserFuzzTarget.HeaderLength; index < seed.Data.Length; index++)
            {
                var corrupted = seed.Data.ToArray();
                corrupted[index] ^= 0xFF;
                RunContiguousAndSegmented(corrupted);
            }

            for (var index = ResponseParserFuzzTarget.HeaderLength;
                 index <= seed.Data.Length - sizeof(int);
                 index++)
            {
                var hostileLength = seed.Data.ToArray();
                BinaryPrimitives.WriteInt32BigEndian(hostileLength.AsSpan(index), int.MaxValue);
                RunContiguousAndSegmented(hostileLength);
            }
        }
    }

    private static void RunContiguousAndSegmented(byte[] input)
    {
        KafkaProtocolFuzzTarget.Run(input);
        input[ResponseParserFuzzTarget.FlagsOffset] |= ResponseParserFuzzTarget.SegmentedInputFlag;
        KafkaProtocolFuzzTarget.Run(input);
    }

    private static IEnumerable<short> SupportedVersions(Type responseType)
    {
        var lowest = GetStaticProperty<short>(responseType, "LowestSupportedVersion");
        var highest = GetStaticProperty<short>(responseType, "HighestSupportedVersion");

        for (var version = lowest; version <= highest; version++)
        {
            yield return version;
        }
    }

    private static T GetStaticProperty<T>(Type type, string propertyName) =>
        (T)(type.GetProperty(propertyName)?.GetValue(null)
            ?? throw new InvalidOperationException($"{type.FullName} has no static {propertyName} property."));
}
