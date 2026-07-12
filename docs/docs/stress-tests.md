---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-12 18:35 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf (3conn) | 0.77 | 1,858,785 | 1,873,799 | +0.3% | +0.05% | 1772.68 | 1,858,785 | 0 | 1.43 |
| Dekaf | 0.80 | 1,685,818 | 1,675,407 | +12.5% | +1.15% | 1607.72 | 1,685,818 | 0 | 1.35 |
| Confluent | 1.34 | 1,283,014 | 1,298,969 | +5.2% | +0.56% | 1223.58 | 1,283,014 | 0 | 1.72 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Connection Scale Timeline - Producer (Fire-and-Forget)

| Client | Event UTC | Broker | Connections | Buffer | Pressure (buffer/send) | Nearest throughput sample |
|--------|-----------|-------:|-------------|-------:|------------------------|---------------------------|
| Dekaf | 2026-07-12T18:04:15.6012751+00:00 | 1 | 1→2 | 0% | 0/100 | 1.0s / 479,310 msg/s |
| Dekaf | 2026-07-12T18:04:20.5968147+00:00 | 1 | 2→5 | 0% | 89/4,348 | 5.0s / 1,482,038 msg/s |
| Dekaf | 2026-07-12T18:04:25.5967062+00:00 | 1 | 5→6 | 1% | 64/4,927 | 10.0s / 1,515,512 msg/s |
| Dekaf (3conn) | 2026-07-12T18:19:16.5666238+00:00 | 1 | 3→4 | 0% | 0/100 | 1.0s / 421,071 msg/s |
| Dekaf (3conn) | 2026-07-12T18:19:21.5640434+00:00 | 1 | 4→6 | 1% | 160/4,819 | 6.0s / 1,560,747 msg/s |

:::tip
**Dekaf uses 1.68x less CPU per message** than Confluent.Kafka for producer (fire-and-forget); comparison throughput is 1.29x.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 1.14 | 1,079,485 | 1,104,897 | -4.2% | -0.35% | 1029.48 | 1,079,485 | 0 | 1.23 |
| Dekaf (3conn) | 1.30 | 938,180 | 982,212 | -35.6% | -3.32% | 894.72 | 938,180 | 0 | 1.22 |
| Confluent | 1.82 | 824,789 | 852,640 | +2.9% | +0.43% | 786.58 | 824,789 | 0 | 1.50 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Connection Scale Timeline - Producer (Fire-and-Forget), 3 Brokers

| Client | Event UTC | Broker | Connections | Buffer | Pressure (buffer/send) | Nearest throughput sample |
|--------|-----------|-------:|-------------|-------:|------------------------|---------------------------|
| Dekaf | 2026-07-12T18:04:20.3284286+00:00 | 1 | 1→2 | 1% | 2/100 | 2.0s / 1,072,099 msg/s |
| Dekaf | 2026-07-12T18:04:20.3918955+00:00 | 3 | 1→2 | 1% | 2/100 | 2.0s / 1,072,099 msg/s |
| Dekaf | 2026-07-12T18:04:20.4436432+00:00 | 2 | 1→2 | 2% | 3/100 | 2.0s / 1,072,099 msg/s |

:::tip
**Dekaf uses 1.60x less CPU per message** than Confluent.Kafka for producer (fire-and-forget), 3 brokers; comparison throughput is 1.30x.
:::

## Producer (Acks All) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.81 | 1,599,862 | 1,597,363 | -5.4% | -0.56% | 1525.75 | 1,599,862 | 0 | 1.30 |
| Confluent | 1.26 | 1,380,230 | 1,410,531 | -2.3% | -0.24% | 1316.29 | 1,380,230 | 0 | 1.74 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Connection Scale Timeline - Producer (Acks All)

