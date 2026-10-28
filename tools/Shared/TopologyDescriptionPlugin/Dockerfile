ARG KAFKA_IMAGE=apache/kafka:4.4.0
FROM ${KAFKA_IMAGE} AS kafka
FROM eclipse-temurin:27-jdk AS compiler
COPY --from=kafka /opt/kafka/libs /kafka-libs
COPY FailingDeleteTopologyDescriptionPlugin.java /source/FailingDeleteTopologyDescriptionPlugin.java
RUN mkdir /classes && javac -proc:none --release 17 -cp '/kafka-libs/*' -d /classes /source/FailingDeleteTopologyDescriptionPlugin.java && jar cf /plugin.jar -C /classes .
FROM kafka
COPY --from=compiler /plugin.jar /opt/kafka/libs/dekaf-test-topology-description-plugin.jar
