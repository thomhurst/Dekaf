"""Keep the timed serial benchmark's final accepted/delivered counters comparable."""
from pathlib import Path

files = []
for version in ['main', 'candidate']:
    path = Path('versions') / version / 'tools/Dekaf.StressTests/Scenarios/ProducerAsyncStressTest.cs'
    text = path.read_text(encoding='utf-8')
    marker = '        using var gcStats = new GcStats();'
    call = 'await producer.ProduceAsync(options.Topic, StressTestHelpers.GetKey(messageIndex), messageValue, cts.Token)'
    if text.count(marker) != 1 or text.count(call) != 1:
        raise ValueError(f'Unexpected producer harness in {path}')
    text = text.replace(marker, '        // Duration ends between deliveries; keep per-operation cancellation enabled.\n'
                        '        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);\n'
                        + marker)
    text = text.replace(call, call.replace('cts.Token', 'operationCts.Token'))
    path.write_text(text, encoding='utf-8')
    files.append(text)
if files[0] != files[1]:
    raise ValueError('Baseline and candidate must use the exact same measurement harness')