| Client | Event UTC | Broker | Connections | Buffer | Pressure (buffer/send) | Nearest throughput sample |
|--------|-----------|-------:|-------------|-------:|------------------------|---------------------------|
| Dekaf | 2026-07-12T18:04:16.2374752+00:00 | 1 | 1→2 | 0% | 1/100 | 1.0s / 460,013 msg/s |
| Dekaf | 2026-07-12T18:04:21.2336169+00:00 | 1 | 2→5 | 1% | 81/4,873 | 5.0s / 1,405,228 msg/s |
| Dekaf | 2026-07-12T18:04:26.2316763+00:00 | 1 | 5→6 | 0% | 63/4,859 | 10.0s / 1,653,738 msg/s |

:::tip
**Dekaf uses 1.55x less CPU per message** than Confluent.Kafka for producer (acks all); comparison throughput is 1.13x.
:::

## Producer (Acks All), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 1.04 | 1,131,721 | 1,137,760 | -1.1% | -0.16% | 1079.29 | 1,131,721 | 0 | 1.18 |
| Confluent | 1.63 | 936,372 | 922,613 | -7.0% | -0.42% | 892.99 | 936,372 | 0 | 1.53 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Connection Scale Timeline - Producer (Acks All), 3 Brokers

| Client | Event UTC | Broker | Connections | Buffer | Pressure (buffer/send) | Nearest throughput sample |
|--------|-----------|-------:|-------------|-------:|------------------------|---------------------------|
| Dekaf | 2026-07-12T18:04:20.10185+00:00 | 1 | 1→2 | 2% | 18/100 | 1.0s / 544,645 msg/s |
| Dekaf | 2026-07-12T18:04:20.1084262+00:00 | 2 | 1→2 | 2% | 22/100 | 1.0s / 544,645 msg/s |
| Dekaf | 2026-07-12T18:04:20.2245123+00:00 | 3 | 1→2 | 2% | 36/100 | 2.0s / 994,166 msg/s |

