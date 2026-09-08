#!/bin/bash
set -euo pipefail
folder=$1
shift
printf '%s\n' "$$" > "$folder/consumer.pid"
taskset -pc 2,3 "$$" > "$folder/consumer-affinity.log"
exec dotnet /inputs/Loaded-B/Loaded.dll consume "$@"
