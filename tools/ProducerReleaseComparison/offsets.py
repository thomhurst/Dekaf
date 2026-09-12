"""Independent broker-confirmed offsets, outside the client's CPU affinity/process."""
import datetime, json, socket, struct, sys, time

def string(s):
    b = s.encode()
    return struct.pack('>h', len(b)) + b

def read(s, n):
    data = b''
    while len(data) < n:
        chunk = s.recv(n - len(data))
        if not chunk:
            raise EOFError('Broker closed connection')
        data += chunk
    return data

def offsets():
    # ListOffsets v1, latest offsets of all six partitions, replica=-1.
    body = struct.pack('>i', -1) + struct.pack('>i', 1) + string('comparison') + struct.pack('>i', 6)
    for p in range(6): body += struct.pack('>iq', p, -1)
    req = struct.pack('>hhi', 2, 1, 1) + string('independent-observer') + body
    with socket.create_connection(('localhost', 9092), timeout=5) as s:
        s.sendall(struct.pack('>i', len(req)) + req)
        buf = memoryview(read(s, struct.unpack('>i', read(s, 4))[0]))
    pos = 0
    def take(fmt):
        nonlocal pos
        result = struct.unpack_from(fmt, buf, pos)
        pos += struct.calcsize(fmt)
        return result
    take('>i')
    topics, = take('>i')
    values = []
    for _ in range(topics):
        n, = take('>h'); pos += n
        parts, = take('>i')
        for _ in range(parts):
            p, error, timestamp, offset = take('>ihqq')
            if error: raise RuntimeError(f'partition {p}: Kafka error {error}')
            values.append(offset)
    if len(values) != 6: raise RuntimeError(f'Expected 6 partitions, got {len(values)}')
    return values

if __name__ == '__main__':
    while True:
        start = time.time()
        try:
            values = offsets()
            row = dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), unix=time.time(), offsets=values, total=sum(values))
        except Exception as exc:
            row = dict(unix=time.time(), error=str(exc))
        print(json.dumps(row), flush=True)
        if len(sys.argv) > 1 and sys.argv[1] == '--once': break
        time.sleep(max(0, 5 - (time.time() - start)))