## Delivery Latency Outliers - Producer (Acks All), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated owner | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|-----------------------|---------------------|-------------------|
| Confluent | 454,000 | 2026-07-12T17:49:19.3145513+00:00 | 1.1s | GC pause | - | 2.0s / 493,912 msg/s | Gen2 +0 / pause +136.3ms |
| Confluent | 464,000 | 2026-07-12T17:49:19.3261889+00:00 | 1.1s | GC pause | - | 2.0s / 493,912 msg/s | Gen2 +0 / pause +136.3ms |
| Confluent | 474,000 | 2026-07-12T17:49:19.3391228+00:00 | 1.1s | GC pause | - | 2.0s / 493,912 msg/s | Gen2 +0 / pause +136.3ms |
| Confluent | 484,000 | 2026-07-12T17:49:19.3637782+00:00 | 1.1s | GC pause | - | 2.0s / 493,912 msg/s | Gen2 +0 / pause +136.3ms |
| Confluent | 494,000 | 2026-07-12T17:49:19.3876841+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +240.3ms |
| Confluent | 504,000 | 2026-07-12T17:49:19.436555+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +240.3ms |
| Confluent | 514,000 | 2026-07-12T17:49:19.4495087+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +240.3ms |
| Confluent | 524,000 | 2026-07-12T17:49:19.460776+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +240.3ms |
| Confluent | 534,000 | 2026-07-12T17:49:19.4714238+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +240.3ms |
| Confluent | 544,000 | 2026-07-12T17:49:19.4836555+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +240.3ms |
| Confluent | 554,000 | 2026-07-12T17:49:19.4992893+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 564,000 | 2026-07-12T17:49:19.511781+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 570,000 | 2026-07-12T17:49:19.5187138+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 574,000 | 2026-07-12T17:49:19.5239117+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 580,000 | 2026-07-12T17:49:19.5310331+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 583,000 | 2026-07-12T17:49:19.5346887+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 584,000 | 2026-07-12T17:49:19.535816+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 593,000 | 2026-07-12T17:49:19.5637381+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 594,000 | 2026-07-12T17:49:19.5646491+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 600,000 | 2026-07-12T17:49:19.5777569+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 603,000 | 2026-07-12T17:49:19.5810074+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 604,000 | 2026-07-12T17:49:19.5823124+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 610,000 | 2026-07-12T17:49:19.5910238+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 613,000 | 2026-07-12T17:49:19.5949673+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 614,000 | 2026-07-12T17:49:19.5961213+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 620,000 | 2026-07-12T17:49:19.6091296+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 623,000 | 2026-07-12T17:49:19.6181228+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 624,000 | 2026-07-12T17:49:19.618769+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 630,000 | 2026-07-12T17:49:19.6247646+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 633,000 | 2026-07-12T17:49:19.6286788+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 634,000 | 2026-07-12T17:49:19.6292948+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 640,000 | 2026-07-12T17:49:19.636299+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 643,000 | 2026-07-12T17:49:19.6394116+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 644,000 | 2026-07-12T17:49:19.6408336+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 650,000 | 2026-07-12T17:49:19.6476209+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 653,000 | 2026-07-12T17:49:19.6515634+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 654,000 | 2026-07-12T17:49:19.6523428+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 660,000 | 2026-07-12T17:49:19.6590947+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 663,000 | 2026-07-12T17:49:19.6659671+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 664,000 | 2026-07-12T17:49:19.6684392+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 670,000 | 2026-07-12T17:49:19.6786983+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 673,000 | 2026-07-12T17:49:19.6862492+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 674,000 | 2026-07-12T17:49:19.6894447+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 680,000 | 2026-07-12T17:49:19.7076653+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 683,000 | 2026-07-12T17:49:19.7144793+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 684,000 | 2026-07-12T17:49:19.7191805+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 690,000 | 2026-07-12T17:49:19.7450437+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 693,000 | 2026-07-12T17:49:19.7478963+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 694,000 | 2026-07-12T17:49:19.7488027+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 700,000 | 2026-07-12T17:49:19.7566541+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 703,000 | 2026-07-12T17:49:19.7626563+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 704,000 | 2026-07-12T17:49:19.7639694+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 710,000 | 2026-07-12T17:49:19.773168+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 713,000 | 2026-07-12T17:49:19.7794758+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 714,000 | 2026-07-12T17:49:19.7837963+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 720,000 | 2026-07-12T17:49:19.7921358+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 723,000 | 2026-07-12T17:49:19.7953033+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 724,000 | 2026-07-12T17:49:19.7962501+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 730,000 | 2026-07-12T17:49:19.8141608+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 733,000 | 2026-07-12T17:49:19.8211911+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 734,000 | 2026-07-12T17:49:19.8224465+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 740,000 | 2026-07-12T17:49:19.8679705+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 743,000 | 2026-07-12T17:49:19.8713052+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 744,000 | 2026-07-12T17:49:19.8723891+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 750,000 | 2026-07-12T17:49:19.8796427+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 753,000 | 2026-07-12T17:49:19.883008+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 754,000 | 2026-07-12T17:49:19.8840634+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 760,000 | 2026-07-12T17:49:19.8913397+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 763,000 | 2026-07-12T17:49:19.8942641+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 764,000 | 2026-07-12T17:49:19.8964196+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 770,000 | 2026-07-12T17:49:19.9030513+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 773,000 | 2026-07-12T17:49:19.9068574+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 774,000 | 2026-07-12T17:49:19.9080087+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 780,000 | 2026-07-12T17:49:19.9239405+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 783,000 | 2026-07-12T17:49:19.9306694+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 784,000 | 2026-07-12T17:49:19.93145+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 790,000 | 2026-07-12T17:49:19.9513243+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 793,000 | 2026-07-12T17:49:19.9534017+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 794,000 | 2026-07-12T17:49:19.9546899+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 800,000 | 2026-07-12T17:49:19.9610186+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 803,000 | 2026-07-12T17:49:19.9659733+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 804,000 | 2026-07-12T17:49:19.9666668+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 810,000 | 2026-07-12T17:49:19.9741931+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 813,000 | 2026-07-12T17:49:19.9776614+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 814,000 | 2026-07-12T17:49:19.9789693+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 820,000 | 2026-07-12T17:49:19.9857586+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 823,000 | 2026-07-12T17:49:19.9898823+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 824,000 | 2026-07-12T17:49:19.9907556+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 830,000 | 2026-07-12T17:49:19.9983672+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 833,000 | 2026-07-12T17:49:20.0017231+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 834,000 | 2026-07-12T17:49:20.002705+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 840,000 | 2026-07-12T17:49:20.0094617+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 843,000 | 2026-07-12T17:49:20.0132728+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 844,000 | 2026-07-12T17:49:20.0140109+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 850,000 | 2026-07-12T17:49:20.0224983+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 853,000 | 2026-07-12T17:49:20.0260088+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 854,000 | 2026-07-12T17:49:20.0286076+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 860,000 | 2026-07-12T17:49:20.039755+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 863,000 | 2026-07-12T17:49:20.0423734+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 864,000 | 2026-07-12T17:49:20.0436084+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 870,000 | 2026-07-12T17:49:20.0523826+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 873,000 | 2026-07-12T17:49:20.0654007+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 874,000 | 2026-07-12T17:49:20.0661183+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 880,000 | 2026-07-12T17:49:20.0738276+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 883,000 | 2026-07-12T17:49:20.0792867+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 884,000 | 2026-07-12T17:49:20.0834291+00:00 | 1.2s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 890,000 | 2026-07-12T17:49:20.0977978+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 893,000 | 2026-07-12T17:49:20.1030921+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 894,000 | 2026-07-12T17:49:20.1044919+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 900,000 | 2026-07-12T17:49:20.1185932+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 903,000 | 2026-07-12T17:49:20.121285+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 904,000 | 2026-07-12T17:49:20.1220584+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 910,000 | 2026-07-12T17:49:20.1296326+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 913,000 | 2026-07-12T17:49:20.1334033+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 914,000 | 2026-07-12T17:49:20.1346072+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 920,000 | 2026-07-12T17:49:20.1537392+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 923,000 | 2026-07-12T17:49:20.1600439+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 924,000 | 2026-07-12T17:49:20.1608647+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 930,000 | 2026-07-12T17:49:20.1719324+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 933,000 | 2026-07-12T17:49:20.1760301+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 934,000 | 2026-07-12T17:49:20.1767785+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 940,000 | 2026-07-12T17:49:20.1829435+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 943,000 | 2026-07-12T17:49:20.1887402+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 944,000 | 2026-07-12T17:49:20.1901243+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 950,000 | 2026-07-12T17:49:20.2047991+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 953,000 | 2026-07-12T17:49:20.2073967+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 954,000 | 2026-07-12T17:49:20.2085503+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 960,000 | 2026-07-12T17:49:20.2160632+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 963,000 | 2026-07-12T17:49:20.2353454+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 964,000 | 2026-07-12T17:49:20.236117+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 970,000 | 2026-07-12T17:49:20.2440888+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 973,000 | 2026-07-12T17:49:20.2522098+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 974,000 | 2026-07-12T17:49:20.2528342+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 980,000 | 2026-07-12T17:49:20.2681339+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 983,000 | 2026-07-12T17:49:20.2727411+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 984,000 | 2026-07-12T17:49:20.2742729+00:00 | 1.1s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 994,000 | 2026-07-12T17:49:20.3466497+00:00 | 1.0s | GC pause | - | 3.0s / 520,696 msg/s | Gen2 +1 / pause +205.0ms |
| Confluent | 1,074,000 | 2026-07-12T17:49:20.5238536+00:00 | 1.0s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,104,000 | 2026-07-12T17:49:20.5576492+00:00 | 1.0s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,114,000 | 2026-07-12T17:49:20.5676359+00:00 | 1.0s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,124,000 | 2026-07-12T17:49:20.5786789+00:00 | 1.1s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,134,000 | 2026-07-12T17:49:20.5906353+00:00 | 1.1s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,144,000 | 2026-07-12T17:49:20.6043538+00:00 | 1.1s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,154,000 | 2026-07-12T17:49:20.6177507+00:00 | 1.1s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,164,000 | 2026-07-12T17:49:20.6380335+00:00 | 1.1s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,174,000 | 2026-07-12T17:49:20.6501561+00:00 | 1.1s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,184,000 | 2026-07-12T17:49:20.660048+00:00 | 1.1s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,194,000 | 2026-07-12T17:49:20.6712579+00:00 | 1.1s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,204,000 | 2026-07-12T17:49:20.6837486+00:00 | 1.1s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,214,000 | 2026-07-12T17:49:20.6975016+00:00 | 1.1s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,224,000 | 2026-07-12T17:49:20.7182028+00:00 | 1.1s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,234,000 | 2026-07-12T17:49:20.7334991+00:00 | 1.1s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,244,000 | 2026-07-12T17:49:20.7569831+00:00 | 1.1s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,254,000 | 2026-07-12T17:49:20.7889577+00:00 | 1.1s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,264,000 | 2026-07-12T17:49:20.8540476+00:00 | 1.0s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,274,000 | 2026-07-12T17:49:20.866314+00:00 | 1.0s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,284,000 | 2026-07-12T17:49:20.8776582+00:00 | 1.0s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |
| Confluent | 1,294,000 | 2026-07-12T17:49:20.8898539+00:00 | 1.0s | GC pause | - | 4.0s / 437,388 msg/s | Gen2 +1 / pause +201.8ms |

