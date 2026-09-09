"""Attribution for failed identical-product calibration; never performance acceptance."""
from pathlib import Path

SETTINGS = {
    'purpose': 'Attribute recurring GC pauses and sustained fresh-process CPU variation in identical binaries',
    'scope': 'Diagnostic only; EventPipe and JIT output affect execution. No product gate can pass from these observations.',
    'providers': 'Microsoft-Windows-DotNETRuntime:0x1:4,Microsoft-DotNETCore-SampleProfiler:0x0:4,Dekaf-AdminEvidence-Phases:0xFFFFFFFFFFFFFFFF:4',
    'jit_disasm': '*DeleteTopics*:* *RefreshMetadata*:* *MetadataManager*:* *CapturePhases*:*',
    'trace_buffer_mib': 128,
    'coverage': 'Entire process, including preparation, continuous 480-second warmup, 180-second measurement and finalization',
    'decision': 'Match GC reason/pause intervals to retained maxima, compare Tier1 code across processes, and inspect CPU stacks. Preserve all samples. Missing events or attribution remain inconclusive.',
}


def capture_command(trace, workload, destination):
    destination = Path(destination)
    # Only the workload receives JIT output settings. The collector is another .NET
    # process and must not concurrently write its own compilation output to this file.
    return [str(trace), 'collect', '--providers', SETTINGS['providers'],
            '--buffersize', str(SETTINGS['trace_buffer_mib']), '--show-child-io',
            '--output', str(destination / 'runtime.nettrace'), '--', 'env',
            'DOTNET_JitDisasm=' + SETTINGS['jit_disasm'], 'DOTNET_JitDisasmDiffable=1',
            'DOTNET_JitStdOutFile=' + str(destination / 'jit.asm'), *workload]


def validate_capture(destination):
    for name in ('runtime.nettrace', 'jit.asm'):
        path = Path(destination) / name
        if not path.is_file() or path.stat().st_size == 0:
            raise ValueError(f'Missing diagnostic output: {path}')
