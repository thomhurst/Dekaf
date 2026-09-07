ARG KAFKA_IMAGE=apache/kafka:4.3.1
FROM ${KAFKA_IMAGE} AS kafka
FROM eclipse-temurin:21-jdk AS compiler
COPY --from=kafka /opt/kafka/libs /kafka-libs
COPY RecordingTelemetryReporter.java /source/RecordingTelemetryReporter.java
RUN mkdir /classes && javac -proc:none --release 17 -cp '/kafka-libs/*' -d /classes /source/RecordingTelemetryReporter.java && jar cf /receiver.jar -C /classes .
FROM kafka
COPY --from=compiler /receiver.jar /opt/kafka/libs/dekaf-test-telemetry-receiver.jar
