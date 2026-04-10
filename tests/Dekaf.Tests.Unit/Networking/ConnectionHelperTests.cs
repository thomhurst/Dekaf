using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Networking;

/// <summary>
/// Tests for <see cref="ConnectionHelper.ReadUnsignedVarInt"/> which decodes
/// Kafka's unsigned variable-length integer encoding used for tagged field parsing.
/// </summary>
public class ConnectionHelperTests
{
    #region Empty and Truncated Input Tests

    [Test]
    public async Task ReadUnsignedVarInt_EmptySpan_ReturnsFailure()
    {
        var (_, _, success) = ConnectionHelper.ReadUnsignedVarInt(ReadOnlySpan<byte>.Empty);

        await Assert.That(success).IsFalse();
    }

    [Test]
    public async Task ReadUnsignedVarInt_TruncatedMultiByte_ReturnsFailure()
    {
        // 0x80 has continuation bit set, indicating more bytes follow, but span ends
        ReadOnlySpan<byte> truncated = [0x80];
        var (_, _, success) = ConnectionHelper.ReadUnsignedVarInt(truncated);

        await Assert.That(success).IsFalse();
    }

    [Test]
    public async Task ReadUnsignedVarInt_TruncatedThreeByte_ReturnsFailure()
    {
        // First two bytes have continuation bits set, but third byte is missing
        // This would encode a value >= 16384 which needs 3 bytes
        ReadOnlySpan<byte> truncated = [0x80, 0x80];
        var (_, _, success) = ConnectionHelper.ReadUnsignedVarInt(truncated);

        await Assert.That(success).IsFalse();
    }

    #endregion

    #region Malformed Over-Length VarInt Tests

    [Test]
    public async Task ReadUnsignedVarInt_OverLengthVarInt_ReturnsFailure()
    {
        // 6 bytes all with continuation bit set - exceeds 5-byte limit for 32-bit int
        // (5 bytes * 7 bits = 35 bits which is the max for a 32-bit varint)
        ReadOnlySpan<byte> overLength = [0x80, 0x80, 0x80, 0x80, 0x80, 0x00];
        var (_, _, success) = ConnectionHelper.ReadUnsignedVarInt(overLength);

        await Assert.That(success).IsFalse();
    }

    [Test]
    public async Task ReadUnsignedVarInt_FiveContinuationBytes_ReturnsFailure()
    {
        // 5 bytes with continuation bits, then a terminating byte = 6 bytes total
        // shift would reach 35, exceeding the limit
        ReadOnlySpan<byte> malformed = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01];
        var (_, _, success) = ConnectionHelper.ReadUnsignedVarInt(malformed);

