"""Validate subscribed hosted-share evidence without changing performance thresholds."""

from uuid import UUID


def telemetry_identity(result):
    telemetry = result.get("shareTelemetry")
    if not isinstance(telemetry, dict):
        return ()
    return (telemetry.get("pushIntervalMilliseconds"), telemetry.get("maximumRetainedPayloads"),
            telemetry.get("maximumPayloadBytes"), tuple(telemetry.get("requestedMetrics") or []),
            len(telemetry.get("workers") or []))


def validate_telemetry(result):
    telemetry = result.get("shareTelemetry")
    if str(result.get("scenario", "")).casefold() != "hosted-share-telemetry":
        if telemetry is not None:
            raise ValueError("Telemetry evidence requires the subscribed hosted-share scenario")
        return
    if not isinstance(telemetry, dict):
        raise ValueError("Subscribed hosted-share requires telemetry evidence")
    if (str(result.get("client", "")).casefold() != "dekaf" or result.get("brokerCount") != 1
            or result.get("idempotent") is not True):
        raise ValueError("Subscribed hosted-share requires one broker and the idempotent Dekaf workload")
    for field in ("pushIntervalMilliseconds", "maximumRetainedPayloads", "maximumPayloadBytes", "retainedPayloads"):
        value = telemetry.get(field)
        if type(value) is not int or value <= 0:
            raise ValueError(f"Invalid telemetry {field}")
    if telemetry["retainedPayloads"] > telemetry["maximumRetainedPayloads"]:
        raise ValueError("Telemetry history exceeds its bound")
    metrics = telemetry.get("requestedMetrics")
    if (not isinstance(metrics, list) or len(metrics) != 2
            or set(metrics) != {"org.apache.kafka.consumer.share.", "com.example.dekaf.stress.worker"}):
        raise ValueError("Missing built-in and application metric subscriptions")
    workers = telemetry.get("workers")
    if not isinstance(workers, list) or len(workers) != 2 or any(not isinstance(worker, dict) for worker in workers):
        raise ValueError("Telemetry requires two worker observations")
    clients, identities, payloads = set(), set(), 0
    for worker in workers:
        client = worker.get("clientId")
        if not isinstance(client, str) or not client or client in clients:
            raise ValueError("Telemetry workers require distinct client IDs")
        clients.add(client)
        try:
            identity = UUID(worker.get("clientInstanceId", ""))
        except (ValueError, TypeError, AttributeError) as error:
            raise ValueError("Invalid telemetry instance identity") from error
        if identity.int == 0 or identity in identities:
            raise ValueError("Telemetry workers require distinct nonempty instance identities")
        identities.add(identity)
        periodic = worker.get("periodicPayloads")
        terminating = worker.get("terminatingPayloads")
        size = worker.get("payloadBytes")
        if type(periodic) is not int or periodic <= 0 or type(terminating) is not int or terminating != 1:
            raise ValueError("Both workers require periodic and exactly one terminating export")
        count = periodic + terminating
        if type(size) is not int or size < count or size > count * telemetry["maximumPayloadBytes"]:
            raise ValueError("Invalid retained telemetry byte count")
        if any(worker.get(field) is not True for field in ("positiveFetch", "positiveRecords", "positiveAcknowledgements")):
            raise ValueError("Periodic telemetry must prove fetch, record and acknowledgement progress")
        payloads += count
    if payloads != telemetry["retainedPayloads"]:
        raise ValueError("Telemetry payload counts disagree")