:::tip
**Dekaf uses 1.57x less CPU per message** than Confluent.Kafka for producer (acks all), 3 brokers; comparison throughput is 1.23x.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf (3conn) | 0.78 | 1,608,872 | 1,624,255 | +0.5% | +0.10% | 1534.34 | 1,608,872 | 0 | 1.26 |
| Dekaf | 0.85 | 1,426,631 | 1,432,029 | +5.3% | +0.52% | 1360.54 | 1,426,631 | 0 | 1.21 |
| Confluent | 1.38 | 1,228,465 | 1,240,341 | -8.9% | -0.91% | 1171.56 | 1,228,465 | 0 | 1.70 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Connection Scale Timeline - Producer (Fire-and-Forget, Idempotent)

| Client | Event UTC | Broker | Connections | Buffer | Pressure (buffer/send) | Nearest throughput sample |
|--------|-----------|-------:|-------------|-------:|------------------------|---------------------------|
| Dekaf | 2026-07-12T18:04:14.3881595+00:00 | 1 | 1→2 | 0% | 1/103 | 1.0s / 430,701 msg/s |
| Dekaf | 2026-07-12T18:04:19.3840935+00:00 | 1 | 2→5 | 0% | 670/3,703 | 5.0s / 1,306,512 msg/s |
| Dekaf | 2026-07-12T18:04:24.383021+00:00 | 1 | 5→6 | 0% | 25/3,927 | 10.0s / 1,496,879 msg/s |
| Dekaf (3conn) | 2026-07-12T18:19:15.0748793+00:00 | 1 | 3→4 | 0% | 1/100 | 1.0s / 492,852 msg/s |
| Dekaf (3conn) | 2026-07-12T18:19:20.0716573+00:00 | 1 | 4→6 | 2% | 82/3,883 | 5.0s / 1,410,264 msg/s |