        await Assert.That(success).IsFalse();
    }

    #endregion

    #region Single-Byte Value Tests

    [Test]
    [Arguments(0, new byte[] { 0x00 })]
    [Arguments(1, new byte[] { 0x01 })]
    [Arguments(127, new byte[] { 0x7F })]
    public async Task ReadUnsignedVarInt_SingleByteValues_DecodesCorrectly(int expectedValue, byte[] input)
    {
        var (value, bytesRead, success) = ConnectionHelper.ReadUnsignedVarInt(input);

        await Assert.That(success).IsTrue();
        await Assert.That(value).IsEqualTo(expectedValue);
        await Assert.That(bytesRead).IsEqualTo(1);
    }

    #endregion

    #region Multi-Byte Value Tests

    [Test]
    [Arguments(128, new byte[] { 0x80, 0x01 })]
    [Arguments(300, new byte[] { 0xAC, 0x02 })]
    [Arguments(16384, new byte[] { 0x80, 0x80, 0x01 })]
    public async Task ReadUnsignedVarInt_MultiByteValues_DecodesCorrectly(int expectedValue, byte[] input)
    {
        var (value, bytesRead, success) = ConnectionHelper.ReadUnsignedVarInt(input);

        await Assert.That(success).IsTrue();
        await Assert.That(value).IsEqualTo(expectedValue);
        await Assert.That(bytesRead).IsEqualTo(input.Length);
    }

    #endregion

    #region Bytes Read Count Tests

    [Test]
    public async Task ReadUnsignedVarInt_SingleByte_ReportsOneBytesRead()
    {
        ReadOnlySpan<byte> input = [0x00];
        var (_, bytesRead, success) = ConnectionHelper.ReadUnsignedVarInt(input);

        await Assert.That(success).IsTrue();
        await Assert.That(bytesRead).IsEqualTo(1);
    }

    [Test]
    public async Task ReadUnsignedVarInt_TwoBytes_ReportsTwoBytesRead()
    {
        // 128 = 0x80 0x01
        ReadOnlySpan<byte> input = [0x80, 0x01];
        var (_, bytesRead, success) = ConnectionHelper.ReadUnsignedVarInt(input);

        await Assert.That(success).IsTrue();
        await Assert.That(bytesRead).IsEqualTo(2);
    }

    [Test]
    public async Task ReadUnsignedVarInt_WithTrailingBytes_OnlyConsumesVarInt()
    {
        // Value 1 (0x01) followed by trailing data that should not be consumed
        ReadOnlySpan<byte> input = [0x01, 0xFF, 0xFF];
        var (value, bytesRead, success) = ConnectionHelper.ReadUnsignedVarInt(input);

        await Assert.That(success).IsTrue();
        await Assert.That(value).IsEqualTo(1);
        await Assert.That(bytesRead).IsEqualTo(1);
    }

    #endregion

    #region Five-Byte Maximum Value Tests

    [Test]
    public async Task ReadUnsignedVarInt_MaxFiveByteValue_DecodesCorrectly()
    {
        // int.MaxValue (2147483647) encoded as unsigned varint = 5 bytes
        // 2147483647 = 0x7FFFFFFF
        // Varint encoding: FF FF FF FF 07
        ReadOnlySpan<byte> input = [0xFF, 0xFF, 0xFF, 0xFF, 0x07];
        var (value, bytesRead, success) = ConnectionHelper.ReadUnsignedVarInt(input);

        await Assert.That(success).IsTrue();
        await Assert.That(value).IsEqualTo(int.MaxValue);
        await Assert.That(bytesRead).IsEqualTo(5);
    }

    #endregion

    #region CalculatePipelineThresholds Tests

    [Test]
    public async Task CalculatePipelineThresholds_ThreeBrokersTwoConnections_CapsAtDefaultMaximum()
    {
        // 256 MB BufferMemory, 2 connections/broker, 3 brokers
        // Per-pipe budget = 256 MB / (2 * 3) / 4 = ~10.7 MB, capped to default 4 MB
        const ulong bufferMemory = 268435456; // 256 MB
        const long expectedPause = ConnectionHelper.DefaultMaximumPauseThresholdBytes; // 4 MB cap

        var (pause, resume) = ConnectionHelper.CalculatePipelineThresholds(bufferMemory, connectionsPerBroker: 2, brokerCount: 3);

        await Assert.That(pause).IsEqualTo(expectedPause);
        await Assert.That(resume).IsEqualTo(expectedPause / 2);
    }

    [Test]
    public async Task CalculatePipelineThresholds_OneBrokerLowBufferMemory_FloorsAtMinimum()
    {
        // 1 MB BufferMemory, 1 connection, 1 broker
        // Per-pipe budget = 1 MB / 1 / 4 = 256 KB, floored to 1 MB
        const ulong bufferMemory = 1L * 1024 * 1024; // 1 MB
        const long expectedPause = 1L * 1024 * 1024; // 1 MB floor

        var (pause, resume) = ConnectionHelper.CalculatePipelineThresholds(bufferMemory, connectionsPerBroker: 1, brokerCount: 1);

        await Assert.That(pause).IsEqualTo(expectedPause);
        await Assert.That(resume).IsEqualTo(expectedPause / 2);
    }

    [Test]
    public async Task CalculatePipelineThresholds_BrokerCountZero_ThrowsArgumentOutOfRangeException()
    {
        var action = () => ConnectionHelper.CalculatePipelineThresholds(268435456, connectionsPerBroker: 1, brokerCount: 0);

        await Assert.That(action).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task CalculatePipelineThresholds_VeryLargeBufferMemory_ClampsToDefaultMaximum()
    {
        // Very large BufferMemory still clamps to DefaultMaximumPauseThresholdBytes (4 MB) when no override
        const ulong bufferMemory = ulong.MaxValue;
        const long expectedPause = ConnectionHelper.DefaultMaximumPauseThresholdBytes; // 4 MB cap

        var (pause, resume) = ConnectionHelper.CalculatePipelineThresholds(bufferMemory, connectionsPerBroker: 1, brokerCount: 1);

        await Assert.That(pause).IsEqualTo(expectedPause);
        await Assert.That(resume).IsEqualTo(expectedPause / 2);
    }

    #endregion

    #region Custom MaxPauseThreshold Tests (Consumer Pipeline Fix)

    [Test]
    public async Task CalculatePipelineThresholds_CustomMaxPauseThreshold_UsesProvidedCap()
    {
        // Consumer scenario: FetchMaxBytes = 50 MB passed as maxPauseThreshold
        // Per-pipe budget = 256 MB / (2 * 3) / 4 = ~10.7 MB, capped to 50 MB → uses 10.7 MB
        const ulong bufferMemory = 268435456; // 256 MB
        const long maxPauseThreshold = 50L * 1024 * 1024; // 50 MB (FetchMaxBytes)
        const long expectedPause = 268435456L / (2 * 3) / 4; // ~10.7 MB, within 50 MB cap

        var (pause, resume) = ConnectionHelper.CalculatePipelineThresholds(
            bufferMemory, connectionsPerBroker: 2, brokerCount: 3, maxPauseThreshold: maxPauseThreshold);

        await Assert.That(pause).IsEqualTo(expectedPause);
        await Assert.That(resume).IsEqualTo(expectedPause / 2);
    }

    [Test]
    public async Task CalculatePipelineThresholds_HighThroughputConsumer_AllowsLargerThreshold()
    {
        // High-throughput consumer: FetchMaxBytes = 100 MB, single broker, 1 connection
        // Per-pipe budget = 256 MB / 1 / 4 = 64 MB, capped to 100 MB → uses 64 MB
        const ulong bufferMemory = 268435456; // 256 MB
        const long maxPauseThreshold = 100L * 1024 * 1024; // 100 MB (ForHighThroughput)
        const long expectedPause = 268435456L / 1 / 4; // 64 MB, within 100 MB cap

        var (pause, resume) = ConnectionHelper.CalculatePipelineThresholds(
            bufferMemory, connectionsPerBroker: 1, brokerCount: 1, maxPauseThreshold: maxPauseThreshold);

        await Assert.That(pause).IsEqualTo(expectedPause);
        await Assert.That(resume).IsEqualTo(expectedPause / 2);
    }

    [Test]
    public async Task CalculatePipelineThresholds_VeryLargeBufferMemory_ClampsToCustomMaximum()
    {
        // Very large BufferMemory clamps to the custom maxPauseThreshold, not the default 4 MB
        const ulong bufferMemory = ulong.MaxValue;
        const long maxPauseThreshold = 50L * 1024 * 1024; // 50 MB

        var (pause, resume) = ConnectionHelper.CalculatePipelineThresholds(
            bufferMemory, connectionsPerBroker: 1, brokerCount: 1, maxPauseThreshold: maxPauseThreshold);

        await Assert.That(pause).IsEqualTo(maxPauseThreshold);
        await Assert.That(resume).IsEqualTo(maxPauseThreshold / 2);
    }

    [Test]
    public async Task CalculatePipelineThresholds_DefaultMaxPauseThreshold_MatchesDefaultConstant()
    {
        // Verify the default parameter matches the constant (producer scenario)
        // Both calls should produce identical results
        const ulong bufferMemory = 268435456; // 256 MB

        var withDefault = ConnectionHelper.CalculatePipelineThresholds(bufferMemory, connectionsPerBroker: 2, brokerCount: 3);
        var withExplicit = ConnectionHelper.CalculatePipelineThresholds(
            bufferMemory, connectionsPerBroker: 2, brokerCount: 3,
            maxPauseThreshold: ConnectionHelper.DefaultMaximumPauseThresholdBytes);

        await Assert.That(withDefault.PauseThreshold).IsEqualTo(withExplicit.PauseThreshold);
        await Assert.That(withDefault.ResumeThreshold).IsEqualTo(withExplicit.ResumeThreshold);
    }

    [Test]
    public async Task CalculatePipelineThresholds_CustomMaxBelowFloor_FloorsAtMinimum()
    {
        // Even with a custom maxPauseThreshold, the floor (1 MB) is respected
        // Very low buffer memory → per-pipe budget < 1 MB → floored to 1 MB
        const ulong bufferMemory = 1L * 1024 * 1024; // 1 MB
        const long maxPauseThreshold = 50L * 1024 * 1024; // 50 MB
        const long expectedPause = 1L * 1024 * 1024; // 1 MB floor

        var (pause, resume) = ConnectionHelper.CalculatePipelineThresholds(
            bufferMemory, connectionsPerBroker: 1, brokerCount: 1, maxPauseThreshold: maxPauseThreshold);

        await Assert.That(pause).IsEqualTo(expectedPause);
        await Assert.That(resume).IsEqualTo(expectedPause / 2);
    }

    #endregion
}
