"""Validate exact maximum-call times independently of performance verdicts."""


def validate_timing(capture):
    clock = capture['TraceClock']
    if not (0 < clock['BeforeTimestamp'] <= clock['AfterTimestamp'] <= capture['Start']['Timestamp']):
        raise ValueError('Invalid trace clock bracket')
    frequency = capture['StopwatchFrequency']
    if frequency <= 0:
        raise ValueError('Invalid stopwatch frequency')
    for interval in capture['Intervals']:
        call = interval['MaximumCall']
        start, end = interval['Start']['Timestamp'], interval['End']['Timestamp']
        if not (start <= call['StartTimestamp'] <= call['EndTimestamp'] <= end):
            raise ValueError('Maximum call falls outside its interval')
        ticks = call['EndTimestamp'] - call['StartTimestamp']
        if ticks != call['Ticks'] or ticks != max(row['Ticks'] for row in interval['Latencies']):
            raise ValueError('Maximum call timing differs from retained histogram')
    return capture