:::tip
**Dekaf uses 1.64x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent); comparison throughput is 1.15x.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf (3conn) | 1.14 | 928,069 | 978,595 | +3.1% | +0.32% | 885.08 | 928,069 | 0 | 1.05 |
| Dekaf | 1.18 | 886,228 | 920,891 | -0.8% | -0.18% | 845.17 | 886,228 | 0 | 1.05 |
| Confluent | 2.09 | 756,468 | 791,238 | +5.6% | +0.73% | 721.42 | 756,468 | 0 | 1.58 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Connection Scale Timeline - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Event UTC | Broker | Connections | Buffer | Pressure (buffer/send) | Nearest throughput sample |
|--------|-----------|-------:|-------------|-------:|------------------------|---------------------------|
| Dekaf | 2026-07-12T18:04:22.359444+00:00 | 3 | 1→2 | 1% | 22/100 | 1.0s / 638,888 msg/s |
| Dekaf | 2026-07-12T18:04:22.5052435+00:00 | 2 | 1→2 | 1% | 39/100 | 1.0s / 638,888 msg/s |
| Dekaf | 2026-07-12T18:04:22.5985134+00:00 | 1 | 1→2 | 0% | 53/100 | 1.0s / 638,888 msg/s |

:::tip
**Dekaf uses 1.77x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers; comparison throughput is 1.16x.
:::

