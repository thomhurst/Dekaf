#!/bin/bash
set -euo pipefail
folder=$1
topic=$2
warmup=$3
seconds=$4
rate=$5
traced=$6
mkdir -p "$folder"
taskset -c 1 dotnet /inputs/Loaded-A/Loaded.dll produce "$topic" "$folder" sync-batches "$warmup" "$seconds" "$rate" > "$folder/producer.log" 2>&1 &
producer=$!
printf '%s\n' "$producer" > "$folder/producer.pid"
trap 'kill "$producer" 2>/dev/null || true' EXIT
if [ "$traced" = true ]; then
  taskset -c 4 dotnet --roll-forward Major /inputs/tracer/dotnet-trace.dll collect --providers Microsoft-Windows-DotNETRuntime:0x10:5 --buffersize 128 --rundown false --duration 00:07:00 --output "$folder/consumer.nettrace" --show-child-io -- /bin/bash /scripts/consumer.sh "$folder" "$topic" "$folder" sync-batches "$warmup" "$seconds" "$rate" > "$folder/consumer.log" 2>&1
else
  /bin/bash /scripts/consumer.sh "$folder" "$topic" "$folder" sync-batches "$warmup" "$seconds" "$rate" > "$folder/consumer.log" 2>&1
fi
wait "$producer"
trap - EXIT
