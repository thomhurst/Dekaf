namespace Dekaf.Protocol;

internal interface IRequestWriteSequenceSource
{
    long NextRequestWriteSequence();
}

internal interface IRequestWriteSequenceTarget
{
    IRequestWriteSequenceSource? WriteSequenceSource { set; }
    Action RequestWriteStarted { get; }
    long WriteSequence { get; }
}