## Producer → Consumer Round-Trip Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 5.99 | 221,752 | - | - | - | 211.48 | 221,752 | 0 | 1.33 |
| Confluent | 6.50 | 79,734 | - | - | - | 76.04 | 79,734 | 0 | 0.52 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Connection Scale Timeline - Producer → Consumer Round-Trip

| Client | Event UTC | Broker | Connections | Buffer | Pressure (buffer/send) | Nearest throughput sample |
|--------|-----------|-------:|-------------|-------:|------------------------|---------------------------|
| Dekaf | 2026-07-12T17:49:17.7064956+00:00 | 1 | 1→2 | 0% | 5/100 | - |

### Round-Trip Validation

| Client | Expected | Consumed | Missing | Duplicates | Corrupt | Out of Order | Wrong Partition | Unexpected | Timed Out | Result |
|--------|----------|----------|---------|------------|---------|--------------|-----------------|------------|-----------|--------|
| Confluent | 250,000 | 250,000 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |
| Dekaf | 250,000 | 250,000 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |

:::tip
**Dekaf uses 1.08x less CPU per message** than Confluent.Kafka for producer → consumer round-trip; comparison throughput is 2.78x.
:::

## Producer (Transactional EOS), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 400.21 | 334 | 445 | +1.3% | +0.08% | 0.32 | 445 | 0 | 0.18 |
| Confluent | 263.45 | 129 | 173 | +1.0% | +0.13% | 0.12 | 173 | 0 | 0.05 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 155,300 | 116,500 | 38,800 | 116,500 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 400,400 | 300,300 | 100,100 | 300,300 | 0 | 0 | 0 | 0 | 0 | PASS |

:::note
Confluent.Kafka uses 1.52x less CPU per message for producer (transactional eos), 3 brokers; comparison throughput is 2.57x.
:::

## Consumer Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.50 | 2,830,359 | 2,820,230 | -0.5% | -0.07% | 2699.24 | - | 0 | 1.43 |
| Confluent | 0.94 | 1,048,877 | 1,078,266 | +4.4% | +0.50% | 1000.29 | - | 0 | 0.98 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

