using System.Buffers.Binary;
using Dekaf.Protocol;

namespace Dekaf.Fuzzing;

public static class ResponseParserFuzzTarget
{
    public const byte InputMarker = 0xFF;
    public const byte SegmentedInputFlag = 0x01;
    public const int FlagsOffset = 1;
    public const int HeaderLength = 6;

    private const int ApiKeyOffset = 2;
    private const int VersionOffset = 4;
    private const int MaxInputLength = 1024 * 1024;

    public static void Run(ReadOnlySpan<byte> input)
    {
        if (input.Length > MaxInputLength ||
            !TryGetSelection(input, out var registration, out var segmented))
        {
            return;
        }

        try
        {
            var version = GetVersion(input);
            _ = Parse(registration, input[HeaderLength..], version, segmented);
        }
        catch (InsufficientDataException)
        {
            // Truncated response is an expected parse outcome.
        }
        catch (MalformedProtocolDataException)
        {
            // Structurally invalid response is an expected parse outcome.
        }
    }

    internal static long ParseValidInput(ReadOnlySpan<byte> input)
    {
        if (!TryGetSelection(input, out var registration, out var segmented))
        {
            throw new InvalidOperationException("Input does not select a registered response parser.");
        }

        return Parse(registration, input[HeaderLength..], GetVersion(input), segmented);
    }

    public static byte[] CreateInput(
        ApiKey apiKey,
        short version,
        ReadOnlySpan<byte> payload,
        bool segmented = false)
    {
        if (!ResponseParserRegistry.TryGet(apiKey, version, out _))
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                version,
                $"{apiKey} response version {version} is not registered.");
        }

        var input = new byte[HeaderLength + payload.Length];
        input[0] = InputMarker;
        input[FlagsOffset] = segmented ? SegmentedInputFlag : (byte)0;
        BinaryPrimitives.WriteInt16BigEndian(input.AsSpan(ApiKeyOffset), (short)apiKey);
        BinaryPrimitives.WriteInt16BigEndian(input.AsSpan(VersionOffset), version);
        payload.CopyTo(input.AsSpan(HeaderLength));
        return input;
    }

    internal static bool IsResponseInput(ReadOnlySpan<byte> input) =>
        !input.IsEmpty && input[0] == InputMarker;

    internal static bool TryGetSelection(
        ReadOnlySpan<byte> input,
        out ResponseParserRegistration registration,
        out bool segmented)
    {
        registration = null!;
        segmented = false;
        if (input.Length < HeaderLength || input[0] != InputMarker)
        {
            return false;
        }

        var apiKey = (ApiKey)BinaryPrimitives.ReadInt16BigEndian(input[ApiKeyOffset..]);
        var version = GetVersion(input);
        segmented = (input[FlagsOffset] & SegmentedInputFlag) != 0;
        return ResponseParserRegistry.TryGet(apiKey, version, out registration);
    }

    internal static short GetVersion(ReadOnlySpan<byte> input) =>
        BinaryPrimitives.ReadInt16BigEndian(input[VersionOffset..]);

    private static long Parse(
        ResponseParserRegistration registration,
        ReadOnlySpan<byte> payload,
        short version,
        bool segmented)
    {
        if (!segmented || payload.IsEmpty)
        {
            var contiguousReader = new KafkaProtocolReader(payload);
            registration.Parser.Parse(ref contiguousReader, version);
            return contiguousReader.Remaining;
        }

        var sequence = SegmentedFuzzInput.Create(payload);
        var reader = new KafkaProtocolReader(sequence);
        registration.Parser.Parse(ref reader, version);
        return reader.Remaining;
    }
}