:::tip
**Dekaf uses 1.85x less CPU per message** than Confluent.Kafka for consumer; comparison throughput is 2.62x.
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.47 | 3,069,731 | 3,093,526 | -3.0% | -0.27% | 2927.52 | - | 0 | 1.44 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.38 | 4,011,911 | 3,996,262 | +0.5% | +0.12% | 3826.06 | - | 0 | 1.54 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.33 | 4,174,716 | 4,097,908 | +1.5% | +0.24% | 3981.32 | - | 0 | 1.40 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 455832 | 1 | 1 | 2145.27 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 277121 | 1 | 1 | 1385.70 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 182642 | 0 | 0 | 890.78 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 304377 | 1 | 1 | 1490.65 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 214368 | 1 | 1 | 1011.34 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 262455 | 1 | 1 | 1326.80 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 166734 | 0 | 0 | 817.03 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip | 95 | 17 | 3 | 787.51 MB | 3.23 KB |
| Confluent | Producer (Transactional EOS), 3 Brokers | 88 | 1 | 1 | 231.71 MB | 1.53 KB |
| Dekaf | Consumer | 1844 | 1802 | 1167 | 39.67 GB | 17 B |
| Dekaf | Consumer (Batch) | 1960 | 1915 | 1231 | 1098.56 GB | 427 B |
| Dekaf | Consumer (Raw Bytes) | 1579 | 1429 | 462 | 360.22 GB | 107 B |
| Dekaf | Consumer (Raw Batch) | 1974 | 1337 | 91 | 92.13 GB | 26 B |
| Dekaf | Producer (Fire-and-Forget) | 790 | 2 | 2 | 147.20 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 274 | 3 | 3 | 305.10 MB | 0 B |
| Dekaf | Producer (Acks All) | 251 | 2 | 2 | 334.17 MB | 0 B |
| Dekaf | Producer (Acks All), 3 Brokers | 303 | 2 | 2 | 176.91 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 611 | 2 | 2 | 180.54 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 182 | 3 | 3 | 250.15 MB | 0 B |
| Dekaf | Producer → Consumer Round-Trip | 101 | 10 | 9 | 726.84 MB | 2.98 KB |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 175 | 1 | 1 | 318.82 MB | 835 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 832 | 3 | 2 | 2.24 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 298 | 3 | 3 | 1.14 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 711 | 5 | 3 | 1.98 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 446 | 3 | 3 | 1.17 GB | 1 B |

*Confluent.Kafka uses native librdkafka; .NET GC allocation counters exclude unmanaged allocations.*

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **CPU Isolation**: Brokers are pinned to dedicated cores and the client under test to its own cores, so the client — not the broker — is the measured bottleneck
- **RAM-backed Broker Logs**: Kafka log dirs are mounted on tmpfs so disk I/O never caps broker ingestion
- **Delivered Throughput**: producer tables report broker-confirmed throughput, measured as the end-offset delta across all partitions — not the client-side append rate, which can run far ahead of what the broker ever accepts
- **Median Interval Throughput**: table order and comparison ratios use median sampled client-side msg/s when available, which is less sensitive to short late-run stalls than the whole-run mean
- **Same-VM Pairing**: comparable Dekaf and Confluent scenarios run sequentially inside one job/VM, with client order alternating by workflow run number to reduce order bias
- **Backpressure Parity**: both producers are bounded to the same 512 MB local buffer (Dekaf BufferMemory, librdkafka queue.buffering.max) and block on a full buffer, so neither client can absorb an unbounded backlog into RAM
- **Consumer Loop Replay**: Consumer tests re-read a pre-seeded topic (seek to beginning when drained) instead of racing a live feeder, so the consumer itself is measured; table headings report the 16KB seed batch size because it amplifies per-batch costs relative to well-batched workloads
- **Delivery Latency Sampling**: 1 in 1000 produced messages is awaited end-to-end to record true broker round-trip latency
- **Round-Trip Correctness**: Bounded sequenced payloads are consumed back and checked for corruption, wrong partitions, gaps, duplicates, and reordering
- **Round-Trip CPU Scope**: CPU time covers both bulk production and consumer validation; it is not a producer-only metric
- **CPU Efficiency**: CPU time per message differentiates client efficiency even at equal throughput
- **Noise-Aware Trends**: each scenario is compared with its last 10 matching runs using a median ± 2×MAD band; one adverse excursion warns and two consecutive regressions fail the workflow
- **Parallel Execution**: Each scenario runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
