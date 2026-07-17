---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-17 00:23 UTC

:::info
The paired Dekaf vs Confluent comparison runs weekly (Sunday 2 AM UTC) and updates this page. 
Manual dispatches stay Dekaf-only unless full_run explicitly requests the same paired publish path. 
Tests measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,567,465 | 1,547,289–1,587,905 | 0.91 | 1.11x |
| Confluent | 2 | 1,417,136 | 1,402,303–1,432,126 | 1.24 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.77 | 723.50 | 1,678,194 | 1,684,415 | +0.1% | +0.02% | 1600.45 | 1,678,194 | 0 | 1.29 |
| Dekaf (dekaf-first) | 0.90 | 924.34 | 1,575,903 | 1,587,905 | -0.6% | -0.08% | 1502.90 | 1,575,903 | 0 | 1.42 |
| Dekaf (confluent-first) | 0.92 | 940.22 | 1,535,742 | 1,547,289 | -0.2% | -0.01% | 1464.60 | 1,535,742 | 0 | 1.41 |
| Confluent (dekaf-first) | 1.22 | - | 1,418,483 | 1,432,126 | -0.3% | -0.05% | 1352.77 | 1,418,483 | 0 | 1.74 |
| Confluent (confluent-first) | 1.26 | - | 1,397,045 | 1,402,303 | -0.1% | +0.05% | 1332.33 | 1,397,045 | 0 | 1.76 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,350,374 | 1500.40 | 1017.51 KB |
| Dekaf | 1 | 1,385,085 | 1538.97 | 1017.94 KB |
| Dekaf (3conn) | 1 | 1,602,590 | 1780.64 | 936.90 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-16T23:07:22.5458788+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 834,621 msg/s |
| Dekaf | 2026-07-16T23:07:49.5598374+00:00 | 1 | 16.0 MiB / 13.6 MiB | 1665.3 MB/s | 0/0 | 38,164 | 27.0s / 1,559,521 msg/s |
| Dekaf | 2026-07-16T23:08:17.5709528+00:00 | 1 | 14.0 MiB / 13.9 MiB | 1719.5 MB/s | 1/0 | 99,978 | 55.0s / 1,606,134 msg/s |
| Dekaf | 2026-07-16T23:08:44.5792737+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1719.5 MB/s | 1/0 | 176,737 | 82.0s / 1,625,664 msg/s |
| Dekaf | 2026-07-16T23:09:11.5894349+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1722.2 MB/s | 2/0 | 257,706 | 109.0s / 1,622,582 msg/s |
| Dekaf | 2026-07-16T23:09:38.5978517+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1722.2 MB/s | 2/1 | 335,746 | 136.0s / 1,599,472 msg/s |
| Dekaf | 2026-07-16T23:10:06.6051333+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1728.4 MB/s | 2/1 | 424,473 | 164.1s / 1,631,803 msg/s |
| Dekaf | 2026-07-16T23:10:33.6240728+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1728.4 MB/s | 2/2 | 504,171 | 191.1s / 1,565,528 msg/s |
| Dekaf | 2026-07-16T23:11:00.6321235+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1728.4 MB/s | 2/2 | 582,107 | 218.1s / 1,571,726 msg/s |
| Dekaf | 2026-07-16T23:11:27.6423208+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1731.0 MB/s | 2/3 | 663,928 | 245.1s / 1,615,227 msg/s |
| Dekaf | 2026-07-16T23:11:55.6489515+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1731.0 MB/s | 3/3 | 748,591 | 273.1s / 1,595,918 msg/s |
| Dekaf | 2026-07-16T23:12:22.654514+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1731.0 MB/s | 3/3 | 828,770 | 300.1s / 1,621,588 msg/s |
| Dekaf | 2026-07-16T23:12:49.6640896+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1731.0 MB/s | 4/3 | 903,072 | 327.1s / 1,580,772 msg/s |
| Dekaf | 2026-07-16T23:13:16.6780132+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1731.0 MB/s | 4/3 | 974,679 | 354.1s / 1,537,769 msg/s |
| Dekaf | 2026-07-16T23:13:44.691713+00:00 | 1 | 15.0 MiB / 13.9 MiB | 1731.0 MB/s | 5/3 | 1,037,255 | 382.1s / 1,616,419 msg/s |
| Dekaf | 2026-07-16T23:14:11.7051508+00:00 | 1 | 15.0 MiB / 13.5 MiB | 1731.0 MB/s | 5/4 | 1,090,428 | 409.1s / 1,536,509 msg/s |
| Dekaf | 2026-07-16T23:14:38.7143311+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1731.0 MB/s | 5/4 | 1,152,835 | 436.1s / 1,593,205 msg/s |
| Dekaf | 2026-07-16T23:15:05.7311943+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1731.0 MB/s | 6/4 | 1,219,275 | 463.1s / 1,572,172 msg/s |
| Dekaf | 2026-07-16T23:15:33.7399457+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1731.0 MB/s | 6/4 | 1,300,106 | 491.1s / 1,595,060 msg/s |
| Dekaf | 2026-07-16T23:16:00.7451529+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1731.0 MB/s | 7/4 | 1,382,312 | 518.1s / 1,600,863 msg/s |
| Dekaf | 2026-07-16T23:16:27.7490261+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1731.0 MB/s | 7/5 | 1,450,063 | 545.1s / 1,568,099 msg/s |
| Dekaf | 2026-07-16T23:16:55.7558661+00:00 | 1 | 12.0 MiB / 10.8 MiB | 1731.0 MB/s | 7/5 | 1,537,353 | 573.2s / 1,625,180 msg/s |
| Dekaf | 2026-07-16T23:17:22.7619437+00:00 | 1 | 11.0 MiB / 10.7 MiB | 1731.0 MB/s | 7/6 | 1,621,279 | 600.2s / 1,600,809 msg/s |
| Dekaf | 2026-07-16T23:17:49.7685945+00:00 | 1 | 9.0 MiB / 1.7 MiB | 1731.0 MB/s | 7/6 | 1,697,018 | 627.2s / 1,500,655 msg/s |
| Dekaf | 2026-07-16T23:18:16.775848+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1743.5 MB/s | 7/7 | 1,776,717 | 654.2s / 1,596,822 msg/s |
| Dekaf | 2026-07-16T23:18:44.7809668+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1743.5 MB/s | 8/7 | 1,860,666 | 682.2s / 1,608,531 msg/s |
| Dekaf | 2026-07-16T23:19:11.7925088+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1743.5 MB/s | 8/7 | 1,941,312 | 709.2s / 1,579,818 msg/s |
| Dekaf | 2026-07-16T23:19:38.8027954+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1743.5 MB/s | 8/8 | 2,022,662 | 736.2s / 1,596,544 msg/s |
| Dekaf | 2026-07-16T23:20:05.8161191+00:00 | 1 | 10.0 MiB / 10.0 MiB | 1743.5 MB/s | 8/8 | 2,101,984 | 763.2s / 1,491,837 msg/s |
| Dekaf | 2026-07-16T23:20:33.8299729+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1743.5 MB/s | 8/9 | 2,184,873 | 791.2s / 1,574,361 msg/s |
| Dekaf | 2026-07-16T23:21:00.8413958+00:00 | 1 | 12.0 MiB / 7.1 MiB | 1743.5 MB/s | 8/10 | 2,265,789 | 818.2s / 1,581,876 msg/s |
| Dekaf | 2026-07-16T23:21:27.8512143+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1743.5 MB/s | 8/10 | 2,344,158 | 845.2s / 1,544,024 msg/s |
| Dekaf | 2026-07-16T23:21:54.8644917+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1743.5 MB/s | 8/11 | 2,424,411 | 872.2s / 1,564,758 msg/s |
| Dekaf | 2026-07-16T23:52:23.7933862+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 485,935 msg/s |
| Dekaf | 2026-07-16T23:52:50.8145292+00:00 | 1 | 16.0 MiB / 14.8 MiB | 1705.3 MB/s | 0/0 | 41,246 | 27.0s / 1,477,868 msg/s |
| Dekaf | 2026-07-16T23:53:17.8227769+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1705.3 MB/s | 1/0 | 101,845 | 54.0s / 1,531,312 msg/s |
| Dekaf | 2026-07-16T23:53:44.8333144+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1705.3 MB/s | 1/0 | 172,117 | 81.0s / 1,557,566 msg/s |
| Dekaf | 2026-07-16T23:54:12.8440554+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1705.3 MB/s | 2/0 | 253,615 | 109.1s / 1,564,088 msg/s |
| Dekaf | 2026-07-16T23:54:39.8489431+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1705.3 MB/s | 2/1 | 327,878 | 136.1s / 1,546,169 msg/s |
| Dekaf | 2026-07-16T23:55:06.8598668+00:00 | 1 | 12.0 MiB / 11.3 MiB | 1705.3 MB/s | 2/1 | 406,291 | 163.1s / 1,554,011 msg/s |
| Dekaf | 2026-07-16T23:55:34.8686799+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1705.3 MB/s | 2/2 | 491,926 | 191.1s / 1,561,406 msg/s |
| Dekaf | 2026-07-16T23:56:01.875377+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1705.3 MB/s | 2/2 | 570,201 | 218.1s / 1,532,889 msg/s |
| Dekaf | 2026-07-16T23:56:28.8853468+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1705.3 MB/s | 2/3 | 644,773 | 245.1s / 1,561,685 msg/s |
| Dekaf | 2026-07-16T23:56:55.8946604+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1705.3 MB/s | 2/4 | 724,851 | 272.1s / 1,532,406 msg/s |
| Dekaf | 2026-07-16T23:57:23.9009642+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1705.3 MB/s | 2/4 | 804,215 | 300.1s / 1,521,348 msg/s |
| Dekaf | 2026-07-16T23:57:50.9120807+00:00 | 1 | 12.0 MiB / 9.8 MiB | 1705.3 MB/s | 2/5 | 874,593 | 327.1s / 1,535,700 msg/s |
| Dekaf | 2026-07-16T23:58:17.9190021+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1705.3 MB/s | 2/5 | 955,487 | 354.1s / 1,553,385 msg/s |
| Dekaf | 2026-07-16T23:58:44.9261167+00:00 | 1 | 13.0 MiB / 12.5 MiB | 1705.3 MB/s | 3/5 | 1,029,656 | 381.1s / 1,565,258 msg/s |
| Dekaf | 2026-07-16T23:59:12.9369714+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1705.3 MB/s | 3/6 | 1,108,169 | 409.1s / 1,557,508 msg/s |
| Dekaf | 2026-07-16T23:59:39.9469437+00:00 | 1 | 11.0 MiB / 10.7 MiB | 1705.3 MB/s | 3/6 | 1,184,460 | 436.1s / 1,502,438 msg/s |
| Dekaf | 2026-07-17T00:00:06.9568861+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1705.3 MB/s | 4/6 | 1,262,664 | 463.1s / 1,555,718 msg/s |
| Dekaf | 2026-07-17T00:00:33.9664409+00:00 | 1 | 11.0 MiB / 10.3 MiB | 1705.3 MB/s | 4/6 | 1,339,623 | 490.2s / 1,567,525 msg/s |
| Dekaf | 2026-07-17T00:01:01.9772826+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1705.3 MB/s | 4/7 | 1,419,984 | 518.2s / 1,576,201 msg/s |
| Dekaf | 2026-07-17T00:01:28.9819658+00:00 | 1 | 11.0 MiB / 10.0 MiB | 1714.3 MB/s | 4/8 | 1,501,473 | 545.2s / 1,516,554 msg/s |
| Dekaf | 2026-07-17T00:01:55.9883606+00:00 | 1 | 9.0 MiB / 9.0 MiB | 1714.3 MB/s | 4/8 | 1,584,506 | 572.2s / 1,478,342 msg/s |
| Dekaf | 2026-07-17T00:02:22.9938494+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1714.3 MB/s | 4/9 | 1,656,949 | 599.2s / 1,551,894 msg/s |
| Dekaf | 2026-07-17T00:02:51.0016107+00:00 | 1 | 12.0 MiB / 10.2 MiB | 1714.3 MB/s | 4/9 | 1,743,033 | 627.2s / 1,556,665 msg/s |
| Dekaf | 2026-07-17T00:03:18.0126727+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1714.3 MB/s | 5/9 | 1,823,519 | 654.2s / 1,553,859 msg/s |
| Dekaf | 2026-07-17T00:03:45.0250095+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1714.3 MB/s | 5/10 | 1,903,302 | 681.2s / 1,567,030 msg/s |
| Dekaf | 2026-07-17T00:04:12.0305972+00:00 | 1 | 10.0 MiB / 8.0 MiB | 1714.3 MB/s | 5/10 | 1,982,308 | 708.2s / 1,524,666 msg/s |
| Dekaf | 2026-07-17T00:04:40.036994+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1714.3 MB/s | 5/11 | 2,061,557 | 736.2s / 1,534,431 msg/s |
| Dekaf | 2026-07-17T00:05:07.044016+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1714.3 MB/s | 5/11 | 2,136,475 | 763.2s / 1,565,671 msg/s |
| Dekaf | 2026-07-17T00:05:34.0541159+00:00 | 1 | 13.0 MiB / 11.4 MiB | 1714.3 MB/s | 6/11 | 2,211,048 | 790.2s / 1,546,389 msg/s |
| Dekaf | 2026-07-17T00:06:02.0639618+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1714.3 MB/s | 6/12 | 2,284,330 | 818.3s / 1,558,087 msg/s |
| Dekaf | 2026-07-17T00:06:29.0817098+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1714.3 MB/s | 6/12 | 2,360,644 | 845.3s / 1,541,370 msg/s |
| Dekaf | 2026-07-17T00:06:56.0896596+00:00 | 1 | 11.0 MiB / 10.7 MiB | 1714.3 MB/s | 7/12 | 2,443,387 | 872.3s / 1,545,181 msg/s |
| Dekaf | 2026-07-17T00:07:23.0961899+00:00 | 1 | 9.0 MiB / 8.1 MiB | 1714.3 MB/s | 7/12 | 2,521,377 | 899.3s / 1,477,472 msg/s |
| Dekaf (3conn) | 2026-07-17T00:07:51.622093+00:00 | 1 | 48.0 MiB / 17.4 MiB | 1979.2 MB/s | 0/0 | 93 | 27.0s / 1,723,012 msg/s |
| Dekaf (3conn) | 2026-07-17T00:08:18.635968+00:00 | 1 | 42.0 MiB / 11.4 MiB | 2021.1 MB/s | 1/0 | 218 | 54.0s / 1,610,455 msg/s |
| Dekaf (3conn) | 2026-07-17T00:08:45.6429983+00:00 | 1 | 42.0 MiB / 16.0 MiB | 2065.3 MB/s | 1/0 | 388 | 81.0s / 1,862,752 msg/s |
| Dekaf (3conn) | 2026-07-17T00:09:12.6573148+00:00 | 1 | 42.0 MiB / 1.6 MiB | 2065.3 MB/s | 1/1 | 635 | 108.1s / 1,488,427 msg/s |
| Dekaf (3conn) | 2026-07-17T00:09:40.6674814+00:00 | 1 | 45.0 MiB / 8.2 MiB | 2065.3 MB/s | 2/1 | 845 | 136.1s / 1,731,737 msg/s |
| Dekaf (3conn) | 2026-07-17T00:10:07.674907+00:00 | 1 | 45.0 MiB / 2.0 MiB | 2065.3 MB/s | 2/1 | 961 | 163.1s / 1,539,530 msg/s |
| Dekaf (3conn) | 2026-07-17T00:10:34.6809526+00:00 | 1 | 45.0 MiB / 3.7 MiB | 2065.3 MB/s | 2/2 | 1,062 | 190.1s / 1,397,578 msg/s |
| Dekaf (3conn) | 2026-07-17T00:11:01.6950666+00:00 | 1 | 45.0 MiB / 3.8 MiB | 2065.3 MB/s | 2/2 | 1,147 | 217.1s / 1,676,985 msg/s |
| Dekaf (3conn) | 2026-07-17T00:11:29.7038077+00:00 | 1 | 39.0 MiB / 3.4 MiB | 2065.3 MB/s | 3/2 | 1,307 | 245.1s / 1,693,719 msg/s |
| Dekaf (3conn) | 2026-07-17T00:11:56.7085279+00:00 | 1 | 33.0 MiB / 7.1 MiB | 2065.3 MB/s | 4/2 | 1,436 | 272.1s / 1,575,488 msg/s |
| Dekaf (3conn) | 2026-07-17T00:12:23.716573+00:00 | 1 | 33.0 MiB / 10.3 MiB | 2065.3 MB/s | 4/2 | 1,829 | 299.1s / 1,541,194 msg/s |
| Dekaf (3conn) | 2026-07-17T00:12:50.7209566+00:00 | 1 | 33.0 MiB / 1.6 MiB | 2065.3 MB/s | 4/3 | 2,443 | 326.1s / 1,651,727 msg/s |
| Dekaf (3conn) | 2026-07-17T00:13:18.7267959+00:00 | 1 | 33.0 MiB / 1.9 MiB | 2065.3 MB/s | 4/3 | 2,723 | 354.2s / 1,801,198 msg/s |
| Dekaf (3conn) | 2026-07-17T00:13:45.7439427+00:00 | 1 | 33.0 MiB / 12.4 MiB | 2065.3 MB/s | 4/4 | 2,966 | 381.2s / 1,646,500 msg/s |
| Dekaf (3conn) | 2026-07-17T00:14:12.7519642+00:00 | 1 | 33.0 MiB / 7.0 MiB | 2123.1 MB/s | 4/5 | 3,412 | 408.2s / 1,615,882 msg/s |
| Dekaf (3conn) | 2026-07-17T00:14:40.7593608+00:00 | 1 | 36.0 MiB / 7.7 MiB | 2123.1 MB/s | 4/5 | 3,725 | 436.2s / 1,534,225 msg/s |
| Dekaf (3conn) | 2026-07-17T00:15:07.7765522+00:00 | 1 | 36.0 MiB / 6.7 MiB | 2123.1 MB/s | 5/5 | 4,125 | 463.2s / 1,666,959 msg/s |
| Dekaf (3conn) | 2026-07-17T00:15:34.7877886+00:00 | 1 | 36.0 MiB / 5.9 MiB | 2160.4 MB/s | 5/5 | 4,476 | 490.2s / 1,339,088 msg/s |
| Dekaf (3conn) | 2026-07-17T00:16:01.8066179+00:00 | 1 | 39.0 MiB / 21.1 MiB | 2160.4 MB/s | 6/5 | 4,700 | 517.3s / 1,922,346 msg/s |
| Dekaf (3conn) | 2026-07-17T00:16:29.8239536+00:00 | 1 | 39.0 MiB / 5.4 MiB | 2160.4 MB/s | 6/6 | 5,014 | 545.3s / 1,826,918 msg/s |
| Dekaf (3conn) | 2026-07-17T00:16:56.8459425+00:00 | 1 | 33.0 MiB / 12.4 MiB | 2160.4 MB/s | 6/6 | 5,281 | 572.3s / 1,706,753 msg/s |
| Dekaf (3conn) | 2026-07-17T00:17:23.8573898+00:00 | 1 | 33.0 MiB / 6.8 MiB | 2381.7 MB/s | 7/6 | 5,666 | 599.3s / 1,927,581 msg/s |
| Dekaf (3conn) | 2026-07-17T00:17:50.8749642+00:00 | 1 | 33.0 MiB / 4.8 MiB | 2381.7 MB/s | 7/6 | 6,354 | 626.3s / 1,772,477 msg/s |
| Dekaf (3conn) | 2026-07-17T00:18:18.8889442+00:00 | 1 | 27.0 MiB / 13.0 MiB | 2381.7 MB/s | 8/6 | 7,044 | 654.3s / 1,721,339 msg/s |
| Dekaf (3conn) | 2026-07-17T00:18:45.8986624+00:00 | 1 | 24.0 MiB / 5.0 MiB | 2381.7 MB/s | 9/6 | 7,827 | 681.4s / 1,811,531 msg/s |
| Dekaf (3conn) | 2026-07-17T00:19:12.9068782+00:00 | 1 | 27.0 MiB / 1.8 MiB | 2381.7 MB/s | 9/6 | 8,619 | 708.4s / 1,653,255 msg/s |
| Dekaf (3conn) | 2026-07-17T00:19:39.916477+00:00 | 1 | 27.0 MiB / 9.7 MiB | 2381.7 MB/s | 10/6 | 9,222 | 735.4s / 1,628,575 msg/s |
| Dekaf (3conn) | 2026-07-17T00:20:07.9299587+00:00 | 1 | 30.0 MiB / 6.2 MiB | 2381.7 MB/s | 10/6 | 9,788 | 763.4s / 1,533,970 msg/s |
| Dekaf (3conn) | 2026-07-17T00:20:34.9421849+00:00 | 1 | 27.0 MiB / 7.6 MiB | 2381.7 MB/s | 10/7 | 10,377 | 790.4s / 1,712,735 msg/s |
| Dekaf (3conn) | 2026-07-17T00:21:01.9481716+00:00 | 1 | 24.0 MiB / 11.1 MiB | 2381.7 MB/s | 11/7 | 11,090 | 817.4s / 1,452,666 msg/s |
| Dekaf (3conn) | 2026-07-17T00:21:28.9625489+00:00 | 1 | 27.0 MiB / 0.9 MiB | 2381.7 MB/s | 11/7 | 11,870 | 844.4s / 1,553,300 msg/s |
| Dekaf (3conn) | 2026-07-17T00:21:56.9699633+00:00 | 1 | 27.0 MiB / 3.1 MiB | 2381.7 MB/s | 12/7 | 12,423 | 872.4s / 1,709,869 msg/s |
| Dekaf (3conn) | 2026-07-17T00:22:23.9818968+00:00 | 1 | 30.0 MiB / 5.2 MiB | 2381.7 MB/s | 12/7 | 12,953 | 899.4s / 1,571,037 msg/s |
*2,600 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-16T23:07:52.6657463+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.8 MiB |
| Dekaf | 2026-07-16T23:08:07.6785992+00:00 | 1 | capacity | succeeded | 15,012ms | 14.0 MiB / 14.0 MiB |
| Dekaf | 2026-07-16T23:08:37.7034681+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-16T23:09:22.7412528+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:09:37.7503666+00:00 | 1 | capacity | failed | 15,008ms | 12.0 MiB / 9.5 MiB |
| Dekaf | 2026-07-16T23:10:07.7703669+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:10:22.7810522+00:00 | 1 | capacity | failed | 15,010ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-16T23:10:52.8111584+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:11:07.8222135+00:00 | 1 | capacity | failed | 15,010ms | 12.0 MiB / 8.6 MiB |
| Dekaf | 2026-07-16T23:11:52.8608925+00:00 | 1 | capacity | succeeded | 15,015ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-16T23:12:22.8790629+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-16T23:12:37.8941661+00:00 | 1 | capacity | succeeded | 15,015ms | 14.0 MiB / 13.0 MiB |
| Dekaf | 2026-07-16T23:13:07.9384874+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.6 MiB |
| Dekaf | 2026-07-16T23:13:22.9530676+00:00 | 1 | capacity | succeeded | 15,014ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-16T23:13:52.9856776+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-16T23:14:38.0311014+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 13.7 MiB |
| Dekaf | 2026-07-16T23:14:53.0490543+00:00 | 1 | capacity | succeeded | 15,017ms | 13.0 MiB / 9.9 MiB |
| Dekaf | 2026-07-16T23:15:23.0781858+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.9 MiB |
| Dekaf | 2026-07-16T23:15:38.1000373+00:00 | 1 | capacity | succeeded | 15,022ms | 11.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-16T23:16:08.1242567+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.5 MiB |
| Dekaf | 2026-07-16T23:16:23.1365929+00:00 | 1 | capacity | failed | 15,012ms | 11.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-16T23:17:08.1740247+00:00 | 1 | capacity | failed | 15,015ms | 11.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:17:38.1956282+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-16T23:17:53.2068181+00:00 | 1 | capacity | failed | 15,011ms | 11.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-16T23:18:23.2247812+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-16T23:18:38.241273+00:00 | 1 | capacity | succeeded | 15,016ms | 12.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-16T23:19:08.2636272+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.6 MiB |
| Dekaf | 2026-07-16T23:19:53.2987669+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:20:08.3119808+00:00 | 1 | capacity | failed | 15,013ms | 12.0 MiB / 9.7 MiB |
| Dekaf | 2026-07-16T23:20:38.3397043+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:20:53.3491+00:00 | 1 | capacity | failed | 15,009ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-16T23:21:23.3710692+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:21:38.380733+00:00 | 1 | capacity | failed | 15,009ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-16T23:52:53.9291939+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.9 MiB |
| Dekaf | 2026-07-16T23:53:08.948474+00:00 | 1 | capacity | succeeded | 15,019ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-16T23:53:38.9717412+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-16T23:53:53.9843563+00:00 | 1 | capacity | succeeded | 15,012ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:54:24.0090592+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-16T23:55:09.043908+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:55:24.0539202+00:00 | 1 | capacity | failed | 15,010ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-16T23:55:54.0759785+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:56:09.0864661+00:00 | 1 | capacity | failed | 15,010ms | 12.0 MiB / 1.4 MiB |
| Dekaf | 2026-07-16T23:56:39.1107821+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:56:54.1199225+00:00 | 1 | capacity | failed | 15,009ms | 12.0 MiB / 12.0 MiB |
| Dekaf | 2026-07-16T23:57:39.1509826+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 4.5 MiB |
| Dekaf | 2026-07-16T23:58:09.1747255+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:58:24.1848833+00:00 | 1 | capacity | succeeded | 15,010ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-16T23:58:54.2121203+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.2 MiB |
| Dekaf | 2026-07-16T23:59:09.2228519+00:00 | 1 | capacity | failed | 15,010ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-16T23:59:39.2442984+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T00:00:24.2788325+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T00:00:39.2919486+00:00 | 1 | capacity | failed | 15,013ms | 11.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-17T00:01:09.3131262+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T00:01:24.3237803+00:00 | 1 | capacity | failed | 15,010ms | 11.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T00:01:54.3474596+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 11.0 MiB |
| Dekaf | 2026-07-17T00:02:09.3593855+00:00 | 1 | capacity | failed | 15,012ms | 11.0 MiB / 6.4 MiB |
| Dekaf | 2026-07-17T00:02:54.3924324+00:00 | 1 | capacity | succeeded | 15,011ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T00:03:24.4420527+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T00:03:39.4544254+00:00 | 1 | capacity | failed | 15,012ms | 12.0 MiB / 11.9 MiB |
| Dekaf | 2026-07-17T00:04:09.4788613+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 12.0 MiB |
| Dekaf | 2026-07-17T00:04:24.4897206+00:00 | 1 | capacity | failed | 15,012ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-17T00:04:54.5164801+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 10.7 MiB |
| Dekaf | 2026-07-17T00:05:39.5554292+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T00:05:54.5725349+00:00 | 1 | capacity | failed | 15,017ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T00:06:24.5969459+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 11.5 MiB |
| Dekaf | 2026-07-17T00:06:39.6099094+00:00 | 1 | capacity | succeeded | 15,012ms | 11.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T00:07:09.633155+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.2 MiB |
| Dekaf (3conn) | 2026-07-17T00:08:09.7618766+00:00 | 1 | capacity | succeeded | 15,026ms | 42.0 MiB / 4.9 MiB |
| Dekaf (3conn) | 2026-07-17T00:08:39.8069574+00:00 | 1 | capacity | started | 0ms | 36.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-07-17T00:08:54.837503+00:00 | 1 | capacity | failed | 15,030ms | 42.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-17T00:09:24.8861769+00:00 | 1 | capacity | started | 0ms | 45.0 MiB / 7.6 MiB |
| Dekaf (3conn) | 2026-07-17T00:09:39.9039826+00:00 | 1 | capacity | succeeded | 15,017ms | 45.0 MiB / 1.6 MiB |
| Dekaf (3conn) | 2026-07-17T00:10:09.9679713+00:00 | 1 | capacity | started | 0ms | 48.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-17T00:10:55.0390968+00:00 | 1 | capacity | started | 0ms | 39.0 MiB / 6.5 MiB |
| Dekaf (3conn) | 2026-07-17T00:11:10.0646156+00:00 | 1 | capacity | succeeded | 15,025ms | 39.0 MiB / 7.1 MiB |
| Dekaf (3conn) | 2026-07-17T00:11:40.1073484+00:00 | 1 | capacity | started | 0ms | 33.0 MiB / 10.6 MiB |
| Dekaf (3conn) | 2026-07-17T00:11:55.139358+00:00 | 1 | capacity | succeeded | 15,032ms | 33.0 MiB / 4.1 MiB |
| Dekaf (3conn) | 2026-07-17T00:12:25.1690519+00:00 | 1 | capacity | started | 0ms | 27.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-07-17T00:12:40.1889623+00:00 | 1 | capacity | failed | 15,019ms | 33.0 MiB / 3.5 MiB |
| Dekaf (3conn) | 2026-07-17T00:13:25.247372+00:00 | 1 | capacity | failed | 15,021ms | 33.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-17T00:13:55.2805721+00:00 | 1 | capacity | started | 0ms | 27.0 MiB / 10.1 MiB |
| Dekaf (3conn) | 2026-07-17T00:14:10.3119593+00:00 | 1 | capacity | failed | 15,031ms | 33.0 MiB / 9.4 MiB |
| Dekaf (3conn) | 2026-07-17T00:14:40.3870257+00:00 | 1 | capacity | started | 0ms | 36.0 MiB / 9.4 MiB |
| Dekaf (3conn) | 2026-07-17T00:14:55.4163694+00:00 | 1 | capacity | succeeded | 15,029ms | 36.0 MiB / 21.9 MiB |
| Dekaf (3conn) | 2026-07-17T00:15:25.4929684+00:00 | 1 | capacity | started | 0ms | 39.0 MiB / 10.2 MiB |
| Dekaf (3conn) | 2026-07-17T00:16:10.5680872+00:00 | 1 | capacity | started | 0ms | 42.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-17T00:16:25.6066204+00:00 | 1 | capacity | failed | 15,038ms | 39.0 MiB / 6.7 MiB |
| Dekaf (3conn) | 2026-07-17T00:16:55.6720141+00:00 | 1 | capacity | started | 0ms | 33.0 MiB / 6.8 MiB |
| Dekaf (3conn) | 2026-07-17T00:17:10.6859789+00:00 | 1 | capacity | succeeded | 15,014ms | 33.0 MiB / 4.5 MiB |
| Dekaf (3conn) | 2026-07-17T00:17:40.7322846+00:00 | 1 | capacity | started | 0ms | 27.0 MiB / 10.6 MiB |
| Dekaf (3conn) | 2026-07-17T00:17:55.7586875+00:00 | 1 | capacity | succeeded | 15,026ms | 27.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-07-17T00:18:40.8307626+00:00 | 1 | capacity | succeeded | 15,024ms | 24.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-07-17T00:19:10.8698203+00:00 | 1 | capacity | started | 0ms | 27.0 MiB / 2.8 MiB |
| Dekaf (3conn) | 2026-07-17T00:19:25.8918577+00:00 | 1 | capacity | succeeded | 15,022ms | 27.0 MiB / 2.8 MiB |
| Dekaf (3conn) | 2026-07-17T00:19:55.9456679+00:00 | 1 | capacity | started | 0ms | 30.0 MiB / 3.8 MiB |
| Dekaf (3conn) | 2026-07-17T00:20:10.9719831+00:00 | 1 | capacity | failed | 15,026ms | 27.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-07-17T00:20:41.0162363+00:00 | 1 | capacity | started | 0ms | 24.0 MiB / 4.5 MiB |
| Dekaf (3conn) | 2026-07-17T00:21:26.1113098+00:00 | 1 | capacity | started | 0ms | 27.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-17T00:21:41.1338043+00:00 | 1 | capacity | succeeded | 15,022ms | 27.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-07-17T00:22:11.3791923+00:00 | 1 | capacity | started | 0ms | 30.0 MiB / 5.2 MiB |
*17 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 1,650 |
| Dekaf | 1 | 0.002–0.004ms | 2,125 |
| Dekaf | 1 | 0.004–0.008ms | 7,622 |
| Dekaf | 1 | 0.008–0.016ms | 39,723 |
| Dekaf | 1 | 0.016–0.032ms | 38,996 |
| Dekaf | 1 | 0.032–0.064ms | 45,954 |
| Dekaf | 1 | 0.064–0.128ms | 88,597 |
| Dekaf | 1 | 0.128–0.256ms | 283,584 |
| Dekaf | 1 | 0.256–0.512ms | 381,240 |
| Dekaf | 1 | 0.512–1.024ms | 80,847 |
| Dekaf | 1 | 1.024–2.048ms | 16,518 |
| Dekaf | 1 | 2.048–4.096ms | 4,099 |
| Dekaf | 1 | 4.096–8.192ms | 761 |
| Dekaf | 1 | 8.192–16.384ms | 44 |
| Dekaf | 1 | 16.384–32.768ms | 1 |
| Dekaf | 1 | 0.001–0.002ms | 1,784 |
| Dekaf | 1 | 0.002–0.004ms | 2,150 |
| Dekaf | 1 | 0.004–0.008ms | 8,116 |
| Dekaf | 1 | 0.008–0.016ms | 41,632 |
| Dekaf | 1 | 0.016–0.032ms | 41,590 |
| Dekaf | 1 | 0.032–0.064ms | 49,646 |
| Dekaf | 1 | 0.064–0.128ms | 91,793 |
| Dekaf | 1 | 0.128–0.256ms | 288,856 |
| Dekaf | 1 | 0.256–0.512ms | 358,392 |
| Dekaf | 1 | 0.512–1.024ms | 77,866 |
| Dekaf | 1 | 1.024–2.048ms | 18,131 |
| Dekaf | 1 | 2.048–4.096ms | 4,090 |
| Dekaf | 1 | 4.096–8.192ms | 890 |
| Dekaf | 1 | 8.192–16.384ms | 57 |
| Dekaf | 1 | 32.768–65.536ms | 3 |
| Dekaf (3conn) | 1 | 0.001–0.002ms | 8 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 5 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 18 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 48 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 99 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 175 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 340 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 659 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 868 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 805 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 450 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 247 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 90 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 13 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 2 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 2 |
| Dekaf (3conn) | 1 | 65.536–131.072ms | 2 |
| Dekaf (3conn) | 1 | 131.072–262.144ms | 4 |

## Delivery Latency Outliers - Producer (Fire-and-Forget)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf (3conn) | 466,082,000 | 2026-07-17T00:12:09.1796898+00:00 | 219.4ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,081,000 | 2026-07-17T00:12:09.1798561+00:00 | 219.2ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,083,000 | 2026-07-17T00:12:09.1822059+00:00 | 220.3ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,084,000 | 2026-07-17T00:12:09.1836398+00:00 | 222.7ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,085,000 | 2026-07-17T00:12:09.184966+00:00 | 218.2ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,086,000 | 2026-07-17T00:12:09.1862958+00:00 | 220.4ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,087,000 | 2026-07-17T00:12:09.1865506+00:00 | 232.4ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,088,000 | 2026-07-17T00:12:09.1867981+00:00 | 218.2ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,089,000 | 2026-07-17T00:12:09.187447+00:00 | 215.7ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,090,000 | 2026-07-17T00:12:09.1878013+00:00 | 228.8ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,091,000 | 2026-07-17T00:12:09.1881703+00:00 | 229.9ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,092,000 | 2026-07-17T00:12:09.1885237+00:00 | 229.6ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,093,000 | 2026-07-17T00:12:09.1887464+00:00 | 214.4ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,094,000 | 2026-07-17T00:12:09.1889834+00:00 | 219.5ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,095,000 | 2026-07-17T00:12:09.1896268+00:00 | 221.2ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,096,000 | 2026-07-17T00:12:09.1898799+00:00 | 218.6ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,097,000 | 2026-07-17T00:12:09.1902762+00:00 | 233.7ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,098,000 | 2026-07-17T00:12:09.1907918+00:00 | 220.0ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,099,000 | 2026-07-17T00:12:09.1910514+00:00 | 213.0ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,100,000 | 2026-07-17T00:12:09.191315+00:00 | 225.6ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,101,000 | 2026-07-17T00:12:09.1919919+00:00 | 227.5ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,102,000 | 2026-07-17T00:12:09.1922485+00:00 | 227.2ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,103,000 | 2026-07-17T00:12:09.1926402+00:00 | 215.8ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,104,000 | 2026-07-17T00:12:09.1930328+00:00 | 221.1ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,105,000 | 2026-07-17T00:12:09.1934285+00:00 | 218.8ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,106,000 | 2026-07-17T00:12:09.1936738+00:00 | 220.5ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,107,000 | 2026-07-17T00:12:09.1943557+00:00 | 229.9ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,108,000 | 2026-07-17T00:12:09.1946115+00:00 | 217.6ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,109,000 | 2026-07-17T00:12:09.1950086+00:00 | 215.8ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,110,000 | 2026-07-17T00:12:09.1954+00:00 | 224.0ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,111,000 | 2026-07-17T00:12:09.1957859+00:00 | 229.1ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,112,000 | 2026-07-17T00:12:09.1960347+00:00 | 228.8ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 466,113,000 | 2026-07-17T00:12:09.1966872+00:00 | 215.5ms | broker/backlog (no scale or GC event) | - | - | 285.1s / 1,358,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,251,000 | 2026-07-17T00:13:59.0780032+00:00 | 217.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,252,000 | 2026-07-17T00:13:59.0782492+00:00 | 217.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,254,000 | 2026-07-17T00:13:59.0799347+00:00 | 215.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,256,000 | 2026-07-17T00:13:59.0809393+00:00 | 220.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,261,000 | 2026-07-17T00:13:59.0836152+00:00 | 217.3ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,262,000 | 2026-07-17T00:13:59.0841373+00:00 | 216.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,264,000 | 2026-07-17T00:13:59.0848164+00:00 | 216.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,266,000 | 2026-07-17T00:13:59.0854769+00:00 | 215.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,267,000 | 2026-07-17T00:13:59.0860417+00:00 | 214.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,270,000 | 2026-07-17T00:13:59.0872913+00:00 | 213.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,271,000 | 2026-07-17T00:13:59.0875242+00:00 | 214.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,272,000 | 2026-07-17T00:13:59.0877585+00:00 | 214.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,274,000 | 2026-07-17T00:13:59.0887133+00:00 | 212.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,276,000 | 2026-07-17T00:13:59.089426+00:00 | 211.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,277,000 | 2026-07-17T00:13:59.0896522+00:00 | 211.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,280,000 | 2026-07-17T00:13:59.0910128+00:00 | 209.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,281,000 | 2026-07-17T00:13:59.0913959+00:00 | 211.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,282,000 | 2026-07-17T00:13:59.0916527+00:00 | 211.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,284,000 | 2026-07-17T00:13:59.0921497+00:00 | 211.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,285,000 | 2026-07-17T00:13:59.1225292+00:00 | 178.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,286,000 | 2026-07-17T00:13:59.1234735+00:00 | 180.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,287,000 | 2026-07-17T00:13:59.1241725+00:00 | 176.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,288,000 | 2026-07-17T00:13:59.1248036+00:00 | 176.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,289,000 | 2026-07-17T00:13:59.1631461+00:00 | 137.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,290,000 | 2026-07-17T00:13:59.1634+00:00 | 137.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,291,000 | 2026-07-17T00:13:59.1638007+00:00 | 141.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,292,000 | 2026-07-17T00:13:59.1655589+00:00 | 139.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,293,000 | 2026-07-17T00:13:59.1657959+00:00 | 135.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,294,000 | 2026-07-17T00:13:59.1661994+00:00 | 139.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 395.2s / 1,366,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,146,000 | 2026-07-17T00:15:29.2068926+00:00 | 225.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,148,000 | 2026-07-17T00:15:29.2086844+00:00 | 225.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,150,000 | 2026-07-17T00:15:29.2094698+00:00 | 241.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,151,000 | 2026-07-17T00:15:29.2108105+00:00 | 239.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,152,000 | 2026-07-17T00:15:29.2110678+00:00 | 239.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,153,000 | 2026-07-17T00:15:29.2113115+00:00 | 224.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,154,000 | 2026-07-17T00:15:29.2116804+00:00 | 225.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,155,000 | 2026-07-17T00:15:29.2119215+00:00 | 227.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,156,000 | 2026-07-17T00:15:29.2132366+00:00 | 223.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,157,000 | 2026-07-17T00:15:29.2145369+00:00 | 236.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,158,000 | 2026-07-17T00:15:29.2150439+00:00 | 224.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,159,000 | 2026-07-17T00:15:29.2152623+00:00 | 220.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,160,000 | 2026-07-17T00:15:29.2154872+00:00 | 235.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,161,000 | 2026-07-17T00:15:29.2158591+00:00 | 234.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,162,000 | 2026-07-17T00:15:29.2163586+00:00 | 234.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,163,000 | 2026-07-17T00:15:29.2167205+00:00 | 221.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,164,000 | 2026-07-17T00:15:29.2172332+00:00 | 221.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,165,000 | 2026-07-17T00:15:29.2174734+00:00 | 222.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,166,000 | 2026-07-17T00:15:29.2177161+00:00 | 221.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,167,000 | 2026-07-17T00:15:29.2181008+00:00 | 232.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,168,000 | 2026-07-17T00:15:29.2186087+00:00 | 221.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,169,000 | 2026-07-17T00:15:29.2190051+00:00 | 220.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,170,000 | 2026-07-17T00:15:29.2193854+00:00 | 234.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,171,000 | 2026-07-17T00:15:29.219769+00:00 | 230.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,172,000 | 2026-07-17T00:15:29.2200338+00:00 | 230.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,173,000 | 2026-07-17T00:15:29.2202867+00:00 | 218.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,174,000 | 2026-07-17T00:15:29.2209501+00:00 | 219.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,175,000 | 2026-07-17T00:15:29.2213449+00:00 | 220.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,176,000 | 2026-07-17T00:15:29.221731+00:00 | 218.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,177,000 | 2026-07-17T00:15:29.22212+00:00 | 228.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,178,000 | 2026-07-17T00:15:29.2223627+00:00 | 219.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,179,000 | 2026-07-17T00:15:29.2226046+00:00 | 216.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,180,000 | 2026-07-17T00:15:29.2232738+00:00 | 231.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,181,000 | 2026-07-17T00:15:29.2236561+00:00 | 227.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,182,000 | 2026-07-17T00:15:29.2240536+00:00 | 226.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,183,000 | 2026-07-17T00:15:29.2242966+00:00 | 215.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,184,000 | 2026-07-17T00:15:29.2246756+00:00 | 217.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,185,000 | 2026-07-17T00:15:29.2249106+00:00 | 217.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,186,000 | 2026-07-17T00:15:29.2255647+00:00 | 216.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,187,000 | 2026-07-17T00:15:29.2259221+00:00 | 228.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,188,000 | 2026-07-17T00:15:29.2262826+00:00 | 224.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,189,000 | 2026-07-17T00:15:29.2265116+00:00 | 214.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 485.2s / 1,425,602 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,794,000 | 2026-07-17T00:15:34.2705831+00:00 | 224.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,796,000 | 2026-07-17T00:15:34.272146+00:00 | 223.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,801,000 | 2026-07-17T00:15:34.2740426+00:00 | 222.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,802,000 | 2026-07-17T00:15:34.2743825+00:00 | 230.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,804,000 | 2026-07-17T00:15:34.2749678+00:00 | 224.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,806,000 | 2026-07-17T00:15:34.2757233+00:00 | 223.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,808,000 | 2026-07-17T00:15:34.2766519+00:00 | 227.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,809,000 | 2026-07-17T00:15:34.2770419+00:00 | 220.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,810,000 | 2026-07-17T00:15:34.2772887+00:00 | 230.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,811,000 | 2026-07-17T00:15:34.2786389+00:00 | 229.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,812,000 | 2026-07-17T00:15:34.2799922+00:00 | 228.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,813,000 | 2026-07-17T00:15:34.281363+00:00 | 218.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,814,000 | 2026-07-17T00:15:34.2819079+00:00 | 222.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,815,000 | 2026-07-17T00:15:34.2822087+00:00 | 225.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,816,000 | 2026-07-17T00:15:34.2824926+00:00 | 221.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,817,000 | 2026-07-17T00:15:34.2828991+00:00 | 225.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,818,000 | 2026-07-17T00:15:34.2834351+00:00 | 224.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,819,000 | 2026-07-17T00:15:34.2838195+00:00 | 220.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,820,000 | 2026-07-17T00:15:34.2843478+00:00 | 223.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,821,000 | 2026-07-17T00:15:34.2846055+00:00 | 223.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,822,000 | 2026-07-17T00:15:34.2848592+00:00 | 223.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,823,000 | 2026-07-17T00:15:34.2854322+00:00 | 219.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,824,000 | 2026-07-17T00:15:34.2858966+00:00 | 222.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,825,000 | 2026-07-17T00:15:34.2865392+00:00 | 221.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,826,000 | 2026-07-17T00:15:34.2869432+00:00 | 221.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,827,000 | 2026-07-17T00:15:34.287409+00:00 | 220.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,828,000 | 2026-07-17T00:15:34.2876522+00:00 | 220.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,829,000 | 2026-07-17T00:15:34.2880469+00:00 | 216.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,830,000 | 2026-07-17T00:15:34.2884442+00:00 | 219.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,831,000 | 2026-07-17T00:15:34.2889927+00:00 | 219.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,832,000 | 2026-07-17T00:15:34.2893992+00:00 | 218.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,833,000 | 2026-07-17T00:15:34.2898041+00:00 | 218.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,834,000 | 2026-07-17T00:15:34.290067+00:00 | 217.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,835,000 | 2026-07-17T00:15:34.2904554+00:00 | 217.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,836,000 | 2026-07-17T00:15:34.2908407+00:00 | 217.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,837,000 | 2026-07-17T00:15:34.2912436+00:00 | 216.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,838,000 | 2026-07-17T00:15:34.2917989+00:00 | 216.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,839,000 | 2026-07-17T00:15:34.2922517+00:00 | 215.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,840,000 | 2026-07-17T00:15:34.2925333+00:00 | 218.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 490.2s / 1,339,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,356,114,000 | 2026-07-17T00:20:46.4630505+00:00 | 221.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,116,000 | 2026-07-17T00:20:46.4637122+00:00 | 220.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,121,000 | 2026-07-17T00:20:46.4658496+00:00 | 218.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,122,000 | 2026-07-17T00:20:46.466113+00:00 | 218.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,124,000 | 2026-07-17T00:20:46.4667589+00:00 | 217.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,126,000 | 2026-07-17T00:20:46.46777+00:00 | 216.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,127,000 | 2026-07-17T00:20:46.4682028+00:00 | 214.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,131,000 | 2026-07-17T00:20:46.4696953+00:00 | 216.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,132,000 | 2026-07-17T00:20:46.4704795+00:00 | 215.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,134,000 | 2026-07-17T00:20:46.4712638+00:00 | 215.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,136,000 | 2026-07-17T00:20:46.4719079+00:00 | 214.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,137,000 | 2026-07-17T00:20:46.4722983+00:00 | 212.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,140,000 | 2026-07-17T00:20:46.4735976+00:00 | 210.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,141,000 | 2026-07-17T00:20:46.4739971+00:00 | 212.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,142,000 | 2026-07-17T00:20:46.4742467+00:00 | 212.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,143,000 | 2026-07-17T00:20:46.474647+00:00 | 205.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,144,000 | 2026-07-17T00:20:46.4750433+00:00 | 212.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,145,000 | 2026-07-17T00:20:46.4754367+00:00 | 207.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,146,000 | 2026-07-17T00:20:46.5071737+00:00 | 180.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,147,000 | 2026-07-17T00:20:46.5078998+00:00 | 176.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,148,000 | 2026-07-17T00:20:46.5081755+00:00 | 176.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,149,000 | 2026-07-17T00:20:46.5481113+00:00 | 135.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,150,000 | 2026-07-17T00:20:46.5485682+00:00 | 137.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,151,000 | 2026-07-17T00:20:46.5491493+00:00 | 141.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,152,000 | 2026-07-17T00:20:46.5494324+00:00 | 140.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,356,153,000 | 2026-07-17T00:20:46.5501023+00:00 | 135.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 802.4s / 1,299,621 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,466,982,000 | 2026-07-17T00:21:57.6357493+00:00 | 222.2ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,466,984,000 | 2026-07-17T00:21:57.6363055+00:00 | 218.9ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,466,986,000 | 2026-07-17T00:21:57.637945+00:00 | 225.1ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,466,987,000 | 2026-07-17T00:21:57.6386492+00:00 | 224.4ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,466,988,000 | 2026-07-17T00:21:57.638975+00:00 | 224.0ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,466,989,000 | 2026-07-17T00:21:57.6392982+00:00 | 219.8ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,466,990,000 | 2026-07-17T00:21:57.6395929+00:00 | 224.1ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,466,991,000 | 2026-07-17T00:21:57.6398895+00:00 | 223.8ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,466,992,000 | 2026-07-17T00:21:57.6412509+00:00 | 222.5ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,466,993,000 | 2026-07-17T00:21:57.64183+00:00 | 221.2ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,466,994,000 | 2026-07-17T00:21:57.6422674+00:00 | 220.7ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,466,995,000 | 2026-07-17T00:21:57.6425566+00:00 | 221.1ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,466,996,000 | 2026-07-17T00:21:57.6428563+00:00 | 220.1ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,466,997,000 | 2026-07-17T00:21:57.6431617+00:00 | 220.5ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,466,998,000 | 2026-07-17T00:21:57.6437124+00:00 | 220.0ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,466,999,000 | 2026-07-17T00:21:57.6443915+00:00 | 218.6ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,467,000,000 | 2026-07-17T00:21:57.6447807+00:00 | 219.3ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,467,001,000 | 2026-07-17T00:21:57.6450365+00:00 | 218.7ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,467,002,000 | 2026-07-17T00:21:57.6452639+00:00 | 218.4ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,467,003,000 | 2026-07-17T00:21:57.6454972+00:00 | 217.5ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,467,004,000 | 2026-07-17T00:21:57.6458747+00:00 | 217.8ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,467,005,000 | 2026-07-17T00:21:57.6466565+00:00 | 217.0ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,467,006,000 | 2026-07-17T00:21:57.6470572+00:00 | 216.6ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,467,007,000 | 2026-07-17T00:21:57.6473083+00:00 | 217.8ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,467,008,000 | 2026-07-17T00:21:57.6475466+00:00 | 216.1ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,467,009,000 | 2026-07-17T00:21:57.647784+00:00 | 215.2ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,467,010,000 | 2026-07-17T00:21:57.6481759+00:00 | 217.0ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,467,011,000 | 2026-07-17T00:21:57.6488552+00:00 | 214.9ms | broker/backlog (no scale or GC event) | - | - | 873.4s / 1,187,827 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

:::tip
**Dekaf uses 1.36x less CPU per message** than Confluent.Kafka for producer (fire-and-forget); comparison throughput is 1.11x.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.08 | 1052.16 | 1,336,103 | 1,330,004 | +3.4% | +0.31% | 1274.21 | 1,336,103 | 0 | 1.44 |
| Dekaf | 1.00 | 965.93 | 1,224,461 | 1,230,270 | +3.3% | +0.34% | 1167.74 | 1,224,461 | 0 | 1.23 |
| Confluent | 1.60 | - | 936,232 | 935,882 | -0.7% | -0.07% | 892.86 | 936,232 | 0 | 1.50 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 382,236 | 424.70 | 950.10 KB |
| Dekaf | 2 | 375,013 | 416.68 | 959.05 KB |
| Dekaf | 3 | 388,450 | 431.61 | 959.44 KB |
| Dekaf (3conn) | 1 | 415,885 | 462.08 | 977.87 KB |
| Dekaf (3conn) | 2 | 409,699 | 455.21 | 967.25 KB |
| Dekaf (3conn) | 3 | 403,944 | 448.81 | 971.56 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-16T23:07:27.1048888+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 491,441 msg/s |
| Dekaf | 2026-07-16T23:07:45.1305433+00:00 | 3 | 16.0 MiB / 1.9 MiB | 417.0 MB/s | 0/0 | 2,137 | 18.0s / 1,080,833 msg/s |
| Dekaf | 2026-07-16T23:08:04.1511248+00:00 | 1 | 16.0 MiB / 1.7 MiB | 437.6 MB/s | 0/0 | 5,763 | 37.0s / 1,197,561 msg/s |
| Dekaf | 2026-07-16T23:08:22.1620916+00:00 | 1 | 14.0 MiB / 2.4 MiB | 442.2 MB/s | 1/0 | 7,086 | 55.1s / 1,174,161 msg/s |
| Dekaf | 2026-07-16T23:08:40.1776402+00:00 | 2 | 12.0 MiB / 6.0 MiB | 443.1 MB/s | 2/0 | 7,728 | 73.1s / 1,277,881 msg/s |
| Dekaf | 2026-07-16T23:08:58.1822204+00:00 | 2 | 10.0 MiB / 3.3 MiB | 443.1 MB/s | 3/0 | 9,435 | 91.1s / 1,213,781 msg/s |
| Dekaf | 2026-07-16T23:09:16.1941826+00:00 | 3 | 10.0 MiB / 2.7 MiB | 455.8 MB/s | 3/0 | 8,837 | 109.1s / 1,169,417 msg/s |
| Dekaf | 2026-07-16T23:09:34.2146413+00:00 | 3 | 10.0 MiB / 3.4 MiB | 472.9 MB/s | 3/0 | 10,783 | 127.1s / 1,241,728 msg/s |
| Dekaf | 2026-07-16T23:09:53.2371861+00:00 | 1 | 12.0 MiB / 2.6 MiB | 483.3 MB/s | 2/2 | 13,770 | 146.1s / 1,208,065 msg/s |
| Dekaf | 2026-07-16T23:10:11.2480408+00:00 | 1 | 12.0 MiB / 4.7 MiB | 483.3 MB/s | 2/2 | 15,123 | 164.1s / 1,297,824 msg/s |
| Dekaf | 2026-07-16T23:10:29.2702574+00:00 | 2 | 11.0 MiB / 2.7 MiB | 455.3 MB/s | 4/1 | 18,957 | 182.2s / 1,194,122 msg/s |
| Dekaf | 2026-07-16T23:10:47.2826462+00:00 | 2 | 11.0 MiB / 2.7 MiB | 455.3 MB/s | 4/1 | 20,111 | 200.2s / 1,270,902 msg/s |
| Dekaf | 2026-07-16T23:11:05.2936872+00:00 | 3 | 8.0 MiB / 6.3 MiB | 479.9 MB/s | 4/1 | 24,449 | 218.2s / 1,257,968 msg/s |
| Dekaf | 2026-07-16T23:11:23.3042616+00:00 | 3 | 8.0 MiB / 4.5 MiB | 479.9 MB/s | 4/1 | 26,458 | 236.2s / 1,152,405 msg/s |
| Dekaf | 2026-07-16T23:11:42.3106811+00:00 | 1 | 8.0 MiB / 3.6 MiB | 483.3 MB/s | 4/2 | 24,786 | 255.2s / 1,229,747 msg/s |
| Dekaf | 2026-07-16T23:12:00.3180326+00:00 | 1 | 8.0 MiB / 0.9 MiB | 483.3 MB/s | 4/3 | 26,631 | 273.2s / 1,201,403 msg/s |
| Dekaf | 2026-07-16T23:12:18.3344214+00:00 | 1 | 8.0 MiB / 2.2 MiB | 483.3 MB/s | 4/3 | 28,527 | 291.2s / 1,248,098 msg/s |
| Dekaf | 2026-07-16T23:12:36.3462218+00:00 | 2 | 8.0 MiB / 2.5 MiB | 458.3 MB/s | 6/2 | 28,004 | 309.3s / 1,171,278 msg/s |
| Dekaf | 2026-07-16T23:12:54.3525789+00:00 | 2 | 8.0 MiB / 3.0 MiB | 458.3 MB/s | 6/2 | 30,547 | 327.3s / 1,230,270 msg/s |
| Dekaf | 2026-07-16T23:13:12.3661627+00:00 | 3 | 9.0 MiB / 1.1 MiB | 479.9 MB/s | 5/3 | 37,121 | 345.3s / 1,183,358 msg/s |
| Dekaf | 2026-07-16T23:13:30.373114+00:00 | 3 | 9.0 MiB / 2.3 MiB | 479.9 MB/s | 5/3 | 39,013 | 363.3s / 1,268,238 msg/s |
| Dekaf | 2026-07-16T23:13:49.3793017+00:00 | 1 | 9.0 MiB / 4.2 MiB | 483.3 MB/s | 5/4 | 41,399 | 382.3s / 1,257,454 msg/s |
| Dekaf | 2026-07-16T23:14:07.3905681+00:00 | 1 | 9.0 MiB / 1.2 MiB | 483.3 MB/s | 5/5 | 43,486 | 400.3s / 1,265,344 msg/s |
| Dekaf | 2026-07-16T23:14:25.4001986+00:00 | 2 | 9.0 MiB / 1.1 MiB | 458.3 MB/s | 7/3 | 43,216 | 418.3s / 1,325,185 msg/s |
| Dekaf | 2026-07-16T23:14:43.424122+00:00 | 2 | 9.0 MiB / 7.6 MiB | 458.3 MB/s | 7/3 | 46,109 | 436.3s / 1,243,650 msg/s |
| Dekaf | 2026-07-16T23:15:01.4278056+00:00 | 3 | 9.0 MiB / 9.0 MiB | 489.7 MB/s | 5/5 | 51,497 | 454.4s / 1,217,945 msg/s |
| Dekaf | 2026-07-16T23:15:19.435267+00:00 | 3 | 10.0 MiB / 3.7 MiB | 489.7 MB/s | 6/5 | 53,468 | 472.4s / 1,247,399 msg/s |
| Dekaf | 2026-07-16T23:15:38.4440432+00:00 | 1 | 8.0 MiB / 1.7 MiB | 483.3 MB/s | 6/6 | 57,994 | 491.4s / 1,316,956 msg/s |
| Dekaf | 2026-07-16T23:15:56.4500976+00:00 | 1 | 8.0 MiB / 6.9 MiB | 483.3 MB/s | 6/6 | 61,620 | 509.4s / 1,140,501 msg/s |
| Dekaf | 2026-07-16T23:16:14.4579935+00:00 | 1 | 8.0 MiB / 7.6 MiB | 483.3 MB/s | 6/6 | 65,159 | 527.4s / 1,216,943 msg/s |
| Dekaf | 2026-07-16T23:16:32.4653067+00:00 | 2 | 9.0 MiB / 9.0 MiB | 467.5 MB/s | 9/4 | 67,332 | 545.4s / 1,254,524 msg/s |
| Dekaf | 2026-07-16T23:16:50.4696846+00:00 | 2 | 9.0 MiB / 9.0 MiB | 477.0 MB/s | 9/4 | 71,450 | 563.4s / 1,297,801 msg/s |
| Dekaf | 2026-07-16T23:17:08.4791048+00:00 | 3 | 9.0 MiB / 2.8 MiB | 497.1 MB/s | 8/5 | 63,540 | 581.4s / 1,279,892 msg/s |
| Dekaf | 2026-07-16T23:17:26.486091+00:00 | 3 | 9.0 MiB / 3.7 MiB | 497.1 MB/s | 8/5 | 66,664 | 599.5s / 1,232,353 msg/s |
| Dekaf | 2026-07-16T23:17:45.4961097+00:00 | 1 | 9.0 MiB / 2.4 MiB | 483.3 MB/s | 7/7 | 78,495 | 618.5s / 1,178,049 msg/s |
| Dekaf | 2026-07-16T23:18:03.5025562+00:00 | 1 | 8.0 MiB / 1.9 MiB | 483.3 MB/s | 8/7 | 81,848 | 636.5s / 1,220,972 msg/s |
| Dekaf | 2026-07-16T23:18:21.5126287+00:00 | 2 | 8.0 MiB / 7.7 MiB | 477.0 MB/s | 9/6 | 86,881 | 654.5s / 1,258,732 msg/s |
| Dekaf | 2026-07-16T23:18:39.5270799+00:00 | 2 | 8.0 MiB / 6.4 MiB | 477.0 MB/s | 10/6 | 90,030 | 672.5s / 1,124,443 msg/s |
| Dekaf | 2026-07-16T23:18:57.5432315+00:00 | 3 | 9.0 MiB / 2.6 MiB | 497.1 MB/s | 8/7 | 79,529 | 690.5s / 1,232,853 msg/s |
| Dekaf | 2026-07-16T23:19:15.5512225+00:00 | 3 | 8.0 MiB / 7.9 MiB | 497.1 MB/s | 9/7 | 82,085 | 708.5s / 1,234,087 msg/s |
| Dekaf | 2026-07-16T23:19:34.5600219+00:00 | 1 | 9.0 MiB / 2.8 MiB | 483.3 MB/s | 9/8 | 97,582 | 727.5s / 1,190,048 msg/s |
| Dekaf | 2026-07-16T23:19:52.5671709+00:00 | 1 | 10.0 MiB / 5.2 MiB | 483.3 MB/s | 9/8 | 99,706 | 745.5s / 1,239,649 msg/s |
| Dekaf | 2026-07-16T23:20:10.5739952+00:00 | 2 | 10.0 MiB / 6.5 MiB | 477.0 MB/s | 12/6 | 103,923 | 763.6s / 1,221,574 msg/s |
| Dekaf | 2026-07-16T23:20:28.5850339+00:00 | 2 | 10.0 MiB / 0.0 MiB | 477.0 MB/s | 12/6 | 105,818 | 781.6s / 1,237,589 msg/s |
| Dekaf | 2026-07-16T23:20:46.5957066+00:00 | 2 | 10.0 MiB / 6.9 MiB | 477.0 MB/s | 12/6 | 107,902 | 799.6s / 1,263,598 msg/s |
| Dekaf | 2026-07-16T23:21:04.6198199+00:00 | 3 | 9.0 MiB / 0.9 MiB | 497.1 MB/s | 9/9 | 104,543 | 817.6s / 1,251,654 msg/s |
| Dekaf | 2026-07-16T23:21:22.6418738+00:00 | 3 | 8.0 MiB / 0.4 MiB | 497.1 MB/s | 9/10 | 107,477 | 835.6s / 1,215,871 msg/s |
| Dekaf | 2026-07-16T23:21:41.646517+00:00 | 1 | 8.0 MiB / 3.8 MiB | 483.3 MB/s | 10/10 | 115,728 | 854.6s / 1,302,922 msg/s |
| Dekaf | 2026-07-16T23:21:59.6533758+00:00 | 1 | 8.0 MiB / 1.4 MiB | 483.3 MB/s | 10/10 | 119,687 | 872.6s / 1,184,214 msg/s |
| Dekaf | 2026-07-16T23:22:17.6617632+00:00 | 2 | 10.0 MiB / 0.8 MiB | 477.0 MB/s | 12/8 | 120,271 | 890.7s / 1,251,492 msg/s |
| Dekaf (3conn) | 2026-07-16T23:37:50.002971+00:00 | 1 | 48.0 MiB / 1.6 MiB | 575.4 MB/s | 0/0 | 791 | 9.0s / 1,447,569 msg/s |
| Dekaf (3conn) | 2026-07-16T23:38:08.0442855+00:00 | 2 | 48.0 MiB / 36.4 MiB | 535.2 MB/s | 0/0 | 242 | 27.1s / 1,061,199 msg/s |
| Dekaf (3conn) | 2026-07-16T23:38:26.0872325+00:00 | 2 | 42.0 MiB / 4.5 MiB | 535.2 MB/s | 0/0 | 597 | 45.1s / 1,300,759 msg/s |
| Dekaf (3conn) | 2026-07-16T23:38:44.1158446+00:00 | 3 | 36.0 MiB / 1.4 MiB | 531.8 MB/s | 1/0 | 1,136 | 63.1s / 1,084,068 msg/s |
| Dekaf (3conn) | 2026-07-16T23:39:02.1299679+00:00 | 3 | 42.0 MiB / 1.6 MiB | 531.8 MB/s | 1/1 | 1,176 | 81.2s / 1,241,881 msg/s |
| Dekaf (3conn) | 2026-07-16T23:39:21.1519077+00:00 | 1 | 42.0 MiB / 1.8 MiB | 575.4 MB/s | 1/1 | 3,030 | 100.2s / 1,158,234 msg/s |
| Dekaf (3conn) | 2026-07-16T23:39:39.1670634+00:00 | 1 | 42.0 MiB / 4.5 MiB | 575.4 MB/s | 1/2 | 3,382 | 118.2s / 1,256,209 msg/s |
| Dekaf (3conn) | 2026-07-16T23:39:57.2540614+00:00 | 1 | 42.0 MiB / 1.8 MiB | 575.4 MB/s | 1/2 | 3,638 | 136.2s / 1,350,306 msg/s |
| Dekaf (3conn) | 2026-07-16T23:40:15.2790609+00:00 | 2 | 48.0 MiB / 2.4 MiB | 560.6 MB/s | 0/3 | 703 | 154.2s / 1,385,361 msg/s |
| Dekaf (3conn) | 2026-07-16T23:40:33.2941379+00:00 | 2 | 48.0 MiB / 1.9 MiB | 573.2 MB/s | 0/3 | 763 | 172.3s / 1,318,466 msg/s |
| Dekaf (3conn) | 2026-07-16T23:40:51.3516268+00:00 | 3 | 48.0 MiB / 10.5 MiB | 572.6 MB/s | 3/1 | 1,686 | 190.3s / 1,326,977 msg/s |
| Dekaf (3conn) | 2026-07-16T23:41:09.4402934+00:00 | 3 | 48.0 MiB / 4.5 MiB | 572.6 MB/s | 3/2 | 1,838 | 208.3s / 1,201,766 msg/s |
| Dekaf (3conn) | 2026-07-16T23:41:28.4630964+00:00 | 1 | 42.0 MiB / 12.4 MiB | 579.0 MB/s | 1/4 | 5,925 | 227.3s / 1,543,300 msg/s |
| Dekaf (3conn) | 2026-07-16T23:41:46.4738988+00:00 | 1 | 42.0 MiB / 31.7 MiB | 579.0 MB/s | 1/5 | 6,439 | 245.3s / 1,214,457 msg/s |
| Dekaf (3conn) | 2026-07-16T23:42:04.4905201+00:00 | 2 | 48.0 MiB / 18.8 MiB | 573.2 MB/s | 0/5 | 1,639 | 263.4s / 1,369,162 msg/s |
| Dekaf (3conn) | 2026-07-16T23:42:22.5292194+00:00 | 2 | 48.0 MiB / 4.4 MiB | 585.7 MB/s | 0/6 | 1,978 | 281.4s / 1,433,954 msg/s |
| Dekaf (3conn) | 2026-07-16T23:42:40.5834712+00:00 | 3 | 48.0 MiB / 23.8 MiB | 599.1 MB/s | 3/4 | 3,008 | 299.5s / 1,392,482 msg/s |
| Dekaf (3conn) | 2026-07-16T23:42:58.5995875+00:00 | 3 | 48.0 MiB / 7.2 MiB | 599.1 MB/s | 3/4 | 3,228 | 317.5s / 1,217,374 msg/s |
| Dekaf (3conn) | 2026-07-16T23:43:17.6599437+00:00 | 1 | 42.0 MiB / 4.9 MiB | 629.4 MB/s | 1/7 | 10,983 | 336.5s / 1,308,124 msg/s |
| Dekaf (3conn) | 2026-07-16T23:43:35.6982297+00:00 | 1 | 42.0 MiB / 3.6 MiB | 638.6 MB/s | 1/7 | 11,442 | 354.6s / 1,398,011 msg/s |
| Dekaf (3conn) | 2026-07-16T23:43:53.7261603+00:00 | 2 | 42.0 MiB / 3.1 MiB | 614.9 MB/s | 1/7 | 3,559 | 372.6s / 1,243,722 msg/s |
| Dekaf (3conn) | 2026-07-16T23:44:11.7398152+00:00 | 2 | 45.0 MiB / 4.2 MiB | 614.9 MB/s | 2/7 | 3,818 | 390.6s / 1,392,344 msg/s |
| Dekaf (3conn) | 2026-07-16T23:44:29.7923705+00:00 | 2 | 45.0 MiB / 18.0 MiB | 614.9 MB/s | 2/7 | 4,758 | 408.6s / 1,437,058 msg/s |
| Dekaf (3conn) | 2026-07-16T23:44:47.8120078+00:00 | 3 | 54.0 MiB / 1.9 MiB | 618.0 MB/s | 4/6 | 3,942 | 426.7s / 1,431,569 msg/s |
| Dekaf (3conn) | 2026-07-16T23:45:05.8395986+00:00 | 3 | 54.0 MiB / 1.7 MiB | 618.0 MB/s | 4/6 | 3,993 | 444.7s / 1,302,323 msg/s |
| Dekaf (3conn) | 2026-07-16T23:45:24.8548062+00:00 | 1 | 45.0 MiB / 5.6 MiB | 638.6 MB/s | 2/8 | 15,831 | 463.7s / 1,401,186 msg/s |
| Dekaf (3conn) | 2026-07-16T23:45:42.8718032+00:00 | 1 | 45.0 MiB / 2.3 MiB | 638.6 MB/s | 2/9 | 16,527 | 481.7s / 1,389,609 msg/s |
| Dekaf (3conn) | 2026-07-16T23:46:00.88125+00:00 | 2 | 45.0 MiB / 17.1 MiB | 614.9 MB/s | 2/9 | 6,016 | 499.7s / 1,306,543 msg/s |
| Dekaf (3conn) | 2026-07-16T23:46:18.9020395+00:00 | 2 | 45.0 MiB / 5.6 MiB | 614.9 MB/s | 2/10 | 6,151 | 517.7s / 1,451,013 msg/s |
| Dekaf (3conn) | 2026-07-16T23:46:36.9327535+00:00 | 3 | 54.0 MiB / 6.9 MiB | 618.0 MB/s | 4/8 | 4,411 | 535.8s / 1,589,608 msg/s |
| Dekaf (3conn) | 2026-07-16T23:46:54.9594629+00:00 | 3 | 54.0 MiB / 3.1 MiB | 618.0 MB/s | 4/8 | 4,799 | 553.8s / 1,460,206 msg/s |
| Dekaf (3conn) | 2026-07-16T23:47:13.9728918+00:00 | 1 | 45.0 MiB / 16.3 MiB | 638.6 MB/s | 2/11 | 19,494 | 572.8s / 1,379,314 msg/s |
| Dekaf (3conn) | 2026-07-16T23:47:31.9885656+00:00 | 1 | 45.0 MiB / 31.4 MiB | 638.6 MB/s | 2/11 | 20,026 | 590.9s / 1,301,218 msg/s |
| Dekaf (3conn) | 2026-07-16T23:47:49.9981784+00:00 | 2 | 45.0 MiB / 14.8 MiB | 645.4 MB/s | 2/12 | 8,754 | 608.9s / 1,294,345 msg/s |
| Dekaf (3conn) | 2026-07-16T23:48:08.0249734+00:00 | 2 | 45.0 MiB / 37.9 MiB | 645.4 MB/s | 2/12 | 8,939 | 626.9s / 1,336,547 msg/s |
| Dekaf (3conn) | 2026-07-16T23:48:26.0347386+00:00 | 2 | 45.0 MiB / 44.0 MiB | 645.4 MB/s | 2/12 | 9,333 | 645.0s / 1,336,817 msg/s |
| Dekaf (3conn) | 2026-07-16T23:48:44.051408+00:00 | 3 | 45.0 MiB / 11.5 MiB | 618.0 MB/s | 5/10 | 5,617 | 663.0s / 1,302,777 msg/s |
| Dekaf (3conn) | 2026-07-16T23:49:02.0602045+00:00 | 3 | 39.0 MiB / 25.4 MiB | 618.0 MB/s | 6/10 | 5,917 | 681.0s / 1,323,159 msg/s |
| Dekaf (3conn) | 2026-07-16T23:49:21.0962515+00:00 | 1 | 27.0 MiB / 0.7 MiB | 638.6 MB/s | 5/12 | 25,340 | 700.0s / 1,275,581 msg/s |
| Dekaf (3conn) | 2026-07-16T23:49:39.1112157+00:00 | 1 | 24.0 MiB / 1.6 MiB | 638.6 MB/s | 6/12 | 26,506 | 718.1s / 1,338,297 msg/s |
| Dekaf (3conn) | 2026-07-16T23:49:57.1460756+00:00 | 2 | 24.0 MiB / 24.0 MiB | 645.4 MB/s | 6/13 | 11,889 | 736.1s / 1,230,934 msg/s |
| Dekaf (3conn) | 2026-07-16T23:50:15.1946857+00:00 | 2 | 24.0 MiB / 1.6 MiB | 645.4 MB/s | 6/13 | 12,994 | 754.1s / 1,343,788 msg/s |
| Dekaf (3conn) | 2026-07-16T23:50:33.2179585+00:00 | 3 | 27.0 MiB / 4.2 MiB | 618.0 MB/s | 8/12 | 9,205 | 772.1s / 1,481,238 msg/s |
| Dekaf (3conn) | 2026-07-16T23:50:51.2709317+00:00 | 3 | 27.0 MiB / 11.7 MiB | 618.0 MB/s | 8/12 | 9,968 | 790.1s / 1,383,685 msg/s |
| Dekaf (3conn) | 2026-07-16T23:51:10.3070641+00:00 | 1 | 27.0 MiB / 18.0 MiB | 638.6 MB/s | 7/13 | 31,797 | 809.2s / 1,410,264 msg/s |
| Dekaf (3conn) | 2026-07-16T23:51:28.321101+00:00 | 1 | 27.0 MiB / 1.9 MiB | 638.6 MB/s | 7/14 | 33,107 | 827.2s / 1,115,683 msg/s |
| Dekaf (3conn) | 2026-07-16T23:51:46.3370221+00:00 | 2 | 24.0 MiB / 2.9 MiB | 645.4 MB/s | 6/15 | 17,382 | 845.2s / 1,236,281 msg/s |
| Dekaf (3conn) | 2026-07-16T23:52:04.3480702+00:00 | 2 | 27.0 MiB / 2.4 MiB | 645.4 MB/s | 7/15 | 17,891 | 863.2s / 1,232,509 msg/s |
| Dekaf (3conn) | 2026-07-16T23:52:22.3769095+00:00 | 3 | 27.0 MiB / 10.9 MiB | 618.0 MB/s | 10/13 | 13,073 | 881.3s / 1,244,797 msg/s |
| Dekaf (3conn) | 2026-07-16T23:52:40.3988224+00:00 | 3 | 27.0 MiB / 3.4 MiB | 618.0 MB/s | 10/13 | 13,545 | 899.3s / 1,416,265 msg/s |
*5,293 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-16T23:07:57.273903+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 6.4 MiB |
| Dekaf | 2026-07-16T23:08:12.3414625+00:00 | 1 | capacity | succeeded | 15,067ms | 14.0 MiB / 3.0 MiB |
| Dekaf | 2026-07-16T23:08:12.5521393+00:00 | 3 | capacity | succeeded | 15,150ms | 14.0 MiB / 2.2 MiB |
| Dekaf | 2026-07-16T23:08:15.566754+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 8.7 MiB |
| Dekaf | 2026-07-16T23:08:30.6192284+00:00 | 3 | capacity | succeeded | 15,052ms | 12.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-16T23:08:33.6649217+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-16T23:09:00.7420909+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 2.0 MiB |
| Dekaf | 2026-07-16T23:09:18.6606998+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 1.9 MiB |
| Dekaf | 2026-07-16T23:09:33.9953174+00:00 | 2 | capacity | failed | 15,129ms | 10.0 MiB / 5.5 MiB |
| Dekaf | 2026-07-16T23:10:03.8283052+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 1.6 MiB |
| Dekaf | 2026-07-16T23:10:18.8822828+00:00 | 1 | capacity | succeeded | 15,053ms | 10.0 MiB / 8.9 MiB |
| Dekaf | 2026-07-16T23:10:46.1930671+00:00 | 3 | capacity | failed | 15,066ms | 8.0 MiB / 1.6 MiB |
| Dekaf | 2026-07-16T23:10:49.2973732+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-16T23:11:16.3187178+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-16T23:11:34.5892633+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-16T23:11:49.6831505+00:00 | 2 | capacity | succeeded | 15,093ms | 9.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-16T23:12:19.5035193+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 5.4 MiB |
| Dekaf | 2026-07-16T23:12:34.5620452+00:00 | 1 | capacity | failed | 15,058ms | 8.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-16T23:13:01.7589114+00:00 | 3 | capacity | failed | 15,062ms | 9.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-16T23:13:19.7499653+00:00 | 1 | capacity | succeeded | 15,059ms | 9.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-16T23:13:31.8817266+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-16T23:13:50.1976767+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 6.9 MiB |
| Dekaf | 2026-07-16T23:14:17.1192833+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-16T23:14:35.1163502+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 5.2 MiB |
| Dekaf | 2026-07-16T23:14:50.5123815+00:00 | 2 | capacity | succeeded | 15,100ms | 8.0 MiB / 5.0 MiB |
| Dekaf | 2026-07-16T23:15:17.3354835+00:00 | 3 | capacity | succeeded | 15,042ms | 10.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-16T23:15:35.315072+00:00 | 1 | capacity | failed | 15,056ms | 8.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-16T23:16:02.5887892+00:00 | 3 | capacity | succeeded | 15,127ms | 11.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-16T23:16:05.8175461+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 6.7 MiB |
| Dekaf | 2026-07-16T23:16:32.6958434+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-16T23:16:50.7292412+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-16T23:17:06.0892087+00:00 | 2 | capacity | failed | 15,057ms | 9.0 MiB / 5.4 MiB |
| Dekaf | 2026-07-16T23:17:35.9886098+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 4.4 MiB |
| Dekaf | 2026-07-16T23:17:51.0585377+00:00 | 1 | capacity | succeeded | 15,069ms | 8.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-16T23:18:18.1880806+00:00 | 3 | capacity | failed | 15,064ms | 9.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-16T23:18:36.1964474+00:00 | 1 | capacity | failed | 15,056ms | 8.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-16T23:18:48.3050546+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 1.1 MiB |
| Dekaf | 2026-07-16T23:19:06.640241+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-16T23:19:21.6872144+00:00 | 2 | capacity | succeeded | 15,046ms | 9.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-16T23:19:51.5879981+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 2.5 MiB |
| Dekaf | 2026-07-16T23:20:06.8593523+00:00 | 2 | capacity | succeeded | 15,055ms | 10.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-16T23:20:33.8001294+00:00 | 3 | capacity | failed | 15,070ms | 8.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-16T23:20:51.833536+00:00 | 1 | capacity | succeeded | 15,054ms | 8.0 MiB / 2.6 MiB |
| Dekaf | 2026-07-16T23:21:03.9772067+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 4.0 MiB |
| Dekaf | 2026-07-16T23:21:22.2372715+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 0.4 MiB |
| Dekaf | 2026-07-16T23:21:49.1628367+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 5.3 MiB |
| Dekaf | 2026-07-16T23:22:07.1600696+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 0.0 MiB |
| Dekaf | 2026-07-16T23:22:22.5169116+00:00 | 2 | capacity | succeeded | 15,117ms | 11.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-07-16T23:38:11.4782527+00:00 | 1 | capacity | started | 0ms | 42.0 MiB / 0.5 MiB |
| Dekaf (3conn) | 2026-07-16T23:38:26.379068+00:00 | 2 | capacity | failed | 15,106ms | 48.0 MiB / 15.5 MiB |
| Dekaf (3conn) | 2026-07-16T23:38:29.6084822+00:00 | 1 | capacity | started | 0ms | 36.0 MiB / 12.1 MiB |
| Dekaf (3conn) | 2026-07-16T23:38:44.7141688+00:00 | 1 | capacity | failed | 15,105ms | 42.0 MiB / 6.4 MiB |
| Dekaf (3conn) | 2026-07-16T23:39:14.7036244+00:00 | 3 | capacity | started | 0ms | 45.0 MiB / 2.6 MiB |
| Dekaf (3conn) | 2026-07-16T23:39:30.0406645+00:00 | 1 | capacity | failed | 15,142ms | 42.0 MiB / 22.7 MiB |
| Dekaf (3conn) | 2026-07-16T23:39:57.0314008+00:00 | 2 | capacity | failed | 15,129ms | 48.0 MiB / 18.6 MiB |
| Dekaf (3conn) | 2026-07-16T23:40:15.1391467+00:00 | 3 | capacity | succeeded | 15,094ms | 48.0 MiB / 26.9 MiB |
| Dekaf (3conn) | 2026-07-16T23:40:42.2966424+00:00 | 2 | capacity | failed | 15,076ms | 48.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-07-16T23:40:45.5292307+00:00 | 1 | capacity | started | 0ms | 36.0 MiB / 1.8 MiB |
| Dekaf (3conn) | 2026-07-16T23:41:12.600455+00:00 | 2 | capacity | started | 0ms | 54.0 MiB / 1.2 MiB |
| Dekaf (3conn) | 2026-07-16T23:41:30.6425616+00:00 | 3 | capacity | started | 0ms | 42.0 MiB / 3.9 MiB |
| Dekaf (3conn) | 2026-07-16T23:41:46.0235255+00:00 | 1 | capacity | failed | 15,188ms | 42.0 MiB / 8.3 MiB |
| Dekaf (3conn) | 2026-07-16T23:42:15.9231777+00:00 | 3 | capacity | started | 0ms | 54.0 MiB / 14.2 MiB |
| Dekaf (3conn) | 2026-07-16T23:42:31.089591+00:00 | 3 | capacity | failed | 15,166ms | 48.0 MiB / 5.6 MiB |
| Dekaf (3conn) | 2026-07-16T23:42:58.3283676+00:00 | 2 | capacity | succeeded | 15,072ms | 42.0 MiB / 9.6 MiB |
| Dekaf (3conn) | 2026-07-16T23:43:01.3450956+00:00 | 2 | capacity | started | 0ms | 36.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-16T23:43:16.4397425+00:00 | 2 | capacity | failed | 15,094ms | 42.0 MiB / 5.0 MiB |
| Dekaf (3conn) | 2026-07-16T23:43:46.6316917+00:00 | 2 | capacity | started | 0ms | 45.0 MiB / 0.6 MiB |
| Dekaf (3conn) | 2026-07-16T23:44:01.6836389+00:00 | 3 | capacity | succeeded | 15,121ms | 54.0 MiB / 4.1 MiB |
| Dekaf (3conn) | 2026-07-16T23:44:31.8341226+00:00 | 3 | capacity | started | 0ms | 60.0 MiB / 1.6 MiB |
| Dekaf (3conn) | 2026-07-16T23:44:46.8926035+00:00 | 3 | capacity | failed | 15,058ms | 54.0 MiB / 3.5 MiB |
| Dekaf (3conn) | 2026-07-16T23:44:47.3035777+00:00 | 1 | capacity | failed | 15,092ms | 45.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-07-16T23:45:17.5263031+00:00 | 1 | capacity | started | 0ms | 39.0 MiB / 5.2 MiB |
| Dekaf (3conn) | 2026-07-16T23:45:32.2485653+00:00 | 2 | capacity | failed | 15,081ms | 45.0 MiB / 2.3 MiB |
| Dekaf (3conn) | 2026-07-16T23:46:02.5127396+00:00 | 3 | capacity | started | 0ms | 45.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-07-16T23:46:17.607073+00:00 | 3 | capacity | failed | 15,094ms | 54.0 MiB / 3.0 MiB |
| Dekaf (3conn) | 2026-07-16T23:46:47.8484246+00:00 | 3 | capacity | started | 0ms | 60.0 MiB / 5.0 MiB |
| Dekaf (3conn) | 2026-07-16T23:47:02.9797601+00:00 | 3 | capacity | failed | 15,131ms | 54.0 MiB / 13.5 MiB |
| Dekaf (3conn) | 2026-07-16T23:47:03.3777802+00:00 | 1 | capacity | failed | 15,136ms | 45.0 MiB / 10.9 MiB |
| Dekaf (3conn) | 2026-07-16T23:47:33.6031386+00:00 | 1 | capacity | started | 0ms | 48.0 MiB / 5.8 MiB |
| Dekaf (3conn) | 2026-07-16T23:47:48.7070981+00:00 | 1 | capacity | failed | 15,103ms | 45.0 MiB / 29.8 MiB |
| Dekaf (3conn) | 2026-07-16T23:48:18.6396874+00:00 | 2 | capacity | started | 0ms | 39.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-07-16T23:48:33.763757+00:00 | 2 | capacity | succeeded | 15,124ms | 39.0 MiB / 1.3 MiB |
| Dekaf (3conn) | 2026-07-16T23:48:36.7932332+00:00 | 2 | capacity | started | 0ms | 33.0 MiB / 6.2 MiB |
| Dekaf (3conn) | 2026-07-16T23:48:51.8050761+00:00 | 3 | capacity | succeeded | 15,074ms | 39.0 MiB / 2.5 MiB |
| Dekaf (3conn) | 2026-07-16T23:48:54.8198585+00:00 | 3 | capacity | started | 0ms | 33.0 MiB / 2.0 MiB |
| Dekaf (3conn) | 2026-07-16T23:48:55.2261317+00:00 | 1 | capacity | started | 0ms | 27.0 MiB / 4.0 MiB |
| Dekaf (3conn) | 2026-07-16T23:49:10.2919693+00:00 | 1 | capacity | succeeded | 15,065ms | 27.0 MiB / 5.8 MiB |
| Dekaf (3conn) | 2026-07-16T23:49:13.3141324+00:00 | 1 | capacity | started | 0ms | 24.0 MiB / 0.6 MiB |
| Dekaf (3conn) | 2026-07-16T23:49:28.1762711+00:00 | 2 | capacity | succeeded | 15,115ms | 24.0 MiB / 3.7 MiB |
| Dekaf (3conn) | 2026-07-16T23:49:31.2019992+00:00 | 2 | capacity | started | 0ms | 21.0 MiB / 13.7 MiB |
| Dekaf (3conn) | 2026-07-16T23:49:46.1821052+00:00 | 3 | capacity | failed | 15,073ms | 27.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-07-16T23:50:16.4015699+00:00 | 3 | capacity | started | 0ms | 21.0 MiB / 7.2 MiB |
| Dekaf (3conn) | 2026-07-16T23:50:31.5647647+00:00 | 3 | capacity | failed | 15,163ms | 27.0 MiB / 2.6 MiB |
| Dekaf (3conn) | 2026-07-16T23:50:31.7860517+00:00 | 1 | capacity | failed | 15,120ms | 27.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-16T23:51:01.9846528+00:00 | 1 | capacity | started | 0ms | 24.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-07-16T23:51:17.0938459+00:00 | 1 | capacity | failed | 15,109ms | 27.0 MiB / 2.3 MiB |
| Dekaf (3conn) | 2026-07-16T23:51:47.1301757+00:00 | 3 | capacity | started | 0ms | 24.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-07-16T23:52:02.2259333+00:00 | 2 | capacity | succeeded | 15,099ms | 27.0 MiB / 4.0 MiB |
| Dekaf (3conn) | 2026-07-16T23:52:05.2213648+00:00 | 3 | capacity | started | 0ms | 27.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-07-16T23:52:32.533647+00:00 | 1 | capacity | started | 0ms | 21.0 MiB / 14.6 MiB |
*160 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 4 |
| Dekaf | 1 | 0.002–0.004ms | 18 |
| Dekaf | 1 | 0.004–0.008ms | 50 |
| Dekaf | 1 | 0.008–0.016ms | 142 |
| Dekaf | 1 | 0.016–0.032ms | 475 |
| Dekaf | 1 | 0.032–0.064ms | 798 |
| Dekaf | 1 | 0.064–0.128ms | 765 |
| Dekaf | 1 | 0.128–0.256ms | 914 |
| Dekaf | 1 | 0.256–0.512ms | 1,654 |
| Dekaf | 1 | 0.512–1.024ms | 2,709 |
| Dekaf | 1 | 1.024–2.048ms | 3,419 |
| Dekaf | 1 | 2.048–4.096ms | 3,288 |
| Dekaf | 1 | 4.096–8.192ms | 1,991 |
| Dekaf | 1 | 8.192–16.384ms | 798 |
| Dekaf | 1 | 16.384–32.768ms | 321 |
| Dekaf | 1 | 32.768–65.536ms | 58 |
| Dekaf | 2 | 0.001–0.002ms | 14 |
| Dekaf | 2 | 0.002–0.004ms | 8 |
| Dekaf | 2 | 0.004–0.008ms | 48 |
| Dekaf | 2 | 0.008–0.016ms | 146 |
| Dekaf | 2 | 0.016–0.032ms | 387 |
| Dekaf | 2 | 0.032–0.064ms | 735 |
| Dekaf | 2 | 0.064–0.128ms | 665 |
| Dekaf | 2 | 0.128–0.256ms | 895 |
| Dekaf | 2 | 0.256–0.512ms | 1,566 |
| Dekaf | 2 | 0.512–1.024ms | 2,618 |
| Dekaf | 2 | 1.024–2.048ms | 3,311 |
| Dekaf | 2 | 2.048–4.096ms | 3,141 |
| Dekaf | 2 | 4.096–8.192ms | 1,846 |
| Dekaf | 2 | 8.192–16.384ms | 805 |
| Dekaf | 2 | 16.384–32.768ms | 338 |
| Dekaf | 2 | 32.768–65.536ms | 59 |
| Dekaf | 2 | 65.536–131.072ms | 1 |
| Dekaf | 3 | 0.001–0.002ms | 10 |
| Dekaf | 3 | 0.002–0.004ms | 16 |
| Dekaf | 3 | 0.004–0.008ms | 52 |
| Dekaf | 3 | 0.008–0.016ms | 165 |
| Dekaf | 3 | 0.016–0.032ms | 485 |
| Dekaf | 3 | 0.032–0.064ms | 856 |
| Dekaf | 3 | 0.064–0.128ms | 757 |
| Dekaf | 3 | 0.128–0.256ms | 1,008 |
| Dekaf | 3 | 0.256–0.512ms | 1,722 |
| Dekaf | 3 | 0.512–1.024ms | 2,806 |
| Dekaf | 3 | 1.024–2.048ms | 3,482 |
| Dekaf | 3 | 2.048–4.096ms | 3,157 |
| Dekaf | 3 | 4.096–8.192ms | 1,711 |
| Dekaf | 3 | 8.192–16.384ms | 561 |
| Dekaf | 3 | 16.384–32.768ms | 168 |
| Dekaf | 3 | 32.768–65.536ms | 32 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 4 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 1 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 6 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 15 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 29 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 47 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 94 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 108 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 216 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 345 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 445 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 499 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 365 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 166 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 22 |
| Dekaf (3conn) | 2 | 0.002–0.004ms | 1 |
| Dekaf (3conn) | 2 | 0.008–0.016ms | 4 |
| Dekaf (3conn) | 2 | 0.016–0.032ms | 3 |
| Dekaf (3conn) | 2 | 0.032–0.064ms | 14 |
| Dekaf (3conn) | 2 | 0.064–0.128ms | 38 |
| Dekaf (3conn) | 2 | 0.128–0.256ms | 42 |
| Dekaf (3conn) | 2 | 0.256–0.512ms | 76 |
| Dekaf (3conn) | 2 | 0.512–1.024ms | 101 |
| Dekaf (3conn) | 2 | 1.024–2.048ms | 163 |
| Dekaf (3conn) | 2 | 2.048–4.096ms | 230 |
| Dekaf (3conn) | 2 | 4.096–8.192ms | 265 |
| Dekaf (3conn) | 2 | 8.192–16.384ms | 182 |
| Dekaf (3conn) | 2 | 16.384–32.768ms | 90 |
| Dekaf (3conn) | 2 | 32.768–65.536ms | 10 |
| Dekaf (3conn) | 3 | 0.004–0.008ms | 2 |
| Dekaf (3conn) | 3 | 0.008–0.016ms | 2 |
| Dekaf (3conn) | 3 | 0.016–0.032ms | 6 |
| Dekaf (3conn) | 3 | 0.032–0.064ms | 16 |
| Dekaf (3conn) | 3 | 0.064–0.128ms | 15 |
| Dekaf (3conn) | 3 | 0.128–0.256ms | 44 |
| Dekaf (3conn) | 3 | 0.256–0.512ms | 53 |
| Dekaf (3conn) | 3 | 0.512–1.024ms | 69 |
| Dekaf (3conn) | 3 | 1.024–2.048ms | 102 |
| Dekaf (3conn) | 3 | 2.048–4.096ms | 161 |
| Dekaf (3conn) | 3 | 4.096–8.192ms | 179 |
| Dekaf (3conn) | 3 | 8.192–16.384ms | 129 |
| Dekaf (3conn) | 3 | 16.384–32.768ms | 65 |
| Dekaf (3conn) | 3 | 32.768–65.536ms | 7 |

## Delivery Latency Outliers - Producer (Fire-and-Forget), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 100,000 | 2026-07-16T23:07:27.2961227+00:00 | 136.4ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 104,000 | 2026-07-16T23:07:27.3043232+00:00 | 129.3ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 106,000 | 2026-07-16T23:07:27.3074678+00:00 | 126.1ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 110,000 | 2026-07-16T23:07:27.3155492+00:00 | 122.5ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 114,000 | 2026-07-16T23:07:27.3252458+00:00 | 112.8ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 116,000 | 2026-07-16T23:07:27.3294075+00:00 | 108.6ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 120,000 | 2026-07-16T23:07:27.3378725+00:00 | 101.0ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 124,000 | 2026-07-16T23:07:27.3460458+00:00 | 100.1ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 235,000 | 2026-07-16T23:07:27.5883782+00:00 | 120.9ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 238,000 | 2026-07-16T23:07:27.5931961+00:00 | 116.0ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 241,000 | 2026-07-16T23:07:27.5971769+00:00 | 126.1ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 242,000 | 2026-07-16T23:07:27.5981841+00:00 | 125.1ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 245,000 | 2026-07-16T23:07:27.6022977+00:00 | 121.0ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 248,000 | 2026-07-16T23:07:27.6083755+00:00 | 114.9ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 251,000 | 2026-07-16T23:07:27.6128902+00:00 | 110.4ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 252,000 | 2026-07-16T23:07:27.614099+00:00 | 109.2ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 255,000 | 2026-07-16T23:07:27.6205262+00:00 | 110.5ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 258,000 | 2026-07-16T23:07:27.6237568+00:00 | 107.3ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 261,000 | 2026-07-16T23:07:27.6295419+00:00 | 102.7ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 262,000 | 2026-07-16T23:07:27.6302738+00:00 | 102.0ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 265,000 | 2026-07-16T23:07:27.6366656+00:00 | 139.4ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 268,000 | 2026-07-16T23:07:27.6421006+00:00 | 140.7ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 271,000 | 2026-07-16T23:07:27.6484679+00:00 | 146.4ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 272,000 | 2026-07-16T23:07:27.652551+00:00 | 142.4ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 275,000 | 2026-07-16T23:07:27.6592975+00:00 | 135.9ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 288,000 | 2026-07-16T23:07:27.7233137+00:00 | 103.8ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 291,000 | 2026-07-16T23:07:27.7322624+00:00 | 101.1ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 327,000 | 2026-07-16T23:07:27.8008109+00:00 | 116.2ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 337,000 | 2026-07-16T23:07:27.8272057+00:00 | 108.7ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 343,000 | 2026-07-16T23:07:27.8355559+00:00 | 100.3ms | GC pause | - | - | 1.0s / 491,441 msg/s | Gen2 +1 / pause +4.1ms |
| Dekaf | 561,000 | 2026-07-16T23:07:28.2762649+00:00 | 125.8ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 562,000 | 2026-07-16T23:07:28.2792463+00:00 | 122.8ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 565,000 | 2026-07-16T23:07:28.2852089+00:00 | 118.4ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 568,000 | 2026-07-16T23:07:28.2988095+00:00 | 104.8ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 581,000 | 2026-07-16T23:07:28.33832+00:00 | 101.8ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 602,000 | 2026-07-16T23:07:28.3744805+00:00 | 104.6ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 608,000 | 2026-07-16T23:07:28.3848868+00:00 | 104.0ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 771,000 | 2026-07-16T23:07:28.6055427+00:00 | 110.1ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 772,000 | 2026-07-16T23:07:28.6059668+00:00 | 109.7ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 781,000 | 2026-07-16T23:07:28.6118078+00:00 | 103.8ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 782,000 | 2026-07-16T23:07:28.6126937+00:00 | 107.7ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 791,000 | 2026-07-16T23:07:28.6181036+00:00 | 107.5ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 792,000 | 2026-07-16T23:07:28.6190277+00:00 | 106.5ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 801,000 | 2026-07-16T23:07:28.6250659+00:00 | 100.8ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 802,000 | 2026-07-16T23:07:28.6256061+00:00 | 100.3ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 826,000 | 2026-07-16T23:07:28.6593771+00:00 | 111.6ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 973,000 | 2026-07-16T23:07:28.8716128+00:00 | 100.8ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 977,000 | 2026-07-16T23:07:28.87574+00:00 | 110.7ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 979,000 | 2026-07-16T23:07:28.8768981+00:00 | 108.1ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 983,000 | 2026-07-16T23:07:28.8805778+00:00 | 104.4ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 987,000 | 2026-07-16T23:07:28.8848867+00:00 | 133.2ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 993,000 | 2026-07-16T23:07:28.9117356+00:00 | 108.6ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 997,000 | 2026-07-16T23:07:28.9165328+00:00 | 107.8ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 999,000 | 2026-07-16T23:07:28.9192112+00:00 | 105.1ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,003,000 | 2026-07-16T23:07:28.9222549+00:00 | 102.0ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,007,000 | 2026-07-16T23:07:28.9259416+00:00 | 103.7ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,009,000 | 2026-07-16T23:07:28.9266604+00:00 | 102.9ms | throughput collapse | - | - | 2.0s / 612,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,325,000 | 2026-07-16T23:07:29.3566161+00:00 | 127.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,038,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,328,000 | 2026-07-16T23:07:29.3581737+00:00 | 126.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,038,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,331,000 | 2026-07-16T23:07:29.3671298+00:00 | 117.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,038,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,332,000 | 2026-07-16T23:07:29.368062+00:00 | 116.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,038,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,335,000 | 2026-07-16T23:07:29.3707674+00:00 | 127.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,038,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,338,000 | 2026-07-16T23:07:29.3726507+00:00 | 130.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,038,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,341,000 | 2026-07-16T23:07:29.3738296+00:00 | 129.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,038,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,342,000 | 2026-07-16T23:07:29.3745897+00:00 | 137.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,038,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,345,000 | 2026-07-16T23:07:29.3763231+00:00 | 135.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,038,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,348,000 | 2026-07-16T23:07:29.3783735+00:00 | 133.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,038,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,351,000 | 2026-07-16T23:07:29.3825499+00:00 | 133.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,038,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,352,000 | 2026-07-16T23:07:29.3832592+00:00 | 132.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,038,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,355,000 | 2026-07-16T23:07:29.3867603+00:00 | 125.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,038,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,356,000 | 2026-07-16T23:07:29.3876684+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,038,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,358,000 | 2026-07-16T23:07:29.3918528+00:00 | 121.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,038,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,361,000 | 2026-07-16T23:07:29.3945323+00:00 | 127.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,038,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,362,000 | 2026-07-16T23:07:29.3954756+00:00 | 126.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,038,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,081,000 | 2026-07-16T23:07:31.9224184+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,017,589 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,082,000 | 2026-07-16T23:07:31.9226868+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,017,589 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,088,000 | 2026-07-16T23:07:31.9278472+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,017,589 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,886,000 | 2026-07-16T23:07:32.7167257+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 1,002,968 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,894,000 | 2026-07-16T23:07:32.7205494+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 1,002,968 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,896,000 | 2026-07-16T23:07:32.7212357+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 1,002,968 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,906,000 | 2026-07-16T23:07:32.7260957+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 1,002,968 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,045,000 | 2026-07-16T23:07:32.8917748+00:00 | 102.8ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 1,002,968 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,048,000 | 2026-07-16T23:07:32.89339+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 1,002,968 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,061,000 | 2026-07-16T23:07:32.9105186+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 1,002,968 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,502,000 | 2026-07-16T23:07:33.3500353+00:00 | 112.3ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,505,000 | 2026-07-16T23:07:33.3513303+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,508,000 | 2026-07-16T23:07:33.3549193+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,511,000 | 2026-07-16T23:07:33.356033+00:00 | 106.3ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,512,000 | 2026-07-16T23:07:33.3564924+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,515,000 | 2026-07-16T23:07:33.3594452+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,518,000 | 2026-07-16T23:07:33.3605893+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,521,000 | 2026-07-16T23:07:33.3628705+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,522,000 | 2026-07-16T23:07:33.3631982+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,525,000 | 2026-07-16T23:07:33.3649601+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,528,000 | 2026-07-16T23:07:33.3665647+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,531,000 | 2026-07-16T23:07:33.3678774+00:00 | 110.6ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,532,000 | 2026-07-16T23:07:33.3687179+00:00 | 119.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,535,000 | 2026-07-16T23:07:33.3703963+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,538,000 | 2026-07-16T23:07:33.3721027+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,541,000 | 2026-07-16T23:07:33.3795823+00:00 | 113.6ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,542,000 | 2026-07-16T23:07:33.380048+00:00 | 113.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,545,000 | 2026-07-16T23:07:33.3850513+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,548,000 | 2026-07-16T23:07:33.3999162+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,017,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,087,000 | 2026-07-16T23:07:34.9318438+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 985,837 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,423,000 | 2026-07-16T23:07:35.3235919+00:00 | 107.3ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 885,077 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,427,000 | 2026-07-16T23:07:35.3269467+00:00 | 104.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 885,077 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,429,000 | 2026-07-16T23:07:35.3278743+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 885,077 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,433,000 | 2026-07-16T23:07:35.3293806+00:00 | 112.9ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 885,077 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,437,000 | 2026-07-16T23:07:35.331915+00:00 | 120.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 885,077 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,439,000 | 2026-07-16T23:07:35.3329611+00:00 | 130.7ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 885,077 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,443,000 | 2026-07-16T23:07:35.3366829+00:00 | 127.0ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 885,077 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,447,000 | 2026-07-16T23:07:35.3404684+00:00 | 126.7ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 885,077 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,449,000 | 2026-07-16T23:07:35.3424736+00:00 | 124.6ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 885,077 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,453,000 | 2026-07-16T23:07:35.3458902+00:00 | 127.0ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 885,077 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,457,000 | 2026-07-16T23:07:35.347813+00:00 | 125.7ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 885,077 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,459,000 | 2026-07-16T23:07:35.3491173+00:00 | 127.3ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 885,077 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,451,000 | 2026-07-16T23:07:36.4108088+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,452,000 | 2026-07-16T23:07:36.41143+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,455,000 | 2026-07-16T23:07:36.4132887+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,458,000 | 2026-07-16T23:07:36.4143613+00:00 | 107.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,461,000 | 2026-07-16T23:07:36.4157222+00:00 | 115.8ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,462,000 | 2026-07-16T23:07:36.4159759+00:00 | 115.5ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,465,000 | 2026-07-16T23:07:36.4175212+00:00 | 112.5ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,468,000 | 2026-07-16T23:07:36.4184973+00:00 | 111.5ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,471,000 | 2026-07-16T23:07:36.419864+00:00 | 132.3ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,472,000 | 2026-07-16T23:07:36.4205214+00:00 | 131.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,475,000 | 2026-07-16T23:07:36.4215657+00:00 | 109.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,478,000 | 2026-07-16T23:07:36.4235599+00:00 | 116.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,481,000 | 2026-07-16T23:07:36.4253318+00:00 | 127.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,482,000 | 2026-07-16T23:07:36.4258483+00:00 | 126.5ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,485,000 | 2026-07-16T23:07:36.4449329+00:00 | 107.5ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,611,000 | 2026-07-16T23:07:36.6223324+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,621,000 | 2026-07-16T23:07:36.6287343+00:00 | 103.6ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,622,000 | 2026-07-16T23:07:36.6290031+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,631,000 | 2026-07-16T23:07:36.6336523+00:00 | 113.3ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,632,000 | 2026-07-16T23:07:36.6342644+00:00 | 112.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,641,000 | 2026-07-16T23:07:36.6444199+00:00 | 109.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 958,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,272,000 | 2026-07-16T23:07:37.2788357+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 978,239 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,278,000 | 2026-07-16T23:07:37.2817405+00:00 | 111.9ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 978,239 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,281,000 | 2026-07-16T23:07:37.2831969+00:00 | 113.9ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 978,239 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,282,000 | 2026-07-16T23:07:37.2836037+00:00 | 113.5ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 978,239 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,285,000 | 2026-07-16T23:07:37.2893682+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 978,239 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,288,000 | 2026-07-16T23:07:37.2912354+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 978,239 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,291,000 | 2026-07-16T23:07:37.2928699+00:00 | 111.2ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 978,239 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,292,000 | 2026-07-16T23:07:37.2933555+00:00 | 110.7ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 978,239 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,295,000 | 2026-07-16T23:07:37.2953354+00:00 | 103.5ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 978,239 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,298,000 | 2026-07-16T23:07:37.2967306+00:00 | 111.3ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 978,239 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,301,000 | 2026-07-16T23:07:37.3008842+00:00 | 107.1ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 978,239 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,834,000 | 2026-07-16T23:07:37.8574049+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 978,239 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,836,000 | 2026-07-16T23:07:37.8590782+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 978,239 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,332,000 | 2026-07-16T23:07:38.3449801+00:00 | 125.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 921,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,341,000 | 2026-07-16T23:07:38.3579211+00:00 | 114.1ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 921,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,342,000 | 2026-07-16T23:07:38.3587536+00:00 | 113.3ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 921,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,351,000 | 2026-07-16T23:07:38.3651987+00:00 | 113.9ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 921,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,352,000 | 2026-07-16T23:07:38.3655314+00:00 | 113.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 921,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,361,000 | 2026-07-16T23:07:38.3718314+00:00 | 110.3ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 921,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,362,000 | 2026-07-16T23:07:38.3724859+00:00 | 109.5ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 921,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,365,000 | 2026-07-16T23:07:38.3818493+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 921,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,375,000 | 2026-07-16T23:07:38.4023664+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 921,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,404,000 | 2026-07-16T23:07:38.4753357+00:00 | 108.7ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 921,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,406,000 | 2026-07-16T23:07:38.4784501+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 921,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,416,000 | 2026-07-16T23:07:38.4884263+00:00 | 103.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 921,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,434,000 | 2026-07-16T23:07:38.5129315+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 921,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,436,000 | 2026-07-16T23:07:38.5145008+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 921,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,444,000 | 2026-07-16T23:07:38.5196217+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 921,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,446,000 | 2026-07-16T23:07:38.5207687+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 921,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,830,000 | 2026-07-16T23:07:38.9296209+00:00 | 102.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 921,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,274,000 | 2026-07-16T23:07:39.3812742+00:00 | 113.8ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 966,289 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,276,000 | 2026-07-16T23:07:39.3884658+00:00 | 106.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 966,289 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,280,000 | 2026-07-16T23:07:39.3903963+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 966,289 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,284,000 | 2026-07-16T23:07:39.3928019+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 966,289 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,286,000 | 2026-07-16T23:07:39.3937402+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 966,289 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,905,000 | 2026-07-16T23:07:41.8577824+00:00 | 112.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,908,000 | 2026-07-16T23:07:41.8603109+00:00 | 111.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,911,000 | 2026-07-16T23:07:41.8612892+00:00 | 110.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,912,000 | 2026-07-16T23:07:41.8622372+00:00 | 109.3ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,915,000 | 2026-07-16T23:07:41.8639745+00:00 | 109.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,918,000 | 2026-07-16T23:07:41.8653524+00:00 | 107.9ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,921,000 | 2026-07-16T23:07:41.8674355+00:00 | 109.9ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,922,000 | 2026-07-16T23:07:41.867816+00:00 | 109.5ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,925,000 | 2026-07-16T23:07:41.8706003+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,928,000 | 2026-07-16T23:07:41.8726287+00:00 | 107.0ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,931,000 | 2026-07-16T23:07:41.8745888+00:00 | 108.1ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,932,000 | 2026-07-16T23:07:41.8753848+00:00 | 107.3ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,935,000 | 2026-07-16T23:07:41.8772607+00:00 | 115.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,938,000 | 2026-07-16T23:07:41.8793874+00:00 | 113.1ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,941,000 | 2026-07-16T23:07:41.8813315+00:00 | 121.0ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,942,000 | 2026-07-16T23:07:41.8820181+00:00 | 120.3ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,945,000 | 2026-07-16T23:07:41.8841372+00:00 | 113.5ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,948,000 | 2026-07-16T23:07:41.8938448+00:00 | 122.6ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,139,764 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,912,000 | 2026-07-16T23:07:42.8395177+00:00 | 113.7ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 1,061,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,921,000 | 2026-07-16T23:07:42.8549283+00:00 | 109.9ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 1,061,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,922,000 | 2026-07-16T23:07:42.8562632+00:00 | 108.5ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 1,061,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,931,000 | 2026-07-16T23:07:42.8619283+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 1,061,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,932,000 | 2026-07-16T23:07:42.862204+00:00 | 102.8ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 1,061,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,942,000 | 2026-07-16T23:07:42.8678402+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 1,061,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,583,000 | 2026-07-16T23:07:43.4567272+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 976,433 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,587,000 | 2026-07-16T23:07:43.4592961+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 976,433 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,597,000 | 2026-07-16T23:07:43.4692287+00:00 | 100.0ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 976,433 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,995,000 | 2026-07-16T23:07:43.9089107+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 976,433 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,537,000 | 2026-07-16T23:07:54.8971649+00:00 | 108.9ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 1,145,209 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,547,000 | 2026-07-16T23:07:54.9029595+00:00 | 118.8ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 1,145,209 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,549,000 | 2026-07-16T23:07:54.9038214+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 1,145,209 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,553,000 | 2026-07-16T23:07:54.9062385+00:00 | 107.3ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 1,145,209 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,557,000 | 2026-07-16T23:07:54.9081534+00:00 | 114.3ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 1,145,209 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,559,000 | 2026-07-16T23:07:54.9091741+00:00 | 104.6ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 1,145,209 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,563,000 | 2026-07-16T23:07:54.9108916+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 1,145,209 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,567,000 | 2026-07-16T23:07:54.9128443+00:00 | 111.1ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 1,145,209 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 35,705,000 | 2026-07-16T23:08:00.9209733+00:00 | 107.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 34.0s / 1,182,302 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 35,708,000 | 2026-07-16T23:08:00.9220663+00:00 | 106.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 34.0s / 1,182,302 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 35,711,000 | 2026-07-16T23:08:00.9233387+00:00 | 110.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 34.0s / 1,182,302 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 35,712,000 | 2026-07-16T23:08:00.9240448+00:00 | 110.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 34.0s / 1,182,302 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 35,715,000 | 2026-07-16T23:08:00.9250068+00:00 | 103.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 34.0s / 1,182,302 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 35,718,000 | 2026-07-16T23:08:00.9263567+00:00 | 101.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 34.0s / 1,182,302 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,594,000 | 2026-07-16T23:08:08.3933653+00:00 | 108.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 42.0s / 1,148,418 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,596,000 | 2026-07-16T23:08:08.3945497+00:00 | 107.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 42.0s / 1,148,418 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,600,000 | 2026-07-16T23:08:08.4011025+00:00 | 100.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 42.0s / 1,148,418 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,604,000 | 2026-07-16T23:08:08.4037807+00:00 | 104.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 42.0s / 1,148,418 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 102,558,000 | 2026-07-16T23:08:56.4096177+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 90.1s / 1,106,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 448,164,000 | 2026-07-16T23:13:39.4269827+00:00 | 110.3ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 373.3s / 1,195,565 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 448,166,000 | 2026-07-16T23:13:39.4307964+00:00 | 106.5ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 373.3s / 1,195,565 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 448,170,000 | 2026-07-16T23:13:39.433228+00:00 | 104.1ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 373.3s / 1,195,565 msg/s | Gen2 +0 / pause +0.5ms |
| Confluent | 80,320,000 | 2026-07-16T23:23:53.8387882+00:00 | 253.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,322,000 | 2026-07-16T23:23:53.8428335+00:00 | 255.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,323,000 | 2026-07-16T23:23:53.8451327+00:00 | 253.4ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,325,000 | 2026-07-16T23:23:53.8468514+00:00 | 263.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,326,000 | 2026-07-16T23:23:53.8475241+00:00 | 263.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,327,000 | 2026-07-16T23:23:53.8481915+00:00 | 294.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,328,000 | 2026-07-16T23:23:53.84925+00:00 | 293.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,329,000 | 2026-07-16T23:23:53.8497914+00:00 | 263.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,330,000 | 2026-07-16T23:23:53.8503382+00:00 | 248.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,331,000 | 2026-07-16T23:23:53.8508562+00:00 | 292.4ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,332,000 | 2026-07-16T23:23:53.8515583+00:00 | 247.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,333,000 | 2026-07-16T23:23:53.8520585+00:00 | 247.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,334,000 | 2026-07-16T23:23:53.8525552+00:00 | 290.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,335,000 | 2026-07-16T23:23:53.8531295+00:00 | 271.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,336,000 | 2026-07-16T23:23:53.854548+00:00 | 270.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,337,000 | 2026-07-16T23:23:53.8550884+00:00 | 295.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,338,000 | 2026-07-16T23:23:53.8555329+00:00 | 294.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,339,000 | 2026-07-16T23:23:53.8560697+00:00 | 268.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,340,000 | 2026-07-16T23:23:53.8572191+00:00 | 244.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,341,000 | 2026-07-16T23:23:53.8580284+00:00 | 299.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,342,000 | 2026-07-16T23:23:53.8585482+00:00 | 243.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,343,000 | 2026-07-16T23:23:53.859+00:00 | 242.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,344,000 | 2026-07-16T23:23:53.8596421+00:00 | 297.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,345,000 | 2026-07-16T23:23:53.8601131+00:00 | 265.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,346,000 | 2026-07-16T23:23:53.8606511+00:00 | 264.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,347,000 | 2026-07-16T23:23:53.8611355+00:00 | 296.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,348,000 | 2026-07-16T23:23:53.8620058+00:00 | 295.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,349,000 | 2026-07-16T23:23:53.8637533+00:00 | 262.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,350,000 | 2026-07-16T23:23:53.8642142+00:00 | 239.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,351,000 | 2026-07-16T23:23:53.8648078+00:00 | 297.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,352,000 | 2026-07-16T23:23:53.8652734+00:00 | 237.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,353,000 | 2026-07-16T23:23:53.8661556+00:00 | 238.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,354,000 | 2026-07-16T23:23:53.8671237+00:00 | 296.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,355,000 | 2026-07-16T23:23:53.8678612+00:00 | 258.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,356,000 | 2026-07-16T23:23:53.8685673+00:00 | 258.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,357,000 | 2026-07-16T23:23:53.8693853+00:00 | 293.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,358,000 | 2026-07-16T23:23:53.8701935+00:00 | 293.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,359,000 | 2026-07-16T23:23:53.8711518+00:00 | 262.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,360,000 | 2026-07-16T23:23:53.8721107+00:00 | 238.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,361,000 | 2026-07-16T23:23:53.8732631+00:00 | 290.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,362,000 | 2026-07-16T23:23:53.8745341+00:00 | 235.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,363,000 | 2026-07-16T23:23:53.875657+00:00 | 234.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,364,000 | 2026-07-16T23:23:53.8767811+00:00 | 294.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,365,000 | 2026-07-16T23:23:53.8778994+00:00 | 255.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,366,000 | 2026-07-16T23:23:53.8792178+00:00 | 254.4ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,367,000 | 2026-07-16T23:23:53.8803246+00:00 | 291.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,368,000 | 2026-07-16T23:23:53.8814327+00:00 | 291.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,369,000 | 2026-07-16T23:23:53.882546+00:00 | 251.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,370,000 | 2026-07-16T23:23:53.8837649+00:00 | 230.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,371,000 | 2026-07-16T23:23:53.8848696+00:00 | 288.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,372,000 | 2026-07-16T23:23:53.8860527+00:00 | 227.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,373,000 | 2026-07-16T23:23:53.8871544+00:00 | 226.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,374,000 | 2026-07-16T23:23:53.8883817+00:00 | 284.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,375,000 | 2026-07-16T23:23:53.8894828+00:00 | 244.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,376,000 | 2026-07-16T23:23:53.8905817+00:00 | 243.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,377,000 | 2026-07-16T23:23:53.8917205+00:00 | 290.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,378,000 | 2026-07-16T23:23:53.8929924+00:00 | 289.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,379,000 | 2026-07-16T23:23:53.8941679+00:00 | 255.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,380,000 | 2026-07-16T23:23:53.8953293+00:00 | 253.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,381,000 | 2026-07-16T23:23:53.8964336+00:00 | 286.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,382,000 | 2026-07-16T23:23:53.8975525+00:00 | 246.4ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,383,000 | 2026-07-16T23:23:53.898782+00:00 | 250.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,384,000 | 2026-07-16T23:23:53.899895+00:00 | 273.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,385,000 | 2026-07-16T23:23:53.9010074+00:00 | 263.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,386,000 | 2026-07-16T23:23:53.9022636+00:00 | 261.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,387,000 | 2026-07-16T23:23:53.9035067+00:00 | 291.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,388,000 | 2026-07-16T23:23:53.9046208+00:00 | 290.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,389,000 | 2026-07-16T23:23:53.9057293+00:00 | 259.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,390,000 | 2026-07-16T23:23:53.906836+00:00 | 243.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,391,000 | 2026-07-16T23:23:53.9080698+00:00 | 288.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,392,000 | 2026-07-16T23:23:53.909173+00:00 | 240.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,393,000 | 2026-07-16T23:23:53.9103285+00:00 | 239.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,394,000 | 2026-07-16T23:23:53.9114265+00:00 | 272.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,395,000 | 2026-07-16T23:23:53.912651+00:00 | 261.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,396,000 | 2026-07-16T23:23:53.9137585+00:00 | 260.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,397,000 | 2026-07-16T23:23:53.9148553+00:00 | 287.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,398,000 | 2026-07-16T23:23:53.9159436+00:00 | 285.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,399,000 | 2026-07-16T23:23:53.9170482+00:00 | 257.4ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,400,000 | 2026-07-16T23:23:53.9183401+00:00 | 240.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,401,000 | 2026-07-16T23:23:53.9194278+00:00 | 282.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,402,000 | 2026-07-16T23:23:53.9205199+00:00 | 238.4ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,403,000 | 2026-07-16T23:23:53.9216168+00:00 | 236.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,404,000 | 2026-07-16T23:23:53.9228271+00:00 | 278.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,405,000 | 2026-07-16T23:23:53.9239194+00:00 | 251.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,406,000 | 2026-07-16T23:23:53.9250171+00:00 | 250.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,407,000 | 2026-07-16T23:23:53.926159+00:00 | 283.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,408,000 | 2026-07-16T23:23:53.9273776+00:00 | 282.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,409,000 | 2026-07-16T23:23:53.9284664+00:00 | 254.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,410,000 | 2026-07-16T23:23:53.9295515+00:00 | 229.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,411,000 | 2026-07-16T23:23:53.9306387+00:00 | 279.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,412,000 | 2026-07-16T23:23:53.931852+00:00 | 230.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,413,000 | 2026-07-16T23:23:53.9330248+00:00 | 229.4ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,414,000 | 2026-07-16T23:23:53.9342168+00:00 | 268.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,415,000 | 2026-07-16T23:23:53.9353342+00:00 | 259.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,416,000 | 2026-07-16T23:23:53.9364976+00:00 | 258.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,417,000 | 2026-07-16T23:23:53.9379503+00:00 | 281.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,418,000 | 2026-07-16T23:23:53.9390562+00:00 | 280.4ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,419,000 | 2026-07-16T23:23:53.9401562+00:00 | 255.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,420,000 | 2026-07-16T23:23:53.9412527+00:00 | 231.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,421,000 | 2026-07-16T23:23:53.9425413+00:00 | 286.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,422,000 | 2026-07-16T23:23:53.9436886+00:00 | 241.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,423,000 | 2026-07-16T23:23:53.9447781+00:00 | 240.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,424,000 | 2026-07-16T23:23:53.9458727+00:00 | 272.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,425,000 | 2026-07-16T23:23:53.947098+00:00 | 253.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,426,000 | 2026-07-16T23:23:53.948188+00:00 | 252.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,427,000 | 2026-07-16T23:23:53.9493341+00:00 | 280.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,428,000 | 2026-07-16T23:23:53.9504259+00:00 | 279.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,429,000 | 2026-07-16T23:23:53.9516385+00:00 | 249.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,430,000 | 2026-07-16T23:23:53.9529085+00:00 | 233.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,431,000 | 2026-07-16T23:23:53.9540994+00:00 | 291.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,432,000 | 2026-07-16T23:23:53.9551935+00:00 | 241.4ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,433,000 | 2026-07-16T23:23:53.9563387+00:00 | 237.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,434,000 | 2026-07-16T23:23:53.9584227+00:00 | 261.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,435,000 | 2026-07-16T23:23:53.9595569+00:00 | 249.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,436,000 | 2026-07-16T23:23:53.9606375+00:00 | 248.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,437,000 | 2026-07-16T23:23:53.9617746+00:00 | 284.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,438,000 | 2026-07-16T23:23:53.9666849+00:00 | 279.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,439,000 | 2026-07-16T23:23:53.9677918+00:00 | 241.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,440,000 | 2026-07-16T23:23:53.9689413+00:00 | 231.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,441,000 | 2026-07-16T23:23:53.9700919+00:00 | 276.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,442,000 | 2026-07-16T23:23:53.9754192+00:00 | 225.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,443,000 | 2026-07-16T23:23:53.9765403+00:00 | 241.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,444,000 | 2026-07-16T23:23:53.9776418+00:00 | 251.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,445,000 | 2026-07-16T23:23:53.9794808+00:00 | 231.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,446,000 | 2026-07-16T23:23:53.9816087+00:00 | 229.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,447,000 | 2026-07-16T23:23:53.9827023+00:00 | 273.4ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,448,000 | 2026-07-16T23:23:53.9837896+00:00 | 273.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,449,000 | 2026-07-16T23:23:53.9848708+00:00 | 226.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,450,000 | 2026-07-16T23:23:53.9860035+00:00 | 245.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,451,000 | 2026-07-16T23:23:53.9909453+00:00 | 266.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,452,000 | 2026-07-16T23:23:53.9920473+00:00 | 226.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,453,000 | 2026-07-16T23:23:53.993331+00:00 | 253.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,454,000 | 2026-07-16T23:23:53.9945732+00:00 | 251.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,455,000 | 2026-07-16T23:23:53.9996657+00:00 | 217.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,456,000 | 2026-07-16T23:23:54.000765+00:00 | 216.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,457,000 | 2026-07-16T23:23:54.0018498+00:00 | 270.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,458,000 | 2026-07-16T23:23:54.0030956+00:00 | 269.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,459,000 | 2026-07-16T23:23:54.0051171+00:00 | 212.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,460,000 | 2026-07-16T23:23:54.0062084+00:00 | 264.4ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,461,000 | 2026-07-16T23:23:54.0073395+00:00 | 287.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,462,000 | 2026-07-16T23:23:54.0084707+00:00 | 261.4ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,463,000 | 2026-07-16T23:23:54.0104525+00:00 | 260.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,464,000 | 2026-07-16T23:23:54.0115476+00:00 | 245.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,465,000 | 2026-07-16T23:23:54.012633+00:00 | 205.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,466,000 | 2026-07-16T23:23:54.0137174+00:00 | 216.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,467,000 | 2026-07-16T23:23:54.01483+00:00 | 280.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,468,000 | 2026-07-16T23:23:54.0168278+00:00 | 279.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,469,000 | 2026-07-16T23:23:54.0179107+00:00 | 212.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,470,000 | 2026-07-16T23:23:54.0190443+00:00 | 266.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,471,000 | 2026-07-16T23:23:54.0201216+00:00 | 284.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,472,000 | 2026-07-16T23:23:54.0256441+00:00 | 259.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,473,000 | 2026-07-16T23:23:54.0267315+00:00 | 274.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,474,000 | 2026-07-16T23:23:54.0278129+00:00 | 244.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,475,000 | 2026-07-16T23:23:54.0289854+00:00 | 201.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,476,000 | 2026-07-16T23:23:54.0309562+00:00 | 200.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,477,000 | 2026-07-16T23:23:54.0320597+00:00 | 277.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,478,000 | 2026-07-16T23:23:54.0331346+00:00 | 276.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,479,000 | 2026-07-16T23:23:54.0342629+00:00 | 213.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,480,000 | 2026-07-16T23:23:54.04012+00:00 | 265.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,481,000 | 2026-07-16T23:23:54.0411958+00:00 | 268.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,482,000 | 2026-07-16T23:23:54.0423356+00:00 | 260.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,483,000 | 2026-07-16T23:23:54.0434533+00:00 | 267.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,484,000 | 2026-07-16T23:23:54.0445297+00:00 | 251.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,485,000 | 2026-07-16T23:23:54.0465172+00:00 | 209.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,486,000 | 2026-07-16T23:23:54.0475931+00:00 | 208.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,487,000 | 2026-07-16T23:23:54.0486627+00:00 | 263.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,488,000 | 2026-07-16T23:23:54.0497752+00:00 | 262.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,489,000 | 2026-07-16T23:23:54.0518144+00:00 | 203.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,490,000 | 2026-07-16T23:23:54.0528945+00:00 | 265.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,491,000 | 2026-07-16T23:23:54.0539427+00:00 | 258.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,492,000 | 2026-07-16T23:23:54.0551072+00:00 | 258.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,493,000 | 2026-07-16T23:23:54.057106+00:00 | 261.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,494,000 | 2026-07-16T23:23:54.0581798+00:00 | 251.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,495,000 | 2026-07-16T23:23:54.0592531+00:00 | 202.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,496,000 | 2026-07-16T23:23:54.0604306+00:00 | 201.4ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,497,000 | 2026-07-16T23:23:54.0661491+00:00 | 247.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,498,000 | 2026-07-16T23:23:54.067227+00:00 | 246.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,499,000 | 2026-07-16T23:23:54.0683016+00:00 | 193.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,500,000 | 2026-07-16T23:23:54.069482+00:00 | 278.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,501,000 | 2026-07-16T23:23:54.071068+00:00 | 244.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,502,000 | 2026-07-16T23:23:54.0715845+00:00 | 254.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,503,000 | 2026-07-16T23:23:54.0721161+00:00 | 275.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,504,000 | 2026-07-16T23:23:54.0726272+00:00 | 238.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,505,000 | 2026-07-16T23:23:54.0731565+00:00 | 197.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,506,000 | 2026-07-16T23:23:54.0753106+00:00 | 195.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,507,000 | 2026-07-16T23:23:54.0758226+00:00 | 240.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,508,000 | 2026-07-16T23:23:54.0763313+00:00 | 240.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,509,000 | 2026-07-16T23:23:54.0767742+00:00 | 194.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,510,000 | 2026-07-16T23:23:54.0791821+00:00 | 268.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,511,000 | 2026-07-16T23:23:54.079696+00:00 | 237.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,512,000 | 2026-07-16T23:23:54.0802186+00:00 | 267.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,513,000 | 2026-07-16T23:23:54.0807379+00:00 | 268.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,514,000 | 2026-07-16T23:23:54.0830735+00:00 | 229.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,515,000 | 2026-07-16T23:23:54.0835899+00:00 | 188.4ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,516,000 | 2026-07-16T23:23:54.084131+00:00 | 187.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,517,000 | 2026-07-16T23:23:54.0846477+00:00 | 233.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,518,000 | 2026-07-16T23:23:54.0869299+00:00 | 231.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,519,000 | 2026-07-16T23:23:54.0877201+00:00 | 198.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,520,000 | 2026-07-16T23:23:54.0881583+00:00 | 261.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,521,000 | 2026-07-16T23:23:54.088679+00:00 | 229.4ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,522,000 | 2026-07-16T23:23:54.0891196+00:00 | 259.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,523,000 | 2026-07-16T23:23:54.0907717+00:00 | 258.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,524,000 | 2026-07-16T23:23:54.0916226+00:00 | 224.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,525,000 | 2026-07-16T23:23:54.0922069+00:00 | 194.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,526,000 | 2026-07-16T23:23:54.0931555+00:00 | 193.4ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,527,000 | 2026-07-16T23:23:54.0938525+00:00 | 228.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,528,000 | 2026-07-16T23:23:54.0944331+00:00 | 227.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,529,000 | 2026-07-16T23:23:54.0948932+00:00 | 191.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,530,000 | 2026-07-16T23:23:54.0954578+00:00 | 254.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,531,000 | 2026-07-16T23:23:54.0973643+00:00 | 225.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,532,000 | 2026-07-16T23:23:54.0997537+00:00 | 249.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,533,000 | 2026-07-16T23:23:54.101706+00:00 | 248.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,534,000 | 2026-07-16T23:23:54.1034981+00:00 | 213.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,535,000 | 2026-07-16T23:23:54.1056018+00:00 | 181.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,536,000 | 2026-07-16T23:23:54.1069121+00:00 | 180.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,537,000 | 2026-07-16T23:23:54.1082608+00:00 | 216.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,538,000 | 2026-07-16T23:23:54.1095032+00:00 | 215.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,539,000 | 2026-07-16T23:23:54.1122609+00:00 | 175.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,540,000 | 2026-07-16T23:23:54.114853+00:00 | 235.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,541,000 | 2026-07-16T23:23:54.1160439+00:00 | 209.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,542,000 | 2026-07-16T23:23:54.1172926+00:00 | 233.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,543,000 | 2026-07-16T23:23:54.1281509+00:00 | 223.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,544,000 | 2026-07-16T23:23:54.1295353+00:00 | 188.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,545,000 | 2026-07-16T23:23:54.1349326+00:00 | 156.6ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,546,000 | 2026-07-16T23:23:54.1384287+00:00 | 153.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,547,000 | 2026-07-16T23:23:54.1396214+00:00 | 186.8ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,548,000 | 2026-07-16T23:23:54.1413092+00:00 | 185.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,549,000 | 2026-07-16T23:23:54.1444674+00:00 | 151.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,550,000 | 2026-07-16T23:23:54.1501627+00:00 | 202.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,551,000 | 2026-07-16T23:23:54.1514181+00:00 | 175.1ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,552,000 | 2026-07-16T23:23:54.1556139+00:00 | 196.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,553,000 | 2026-07-16T23:23:54.1614016+00:00 | 193.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,554,000 | 2026-07-16T23:23:54.1658619+00:00 | 157.0ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,555,000 | 2026-07-16T23:23:54.1702688+00:00 | 126.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,556,000 | 2026-07-16T23:23:54.1758653+00:00 | 121.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,557,000 | 2026-07-16T23:23:54.1802785+00:00 | 151.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,558,000 | 2026-07-16T23:23:54.1861853+00:00 | 145.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,559,000 | 2026-07-16T23:23:54.188292+00:00 | 109.5ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,560,000 | 2026-07-16T23:23:54.1897175+00:00 | 167.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,561,000 | 2026-07-16T23:23:54.1983449+00:00 | 133.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,562,000 | 2026-07-16T23:23:54.203293+00:00 | 151.2ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,563,000 | 2026-07-16T23:23:54.2080958+00:00 | 149.7ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,564,000 | 2026-07-16T23:23:54.2131614+00:00 | 111.9ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 80,570,000 | 2026-07-16T23:23:54.2522382+00:00 | 106.3ms | GC pause | - | - | 87.1s / 767,362 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 211,590,000 | 2026-07-16T23:26:11.9191021+00:00 | 108.6ms | GC pause | - | - | 225.2s / 785,027 msg/s | Gen2 +0 / pause +271.5ms |
| Confluent | 211,592,000 | 2026-07-16T23:26:11.9208311+00:00 | 110.4ms | GC pause | - | - | 225.2s / 785,027 msg/s | Gen2 +0 / pause +271.5ms |
| Confluent | 211,593,000 | 2026-07-16T23:26:11.9213238+00:00 | 110.1ms | GC pause | - | - | 225.2s / 785,027 msg/s | Gen2 +0 / pause +271.5ms |
| Confluent | 211,600,000 | 2026-07-16T23:26:11.9283843+00:00 | 119.3ms | GC pause | - | - | 225.2s / 785,027 msg/s | Gen2 +0 / pause +271.5ms |
| Confluent | 211,602,000 | 2026-07-16T23:26:11.9297561+00:00 | 118.1ms | GC pause | - | - | 225.2s / 785,027 msg/s | Gen2 +0 / pause +271.5ms |
| Confluent | 211,603,000 | 2026-07-16T23:26:11.9303279+00:00 | 117.6ms | GC pause | - | - | 225.2s / 785,027 msg/s | Gen2 +0 / pause +271.5ms |
| Confluent | 211,610,000 | 2026-07-16T23:26:11.9357064+00:00 | 112.8ms | GC pause | - | - | 225.2s / 785,027 msg/s | Gen2 +0 / pause +271.5ms |
| Confluent | 211,612,000 | 2026-07-16T23:26:11.9368622+00:00 | 115.0ms | GC pause | - | - | 225.2s / 785,027 msg/s | Gen2 +0 / pause +271.5ms |
| Confluent | 211,613,000 | 2026-07-16T23:26:11.9374415+00:00 | 111.5ms | GC pause | - | - | 225.2s / 785,027 msg/s | Gen2 +0 / pause +271.5ms |
| Confluent | 211,620,000 | 2026-07-16T23:26:11.9427386+00:00 | 108.6ms | GC pause | - | - | 225.2s / 785,027 msg/s | Gen2 +0 / pause +271.5ms |
| Confluent | 211,623,000 | 2026-07-16T23:26:11.9454439+00:00 | 106.2ms | GC pause | - | - | 225.2s / 785,027 msg/s | Gen2 +0 / pause +271.5ms |
| Confluent | 211,630,000 | 2026-07-16T23:26:11.949844+00:00 | 102.7ms | GC pause | - | - | 225.2s / 785,027 msg/s | Gen2 +0 / pause +271.5ms |
| Dekaf (3conn) | 507,000 | 2026-07-16T23:37:41.8951718+00:00 | 157.8ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 509,000 | 2026-07-16T23:37:41.8969109+00:00 | 156.0ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 513,000 | 2026-07-16T23:37:41.8984323+00:00 | 154.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 517,000 | 2026-07-16T23:37:41.9007563+00:00 | 152.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 519,000 | 2026-07-16T23:37:41.9019247+00:00 | 151.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 523,000 | 2026-07-16T23:37:41.9045643+00:00 | 148.4ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 527,000 | 2026-07-16T23:37:41.9075947+00:00 | 145.4ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 529,000 | 2026-07-16T23:37:41.9089328+00:00 | 144.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 533,000 | 2026-07-16T23:37:41.912101+00:00 | 150.9ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 537,000 | 2026-07-16T23:37:41.9157383+00:00 | 137.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 539,000 | 2026-07-16T23:37:41.9173688+00:00 | 149.7ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 543,000 | 2026-07-16T23:37:41.9241462+00:00 | 142.9ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 547,000 | 2026-07-16T23:37:41.9272072+00:00 | 169.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 549,000 | 2026-07-16T23:37:41.9284776+00:00 | 138.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 553,000 | 2026-07-16T23:37:41.9319765+00:00 | 140.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 557,000 | 2026-07-16T23:37:41.9344809+00:00 | 162.3ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 559,000 | 2026-07-16T23:37:41.935677+00:00 | 136.9ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 563,000 | 2026-07-16T23:37:41.9383309+00:00 | 134.8ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 567,000 | 2026-07-16T23:37:41.9409215+00:00 | 163.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 569,000 | 2026-07-16T23:37:41.9422676+00:00 | 136.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 573,000 | 2026-07-16T23:37:41.9445979+00:00 | 134.3ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 577,000 | 2026-07-16T23:37:41.9480252+00:00 | 172.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 579,000 | 2026-07-16T23:37:41.9504531+00:00 | 128.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 583,000 | 2026-07-16T23:37:41.9543317+00:00 | 124.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 587,000 | 2026-07-16T23:37:41.9568205+00:00 | 192.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 589,000 | 2026-07-16T23:37:41.9578709+00:00 | 121.0ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 593,000 | 2026-07-16T23:37:41.9606168+00:00 | 120.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 594,000 | 2026-07-16T23:37:41.961044+00:00 | 105.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 596,000 | 2026-07-16T23:37:41.9631192+00:00 | 103.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 597,000 | 2026-07-16T23:37:41.963768+00:00 | 198.0ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 599,000 | 2026-07-16T23:37:41.9649199+00:00 | 115.9ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 603,000 | 2026-07-16T23:37:41.9675204+00:00 | 113.3ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 604,000 | 2026-07-16T23:37:41.9681758+00:00 | 104.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 606,000 | 2026-07-16T23:37:41.9694601+00:00 | 103.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 607,000 | 2026-07-16T23:37:41.9700547+00:00 | 191.7ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 609,000 | 2026-07-16T23:37:41.9721631+00:00 | 113.9ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 613,000 | 2026-07-16T23:37:41.9746747+00:00 | 111.4ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 617,000 | 2026-07-16T23:37:41.9773163+00:00 | 193.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 619,000 | 2026-07-16T23:37:41.9791917+00:00 | 112.0ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 623,000 | 2026-07-16T23:37:41.9822439+00:00 | 109.0ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 627,000 | 2026-07-16T23:37:41.984812+00:00 | 185.7ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 629,000 | 2026-07-16T23:37:41.986719+00:00 | 110.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 633,000 | 2026-07-16T23:37:41.9906392+00:00 | 106.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 637,000 | 2026-07-16T23:37:41.9929724+00:00 | 177.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 639,000 | 2026-07-16T23:37:41.9942544+00:00 | 102.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 643,000 | 2026-07-16T23:37:41.996604+00:00 | 100.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 644,000 | 2026-07-16T23:37:41.9974415+00:00 | 116.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 646,000 | 2026-07-16T23:37:41.9996006+00:00 | 114.0ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 647,000 | 2026-07-16T23:37:42.0003232+00:00 | 177.4ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +3 / pause +3.4ms |
| Dekaf (3conn) | 657,000 | 2026-07-16T23:37:42.0590486+00:00 | 129.9ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 667,000 | 2026-07-16T23:37:42.0674432+00:00 | 134.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 677,000 | 2026-07-16T23:37:42.0735438+00:00 | 128.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 687,000 | 2026-07-16T23:37:42.0804032+00:00 | 144.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 697,000 | 2026-07-16T23:37:42.0862956+00:00 | 142.7ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 707,000 | 2026-07-16T23:37:42.0926854+00:00 | 136.3ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 717,000 | 2026-07-16T23:37:42.0995914+00:00 | 151.0ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 727,000 | 2026-07-16T23:37:42.1052294+00:00 | 145.4ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 737,000 | 2026-07-16T23:37:42.1114291+00:00 | 139.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 747,000 | 2026-07-16T23:37:42.1180663+00:00 | 141.4ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 757,000 | 2026-07-16T23:37:42.125357+00:00 | 134.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 767,000 | 2026-07-16T23:37:42.1539045+00:00 | 105.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 777,000 | 2026-07-16T23:37:42.1607935+00:00 | 112.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 787,000 | 2026-07-16T23:37:42.1676137+00:00 | 105.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 797,000 | 2026-07-16T23:37:42.1750486+00:00 | 105.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 817,000 | 2026-07-16T23:37:42.1907551+00:00 | 146.3ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 827,000 | 2026-07-16T23:37:42.1977426+00:00 | 139.3ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 837,000 | 2026-07-16T23:37:42.2051977+00:00 | 136.8ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 847,000 | 2026-07-16T23:37:42.2129414+00:00 | 129.0ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 857,000 | 2026-07-16T23:37:42.2201576+00:00 | 137.9ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 867,000 | 2026-07-16T23:37:42.2284512+00:00 | 129.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 877,000 | 2026-07-16T23:37:42.2377194+00:00 | 158.3ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 887,000 | 2026-07-16T23:37:42.2455002+00:00 | 150.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 897,000 | 2026-07-16T23:37:42.2547695+00:00 | 157.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 907,000 | 2026-07-16T23:37:42.2660821+00:00 | 147.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 917,000 | 2026-07-16T23:37:42.2743472+00:00 | 145.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 927,000 | 2026-07-16T23:37:42.2824239+00:00 | 137.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 937,000 | 2026-07-16T23:37:42.288786+00:00 | 137.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 947,000 | 2026-07-16T23:37:42.2966874+00:00 | 154.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 957,000 | 2026-07-16T23:37:42.3074334+00:00 | 143.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 967,000 | 2026-07-16T23:37:42.31488+00:00 | 136.0ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 977,000 | 2026-07-16T23:37:42.3233545+00:00 | 153.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 987,000 | 2026-07-16T23:37:42.3345002+00:00 | 154.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 997,000 | 2026-07-16T23:37:42.3481602+00:00 | 140.4ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,007,000 | 2026-07-16T23:37:42.3544784+00:00 | 154.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,017,000 | 2026-07-16T23:37:42.3662406+00:00 | 149.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,027,000 | 2026-07-16T23:37:42.3724909+00:00 | 143.3ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,037,000 | 2026-07-16T23:37:42.3811715+00:00 | 134.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,040,000 | 2026-07-16T23:37:42.382744+00:00 | 111.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,047,000 | 2026-07-16T23:37:42.387167+00:00 | 161.9ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,050,000 | 2026-07-16T23:37:42.3892094+00:00 | 136.4ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,054,000 | 2026-07-16T23:37:42.3917921+00:00 | 102.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,056,000 | 2026-07-16T23:37:42.3935008+00:00 | 100.8ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,057,000 | 2026-07-16T23:37:42.3963852+00:00 | 169.0ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,060,000 | 2026-07-16T23:37:42.3974228+00:00 | 128.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,064,000 | 2026-07-16T23:37:42.4031836+00:00 | 122.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,066,000 | 2026-07-16T23:37:42.4045369+00:00 | 121.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,067,000 | 2026-07-16T23:37:42.405039+00:00 | 176.0ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,069,000 | 2026-07-16T23:37:42.4061206+00:00 | 119.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,070,000 | 2026-07-16T23:37:42.4063635+00:00 | 122.4ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,073,000 | 2026-07-16T23:37:42.4109319+00:00 | 114.7ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,074,000 | 2026-07-16T23:37:42.4113969+00:00 | 114.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,076,000 | 2026-07-16T23:37:42.4141194+00:00 | 114.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,077,000 | 2026-07-16T23:37:42.4148163+00:00 | 188.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,080,000 | 2026-07-16T23:37:42.4163877+00:00 | 112.4ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,083,000 | 2026-07-16T23:37:42.4183675+00:00 | 134.0ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,084,000 | 2026-07-16T23:37:42.4187584+00:00 | 110.0ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,086,000 | 2026-07-16T23:37:42.4206387+00:00 | 108.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,087,000 | 2026-07-16T23:37:42.421401+00:00 | 209.7ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,089,000 | 2026-07-16T23:37:42.4222666+00:00 | 126.8ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,090,000 | 2026-07-16T23:37:42.4225795+00:00 | 124.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,093,000 | 2026-07-16T23:37:42.425571+00:00 | 123.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,094,000 | 2026-07-16T23:37:42.4260716+00:00 | 102.7ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,096,000 | 2026-07-16T23:37:42.4275062+00:00 | 101.3ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,097,000 | 2026-07-16T23:37:42.4280681+00:00 | 203.9ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,099,000 | 2026-07-16T23:37:42.4303354+00:00 | 118.7ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,100,000 | 2026-07-16T23:37:42.430688+00:00 | 121.7ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,103,000 | 2026-07-16T23:37:42.4323625+00:00 | 116.7ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,107,000 | 2026-07-16T23:37:42.4372287+00:00 | 199.3ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,109,000 | 2026-07-16T23:37:42.4398543+00:00 | 109.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,110,000 | 2026-07-16T23:37:42.4419584+00:00 | 119.7ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,113,000 | 2026-07-16T23:37:42.4482474+00:00 | 116.9ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,117,000 | 2026-07-16T23:37:42.4528528+00:00 | 186.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,119,000 | 2026-07-16T23:37:42.4539837+00:00 | 111.4ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,120,000 | 2026-07-16T23:37:42.4553529+00:00 | 106.4ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,123,000 | 2026-07-16T23:37:42.459813+00:00 | 121.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,127,000 | 2026-07-16T23:37:42.4630628+00:00 | 183.0ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,129,000 | 2026-07-16T23:37:42.4642723+00:00 | 118.8ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,133,000 | 2026-07-16T23:37:42.4699652+00:00 | 113.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,137,000 | 2026-07-16T23:37:42.4723201+00:00 | 173.7ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,139,000 | 2026-07-16T23:37:42.4733354+00:00 | 133.0ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,143,000 | 2026-07-16T23:37:42.4766002+00:00 | 129.7ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,147,000 | 2026-07-16T23:37:42.4798315+00:00 | 201.4ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,149,000 | 2026-07-16T23:37:42.48166+00:00 | 124.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,153,000 | 2026-07-16T23:37:42.485215+00:00 | 124.8ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,157,000 | 2026-07-16T23:37:42.4878001+00:00 | 193.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,159,000 | 2026-07-16T23:37:42.4898611+00:00 | 120.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,163,000 | 2026-07-16T23:37:42.492157+00:00 | 117.8ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,167,000 | 2026-07-16T23:37:42.4962785+00:00 | 190.8ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,169,000 | 2026-07-16T23:37:42.4967889+00:00 | 113.2ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,173,000 | 2026-07-16T23:37:42.4994239+00:00 | 114.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,177,000 | 2026-07-16T23:37:42.5065218+00:00 | 180.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,179,000 | 2026-07-16T23:37:42.5090743+00:00 | 104.4ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,183,000 | 2026-07-16T23:37:42.5118087+00:00 | 106.9ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,187,000 | 2026-07-16T23:37:42.518552+00:00 | 168.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,189,000 | 2026-07-16T23:37:42.519902+00:00 | 111.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,193,000 | 2026-07-16T23:37:42.5213729+00:00 | 109.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,197,000 | 2026-07-16T23:37:42.5316909+00:00 | 155.4ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,207,000 | 2026-07-16T23:37:42.5493361+00:00 | 137.7ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,217,000 | 2026-07-16T23:37:42.5555576+00:00 | 131.5ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,227,000 | 2026-07-16T23:37:42.5677095+00:00 | 121.6ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,237,000 | 2026-07-16T23:37:42.5826248+00:00 | 111.4ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,247,000 | 2026-07-16T23:37:42.5958479+00:00 | 106.1ms | GC pause | - | - | 2.0s / 1,188,770 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 1,817,000 | 2026-07-16T23:37:43.00945+00:00 | 101.0ms | GC pause | - | - | 3.0s / 1,398,031 msg/s | Gen2 +1 / pause +2.1ms |
| Dekaf (3conn) | 3,077,000 | 2026-07-16T23:37:43.9187316+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,398,031 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,097,000 | 2026-07-16T23:37:43.9299766+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,107,000 | 2026-07-16T23:37:43.9355816+00:00 | 114.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,117,000 | 2026-07-16T23:37:43.9411822+00:00 | 109.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,127,000 | 2026-07-16T23:37:43.9466826+00:00 | 122.5ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,137,000 | 2026-07-16T23:37:43.9509043+00:00 | 186.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,147,000 | 2026-07-16T23:37:43.957102+00:00 | 182.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,157,000 | 2026-07-16T23:37:43.9616591+00:00 | 184.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,167,000 | 2026-07-16T23:37:43.9670025+00:00 | 179.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,177,000 | 2026-07-16T23:37:43.9737159+00:00 | 179.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,187,000 | 2026-07-16T23:37:43.9802206+00:00 | 172.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,193,000 | 2026-07-16T23:37:43.9848079+00:00 | 118.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,197,000 | 2026-07-16T23:37:43.9874881+00:00 | 169.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,199,000 | 2026-07-16T23:37:43.9885922+00:00 | 114.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,203,000 | 2026-07-16T23:37:43.9932681+00:00 | 109.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,207,000 | 2026-07-16T23:37:43.9948386+00:00 | 165.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,209,000 | 2026-07-16T23:37:43.996181+00:00 | 107.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,213,000 | 2026-07-16T23:37:43.9983097+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,217,000 | 2026-07-16T23:37:44.0007704+00:00 | 159.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,219,000 | 2026-07-16T23:37:44.0017297+00:00 | 110.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,223,000 | 2026-07-16T23:37:44.0041355+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,227,000 | 2026-07-16T23:37:44.006825+00:00 | 160.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,229,000 | 2026-07-16T23:37:44.007674+00:00 | 109.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,237,000 | 2026-07-16T23:37:44.0245208+00:00 | 143.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,247,000 | 2026-07-16T23:37:44.0299657+00:00 | 143.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,249,000 | 2026-07-16T23:37:44.0310756+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,257,000 | 2026-07-16T23:37:44.0433195+00:00 | 129.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,267,000 | 2026-07-16T23:37:44.0491857+00:00 | 135.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,277,000 | 2026-07-16T23:37:44.0563035+00:00 | 144.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,287,000 | 2026-07-16T23:37:44.0612239+00:00 | 144.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,297,000 | 2026-07-16T23:37:44.0722282+00:00 | 133.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,307,000 | 2026-07-16T23:37:44.0778297+00:00 | 135.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,317,000 | 2026-07-16T23:37:44.1077414+00:00 | 113.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,327,000 | 2026-07-16T23:37:44.1176532+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,717,000 | 2026-07-16T23:37:44.4393668+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,727,000 | 2026-07-16T23:37:44.4445399+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,737,000 | 2026-07-16T23:37:44.4494395+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,747,000 | 2026-07-16T23:37:44.455513+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,357,000 | 2026-07-16T23:37:44.8502237+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,361,000 | 2026-07-16T23:37:44.8526721+00:00 | 112.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,362,000 | 2026-07-16T23:37:44.8531513+00:00 | 112.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,367,000 | 2026-07-16T23:37:44.8567756+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,371,000 | 2026-07-16T23:37:44.8590505+00:00 | 116.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,372,000 | 2026-07-16T23:37:44.8595151+00:00 | 116.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,379,000 | 2026-07-16T23:37:44.8633233+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,381,000 | 2026-07-16T23:37:44.8642237+00:00 | 112.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,382,000 | 2026-07-16T23:37:44.8644787+00:00 | 112.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,383,000 | 2026-07-16T23:37:44.8649471+00:00 | 111.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,387,000 | 2026-07-16T23:37:44.8669753+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,389,000 | 2026-07-16T23:37:44.8680519+00:00 | 107.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,391,000 | 2026-07-16T23:37:44.8691073+00:00 | 140.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,392,000 | 2026-07-16T23:37:44.869531+00:00 | 139.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,393,000 | 2026-07-16T23:37:44.8703309+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,397,000 | 2026-07-16T23:37:44.8718646+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,399,000 | 2026-07-16T23:37:44.8732401+00:00 | 121.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,401,000 | 2026-07-16T23:37:44.8790883+00:00 | 137.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,402,000 | 2026-07-16T23:37:44.8794373+00:00 | 136.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,403,000 | 2026-07-16T23:37:44.8797456+00:00 | 114.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,407,000 | 2026-07-16T23:37:44.8863128+00:00 | 108.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,409,000 | 2026-07-16T23:37:44.8871458+00:00 | 107.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,411,000 | 2026-07-16T23:37:44.891916+00:00 | 124.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,412,000 | 2026-07-16T23:37:44.8929414+00:00 | 123.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,342,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,421,000 | 2026-07-16T23:37:44.9035891+00:00 | 123.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,422,000 | 2026-07-16T23:37:44.9041483+00:00 | 123.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,431,000 | 2026-07-16T23:37:44.9120325+00:00 | 120.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,432,000 | 2026-07-16T23:37:44.9122875+00:00 | 120.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,437,000 | 2026-07-16T23:37:44.9170478+00:00 | 120.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,441,000 | 2026-07-16T23:37:44.9185005+00:00 | 114.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,442,000 | 2026-07-16T23:37:44.9190538+00:00 | 113.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,447,000 | 2026-07-16T23:37:44.9210241+00:00 | 124.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,451,000 | 2026-07-16T23:37:44.9228398+00:00 | 121.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,452,000 | 2026-07-16T23:37:44.9230915+00:00 | 121.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,457,000 | 2026-07-16T23:37:44.9252814+00:00 | 119.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,461,000 | 2026-07-16T23:37:44.9269439+00:00 | 129.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,462,000 | 2026-07-16T23:37:44.9274103+00:00 | 129.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,467,000 | 2026-07-16T23:37:44.9303049+00:00 | 122.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,471,000 | 2026-07-16T23:37:44.9323528+00:00 | 124.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,472,000 | 2026-07-16T23:37:44.9329407+00:00 | 123.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,477,000 | 2026-07-16T23:37:44.9349336+00:00 | 118.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,481,000 | 2026-07-16T23:37:44.9375131+00:00 | 119.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,482,000 | 2026-07-16T23:37:44.9379193+00:00 | 118.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,487,000 | 2026-07-16T23:37:44.9408721+00:00 | 112.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,491,000 | 2026-07-16T23:37:44.9454417+00:00 | 111.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,492,000 | 2026-07-16T23:37:44.9458341+00:00 | 110.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,497,000 | 2026-07-16T23:37:44.9498144+00:00 | 107.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,501,000 | 2026-07-16T23:37:44.9518382+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,502,000 | 2026-07-16T23:37:44.9526556+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,507,000 | 2026-07-16T23:37:44.9563245+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,100,000 | 2026-07-16T23:37:45.5133115+00:00 | 103.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,557,000 | 2026-07-16T23:37:45.8554723+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,564,000 | 2026-07-16T23:37:45.8589272+00:00 | 118.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,566,000 | 2026-07-16T23:37:45.8598511+00:00 | 117.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,567,000 | 2026-07-16T23:37:45.8604212+00:00 | 111.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,574,000 | 2026-07-16T23:37:45.863883+00:00 | 113.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,576,000 | 2026-07-16T23:37:45.8649397+00:00 | 122.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,577,000 | 2026-07-16T23:37:45.8652398+00:00 | 139.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,584,000 | 2026-07-16T23:37:45.8684346+00:00 | 118.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,586,000 | 2026-07-16T23:37:45.8696403+00:00 | 117.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,589,000 | 2026-07-16T23:37:45.8711737+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,590,000 | 2026-07-16T23:37:45.8715534+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,594,000 | 2026-07-16T23:37:45.8747499+00:00 | 112.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,596,000 | 2026-07-16T23:37:45.8775044+00:00 | 109.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,600,000 | 2026-07-16T23:37:45.8836453+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,176,530 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*12,344 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.60x less CPU per message** than Confluent.Kafka for producer (fire-and-forget), 3 brokers; comparison throughput is 1.31x.
:::

## Producer (Acks All) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,476,191 | 1,462,766–1,489,740 | 0.98 | 1.22x |
| Confluent | 2 | 1,210,311 | 1,203,420–1,217,242 | 1.45 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (dekaf-first) | 1.01 | 1043.55 | 1,484,113 | 1,489,740 | +0.3% | +0.01% | 1415.36 | 1,484,113 | 0 | 1.51 |
| Dekaf (confluent-first) | 0.94 | 958.28 | 1,443,529 | 1,462,766 | -4.2% | -0.42% | 1376.66 | 1,443,529 | 0 | 1.35 |
| Confluent (dekaf-first) | 1.43 | - | 1,180,333 | 1,217,242 | +7.7% | +0.53% | 1125.65 | 1,180,333 | 0 | 1.68 |
| Confluent (confluent-first) | 1.47 | - | 1,152,759 | 1,203,420 | +7.0% | +0.59% | 1099.36 | 1,152,759 | 0 | 1.69 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Acks All)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,298,332 | 1442.57 | 1022.71 KB |
| Dekaf | 1 | 1,269,123 | 1410.12 | 1017.64 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Acks All)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-16T23:07:32.035121+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 495,872 msg/s |
| Dekaf | 2026-07-16T23:07:50.0391789+00:00 | 1 | 16.0 MiB / 15.2 MiB | 1548.8 MB/s | 0/0 | 21,785 | 18.0s / 1,405,071 msg/s |
| Dekaf | 2026-07-16T23:08:08.0487471+00:00 | 1 | 16.0 MiB / 15.7 MiB | 1604.6 MB/s | 0/0 | 48,338 | 36.0s / 1,487,614 msg/s |
| Dekaf | 2026-07-16T23:08:27.0525361+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1604.6 MB/s | 1/0 | 83,288 | 55.0s / 1,330,475 msg/s |
| Dekaf | 2026-07-16T23:08:45.057737+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1604.6 MB/s | 1/0 | 118,752 | 73.0s / 1,431,651 msg/s |
| Dekaf | 2026-07-16T23:09:03.0642869+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1604.6 MB/s | 1/1 | 156,737 | 91.0s / 1,436,483 msg/s |
| Dekaf | 2026-07-16T23:09:21.074134+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1604.6 MB/s | 1/1 | 192,689 | 109.0s / 1,470,433 msg/s |
| Dekaf | 2026-07-16T23:09:39.0811555+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1628.5 MB/s | 1/1 | 228,037 | 127.1s / 1,462,278 msg/s |
| Dekaf | 2026-07-16T23:09:57.0855852+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1651.7 MB/s | 1/2 | 263,196 | 145.1s / 1,475,651 msg/s |
| Dekaf | 2026-07-16T23:10:16.0902912+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1651.7 MB/s | 1/2 | 306,463 | 164.1s / 1,540,142 msg/s |
| Dekaf | 2026-07-16T23:10:34.0957568+00:00 | 1 | 14.0 MiB / 12.9 MiB | 1651.7 MB/s | 1/3 | 348,589 | 182.1s / 1,541,300 msg/s |
| Dekaf | 2026-07-16T23:10:52.100798+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1653.1 MB/s | 1/3 | 388,006 | 200.1s / 1,536,541 msg/s |
| Dekaf | 2026-07-16T23:11:10.1084409+00:00 | 1 | 14.0 MiB / 13.2 MiB | 1676.6 MB/s | 1/3 | 425,018 | 218.1s / 1,514,436 msg/s |
| Dekaf | 2026-07-16T23:11:28.1160952+00:00 | 1 | 15.0 MiB / 14.9 MiB | 1676.6 MB/s | 2/3 | 458,429 | 236.1s / 1,492,998 msg/s |
| Dekaf | 2026-07-16T23:11:46.1264977+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1676.6 MB/s | 2/3 | 490,970 | 254.1s / 1,499,134 msg/s |
| Dekaf | 2026-07-16T23:12:05.1370359+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1676.6 MB/s | 3/3 | 521,676 | 273.1s / 1,515,990 msg/s |
| Dekaf | 2026-07-16T23:12:23.1463019+00:00 | 1 | 16.0 MiB / 14.6 MiB | 1676.6 MB/s | 3/3 | 547,793 | 291.1s / 1,553,299 msg/s |
| Dekaf | 2026-07-16T23:12:41.1542264+00:00 | 1 | 16.0 MiB / 15.9 MiB | 1676.6 MB/s | 3/3 | 574,182 | 309.1s / 1,523,830 msg/s |
| Dekaf | 2026-07-16T23:12:59.1675475+00:00 | 1 | 18.0 MiB / 15.7 MiB | 1676.6 MB/s | 4/3 | 597,316 | 327.1s / 1,502,151 msg/s |
| Dekaf | 2026-07-16T23:13:17.1798652+00:00 | 1 | 18.0 MiB / 17.9 MiB | 1676.6 MB/s | 4/3 | 619,034 | 345.1s / 1,546,606 msg/s |
| Dekaf | 2026-07-16T23:13:35.1903725+00:00 | 1 | 20.0 MiB / 19.0 MiB | 1676.6 MB/s | 5/3 | 642,546 | 363.2s / 1,481,133 msg/s |
| Dekaf | 2026-07-16T23:13:54.2073747+00:00 | 1 | 20.0 MiB / 20.0 MiB | 1676.6 MB/s | 5/3 | 669,417 | 382.2s / 1,518,700 msg/s |
| Dekaf | 2026-07-16T23:14:12.2259222+00:00 | 1 | 20.0 MiB / 20.0 MiB | 1676.6 MB/s | 5/3 | 696,305 | 400.2s / 1,491,292 msg/s |
| Dekaf | 2026-07-16T23:14:30.2340389+00:00 | 1 | 22.0 MiB / 20.7 MiB | 1676.6 MB/s | 6/3 | 723,092 | 418.2s / 1,518,269 msg/s |
| Dekaf | 2026-07-16T23:14:48.2623114+00:00 | 1 | 19.0 MiB / 16.3 MiB | 1676.6 MB/s | 6/3 | 746,542 | 436.2s / 1,455,851 msg/s |
| Dekaf | 2026-07-16T23:15:06.2753424+00:00 | 1 | 22.0 MiB / 14.3 MiB | 1676.6 MB/s | 6/4 | 769,150 | 454.2s / 1,472,244 msg/s |
| Dekaf | 2026-07-16T23:15:24.2868441+00:00 | 1 | 22.0 MiB / 20.0 MiB | 1676.6 MB/s | 6/4 | 795,392 | 472.2s / 1,527,247 msg/s |
| Dekaf | 2026-07-16T23:15:43.2964807+00:00 | 1 | 22.0 MiB / 22.0 MiB | 1676.6 MB/s | 6/4 | 822,769 | 491.2s / 1,425,914 msg/s |
| Dekaf | 2026-07-16T23:16:01.3034048+00:00 | 1 | 19.0 MiB / 19.0 MiB | 1676.6 MB/s | 7/4 | 849,604 | 509.2s / 1,472,532 msg/s |
| Dekaf | 2026-07-16T23:16:19.3163109+00:00 | 1 | 16.0 MiB / 14.6 MiB | 1697.9 MB/s | 8/4 | 880,575 | 527.2s / 1,484,338 msg/s |
| Dekaf | 2026-07-16T23:16:37.3259616+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1711.1 MB/s | 8/4 | 912,553 | 545.2s / 1,481,397 msg/s |
| Dekaf | 2026-07-16T23:16:55.3358608+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1711.1 MB/s | 8/5 | 947,812 | 563.3s / 1,486,275 msg/s |
| Dekaf | 2026-07-16T23:17:13.3502866+00:00 | 1 | 16.0 MiB / 14.1 MiB | 1711.1 MB/s | 8/5 | 976,540 | 581.3s / 1,492,725 msg/s |
| Dekaf | 2026-07-16T23:17:32.3652351+00:00 | 1 | 18.0 MiB / 15.7 MiB | 1711.1 MB/s | 8/5 | 1,005,951 | 600.3s / 1,444,119 msg/s |
| Dekaf | 2026-07-16T23:17:50.3783457+00:00 | 1 | 18.0 MiB / 16.9 MiB | 1711.1 MB/s | 9/5 | 1,030,901 | 618.3s / 1,442,434 msg/s |
| Dekaf | 2026-07-16T23:18:08.3890769+00:00 | 1 | 20.0 MiB / 19.1 MiB | 1711.1 MB/s | 9/5 | 1,053,266 | 636.3s / 1,489,532 msg/s |
| Dekaf | 2026-07-16T23:18:26.4043007+00:00 | 1 | 20.0 MiB / 15.4 MiB | 1711.1 MB/s | 10/5 | 1,076,721 | 654.3s / 1,507,605 msg/s |
| Dekaf | 2026-07-16T23:18:44.4097154+00:00 | 1 | 20.0 MiB / 19.1 MiB | 1731.1 MB/s | 10/5 | 1,104,452 | 672.3s / 1,513,573 msg/s |
| Dekaf | 2026-07-16T23:19:03.4180884+00:00 | 1 | 22.0 MiB / 19.3 MiB | 1731.1 MB/s | 10/5 | 1,128,361 | 691.3s / 1,554,794 msg/s |
| Dekaf | 2026-07-16T23:19:21.43256+00:00 | 1 | 22.0 MiB / 20.9 MiB | 1731.1 MB/s | 11/5 | 1,146,104 | 709.3s / 1,447,102 msg/s |
| Dekaf | 2026-07-16T23:19:39.4404965+00:00 | 1 | 19.0 MiB / 17.6 MiB | 1731.1 MB/s | 11/5 | 1,170,253 | 727.4s / 1,450,408 msg/s |
| Dekaf | 2026-07-16T23:19:57.4486948+00:00 | 1 | 22.0 MiB / 20.2 MiB | 1731.1 MB/s | 11/6 | 1,198,688 | 745.4s / 1,514,948 msg/s |
| Dekaf | 2026-07-16T23:20:15.4668759+00:00 | 1 | 22.0 MiB / 22.0 MiB | 1731.1 MB/s | 11/6 | 1,223,368 | 763.4s / 1,513,053 msg/s |
| Dekaf | 2026-07-16T23:20:33.4784126+00:00 | 1 | 19.0 MiB / 15.1 MiB | 1731.1 MB/s | 11/6 | 1,248,405 | 781.4s / 1,483,562 msg/s |
| Dekaf | 2026-07-16T23:20:52.4920522+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1731.1 MB/s | 12/6 | 1,279,007 | 800.4s / 1,525,273 msg/s |
| Dekaf | 2026-07-16T23:21:10.4942245+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1731.1 MB/s | 13/6 | 1,309,856 | 818.4s / 1,503,727 msg/s |
| Dekaf | 2026-07-16T23:21:28.5036042+00:00 | 1 | 16.0 MiB / 14.1 MiB | 1731.1 MB/s | 13/6 | 1,345,248 | 836.4s / 1,312,260 msg/s |
| Dekaf | 2026-07-16T23:21:46.5099968+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1731.1 MB/s | 13/7 | 1,378,874 | 854.4s / 1,438,056 msg/s |
| Dekaf | 2026-07-16T23:22:04.5239982+00:00 | 1 | 16.0 MiB / 15.3 MiB | 1731.1 MB/s | 13/7 | 1,408,789 | 872.4s / 1,427,044 msg/s |
| Dekaf | 2026-07-16T23:22:22.5344349+00:00 | 1 | 18.0 MiB / 17.1 MiB | 1731.1 MB/s | 13/7 | 1,435,937 | 890.4s / 1,560,599 msg/s |
| Dekaf | 2026-07-16T23:52:42.2849466+00:00 | 1 | 16.0 MiB / 15.8 MiB | 1619.9 MB/s | 0/0 | 12,323 | 9.0s / 1,518,117 msg/s |
| Dekaf | 2026-07-16T23:53:00.2934191+00:00 | 1 | 16.0 MiB / 13.1 MiB | 1619.9 MB/s | 0/0 | 42,013 | 27.0s / 1,536,275 msg/s |
| Dekaf | 2026-07-16T23:53:18.3038364+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1619.9 MB/s | 0/0 | 79,813 | 45.0s / 1,469,025 msg/s |
| Dekaf | 2026-07-16T23:53:36.3118651+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1619.9 MB/s | 1/0 | 120,493 | 63.0s / 1,475,989 msg/s |
| Dekaf | 2026-07-16T23:53:54.3195379+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1619.9 MB/s | 1/0 | 161,940 | 81.0s / 1,482,637 msg/s |
| Dekaf | 2026-07-16T23:54:12.3268067+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1619.9 MB/s | 2/0 | 207,211 | 99.0s / 1,473,365 msg/s |
| Dekaf | 2026-07-16T23:54:31.331533+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1619.9 MB/s | 2/0 | 255,356 | 118.0s / 1,499,226 msg/s |
| Dekaf | 2026-07-16T23:54:49.3399249+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1619.9 MB/s | 2/1 | 295,044 | 136.0s / 1,478,889 msg/s |
| Dekaf | 2026-07-16T23:55:07.3480291+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1619.9 MB/s | 2/1 | 339,441 | 154.0s / 1,443,305 msg/s |
| Dekaf | 2026-07-16T23:55:25.3550768+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1619.9 MB/s | 2/1 | 384,743 | 172.1s / 1,487,970 msg/s |
| Dekaf | 2026-07-16T23:55:43.3590385+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1619.9 MB/s | 2/2 | 429,728 | 190.1s / 1,472,866 msg/s |
| Dekaf | 2026-07-16T23:56:01.3641236+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1619.9 MB/s | 2/2 | 473,952 | 208.1s / 1,472,807 msg/s |
| Dekaf | 2026-07-16T23:56:20.371997+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1619.9 MB/s | 2/3 | 515,451 | 227.1s / 1,453,616 msg/s |
| Dekaf | 2026-07-16T23:56:38.3780016+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1619.9 MB/s | 2/3 | 561,928 | 245.1s / 1,475,982 msg/s |
| Dekaf | 2026-07-16T23:56:56.3876939+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1619.9 MB/s | 2/3 | 606,403 | 263.1s / 1,487,235 msg/s |
| Dekaf | 2026-07-16T23:57:14.3937175+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1619.9 MB/s | 2/4 | 651,344 | 281.1s / 1,485,276 msg/s |
| Dekaf | 2026-07-16T23:57:32.4023599+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1619.9 MB/s | 2/4 | 697,273 | 299.1s / 1,435,054 msg/s |
| Dekaf | 2026-07-16T23:57:51.4030474+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1619.9 MB/s | 2/5 | 740,168 | 318.1s / 1,465,963 msg/s |
| Dekaf | 2026-07-16T23:58:09.4092353+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1619.9 MB/s | 2/5 | 786,449 | 336.1s / 1,469,866 msg/s |
| Dekaf | 2026-07-16T23:58:27.4139969+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1619.9 MB/s | 2/5 | 830,063 | 354.1s / 1,484,937 msg/s |
| Dekaf | 2026-07-16T23:58:45.4203679+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1619.9 MB/s | 2/6 | 874,594 | 372.1s / 1,451,533 msg/s |
| Dekaf | 2026-07-16T23:59:03.4241213+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1619.9 MB/s | 2/6 | 920,720 | 390.1s / 1,447,324 msg/s |
| Dekaf | 2026-07-16T23:59:21.4284872+00:00 | 1 | 10.0 MiB / 10.0 MiB | 1619.9 MB/s | 3/6 | 964,001 | 408.1s / 1,469,659 msg/s |
| Dekaf | 2026-07-16T23:59:40.4301935+00:00 | 1 | 10.0 MiB / 10.0 MiB | 1619.9 MB/s | 3/6 | 1,014,973 | 427.1s / 1,430,117 msg/s |
| Dekaf | 2026-07-16T23:59:58.4348105+00:00 | 1 | 10.0 MiB / 10.0 MiB | 1619.9 MB/s | 3/6 | 1,059,408 | 445.1s / 1,435,167 msg/s |
| Dekaf | 2026-07-17T00:00:16.4386272+00:00 | 1 | 10.0 MiB / 10.0 MiB | 1619.9 MB/s | 3/7 | 1,105,422 | 463.1s / 1,472,386 msg/s |
| Dekaf | 2026-07-17T00:00:34.4397842+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1619.9 MB/s | 3/7 | 1,151,142 | 481.1s / 1,452,374 msg/s |
| Dekaf | 2026-07-17T00:00:52.4420215+00:00 | 1 | 11.0 MiB / 10.8 MiB | 1619.9 MB/s | 4/7 | 1,202,877 | 499.1s / 1,441,015 msg/s |
| Dekaf | 2026-07-17T00:01:10.4552899+00:00 | 1 | 11.0 MiB / 10.4 MiB | 1619.9 MB/s | 4/7 | 1,255,557 | 517.1s / 1,431,519 msg/s |
| Dekaf | 2026-07-17T00:01:29.4580073+00:00 | 1 | 11.0 MiB / 10.7 MiB | 1645.7 MB/s | 4/7 | 1,312,414 | 536.1s / 1,496,544 msg/s |
| Dekaf | 2026-07-17T00:01:47.4649881+00:00 | 1 | 11.0 MiB / 10.9 MiB | 1645.7 MB/s | 4/8 | 1,367,054 | 554.1s / 1,495,195 msg/s |
| Dekaf | 2026-07-17T00:02:05.4671513+00:00 | 1 | 9.0 MiB / 6.4 MiB | 1645.7 MB/s | 4/8 | 1,420,399 | 572.1s / 1,382,318 msg/s |
| Dekaf | 2026-07-17T00:02:23.4717711+00:00 | 1 | 11.0 MiB / 10.7 MiB | 1645.7 MB/s | 4/9 | 1,466,449 | 590.1s / 1,481,522 msg/s |
| Dekaf | 2026-07-17T00:02:41.4766348+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1645.7 MB/s | 4/9 | 1,519,957 | 608.2s / 1,456,276 msg/s |
| Dekaf | 2026-07-17T00:02:59.4832332+00:00 | 1 | 11.0 MiB / 10.4 MiB | 1645.7 MB/s | 4/9 | 1,573,595 | 626.2s / 1,446,774 msg/s |
| Dekaf | 2026-07-17T00:03:18.488369+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1645.7 MB/s | 5/9 | 1,626,634 | 645.2s / 1,421,338 msg/s |
| Dekaf | 2026-07-17T00:03:36.4953633+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1645.7 MB/s | 5/9 | 1,680,945 | 663.2s / 1,469,062 msg/s |
| Dekaf | 2026-07-17T00:03:54.5061168+00:00 | 1 | 13.0 MiB / 12.6 MiB | 1645.7 MB/s | 6/9 | 1,732,256 | 681.2s / 1,436,757 msg/s |
| Dekaf | 2026-07-17T00:04:12.5208326+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1645.7 MB/s | 6/9 | 1,781,658 | 699.2s / 1,455,404 msg/s |
| Dekaf | 2026-07-17T00:04:30.5329336+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1645.7 MB/s | 6/9 | 1,829,839 | 717.2s / 1,466,439 msg/s |
| Dekaf | 2026-07-17T00:04:48.5393571+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1645.7 MB/s | 6/10 | 1,879,776 | 735.2s / 1,380,031 msg/s |
| Dekaf | 2026-07-17T00:05:07.5481548+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1645.7 MB/s | 6/10 | 1,932,689 | 754.2s / 1,439,493 msg/s |
| Dekaf | 2026-07-17T00:05:25.5530439+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1645.7 MB/s | 6/11 | 1,981,741 | 772.2s / 1,446,060 msg/s |
| Dekaf | 2026-07-17T00:05:43.5637425+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1645.7 MB/s | 6/11 | 2,029,771 | 790.2s / 1,422,520 msg/s |
| Dekaf | 2026-07-17T00:06:01.5740861+00:00 | 1 | 14.0 MiB / 13.9 MiB | 1645.7 MB/s | 6/11 | 2,075,813 | 808.2s / 1,267,390 msg/s |
| Dekaf | 2026-07-17T00:06:19.5815666+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1645.7 MB/s | 6/12 | 2,117,017 | 826.2s / 1,258,300 msg/s |
| Dekaf | 2026-07-17T00:06:37.5877603+00:00 | 1 | 11.0 MiB / 10.9 MiB | 1645.7 MB/s | 6/12 | 2,162,375 | 844.2s / 1,331,577 msg/s |
| Dekaf | 2026-07-17T00:06:56.5900744+00:00 | 1 | 13.0 MiB / 11.6 MiB | 1645.7 MB/s | 6/13 | 2,206,231 | 863.2s / 1,379,603 msg/s |
| Dekaf | 2026-07-17T00:07:14.6030614+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1645.7 MB/s | 6/13 | 2,248,479 | 881.2s / 1,467,025 msg/s |
| Dekaf | 2026-07-17T00:07:32.6128+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1662.2 MB/s | 6/13 | 2,298,571 | 899.2s / 1,482,910 msg/s |
*1,700 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Acks All)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-16T23:08:02.1521027+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.6 MiB |
| Dekaf | 2026-07-16T23:08:17.165324+00:00 | 1 | capacity | succeeded | 15,013ms | 14.0 MiB / 12.6 MiB |
| Dekaf | 2026-07-16T23:08:47.191551+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.2 MiB |
| Dekaf | 2026-07-16T23:09:02.203426+00:00 | 1 | capacity | failed | 15,011ms | 14.0 MiB / 11.3 MiB |
| Dekaf | 2026-07-16T23:09:32.2301595+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-16T23:09:47.2465981+00:00 | 1 | capacity | failed | 15,016ms | 14.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-16T23:10:17.2749227+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-16T23:10:32.2876043+00:00 | 1 | capacity | failed | 15,012ms | 14.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:11:02.3110801+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:11:17.3252714+00:00 | 1 | capacity | succeeded | 15,014ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-16T23:11:47.3822704+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-16T23:12:02.4002707+00:00 | 1 | capacity | succeeded | 15,018ms | 16.0 MiB / 13.7 MiB |
| Dekaf | 2026-07-16T23:12:32.4335707+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-16T23:12:47.4514182+00:00 | 1 | capacity | succeeded | 15,017ms | 18.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-16T23:13:17.5036941+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-16T23:13:32.5282636+00:00 | 1 | capacity | succeeded | 15,024ms | 20.0 MiB / 17.5 MiB |
| Dekaf | 2026-07-16T23:14:02.5829164+00:00 | 1 | capacity | started | 0ms | 22.0 MiB / 17.2 MiB |
| Dekaf | 2026-07-16T23:14:17.6046777+00:00 | 1 | capacity | succeeded | 15,021ms | 22.0 MiB / 20.1 MiB |
| Dekaf | 2026-07-16T23:14:47.6726518+00:00 | 1 | capacity | started | 0ms | 19.0 MiB / 19.1 MiB |
| Dekaf | 2026-07-16T23:15:02.7042255+00:00 | 1 | capacity | failed | 15,031ms | 22.0 MiB / 17.1 MiB |
| Dekaf | 2026-07-16T23:15:32.7609566+00:00 | 1 | capacity | started | 0ms | 19.0 MiB / 19.1 MiB |
| Dekaf | 2026-07-16T23:15:47.7855646+00:00 | 1 | capacity | succeeded | 15,024ms | 19.0 MiB / 18.1 MiB |
| Dekaf | 2026-07-16T23:15:50.7943904+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-16T23:16:05.8182201+00:00 | 1 | capacity | succeeded | 15,023ms | 16.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-16T23:16:35.8642542+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-16T23:16:50.8858859+00:00 | 1 | capacity | failed | 15,021ms | 16.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-16T23:17:20.9270274+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-16T23:17:35.943318+00:00 | 1 | capacity | succeeded | 15,016ms | 18.0 MiB / 16.1 MiB |
| Dekaf | 2026-07-16T23:18:05.9974264+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 16.1 MiB |
| Dekaf | 2026-07-16T23:18:21.030086+00:00 | 1 | capacity | succeeded | 15,032ms | 20.0 MiB / 17.1 MiB |
| Dekaf | 2026-07-16T23:18:51.0842188+00:00 | 1 | capacity | started | 0ms | 22.0 MiB / 14.7 MiB |
| Dekaf | 2026-07-16T23:19:06.1196105+00:00 | 1 | capacity | succeeded | 15,035ms | 22.0 MiB / 15.4 MiB |
| Dekaf | 2026-07-16T23:19:36.1843955+00:00 | 1 | capacity | started | 0ms | 19.0 MiB / 21.1 MiB |
| Dekaf | 2026-07-16T23:19:51.2133765+00:00 | 1 | capacity | failed | 15,028ms | 22.0 MiB / 16.1 MiB |
| Dekaf | 2026-07-16T23:20:21.2780073+00:00 | 1 | capacity | started | 0ms | 19.0 MiB / 19.1 MiB |
| Dekaf | 2026-07-16T23:20:36.3081726+00:00 | 1 | capacity | succeeded | 15,030ms | 19.0 MiB / 16.1 MiB |
| Dekaf | 2026-07-16T23:20:39.3132054+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 15.6 MiB |
| Dekaf | 2026-07-16T23:20:54.3375798+00:00 | 1 | capacity | succeeded | 15,024ms | 16.0 MiB / 14.8 MiB |
| Dekaf | 2026-07-16T23:21:24.4059158+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.5 MiB |
| Dekaf | 2026-07-16T23:21:39.4227945+00:00 | 1 | capacity | failed | 15,017ms | 16.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-16T23:22:09.4599434+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-16T23:22:24.4828168+00:00 | 1 | capacity | succeeded | 15,022ms | 18.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-16T23:53:03.4008266+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.4 MiB |
| Dekaf | 2026-07-16T23:53:18.4157307+00:00 | 1 | capacity | succeeded | 15,015ms | 14.0 MiB / 13.8 MiB |
| Dekaf | 2026-07-16T23:53:48.4407264+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 12.6 MiB |
| Dekaf | 2026-07-16T23:54:03.4519028+00:00 | 1 | capacity | succeeded | 15,011ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:54:33.4745618+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:54:48.4864048+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 8.7 MiB |
| Dekaf | 2026-07-16T23:55:18.5133725+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 10.7 MiB |
| Dekaf | 2026-07-16T23:55:33.526559+00:00 | 1 | capacity | failed | 15,013ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-16T23:56:03.5508984+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:56:18.5606174+00:00 | 1 | capacity | failed | 15,009ms | 12.0 MiB / 9.3 MiB |
| Dekaf | 2026-07-16T23:56:48.5851863+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:57:03.5967373+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-16T23:57:33.6238056+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:57:48.6346536+00:00 | 1 | capacity | failed | 15,010ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-16T23:58:18.6579814+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-16T23:58:33.6708178+00:00 | 1 | capacity | failed | 15,012ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-16T23:59:03.6917203+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:59:18.7014451+00:00 | 1 | capacity | succeeded | 15,009ms | 10.0 MiB / 8.9 MiB |
| Dekaf | 2026-07-16T23:59:48.7269609+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 10.0 MiB |
| Dekaf | 2026-07-17T00:00:03.737615+00:00 | 1 | capacity | failed | 15,011ms | 10.0 MiB / 6.5 MiB |
| Dekaf | 2026-07-17T00:00:33.7605221+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-17T00:00:48.7741096+00:00 | 1 | capacity | succeeded | 15,013ms | 11.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T00:01:18.7953041+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T00:01:33.8069448+00:00 | 1 | capacity | failed | 15,011ms | 11.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T00:02:03.8270269+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.3 MiB |
| Dekaf | 2026-07-17T00:02:18.8553503+00:00 | 1 | capacity | failed | 15,028ms | 11.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-17T00:02:48.8955125+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T00:03:03.9091679+00:00 | 1 | capacity | succeeded | 15,013ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T00:03:33.9382509+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T00:03:48.9507374+00:00 | 1 | capacity | succeeded | 15,012ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T00:04:18.97813+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.8 MiB |
| Dekaf | 2026-07-17T00:04:33.9914256+00:00 | 1 | capacity | failed | 15,013ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T00:05:04.0115411+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T00:05:19.0228068+00:00 | 1 | capacity | failed | 15,011ms | 13.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T00:05:49.0518604+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.0 MiB |
| Dekaf | 2026-07-17T00:06:04.0631698+00:00 | 1 | capacity | failed | 15,011ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T00:06:34.0918699+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T00:06:49.10584+00:00 | 1 | capacity | failed | 15,013ms | 13.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T00:07:19.1348447+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |

## Producer Admission Block Durations - Producer (Acks All)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 854 |
| Dekaf | 1 | 0.002–0.004ms | 1,159 |
| Dekaf | 1 | 0.004–0.008ms | 3,833 |
| Dekaf | 1 | 0.008–0.016ms | 41,181 |
| Dekaf | 1 | 0.016–0.032ms | 38,277 |
| Dekaf | 1 | 0.032–0.064ms | 27,552 |
| Dekaf | 1 | 0.064–0.128ms | 48,053 |
| Dekaf | 1 | 0.128–0.256ms | 98,833 |
| Dekaf | 1 | 0.256–0.512ms | 120,252 |
| Dekaf | 1 | 0.512–1.024ms | 108,879 |
| Dekaf | 1 | 1.024–2.048ms | 46,293 |
| Dekaf | 1 | 2.048–4.096ms | 5,906 |
| Dekaf | 1 | 4.096–8.192ms | 1,445 |
| Dekaf | 1 | 8.192–16.384ms | 282 |
| Dekaf | 1 | 16.384–32.768ms | 11 |
| Dekaf | 1 | 32.768–65.536ms | 1 |
| Dekaf | 1 | 0.001–0.002ms | 1,214 |
| Dekaf | 1 | 0.002–0.004ms | 1,532 |
| Dekaf | 1 | 0.004–0.008ms | 4,723 |
| Dekaf | 1 | 0.008–0.016ms | 37,791 |
| Dekaf | 1 | 0.016–0.032ms | 37,297 |
| Dekaf | 1 | 0.032–0.064ms | 38,248 |
| Dekaf | 1 | 0.064–0.128ms | 81,246 |
| Dekaf | 1 | 0.128–0.256ms | 259,329 |
| Dekaf | 1 | 0.256–0.512ms | 399,312 |
| Dekaf | 1 | 0.512–1.024ms | 86,065 |
| Dekaf | 1 | 1.024–2.048ms | 14,475 |
| Dekaf | 1 | 2.048–4.096ms | 4,811 |
| Dekaf | 1 | 4.096–8.192ms | 1,308 |
| Dekaf | 1 | 8.192–16.384ms | 160 |
| Dekaf | 1 | 16.384–32.768ms | 3 |

:::tip
**Dekaf uses 1.48x less CPU per message** than Confluent.Kafka for producer (acks all); comparison throughput is 1.22x.
:::

## Producer (Acks All), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.13 | 1148.48 | 1,121,515 | 1,132,060 | +1.8% | +0.21% | 1069.56 | 1,121,515 | 0 | 1.27 |
| Confluent | 1.72 | - | 893,832 | 891,373 | -0.2% | -0.01% | 852.42 | 893,832 | 0 | 1.54 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Acks All), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 338,168 | 375.73 | 1020.13 KB |
| Dekaf | 2 | 330,205 | 366.89 | 1003.40 KB |
| Dekaf | 3 | 325,305 | 361.44 | 1005.56 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Acks All), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-16T23:07:26.1326994+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 306,331 msg/s |
| Dekaf | 2026-07-16T23:07:35.1359865+00:00 | 3 | 16.0 MiB / 8.1 MiB | 306.6 MB/s | 0/0 | 2,111 | 9.0s / 871,423 msg/s |
| Dekaf | 2026-07-16T23:07:44.1485449+00:00 | 3 | 16.0 MiB / 5.4 MiB | 376.7 MB/s | 0/0 | 2,675 | 18.0s / 1,109,369 msg/s |
| Dekaf | 2026-07-16T23:07:54.1641628+00:00 | 1 | 16.0 MiB / 15.1 MiB | 403.7 MB/s | 0/0 | 13,821 | 28.0s / 1,066,380 msg/s |
| Dekaf | 2026-07-16T23:08:03.1674088+00:00 | 1 | 16.0 MiB / 14.9 MiB | 407.1 MB/s | 0/0 | 17,537 | 37.0s / 1,037,088 msg/s |
| Dekaf | 2026-07-16T23:08:12.1728372+00:00 | 1 | 16.0 MiB / 8.9 MiB | 407.9 MB/s | 0/1 | 22,496 | 46.0s / 1,014,192 msg/s |
| Dekaf | 2026-07-16T23:08:21.1754862+00:00 | 1 | 16.0 MiB / 7.1 MiB | 419.2 MB/s | 0/1 | 25,110 | 55.0s / 1,102,260 msg/s |
| Dekaf | 2026-07-16T23:08:30.1814493+00:00 | 2 | 16.0 MiB / 4.8 MiB | 398.4 MB/s | 0/1 | 3,223 | 64.0s / 1,094,709 msg/s |
| Dekaf | 2026-07-16T23:08:39.1866001+00:00 | 2 | 16.0 MiB / 3.3 MiB | 403.5 MB/s | 0/1 | 3,547 | 73.0s / 1,089,475 msg/s |
| Dekaf | 2026-07-16T23:08:48.1945963+00:00 | 2 | 16.0 MiB / 2.5 MiB | 403.5 MB/s | 0/1 | 3,974 | 82.0s / 1,074,411 msg/s |
| Dekaf | 2026-07-16T23:08:57.1982683+00:00 | 2 | 16.0 MiB / 7.8 MiB | 412.9 MB/s | 0/2 | 4,343 | 91.0s / 1,057,175 msg/s |
| Dekaf | 2026-07-16T23:09:06.2009492+00:00 | 3 | 12.0 MiB / 3.8 MiB | 410.0 MB/s | 2/1 | 10,897 | 100.1s / 1,191,453 msg/s |
| Dekaf | 2026-07-16T23:09:15.2095085+00:00 | 3 | 12.0 MiB / 7.6 MiB | 410.0 MB/s | 2/1 | 11,555 | 109.1s / 1,169,328 msg/s |
| Dekaf | 2026-07-16T23:09:24.2135664+00:00 | 3 | 12.0 MiB / 10.7 MiB | 410.0 MB/s | 2/1 | 12,190 | 118.1s / 1,122,390 msg/s |
| Dekaf | 2026-07-16T23:09:33.2184378+00:00 | 3 | 12.0 MiB / 8.2 MiB | 422.3 MB/s | 2/2 | 12,815 | 127.1s / 1,217,986 msg/s |
| Dekaf | 2026-07-16T23:09:43.2204732+00:00 | 1 | 16.0 MiB / 10.4 MiB | 452.0 MB/s | 0/3 | 56,188 | 137.1s / 1,193,415 msg/s |
| Dekaf | 2026-07-16T23:09:52.2245231+00:00 | 1 | 16.0 MiB / 16.0 MiB | 452.0 MB/s | 0/3 | 59,772 | 146.1s / 1,187,087 msg/s |
| Dekaf | 2026-07-16T23:10:01.2300564+00:00 | 1 | 16.0 MiB / 9.8 MiB | 452.0 MB/s | 0/3 | 63,358 | 155.1s / 1,153,120 msg/s |
| Dekaf | 2026-07-16T23:10:10.2359163+00:00 | 1 | 16.0 MiB / 16.0 MiB | 452.0 MB/s | 0/3 | 67,243 | 164.1s / 1,112,397 msg/s |
| Dekaf | 2026-07-16T23:10:19.2375794+00:00 | 2 | 16.0 MiB / 1.6 MiB | 426.0 MB/s | 0/3 | 7,671 | 173.1s / 1,138,644 msg/s |
| Dekaf | 2026-07-16T23:10:28.2414703+00:00 | 2 | 18.0 MiB / 6.4 MiB | 436.9 MB/s | 1/3 | 8,475 | 182.1s / 1,099,656 msg/s |
| Dekaf | 2026-07-16T23:10:37.248223+00:00 | 2 | 18.0 MiB / 10.6 MiB | 436.9 MB/s | 1/3 | 8,714 | 191.1s / 1,188,473 msg/s |
| Dekaf | 2026-07-16T23:10:46.2501226+00:00 | 2 | 18.0 MiB / 4.4 MiB | 436.9 MB/s | 1/3 | 8,965 | 200.1s / 1,170,432 msg/s |
| Dekaf | 2026-07-16T23:10:55.2536463+00:00 | 3 | 8.0 MiB / 3.6 MiB | 426.1 MB/s | 4/3 | 25,164 | 209.1s / 1,139,093 msg/s |
| Dekaf | 2026-07-16T23:11:04.262561+00:00 | 3 | 8.0 MiB / 5.6 MiB | 426.1 MB/s | 4/3 | 27,007 | 218.1s / 1,086,034 msg/s |
| Dekaf | 2026-07-16T23:11:13.2696365+00:00 | 3 | 8.0 MiB / 3.7 MiB | 426.1 MB/s | 4/3 | 29,148 | 227.1s / 935,309 msg/s |
| Dekaf | 2026-07-16T23:11:22.2746554+00:00 | 3 | 8.0 MiB / 3.6 MiB | 426.1 MB/s | 4/3 | 31,068 | 236.1s / 1,128,983 msg/s |
| Dekaf | 2026-07-16T23:11:32.2853797+00:00 | 1 | 8.0 MiB / 5.1 MiB | 461.4 MB/s | 4/3 | 106,946 | 246.1s / 1,156,254 msg/s |
| Dekaf | 2026-07-16T23:11:41.2877699+00:00 | 1 | 8.0 MiB / 7.4 MiB | 461.4 MB/s | 4/4 | 112,065 | 255.1s / 1,163,437 msg/s |
| Dekaf | 2026-07-16T23:11:50.2983706+00:00 | 1 | 8.0 MiB / 7.4 MiB | 461.4 MB/s | 4/4 | 118,020 | 264.2s / 1,146,126 msg/s |
| Dekaf | 2026-07-16T23:11:59.3005631+00:00 | 1 | 8.0 MiB / 5.8 MiB | 461.4 MB/s | 4/4 | 123,523 | 273.2s / 1,182,945 msg/s |
| Dekaf | 2026-07-16T23:12:08.307079+00:00 | 2 | 15.0 MiB / 5.5 MiB | 436.9 MB/s | 2/4 | 9,906 | 282.2s / 1,139,556 msg/s |
| Dekaf | 2026-07-16T23:12:17.3128313+00:00 | 2 | 15.0 MiB / 5.3 MiB | 436.9 MB/s | 2/5 | 10,021 | 291.2s / 1,139,268 msg/s |
| Dekaf | 2026-07-16T23:12:26.313639+00:00 | 2 | 15.0 MiB / 2.3 MiB | 436.9 MB/s | 2/5 | 10,168 | 300.2s / 1,194,902 msg/s |
| Dekaf | 2026-07-16T23:12:35.3145452+00:00 | 2 | 15.0 MiB / 3.7 MiB | 436.9 MB/s | 2/5 | 10,180 | 309.2s / 1,180,960 msg/s |
| Dekaf | 2026-07-16T23:12:44.3166001+00:00 | 2 | 15.0 MiB / 8.0 MiB | 436.9 MB/s | 2/5 | 10,268 | 318.2s / 1,097,183 msg/s |
| Dekaf | 2026-07-16T23:12:53.3213736+00:00 | 3 | 8.0 MiB / 6.6 MiB | 426.1 MB/s | 4/5 | 47,085 | 327.2s / 1,156,681 msg/s |
| Dekaf | 2026-07-16T23:13:02.3253914+00:00 | 3 | 8.0 MiB / 3.9 MiB | 426.1 MB/s | 4/5 | 48,420 | 336.2s / 1,121,517 msg/s |
| Dekaf | 2026-07-16T23:13:11.3281799+00:00 | 3 | 9.0 MiB / 6.0 MiB | 426.1 MB/s | 5/5 | 49,467 | 345.2s / 1,106,861 msg/s |
| Dekaf | 2026-07-16T23:13:20.3365339+00:00 | 3 | 9.0 MiB / 5.0 MiB | 426.1 MB/s | 5/5 | 52,922 | 354.2s / 1,067,597 msg/s |
| Dekaf | 2026-07-16T23:13:30.3405742+00:00 | 1 | 6.0 MiB / 6.0 MiB | 461.4 MB/s | 6/5 | 196,112 | 364.2s / 1,156,110 msg/s |
| Dekaf | 2026-07-16T23:13:39.3420962+00:00 | 1 | 6.0 MiB / 6.0 MiB | 461.4 MB/s | 6/5 | 204,136 | 373.2s / 1,169,706 msg/s |
| Dekaf | 2026-07-16T23:13:48.3450569+00:00 | 1 | 6.0 MiB / 6.0 MiB | 461.4 MB/s | 6/6 | 212,386 | 382.2s / 1,149,506 msg/s |
| Dekaf | 2026-07-16T23:13:57.3469536+00:00 | 1 | 6.0 MiB / 5.3 MiB | 461.4 MB/s | 6/6 | 220,546 | 391.2s / 1,128,053 msg/s |
| Dekaf | 2026-07-16T23:14:06.3484773+00:00 | 2 | 11.0 MiB / 6.9 MiB | 436.9 MB/s | 4/6 | 12,076 | 400.2s / 1,139,618 msg/s |
| Dekaf | 2026-07-16T23:14:15.3510721+00:00 | 2 | 11.0 MiB / 6.9 MiB | 436.9 MB/s | 4/6 | 12,363 | 409.2s / 1,170,552 msg/s |
| Dekaf | 2026-07-16T23:14:24.3531436+00:00 | 2 | 11.0 MiB / 5.6 MiB | 436.9 MB/s | 4/6 | 12,371 | 418.2s / 1,165,267 msg/s |
| Dekaf | 2026-07-16T23:14:33.3549173+00:00 | 2 | 11.0 MiB / 1.6 MiB | 436.9 MB/s | 4/6 | 12,607 | 427.2s / 1,112,137 msg/s |
| Dekaf | 2026-07-16T23:14:42.3635619+00:00 | 3 | 10.0 MiB / 3.3 MiB | 426.1 MB/s | 6/6 | 62,377 | 436.2s / 1,000,865 msg/s |
| Dekaf | 2026-07-16T23:14:51.3708065+00:00 | 3 | 10.0 MiB / 4.6 MiB | 426.1 MB/s | 6/6 | 62,907 | 445.2s / 1,119,847 msg/s |
| Dekaf | 2026-07-16T23:15:00.371774+00:00 | 3 | 10.0 MiB / 5.8 MiB | 426.1 MB/s | 6/6 | 63,680 | 454.2s / 1,092,523 msg/s |
| Dekaf | 2026-07-16T23:15:09.3721606+00:00 | 3 | 10.0 MiB / 5.6 MiB | 426.1 MB/s | 6/6 | 63,845 | 463.2s / 1,122,679 msg/s |
| Dekaf | 2026-07-16T23:15:19.3748004+00:00 | 1 | 7.0 MiB / 7.0 MiB | 461.4 MB/s | 8/6 | 306,099 | 473.2s / 1,158,401 msg/s |
| Dekaf | 2026-07-16T23:15:28.3753752+00:00 | 1 | 6.0 MiB / 5.5 MiB | 461.4 MB/s | 8/6 | 313,634 | 482.2s / 1,172,808 msg/s |
| Dekaf | 2026-07-16T23:15:37.3795697+00:00 | 1 | 6.0 MiB / 6.0 MiB | 461.4 MB/s | 8/7 | 320,401 | 491.2s / 1,155,950 msg/s |
| Dekaf | 2026-07-16T23:15:46.3825965+00:00 | 1 | 6.0 MiB / 6.0 MiB | 461.4 MB/s | 8/7 | 329,795 | 500.2s / 1,165,366 msg/s |
| Dekaf | 2026-07-16T23:15:55.3854955+00:00 | 2 | 7.0 MiB / 6.9 MiB | 436.9 MB/s | 6/7 | 23,701 | 509.2s / 1,101,483 msg/s |
| Dekaf | 2026-07-16T23:16:04.3898065+00:00 | 2 | 7.0 MiB / 1.1 MiB | 436.9 MB/s | 6/7 | 24,784 | 518.2s / 1,125,991 msg/s |
| Dekaf | 2026-07-16T23:16:13.3918197+00:00 | 2 | 7.0 MiB / 1.1 MiB | 436.9 MB/s | 6/7 | 25,610 | 527.2s / 1,148,885 msg/s |
| Dekaf | 2026-07-16T23:16:22.3968531+00:00 | 2 | 6.0 MiB / 5.1 MiB | 436.9 MB/s | 6/7 | 27,270 | 536.2s / 1,163,045 msg/s |
| Dekaf | 2026-07-16T23:16:31.3999199+00:00 | 3 | 11.0 MiB / 3.6 MiB | 435.2 MB/s | 7/7 | 66,841 | 545.2s / 1,160,377 msg/s |
| Dekaf | 2026-07-16T23:16:40.4017321+00:00 | 3 | 12.0 MiB / 11.7 MiB | 435.2 MB/s | 7/7 | 67,057 | 554.2s / 1,078,817 msg/s |
| Dekaf | 2026-07-16T23:16:49.4041962+00:00 | 3 | 11.0 MiB / 3.2 MiB | 435.2 MB/s | 7/7 | 67,264 | 563.3s / 1,059,562 msg/s |
| Dekaf | 2026-07-16T23:16:58.405178+00:00 | 3 | 12.0 MiB / 5.0 MiB | 435.2 MB/s | 8/7 | 67,264 | 572.3s / 1,123,489 msg/s |
| Dekaf | 2026-07-16T23:17:08.4086078+00:00 | 1 | 6.0 MiB / 2.7 MiB | 461.4 MB/s | 8/9 | 409,313 | 582.3s / 1,135,693 msg/s |
| Dekaf | 2026-07-16T23:17:17.4134985+00:00 | 1 | 6.0 MiB / 5.5 MiB | 461.4 MB/s | 8/9 | 417,819 | 591.3s / 1,151,187 msg/s |
| Dekaf | 2026-07-16T23:17:26.4166577+00:00 | 1 | 6.0 MiB / 5.2 MiB | 461.4 MB/s | 8/9 | 425,059 | 600.3s / 1,163,942 msg/s |
| Dekaf | 2026-07-16T23:17:35.4203922+00:00 | 1 | 5.0 MiB / 4.7 MiB | 461.4 MB/s | 8/9 | 433,674 | 609.3s / 1,157,579 msg/s |
| Dekaf | 2026-07-16T23:17:44.4271536+00:00 | 1 | 6.0 MiB / 5.2 MiB | 461.4 MB/s | 8/9 | 442,659 | 618.3s / 1,124,936 msg/s |
| Dekaf | 2026-07-16T23:17:53.4324638+00:00 | 2 | 8.0 MiB / 3.8 MiB | 440.7 MB/s | 6/9 | 45,886 | 627.3s / 1,195,982 msg/s |
| Dekaf | 2026-07-16T23:18:02.4365255+00:00 | 2 | 7.0 MiB / 5.6 MiB | 440.7 MB/s | 6/9 | 47,143 | 636.3s / 1,118,042 msg/s |
| Dekaf | 2026-07-16T23:18:11.4379039+00:00 | 2 | 7.0 MiB / 3.6 MiB | 440.7 MB/s | 6/10 | 49,029 | 645.3s / 1,100,669 msg/s |
| Dekaf | 2026-07-16T23:18:20.4402589+00:00 | 2 | 7.0 MiB / 5.1 MiB | 440.7 MB/s | 6/10 | 52,331 | 654.3s / 1,099,488 msg/s |
| Dekaf | 2026-07-16T23:18:29.4445879+00:00 | 3 | 13.0 MiB / 5.8 MiB | 435.2 MB/s | 9/8 | 68,884 | 663.3s / 1,166,319 msg/s |
| Dekaf | 2026-07-16T23:18:38.447549+00:00 | 3 | 13.0 MiB / 3.6 MiB | 435.2 MB/s | 9/8 | 69,017 | 672.3s / 1,127,607 msg/s |
| Dekaf | 2026-07-16T23:18:47.4513297+00:00 | 3 | 13.0 MiB / 3.8 MiB | 435.2 MB/s | 9/8 | 69,017 | 681.3s / 1,169,076 msg/s |
| Dekaf | 2026-07-16T23:18:56.4528253+00:00 | 3 | 11.0 MiB / 9.9 MiB | 435.2 MB/s | 9/8 | 69,017 | 690.3s / 1,198,749 msg/s |
| Dekaf | 2026-07-16T23:19:06.4545369+00:00 | 1 | 5.0 MiB / 5.0 MiB | 473.2 MB/s | 9/11 | 525,809 | 700.3s / 1,155,943 msg/s |
| Dekaf | 2026-07-16T23:19:15.4556714+00:00 | 1 | 5.0 MiB / 4.5 MiB | 473.2 MB/s | 9/11 | 534,930 | 709.3s / 1,163,000 msg/s |
| Dekaf | 2026-07-16T23:19:24.4582216+00:00 | 1 | 6.0 MiB / 4.7 MiB | 473.2 MB/s | 9/11 | 544,354 | 718.3s / 1,010,575 msg/s |
| Dekaf | 2026-07-16T23:19:33.4602679+00:00 | 1 | 5.0 MiB / 5.0 MiB | 473.2 MB/s | 9/11 | 552,913 | 727.3s / 1,142,745 msg/s |
| Dekaf | 2026-07-16T23:19:42.4618627+00:00 | 2 | 8.0 MiB / 5.4 MiB | 440.7 MB/s | 7/11 | 62,978 | 736.3s / 1,135,924 msg/s |
| Dekaf | 2026-07-16T23:19:51.4637047+00:00 | 2 | 8.0 MiB / 6.2 MiB | 440.7 MB/s | 7/11 | 64,068 | 745.3s / 1,131,703 msg/s |
| Dekaf | 2026-07-16T23:20:00.4665327+00:00 | 2 | 8.0 MiB / 5.8 MiB | 440.7 MB/s | 7/11 | 65,200 | 754.3s / 1,019,885 msg/s |
| Dekaf | 2026-07-16T23:20:09.4702231+00:00 | 2 | 9.0 MiB / 3.3 MiB | 440.7 MB/s | 7/11 | 65,872 | 763.3s / 1,122,400 msg/s |
| Dekaf | 2026-07-16T23:20:18.4711645+00:00 | 3 | 9.0 MiB / 2.0 MiB | 435.2 MB/s | 11/9 | 75,133 | 772.3s / 1,134,218 msg/s |
| Dekaf | 2026-07-16T23:20:27.474493+00:00 | 3 | 9.0 MiB / 9.0 MiB | 435.2 MB/s | 11/9 | 76,324 | 781.3s / 1,181,185 msg/s |
| Dekaf | 2026-07-16T23:20:36.4759324+00:00 | 3 | 9.0 MiB / 7.1 MiB | 435.2 MB/s | 11/9 | 77,636 | 790.3s / 1,054,308 msg/s |
| Dekaf | 2026-07-16T23:20:45.480009+00:00 | 3 | 7.0 MiB / 2.9 MiB | 435.2 MB/s | 11/9 | 79,255 | 799.3s / 1,115,790 msg/s |
| Dekaf | 2026-07-16T23:20:55.4845727+00:00 | 1 | 7.0 MiB / 3.7 MiB | 473.2 MB/s | 10/12 | 631,585 | 809.3s / 1,102,206 msg/s |
| Dekaf | 2026-07-16T23:21:04.4854824+00:00 | 1 | 7.0 MiB / 6.3 MiB | 473.2 MB/s | 10/12 | 639,176 | 818.3s / 1,129,879 msg/s |
| Dekaf | 2026-07-16T23:21:13.4894948+00:00 | 1 | 6.0 MiB / 6.0 MiB | 473.2 MB/s | 10/13 | 647,361 | 827.3s / 1,127,800 msg/s |
| Dekaf | 2026-07-16T23:21:22.4906448+00:00 | 1 | 6.0 MiB / 5.3 MiB | 473.2 MB/s | 10/13 | 655,903 | 836.3s / 1,100,497 msg/s |
| Dekaf | 2026-07-16T23:21:31.4934754+00:00 | 2 | 9.0 MiB / 5.6 MiB | 440.7 MB/s | 8/12 | 73,124 | 845.3s / 1,155,534 msg/s |
| Dekaf | 2026-07-16T23:21:40.4964859+00:00 | 2 | 8.0 MiB / 2.2 MiB | 440.7 MB/s | 8/12 | 74,207 | 854.3s / 1,145,719 msg/s |
| Dekaf | 2026-07-16T23:21:49.497724+00:00 | 2 | 8.0 MiB / 8.0 MiB | 440.7 MB/s | 8/12 | 74,925 | 863.3s / 1,139,333 msg/s |
| Dekaf | 2026-07-16T23:21:58.5060328+00:00 | 2 | 9.0 MiB / 5.2 MiB | 440.7 MB/s | 9/12 | 76,035 | 872.3s / 1,111,472 msg/s |
| Dekaf | 2026-07-16T23:22:07.5069265+00:00 | 3 | 9.0 MiB / 3.8 MiB | 435.2 MB/s | 11/11 | 93,992 | 881.3s / 1,101,338 msg/s |
| Dekaf | 2026-07-16T23:22:16.5074205+00:00 | 3 | 7.0 MiB / 7.0 MiB | 435.2 MB/s | 11/11 | 95,939 | 890.3s / 1,135,794 msg/s |
| Dekaf | 2026-07-16T23:22:25.5113741+00:00 | 3 | 7.0 MiB / 6.6 MiB | 435.2 MB/s | 11/11 | 97,393 | 899.3s / 1,094,565 msg/s |
*2,598 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Acks All), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-16T23:07:56.3177289+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 9.4 MiB |
| Dekaf | 2026-07-16T23:07:56.3189337+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-16T23:08:11.4125384+00:00 | 2 | capacity | failed | 15,091ms | 16.0 MiB / 6.7 MiB |
| Dekaf | 2026-07-16T23:08:11.4228065+00:00 | 3 | capacity | succeeded | 15,104ms | 14.0 MiB / 6.2 MiB |
| Dekaf | 2026-07-16T23:08:14.4306694+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-16T23:08:29.4991167+00:00 | 3 | capacity | succeeded | 15,068ms | 12.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-16T23:08:32.5114202+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-16T23:08:41.6128707+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 11.5 MiB |
| Dekaf | 2026-07-16T23:08:47.5709292+00:00 | 3 | capacity | failed | 15,059ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-16T23:08:56.6000068+00:00 | 2 | capacity | failed | 15,062ms | 16.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-16T23:09:17.6672091+00:00 | 3 | capacity | started | 0ms | 13.0 MiB / 4.2 MiB |
| Dekaf | 2026-07-16T23:09:26.7007953+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 10.8 MiB |
| Dekaf | 2026-07-16T23:09:32.7196667+00:00 | 3 | capacity | failed | 15,052ms | 12.0 MiB / 7.7 MiB |
| Dekaf | 2026-07-16T23:09:41.757711+00:00 | 2 | capacity | failed | 15,057ms | 16.0 MiB / 9.4 MiB |
| Dekaf | 2026-07-16T23:09:41.940714+00:00 | 1 | capacity | failed | 15,102ms | 16.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-16T23:10:11.8544191+00:00 | 2 | capacity | started | 0ms | 18.0 MiB / 9.7 MiB |
| Dekaf | 2026-07-16T23:10:12.0759198+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 11.7 MiB |
| Dekaf | 2026-07-16T23:10:20.8954254+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-16T23:10:26.9390745+00:00 | 2 | capacity | succeeded | 15,084ms | 18.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-16T23:10:27.123382+00:00 | 1 | capacity | succeeded | 15,047ms | 14.0 MiB / 7.5 MiB |
| Dekaf | 2026-07-16T23:10:35.9598439+00:00 | 3 | capacity | succeeded | 15,064ms | 8.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-16T23:10:38.967775+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-16T23:10:45.1761828+00:00 | 1 | capacity | succeeded | 15,040ms | 12.0 MiB / 7.3 MiB |
| Dekaf | 2026-07-16T23:10:54.0294364+00:00 | 3 | capacity | failed | 15,061ms | 8.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-16T23:10:57.0606813+00:00 | 2 | capacity | started | 0ms | 20.0 MiB / 4.5 MiB |
| Dekaf | 2026-07-16T23:11:06.233519+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 7.2 MiB |
| Dekaf | 2026-07-16T23:11:12.0931205+00:00 | 2 | capacity | failed | 15,032ms | 18.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-16T23:11:21.2885464+00:00 | 1 | capacity | succeeded | 15,055ms | 8.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-16T23:11:24.3050641+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 6.4 MiB |
| Dekaf | 2026-07-16T23:11:39.1894494+00:00 | 3 | capacity | failed | 15,056ms | 8.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-16T23:11:42.1929832+00:00 | 2 | capacity | started | 0ms | 15.0 MiB / 7.2 MiB |
| Dekaf | 2026-07-16T23:11:57.2398186+00:00 | 2 | capacity | succeeded | 15,046ms | 15.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-16T23:12:00.251751+00:00 | 2 | capacity | started | 0ms | 13.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-16T23:12:09.4824277+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 6.9 MiB |
| Dekaf | 2026-07-16T23:12:15.298934+00:00 | 2 | capacity | failed | 15,047ms | 15.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-16T23:12:24.3090033+00:00 | 3 | capacity | failed | 15,045ms | 8.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-16T23:12:45.4428149+00:00 | 2 | capacity | started | 0ms | 16.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-16T23:12:54.450106+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 3.0 MiB |
| Dekaf | 2026-07-16T23:13:00.4894846+00:00 | 2 | capacity | failed | 15,046ms | 15.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-16T23:13:09.5017997+00:00 | 3 | capacity | succeeded | 15,051ms | 9.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-16T23:13:09.6639515+00:00 | 1 | capacity | succeeded | 15,031ms | 7.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-16T23:13:27.7250476+00:00 | 1 | capacity | succeeded | 15,048ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-16T23:13:30.5818071+00:00 | 2 | capacity | started | 0ms | 13.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-16T23:13:39.5854291+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-16T23:13:45.6202916+00:00 | 2 | capacity | succeeded | 15,038ms | 13.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-16T23:13:45.7824793+00:00 | 1 | capacity | failed | 15,045ms | 6.0 MiB / 6.5 MiB |
| Dekaf | 2026-07-16T23:13:54.6267373+00:00 | 3 | capacity | failed | 15,041ms | 9.0 MiB / 3.8 MiB |
| Dekaf | 2026-07-16T23:14:03.67487+00:00 | 2 | capacity | succeeded | 15,047ms | 11.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-16T23:14:15.8876362+00:00 | 1 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-16T23:14:30.9315687+00:00 | 1 | capacity | succeeded | 15,044ms | 5.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-16T23:14:33.7510505+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 4.2 MiB |
| Dekaf | 2026-07-16T23:14:39.7452909+00:00 | 3 | capacity | succeeded | 15,037ms | 10.0 MiB / 8.4 MiB |
| Dekaf | 2026-07-16T23:14:48.7894842+00:00 | 2 | capacity | succeeded | 15,038ms | 9.0 MiB / 7.9 MiB |
| Dekaf | 2026-07-16T23:14:48.9947661+00:00 | 1 | capacity | succeeded | 15,050ms | 6.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-16T23:15:18.9200636+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-16T23:15:19.0720811+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 4.4 MiB |
| Dekaf | 2026-07-16T23:15:24.9184844+00:00 | 3 | capacity | succeeded | 15,090ms | 11.0 MiB / 1.4 MiB |
| Dekaf | 2026-07-16T23:15:34.1125029+00:00 | 1 | capacity | failed | 15,040ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-16T23:15:36.9684916+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-16T23:15:54.9953229+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-16T23:16:04.1862647+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 4.8 MiB |
| Dekaf | 2026-07-16T23:16:10.0235051+00:00 | 3 | capacity | failed | 15,028ms | 11.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-16T23:16:22.1013078+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-16T23:16:37.1350496+00:00 | 2 | capacity | failed | 15,033ms | 7.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-16T23:16:49.3203273+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 1.1 MiB |
| Dekaf | 2026-07-16T23:16:55.1728051+00:00 | 3 | capacity | succeeded | 15,049ms | 12.0 MiB / 9.2 MiB |
| Dekaf | 2026-07-16T23:17:04.4113222+00:00 | 1 | capacity | failed | 15,090ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-16T23:17:22.2863055+00:00 | 2 | capacity | failed | 15,053ms | 7.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-16T23:17:25.2834129+00:00 | 3 | capacity | started | 0ms | 13.0 MiB / 4.4 MiB |
| Dekaf | 2026-07-16T23:17:34.5031803+00:00 | 1 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-16T23:17:49.5501177+00:00 | 1 | capacity | failed | 15,046ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-16T23:17:52.4215511+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-16T23:18:10.4739943+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-16T23:18:19.6259324+00:00 | 1 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-16T23:18:25.5227862+00:00 | 3 | capacity | failed | 15,048ms | 13.0 MiB / 0.9 MiB |
| Dekaf | 2026-07-16T23:18:37.5571294+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 0.9 MiB |
| Dekaf | 2026-07-16T23:18:37.6787731+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-16T23:18:52.7256777+00:00 | 1 | capacity | failed | 15,046ms | 5.0 MiB / 4.4 MiB |
| Dekaf | 2026-07-16T23:18:55.6175791+00:00 | 3 | capacity | started | 0ms | 11.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-16T23:19:10.6630675+00:00 | 3 | capacity | succeeded | 15,047ms | 11.0 MiB / 2.5 MiB |
| Dekaf | 2026-07-16T23:19:22.8345445+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-16T23:19:37.7210685+00:00 | 2 | capacity | failed | 15,037ms | 8.0 MiB / 6.8 MiB |
| Dekaf | 2026-07-16T23:19:37.9228189+00:00 | 1 | capacity | failed | 15,088ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-16T23:19:55.792691+00:00 | 3 | capacity | succeeded | 15,041ms | 9.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-16T23:19:58.8012979+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 3.8 MiB |
| Dekaf | 2026-07-16T23:20:08.0247426+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-16T23:20:13.8516909+00:00 | 3 | capacity | failed | 15,047ms | 9.0 MiB / 6.9 MiB |
| Dekaf | 2026-07-16T23:20:22.9077006+00:00 | 2 | capacity | succeeded | 15,086ms | 9.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-16T23:20:43.9604375+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-16T23:20:53.0011883+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 2.0 MiB |
| Dekaf | 2026-07-16T23:20:59.0051556+00:00 | 3 | capacity | failed | 15,044ms | 9.0 MiB / 3.0 MiB |
| Dekaf | 2026-07-16T23:21:08.0492495+00:00 | 2 | capacity | failed | 15,048ms | 9.0 MiB / 6.2 MiB |
| Dekaf | 2026-07-16T23:21:08.2024876+00:00 | 1 | capacity | failed | 15,057ms | 6.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-16T23:21:38.1406369+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-16T23:21:38.2814029+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-16T23:21:44.1379556+00:00 | 3 | capacity | failed | 15,050ms | 9.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-16T23:21:53.3236627+00:00 | 1 | capacity | succeeded | 15,042ms | 7.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-16T23:21:56.1964879+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-16T23:22:14.2381032+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-16T23:22:23.4507245+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 3.9 MiB |
*38 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Acks All), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 110 |
| Dekaf | 1 | 0.002–0.004ms | 149 |
| Dekaf | 1 | 0.004–0.008ms | 545 |
| Dekaf | 1 | 0.008–0.016ms | 1,526 |
| Dekaf | 1 | 0.016–0.032ms | 3,151 |
| Dekaf | 1 | 0.032–0.064ms | 4,728 |
| Dekaf | 1 | 0.064–0.128ms | 6,651 |
| Dekaf | 1 | 0.128–0.256ms | 13,152 |
| Dekaf | 1 | 0.256–0.512ms | 27,522 |
| Dekaf | 1 | 0.512–1.024ms | 45,508 |
| Dekaf | 1 | 1.024–2.048ms | 36,823 |
| Dekaf | 1 | 2.048–4.096ms | 15,496 |
| Dekaf | 1 | 4.096–8.192ms | 5,239 |
| Dekaf | 1 | 8.192–16.384ms | 1,647 |
| Dekaf | 1 | 16.384–32.768ms | 735 |
| Dekaf | 1 | 32.768–65.536ms | 77 |
| Dekaf | 1 | 65.536–131.072ms | 1 |
| Dekaf | 2 | 0.001–0.002ms | 14 |
| Dekaf | 2 | 0.002–0.004ms | 22 |
| Dekaf | 2 | 0.004–0.008ms | 100 |
| Dekaf | 2 | 0.008–0.016ms | 216 |
| Dekaf | 2 | 0.016–0.032ms | 441 |
| Dekaf | 2 | 0.032–0.064ms | 645 |
| Dekaf | 2 | 0.064–0.128ms | 881 |
| Dekaf | 2 | 0.128–0.256ms | 1,555 |
| Dekaf | 2 | 0.256–0.512ms | 3,084 |
| Dekaf | 2 | 0.512–1.024ms | 4,597 |
| Dekaf | 2 | 1.024–2.048ms | 3,982 |
| Dekaf | 2 | 2.048–4.096ms | 2,006 |
| Dekaf | 2 | 4.096–8.192ms | 694 |
| Dekaf | 2 | 8.192–16.384ms | 204 |
| Dekaf | 2 | 16.384–32.768ms | 61 |
| Dekaf | 2 | 32.768–65.536ms | 12 |
| Dekaf | 3 | 0.001–0.002ms | 20 |
| Dekaf | 3 | 0.002–0.004ms | 25 |
| Dekaf | 3 | 0.004–0.008ms | 115 |
| Dekaf | 3 | 0.008–0.016ms | 304 |
| Dekaf | 3 | 0.016–0.032ms | 610 |
| Dekaf | 3 | 0.032–0.064ms | 785 |
| Dekaf | 3 | 0.064–0.128ms | 1,028 |
| Dekaf | 3 | 0.128–0.256ms | 1,902 |
| Dekaf | 3 | 0.256–0.512ms | 3,791 |
| Dekaf | 3 | 0.512–1.024ms | 5,696 |
| Dekaf | 3 | 1.024–2.048ms | 4,857 |
| Dekaf | 3 | 2.048–4.096ms | 2,327 |
| Dekaf | 3 | 4.096–8.192ms | 905 |
| Dekaf | 3 | 8.192–16.384ms | 346 |
| Dekaf | 3 | 16.384–32.768ms | 133 |
| Dekaf | 3 | 32.768–65.536ms | 17 |

## Delivery Latency Outliers - Producer (Acks All), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 24,000 | 2026-07-16T23:07:26.240208+00:00 | 132.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 26,000 | 2026-07-16T23:07:26.241981+00:00 | 130.6ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 34,000 | 2026-07-16T23:07:26.2529456+00:00 | 120.5ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 36,000 | 2026-07-16T23:07:26.2564998+00:00 | 117.0ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 37,000 | 2026-07-16T23:07:26.2579241+00:00 | 130.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 44,000 | 2026-07-16T23:07:26.2667904+00:00 | 143.6ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 46,000 | 2026-07-16T23:07:26.2688721+00:00 | 141.6ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 47,000 | 2026-07-16T23:07:26.270811+00:00 | 180.6ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 49,000 | 2026-07-16T23:07:26.2726305+00:00 | 101.7ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 52,000 | 2026-07-16T23:07:26.275736+00:00 | 105.0ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 53,000 | 2026-07-16T23:07:26.2773678+00:00 | 110.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 54,000 | 2026-07-16T23:07:26.2782457+00:00 | 141.6ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 56,000 | 2026-07-16T23:07:26.2801923+00:00 | 139.6ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 57,000 | 2026-07-16T23:07:26.2814802+00:00 | 185.8ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 58,000 | 2026-07-16T23:07:26.2827513+00:00 | 103.6ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 59,000 | 2026-07-16T23:07:26.2841409+00:00 | 117.4ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 60,000 | 2026-07-16T23:07:26.2854045+00:00 | 111.8ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 61,000 | 2026-07-16T23:07:26.2864576+00:00 | 108.8ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 62,000 | 2026-07-16T23:07:26.2875056+00:00 | 107.7ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 63,000 | 2026-07-16T23:07:26.2889607+00:00 | 112.6ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 64,000 | 2026-07-16T23:07:26.2902232+00:00 | 161.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 65,000 | 2026-07-16T23:07:26.2918609+00:00 | 100.4ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 66,000 | 2026-07-16T23:07:26.2932304+00:00 | 158.1ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 67,000 | 2026-07-16T23:07:26.2944568+00:00 | 188.5ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 69,000 | 2026-07-16T23:07:26.3007711+00:00 | 116.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 70,000 | 2026-07-16T23:07:26.3021881+00:00 | 101.1ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 71,000 | 2026-07-16T23:07:26.3197115+00:00 | 104.5ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 73,000 | 2026-07-16T23:07:26.3294311+00:00 | 106.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 74,000 | 2026-07-16T23:07:26.3351898+00:00 | 147.8ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 76,000 | 2026-07-16T23:07:26.3379311+00:00 | 162.5ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 77,000 | 2026-07-16T23:07:26.3732757+00:00 | 126.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 84,000 | 2026-07-16T23:07:26.3807997+00:00 | 133.8ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 86,000 | 2026-07-16T23:07:26.3825715+00:00 | 132.0ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 87,000 | 2026-07-16T23:07:26.3891718+00:00 | 136.0ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 89,000 | 2026-07-16T23:07:26.3919755+00:00 | 100.6ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 94,000 | 2026-07-16T23:07:26.4116948+00:00 | 114.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 96,000 | 2026-07-16T23:07:26.4144277+00:00 | 125.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 97,000 | 2026-07-16T23:07:26.4155044+00:00 | 125.0ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 101,000 | 2026-07-16T23:07:26.4230159+00:00 | 105.5ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 104,000 | 2026-07-16T23:07:26.4507429+00:00 | 107.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 105,000 | 2026-07-16T23:07:26.4516495+00:00 | 125.4ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 106,000 | 2026-07-16T23:07:26.4525378+00:00 | 105.5ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 107,000 | 2026-07-16T23:07:26.4537878+00:00 | 104.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 108,000 | 2026-07-16T23:07:26.4550243+00:00 | 149.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 110,000 | 2026-07-16T23:07:26.4570854+00:00 | 120.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 114,000 | 2026-07-16T23:07:26.4664888+00:00 | 106.1ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 115,000 | 2026-07-16T23:07:26.4834826+00:00 | 185.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 116,000 | 2026-07-16T23:07:26.4844242+00:00 | 106.8ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 118,000 | 2026-07-16T23:07:26.487651+00:00 | 181.8ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 120,000 | 2026-07-16T23:07:26.4903839+00:00 | 193.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 124,000 | 2026-07-16T23:07:26.5002575+00:00 | 120.5ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 125,000 | 2026-07-16T23:07:26.5016461+00:00 | 182.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 126,000 | 2026-07-16T23:07:26.5027587+00:00 | 118.0ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 127,000 | 2026-07-16T23:07:26.504072+00:00 | 161.8ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 128,000 | 2026-07-16T23:07:26.505228+00:00 | 192.0ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 130,000 | 2026-07-16T23:07:26.5072205+00:00 | 189.7ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 134,000 | 2026-07-16T23:07:26.5151965+00:00 | 160.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 135,000 | 2026-07-16T23:07:26.5163033+00:00 | 188.7ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 136,000 | 2026-07-16T23:07:26.517202+00:00 | 184.1ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 137,000 | 2026-07-16T23:07:26.5256699+00:00 | 159.7ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 138,000 | 2026-07-16T23:07:26.5268275+00:00 | 178.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 140,000 | 2026-07-16T23:07:26.5289432+00:00 | 190.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 144,000 | 2026-07-16T23:07:26.5331585+00:00 | 192.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 145,000 | 2026-07-16T23:07:26.5340255+00:00 | 187.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 146,000 | 2026-07-16T23:07:26.5352134+00:00 | 190.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 147,000 | 2026-07-16T23:07:26.5413017+00:00 | 191.7ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 148,000 | 2026-07-16T23:07:26.5425842+00:00 | 194.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 149,000 | 2026-07-16T23:07:26.5436583+00:00 | 129.7ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 150,000 | 2026-07-16T23:07:26.5447155+00:00 | 204.8ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 151,000 | 2026-07-16T23:07:26.5777306+00:00 | 101.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 154,000 | 2026-07-16T23:07:26.5814504+00:00 | 187.6ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 155,000 | 2026-07-16T23:07:26.5825276+00:00 | 154.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 156,000 | 2026-07-16T23:07:26.583823+00:00 | 185.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 157,000 | 2026-07-16T23:07:26.5847602+00:00 | 178.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 158,000 | 2026-07-16T23:07:26.5859224+00:00 | 151.5ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 159,000 | 2026-07-16T23:07:26.5870318+00:00 | 108.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 160,000 | 2026-07-16T23:07:26.604964+00:00 | 145.8ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 161,000 | 2026-07-16T23:07:26.6061519+00:00 | 171.6ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 162,000 | 2026-07-16T23:07:26.6072424+00:00 | 170.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 163,000 | 2026-07-16T23:07:26.6080865+00:00 | 109.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 165,000 | 2026-07-16T23:07:26.6709302+00:00 | 118.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 167,000 | 2026-07-16T23:07:26.6732026+00:00 | 109.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 168,000 | 2026-07-16T23:07:26.674211+00:00 | 115.0ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 169,000 | 2026-07-16T23:07:26.6750511+00:00 | 102.6ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 170,000 | 2026-07-16T23:07:26.684501+00:00 | 105.1ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 171,000 | 2026-07-16T23:07:26.6853806+00:00 | 113.7ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 172,000 | 2026-07-16T23:07:26.686787+00:00 | 112.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 173,000 | 2026-07-16T23:07:26.6882401+00:00 | 110.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 174,000 | 2026-07-16T23:07:26.6892049+00:00 | 144.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 175,000 | 2026-07-16T23:07:26.690007+00:00 | 111.5ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 176,000 | 2026-07-16T23:07:26.6974125+00:00 | 136.0ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 177,000 | 2026-07-16T23:07:26.6986354+00:00 | 134.8ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 178,000 | 2026-07-16T23:07:26.6999+00:00 | 114.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 179,000 | 2026-07-16T23:07:26.7009057+00:00 | 136.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 180,000 | 2026-07-16T23:07:26.7021954+00:00 | 111.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 181,000 | 2026-07-16T23:07:26.7030324+00:00 | 186.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 182,000 | 2026-07-16T23:07:26.7038418+00:00 | 186.1ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 183,000 | 2026-07-16T23:07:26.7048152+00:00 | 133.0ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 184,000 | 2026-07-16T23:07:26.7059632+00:00 | 151.6ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 185,000 | 2026-07-16T23:07:26.7069129+00:00 | 120.7ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 186,000 | 2026-07-16T23:07:26.7077259+00:00 | 149.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 187,000 | 2026-07-16T23:07:26.7088583+00:00 | 148.7ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 188,000 | 2026-07-16T23:07:26.7096718+00:00 | 118.0ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 189,000 | 2026-07-16T23:07:26.7200769+00:00 | 170.1ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 190,000 | 2026-07-16T23:07:26.7210993+00:00 | 106.5ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 191,000 | 2026-07-16T23:07:26.7265406+00:00 | 211.4ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 192,000 | 2026-07-16T23:07:26.7273196+00:00 | 210.6ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 193,000 | 2026-07-16T23:07:26.7285506+00:00 | 184.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 194,000 | 2026-07-16T23:07:26.7293238+00:00 | 158.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 195,000 | 2026-07-16T23:07:26.7370178+00:00 | 102.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 196,000 | 2026-07-16T23:07:26.7380385+00:00 | 149.5ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 197,000 | 2026-07-16T23:07:26.7397315+00:00 | 159.4ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 198,000 | 2026-07-16T23:07:26.7405232+00:00 | 119.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 199,000 | 2026-07-16T23:07:26.7417888+00:00 | 198.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 200,000 | 2026-07-16T23:07:26.7631407+00:00 | 112.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 201,000 | 2026-07-16T23:07:26.7642405+00:00 | 223.4ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 202,000 | 2026-07-16T23:07:26.7652564+00:00 | 222.4ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 203,000 | 2026-07-16T23:07:26.7662082+00:00 | 221.5ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 204,000 | 2026-07-16T23:07:26.7671692+00:00 | 138.0ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 205,000 | 2026-07-16T23:07:26.7681842+00:00 | 137.7ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 206,000 | 2026-07-16T23:07:26.7692177+00:00 | 145.0ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 207,000 | 2026-07-16T23:07:26.7702577+00:00 | 166.0ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 208,000 | 2026-07-16T23:07:26.7712732+00:00 | 134.6ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 209,000 | 2026-07-16T23:07:26.7734404+00:00 | 248.4ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 210,000 | 2026-07-16T23:07:26.7745645+00:00 | 150.5ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 211,000 | 2026-07-16T23:07:26.7776191+00:00 | 242.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 212,000 | 2026-07-16T23:07:26.7785261+00:00 | 242.0ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 213,000 | 2026-07-16T23:07:26.7796772+00:00 | 242.1ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 214,000 | 2026-07-16T23:07:26.7807589+00:00 | 147.1ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 215,000 | 2026-07-16T23:07:26.7818452+00:00 | 154.7ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 216,000 | 2026-07-16T23:07:26.7894228+00:00 | 138.4ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 217,000 | 2026-07-16T23:07:26.7996016+00:00 | 141.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 218,000 | 2026-07-16T23:07:26.8005946+00:00 | 161.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 219,000 | 2026-07-16T23:07:26.8042399+00:00 | 237.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 220,000 | 2026-07-16T23:07:26.8057355+00:00 | 150.4ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 221,000 | 2026-07-16T23:07:26.806762+00:00 | 238.7ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 222,000 | 2026-07-16T23:07:26.8075768+00:00 | 237.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 223,000 | 2026-07-16T23:07:26.8377412+00:00 | 207.7ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 224,000 | 2026-07-16T23:07:26.8386014+00:00 | 106.4ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 225,000 | 2026-07-16T23:07:26.8396234+00:00 | 161.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 226,000 | 2026-07-16T23:07:26.8408693+00:00 | 125.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 227,000 | 2026-07-16T23:07:26.8418909+00:00 | 120.4ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 228,000 | 2026-07-16T23:07:26.8426878+00:00 | 158.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 229,000 | 2026-07-16T23:07:26.8902856+00:00 | 166.4ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 230,000 | 2026-07-16T23:07:26.8911358+00:00 | 123.0ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 231,000 | 2026-07-16T23:07:26.8919765+00:00 | 232.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 232,000 | 2026-07-16T23:07:26.8933783+00:00 | 231.5ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 233,000 | 2026-07-16T23:07:26.8943857+00:00 | 231.7ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 235,000 | 2026-07-16T23:07:26.9116029+00:00 | 110.2ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 238,000 | 2026-07-16T23:07:26.9158807+00:00 | 128.7ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 239,000 | 2026-07-16T23:07:26.9170157+00:00 | 209.1ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 240,000 | 2026-07-16T23:07:26.9178785+00:00 | 114.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 241,000 | 2026-07-16T23:07:26.9381517+00:00 | 191.1ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 242,000 | 2026-07-16T23:07:26.9409409+00:00 | 188.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 243,000 | 2026-07-16T23:07:26.942315+00:00 | 183.8ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 245,000 | 2026-07-16T23:07:26.94747+00:00 | 115.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 249,000 | 2026-07-16T23:07:26.9911092+00:00 | 151.6ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 251,000 | 2026-07-16T23:07:26.9974504+00:00 | 155.9ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 252,000 | 2026-07-16T23:07:26.9987996+00:00 | 154.6ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 253,000 | 2026-07-16T23:07:27.0022713+00:00 | 139.8ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 259,000 | 2026-07-16T23:07:27.0260431+00:00 | 127.3ms | GC pause | - | - | 1.0s / 306,331 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 261,000 | 2026-07-16T23:07:27.0301444+00:00 | 139.2ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 262,000 | 2026-07-16T23:07:27.0414784+00:00 | 127.9ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 263,000 | 2026-07-16T23:07:27.0438177+00:00 | 125.8ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 269,000 | 2026-07-16T23:07:27.0537973+00:00 | 130.3ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 271,000 | 2026-07-16T23:07:27.0560148+00:00 | 128.1ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 272,000 | 2026-07-16T23:07:27.0570374+00:00 | 127.0ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 273,000 | 2026-07-16T23:07:27.057507+00:00 | 126.6ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 279,000 | 2026-07-16T23:07:27.1265117+00:00 | 139.2ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 281,000 | 2026-07-16T23:07:27.128894+00:00 | 137.2ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 282,000 | 2026-07-16T23:07:27.1294669+00:00 | 136.6ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 283,000 | 2026-07-16T23:07:27.1300372+00:00 | 135.7ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 289,000 | 2026-07-16T23:07:27.1349338+00:00 | 161.0ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 291,000 | 2026-07-16T23:07:27.1363059+00:00 | 160.6ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 292,000 | 2026-07-16T23:07:27.1421721+00:00 | 154.7ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 293,000 | 2026-07-16T23:07:27.1432047+00:00 | 153.7ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 299,000 | 2026-07-16T23:07:27.1542431+00:00 | 179.3ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 301,000 | 2026-07-16T23:07:27.158486+00:00 | 179.1ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 302,000 | 2026-07-16T23:07:27.1590136+00:00 | 178.5ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 303,000 | 2026-07-16T23:07:27.1598859+00:00 | 177.9ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 304,000 | 2026-07-16T23:07:27.160499+00:00 | 102.5ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 306,000 | 2026-07-16T23:07:27.1617562+00:00 | 101.2ms | GC pause | - | - | 2.0s / 516,494 msg/s | Gen2 +1 / pause +7.5ms |
| Dekaf | 307,000 | 2026-07-16T23:07:27.1694944+00:00 | 100.8ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 309,000 | 2026-07-16T23:07:27.1706476+00:00 | 184.7ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 311,000 | 2026-07-16T23:07:27.1719126+00:00 | 209.6ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 312,000 | 2026-07-16T23:07:27.1725398+00:00 | 208.9ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 313,000 | 2026-07-16T23:07:27.1830154+00:00 | 172.4ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 319,000 | 2026-07-16T23:07:27.1869156+00:00 | 168.5ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 321,000 | 2026-07-16T23:07:27.1881603+00:00 | 194.4ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 322,000 | 2026-07-16T23:07:27.1995786+00:00 | 183.0ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 323,000 | 2026-07-16T23:07:27.2009269+00:00 | 181.6ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 329,000 | 2026-07-16T23:07:27.266992+00:00 | 129.5ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 331,000 | 2026-07-16T23:07:27.2680519+00:00 | 144.0ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 332,000 | 2026-07-16T23:07:27.2686757+00:00 | 143.3ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 333,000 | 2026-07-16T23:07:27.2699697+00:00 | 142.0ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 339,000 | 2026-07-16T23:07:27.3002016+00:00 | 111.8ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 341,000 | 2026-07-16T23:07:27.3017452+00:00 | 117.2ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 342,000 | 2026-07-16T23:07:27.3025315+00:00 | 116.7ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 343,000 | 2026-07-16T23:07:27.3029394+00:00 | 109.1ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 571,000 | 2026-07-16T23:07:27.6937438+00:00 | 143.6ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 572,000 | 2026-07-16T23:07:27.6943403+00:00 | 143.0ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 573,000 | 2026-07-16T23:07:27.6963142+00:00 | 139.1ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 579,000 | 2026-07-16T23:07:27.7108419+00:00 | 126.5ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 581,000 | 2026-07-16T23:07:27.7120676+00:00 | 123.3ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 582,000 | 2026-07-16T23:07:27.7144461+00:00 | 124.5ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 583,000 | 2026-07-16T23:07:27.7149033+00:00 | 131.5ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 589,000 | 2026-07-16T23:07:27.7212213+00:00 | 164.4ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 591,000 | 2026-07-16T23:07:27.723076+00:00 | 141.3ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 592,000 | 2026-07-16T23:07:27.7264876+00:00 | 137.9ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 593,000 | 2026-07-16T23:07:27.7281225+00:00 | 157.5ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 599,000 | 2026-07-16T23:07:27.7420568+00:00 | 148.3ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 601,000 | 2026-07-16T23:07:27.7462624+00:00 | 144.0ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 602,000 | 2026-07-16T23:07:27.7465331+00:00 | 143.8ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 603,000 | 2026-07-16T23:07:27.7469888+00:00 | 163.5ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 609,000 | 2026-07-16T23:07:27.7561934+00:00 | 163.2ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 611,000 | 2026-07-16T23:07:27.7567433+00:00 | 164.3ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 612,000 | 2026-07-16T23:07:27.758333+00:00 | 162.7ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 613,000 | 2026-07-16T23:07:27.7598659+00:00 | 165.5ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 617,000 | 2026-07-16T23:07:27.7634096+00:00 | 129.6ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 631,000 | 2026-07-16T23:07:27.8481251+00:00 | 110.2ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 632,000 | 2026-07-16T23:07:27.8486809+00:00 | 109.6ms | throughput collapse | - | - | 2.0s / 516,494 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 861,000 | 2026-07-16T23:07:28.2319433+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 862,000 | 2026-07-16T23:07:28.2326076+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 869,000 | 2026-07-16T23:07:28.2418407+00:00 | 128.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 871,000 | 2026-07-16T23:07:28.2470203+00:00 | 129.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 872,000 | 2026-07-16T23:07:28.2475518+00:00 | 129.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 873,000 | 2026-07-16T23:07:28.2478729+00:00 | 122.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 879,000 | 2026-07-16T23:07:28.2556476+00:00 | 115.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 881,000 | 2026-07-16T23:07:28.2567425+00:00 | 141.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 882,000 | 2026-07-16T23:07:28.2600389+00:00 | 137.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 883,000 | 2026-07-16T23:07:28.2607913+00:00 | 136.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 889,000 | 2026-07-16T23:07:28.2673215+00:00 | 130.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 891,000 | 2026-07-16T23:07:28.2714811+00:00 | 134.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 892,000 | 2026-07-16T23:07:28.2719105+00:00 | 133.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 893,000 | 2026-07-16T23:07:28.272412+00:00 | 133.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 897,000 | 2026-07-16T23:07:28.2739155+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 899,000 | 2026-07-16T23:07:28.2752328+00:00 | 142.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 901,000 | 2026-07-16T23:07:28.2792813+00:00 | 138.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 902,000 | 2026-07-16T23:07:28.2799494+00:00 | 150.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 903,000 | 2026-07-16T23:07:28.2805417+00:00 | 137.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 904,000 | 2026-07-16T23:07:28.2813884+00:00 | 107.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 905,000 | 2026-07-16T23:07:28.2818621+00:00 | 110.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 908,000 | 2026-07-16T23:07:28.2897473+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 909,000 | 2026-07-16T23:07:28.2902364+00:00 | 139.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 910,000 | 2026-07-16T23:07:28.2905681+00:00 | 111.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 911,000 | 2026-07-16T23:07:28.2917543+00:00 | 145.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 912,000 | 2026-07-16T23:07:28.335934+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 913,000 | 2026-07-16T23:07:28.3367585+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,074,000 | 2026-07-16T23:07:28.5877054+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,084,000 | 2026-07-16T23:07:28.5948578+00:00 | 108.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,086,000 | 2026-07-16T23:07:28.5956197+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,087,000 | 2026-07-16T23:07:28.595875+00:00 | 111.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,094,000 | 2026-07-16T23:07:28.6002617+00:00 | 119.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,096,000 | 2026-07-16T23:07:28.6145123+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,097,000 | 2026-07-16T23:07:28.6150401+00:00 | 113.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,106,000 | 2026-07-16T23:07:28.6264566+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 626,193 msg/s | Gen2 +0 / pause +0.0ms |
| Confluent | 70,000 | 2026-07-16T23:22:26.455576+00:00 | 103.3ms | GC pause | - | - | 1.0s / 669,384 msg/s | Gen2 +1 / pause +94.2ms |
| Confluent | 73,000 | 2026-07-16T23:22:26.4588611+00:00 | 110.2ms | GC pause | - | - | 1.0s / 669,384 msg/s | Gen2 +1 / pause +94.2ms |
| Confluent | 80,000 | 2026-07-16T23:22:26.4686944+00:00 | 102.8ms | GC pause | - | - | 1.0s / 669,384 msg/s | Gen2 +1 / pause +94.2ms |
| Confluent | 83,000 | 2026-07-16T23:22:26.4729922+00:00 | 104.8ms | GC pause | - | - | 1.0s / 669,384 msg/s | Gen2 +1 / pause +94.2ms |
| Confluent | 90,000 | 2026-07-16T23:22:26.483605+00:00 | 124.4ms | GC pause | - | - | 1.0s / 669,384 msg/s | Gen2 +1 / pause +94.2ms |
| Confluent | 93,000 | 2026-07-16T23:22:26.4879138+00:00 | 128.2ms | GC pause | - | - | 1.0s / 669,384 msg/s | Gen2 +1 / pause +94.2ms |
| Confluent | 100,000 | 2026-07-16T23:22:26.4984784+00:00 | 142.7ms | GC pause | - | - | 1.0s / 669,384 msg/s | Gen2 +1 / pause +94.2ms |
| Confluent | 103,000 | 2026-07-16T23:22:26.5054268+00:00 | 139.9ms | GC pause | - | - | 1.0s / 669,384 msg/s | Gen2 +1 / pause +94.2ms |
| Confluent | 104,000 | 2026-07-16T23:22:26.5071179+00:00 | 105.9ms | GC pause | - | - | 1.0s / 669,384 msg/s | Gen2 +1 / pause +94.2ms |
| Confluent | 110,000 | 2026-07-16T23:22:26.5183334+00:00 | 127.8ms | GC pause | - | - | 1.0s / 669,384 msg/s | Gen2 +1 / pause +94.2ms |
| Confluent | 113,000 | 2026-07-16T23:22:26.5231333+00:00 | 123.2ms | GC pause | - | - | 1.0s / 669,384 msg/s | Gen2 +1 / pause +94.2ms |
| Confluent | 114,000 | 2026-07-16T23:22:26.5248322+00:00 | 107.2ms | GC pause | - | - | 1.0s / 669,384 msg/s | Gen2 +1 / pause +94.2ms |
| Confluent | 120,000 | 2026-07-16T23:22:26.5344788+00:00 | 120.8ms | GC pause | - | - | 1.0s / 669,384 msg/s | Gen2 +1 / pause +94.2ms |
| Confluent | 123,000 | 2026-07-16T23:22:26.5392012+00:00 | 116.2ms | GC pause | - | - | 1.0s / 669,384 msg/s | Gen2 +1 / pause +94.2ms |
| Confluent | 130,000 | 2026-07-16T23:22:26.5512623+00:00 | 113.9ms | GC pause | - | - | 1.0s / 669,384 msg/s | Gen2 +1 / pause +94.2ms |
| Confluent | 133,000 | 2026-07-16T23:22:26.55689+00:00 | 108.6ms | GC pause | - | - | 1.0s / 669,384 msg/s | Gen2 +1 / pause +94.2ms |
| Confluent | 153,000 | 2026-07-16T23:22:26.5935126+00:00 | 106.0ms | GC pause | - | - | 1.0s / 669,384 msg/s | Gen2 +1 / pause +94.2ms |
| Confluent | 162,140,000 | 2026-07-16T23:25:27.8981353+00:00 | 101.5ms | GC pause | - | - | 182.1s / 864,720 msg/s | Gen2 +0 / pause +103.9ms |
| Confluent | 333,637,000 | 2026-07-16T23:28:38.8789568+00:00 | 101.5ms | GC pause | - | - | 373.3s / 854,848 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 333,638,000 | 2026-07-16T23:28:38.8799206+00:00 | 100.6ms | GC pause | - | - | 373.3s / 854,848 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 466,624,000 | 2026-07-16T23:31:08.3733818+00:00 | 103.5ms | GC pause | - | - | 522.4s / 870,299 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 466,634,000 | 2026-07-16T23:31:08.3825963+00:00 | 114.9ms | GC pause | - | - | 522.4s / 870,299 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 466,643,000 | 2026-07-16T23:31:08.3942176+00:00 | 101.7ms | GC pause | - | - | 522.4s / 870,299 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 466,644,000 | 2026-07-16T23:31:08.3950075+00:00 | 113.6ms | GC pause | - | - | 522.4s / 870,299 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 466,650,000 | 2026-07-16T23:31:08.3999456+00:00 | 100.7ms | GC pause | - | - | 522.4s / 870,299 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 466,654,000 | 2026-07-16T23:31:08.4041533+00:00 | 110.1ms | GC pause | - | - | 522.4s / 870,299 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 466,660,000 | 2026-07-16T23:31:08.4107398+00:00 | 105.4ms | GC pause | - | - | 522.4s / 870,299 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 466,662,000 | 2026-07-16T23:31:08.412566+00:00 | 112.9ms | GC pause | - | - | 522.4s / 870,299 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 466,663,000 | 2026-07-16T23:31:08.4141362+00:00 | 105.1ms | GC pause | - | - | 522.4s / 870,299 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 466,664,000 | 2026-07-16T23:31:08.4163081+00:00 | 100.3ms | GC pause | - | - | 522.4s / 870,299 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 466,665,000 | 2026-07-16T23:31:08.4171453+00:00 | 100.1ms | GC pause | - | - | 522.4s / 870,299 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 466,672,000 | 2026-07-16T23:31:08.4283112+00:00 | 101.4ms | GC pause | - | - | 522.4s / 870,299 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 647,530,000 | 2026-07-16T23:34:30.360986+00:00 | 105.3ms | GC pause | - | - | 724.5s / 858,699 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 647,533,000 | 2026-07-16T23:34:30.3652631+00:00 | 107.5ms | GC pause | - | - | 724.5s / 858,699 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 647,540,000 | 2026-07-16T23:34:30.3712182+00:00 | 110.9ms | GC pause | - | - | 724.5s / 858,699 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 647,543,000 | 2026-07-16T23:34:30.3729913+00:00 | 109.5ms | GC pause | - | - | 724.5s / 858,699 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 647,550,000 | 2026-07-16T23:34:30.3773184+00:00 | 112.3ms | GC pause | - | - | 724.5s / 858,699 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 647,553,000 | 2026-07-16T23:34:30.3800339+00:00 | 118.3ms | GC pause | - | - | 724.5s / 858,699 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 647,560,000 | 2026-07-16T23:34:30.3847336+00:00 | 116.1ms | GC pause | - | - | 724.5s / 858,699 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 647,563,000 | 2026-07-16T23:34:30.3871472+00:00 | 117.2ms | GC pause | - | - | 724.5s / 858,699 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 647,570,000 | 2026-07-16T23:34:30.3974024+00:00 | 111.2ms | GC pause | - | - | 724.5s / 858,699 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 647,573,000 | 2026-07-16T23:34:30.3998865+00:00 | 108.8ms | GC pause | - | - | 724.5s / 858,699 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 647,580,000 | 2026-07-16T23:34:30.4091662+00:00 | 111.0ms | GC pause | - | - | 724.5s / 858,699 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 647,583,000 | 2026-07-16T23:34:30.4120542+00:00 | 112.5ms | GC pause | - | - | 724.5s / 858,699 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 647,590,000 | 2026-07-16T23:34:30.4240955+00:00 | 106.5ms | GC pause | - | - | 724.5s / 858,699 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 647,593,000 | 2026-07-16T23:34:30.4291171+00:00 | 102.1ms | GC pause | - | - | 724.5s / 858,699 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 734,150,000 | 2026-07-16T23:36:07.3700845+00:00 | 106.6ms | GC pause | - | - | 821.5s / 896,999 msg/s | Gen2 +0 / pause +97.1ms |
| Confluent | 734,153,000 | 2026-07-16T23:36:07.3718014+00:00 | 105.0ms | GC pause | - | - | 821.5s / 896,999 msg/s | Gen2 +0 / pause +97.1ms |
| Confluent | 734,173,000 | 2026-07-16T23:36:07.3887768+00:00 | 103.5ms | GC pause | - | - | 821.5s / 896,999 msg/s | Gen2 +0 / pause +97.1ms |
| Confluent | 734,180,000 | 2026-07-16T23:36:07.3963127+00:00 | 103.2ms | GC pause | - | - | 821.5s / 896,999 msg/s | Gen2 +0 / pause +97.1ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*241 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.52x less CPU per message** than Confluent.Kafka for producer (acks all), 3 brokers; comparison throughput is 1.27x.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,610,650 | 1,577,865–1,644,116 | 0.92 | 1.11x |
| Confluent | 2 | 1,449,433 | 1,449,292–1,449,573 | 1.25 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.72 | 699.43 | 1,888,910 | 1,927,431 | +41.8% | +4.14% | 1801.40 | 1,888,910 | 0 | 1.37 |
| Dekaf (dekaf-first) | 0.87 | 890.87 | 1,624,161 | 1,644,116 | +0.2% | +0.07% | 1548.92 | 1,624,161 | 0 | 1.41 |
| Dekaf (confluent-first) | 0.97 | 991.46 | 1,566,474 | 1,577,865 | -1.5% | -0.20% | 1493.91 | 1,566,474 | 0 | 1.51 |
| Confluent (confluent-first) | 1.25 | - | 1,435,816 | 1,449,573 | +3.0% | +0.27% | 1369.30 | 1,435,816 | 0 | 1.80 |
| Confluent (dekaf-first) | 1.25 | - | 1,442,448 | 1,449,292 | -1.8% | -0.17% | 1375.63 | 1,442,448 | 0 | 1.80 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget, Idempotent)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,372,535 | 1525.01 | 1021.12 KB |
| Dekaf | 1 | 1,427,743 | 1586.36 | 1017.77 KB |
| Dekaf (3conn) | 1 | 1,759,664 | 1955.16 | 960.40 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget, Idempotent)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-16T23:07:30.089048+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 438,310 msg/s |
| Dekaf | 2026-07-16T23:07:57.1049147+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1759.6 MB/s | 0/0 | 52,769 | 27.0s / 1,593,157 msg/s |
| Dekaf | 2026-07-16T23:08:25.1108921+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1766.1 MB/s | 1/0 | 115,274 | 55.0s / 1,471,691 msg/s |
| Dekaf | 2026-07-16T23:08:52.1198036+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1766.1 MB/s | 1/0 | 179,540 | 82.0s / 1,337,710 msg/s |
| Dekaf | 2026-07-16T23:09:19.1266898+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1766.1 MB/s | 2/0 | 241,975 | 109.0s / 1,595,574 msg/s |
| Dekaf | 2026-07-16T23:09:46.1432152+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1805.3 MB/s | 2/1 | 311,604 | 136.0s / 1,675,134 msg/s |
| Dekaf | 2026-07-16T23:10:14.1540084+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1805.3 MB/s | 2/1 | 385,885 | 164.0s / 1,682,288 msg/s |
| Dekaf | 2026-07-16T23:10:41.1614015+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1805.3 MB/s | 3/1 | 458,701 | 191.1s / 1,659,186 msg/s |
| Dekaf | 2026-07-16T23:11:08.1790269+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1809.0 MB/s | 3/1 | 528,064 | 218.1s / 1,711,771 msg/s |
| Dekaf | 2026-07-16T23:11:35.1903937+00:00 | 1 | 13.0 MiB / 12.7 MiB | 1809.0 MB/s | 3/2 | 598,383 | 245.1s / 1,625,718 msg/s |
| Dekaf | 2026-07-16T23:12:03.1981979+00:00 | 1 | 11.0 MiB / 10.7 MiB | 1809.0 MB/s | 4/2 | 673,580 | 273.1s / 1,693,863 msg/s |
| Dekaf | 2026-07-16T23:12:30.2019159+00:00 | 1 | 11.0 MiB / 10.9 MiB | 1809.0 MB/s | 4/2 | 751,241 | 300.1s / 1,649,806 msg/s |
| Dekaf | 2026-07-16T23:12:57.207333+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1809.0 MB/s | 4/3 | 812,803 | 327.1s / 1,583,635 msg/s |
| Dekaf | 2026-07-16T23:13:24.2111117+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1809.0 MB/s | 4/3 | 885,957 | 354.1s / 1,624,951 msg/s |
| Dekaf | 2026-07-16T23:13:52.2185825+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1809.0 MB/s | 4/4 | 961,420 | 382.1s / 1,645,796 msg/s |
| Dekaf | 2026-07-16T23:14:19.221511+00:00 | 1 | 11.0 MiB / 10.1 MiB | 1809.0 MB/s | 4/5 | 1,023,924 | 409.1s / 1,657,542 msg/s |
| Dekaf | 2026-07-16T23:14:46.2291023+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1809.0 MB/s | 4/5 | 1,099,966 | 436.1s / 1,614,009 msg/s |
| Dekaf | 2026-07-16T23:15:13.2386626+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1809.0 MB/s | 5/5 | 1,172,435 | 463.1s / 1,518,653 msg/s |
| Dekaf | 2026-07-16T23:15:41.2514985+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1809.0 MB/s | 5/5 | 1,245,606 | 491.2s / 1,627,474 msg/s |
| Dekaf | 2026-07-16T23:16:08.2576966+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1809.0 MB/s | 6/5 | 1,317,681 | 518.2s / 1,651,746 msg/s |
| Dekaf | 2026-07-16T23:16:35.2737097+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1809.0 MB/s | 6/6 | 1,380,809 | 545.2s / 1,675,525 msg/s |
| Dekaf | 2026-07-16T23:17:03.2786306+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1809.0 MB/s | 6/6 | 1,453,071 | 573.2s / 1,618,968 msg/s |
| Dekaf | 2026-07-16T23:17:30.2933404+00:00 | 1 | 11.0 MiB / 10.8 MiB | 1809.0 MB/s | 7/6 | 1,526,449 | 600.2s / 1,678,074 msg/s |
| Dekaf | 2026-07-16T23:17:57.2974256+00:00 | 1 | 9.0 MiB / 3.1 MiB | 1809.0 MB/s | 7/6 | 1,595,301 | 627.2s / 1,490,048 msg/s |
| Dekaf | 2026-07-16T23:18:24.3049026+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1809.0 MB/s | 7/7 | 1,667,299 | 654.2s / 1,623,845 msg/s |
| Dekaf | 2026-07-16T23:18:52.3099152+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1809.0 MB/s | 7/8 | 1,743,462 | 682.2s / 1,601,281 msg/s |
| Dekaf | 2026-07-16T23:19:19.3167112+00:00 | 1 | 9.0 MiB / 7.4 MiB | 1809.0 MB/s | 7/8 | 1,813,146 | 709.2s / 1,411,175 msg/s |
| Dekaf | 2026-07-16T23:19:46.3246914+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1809.0 MB/s | 7/9 | 1,880,676 | 736.2s / 1,631,487 msg/s |
| Dekaf | 2026-07-16T23:20:13.3289639+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1809.0 MB/s | 7/9 | 1,959,185 | 763.2s / 1,674,772 msg/s |
| Dekaf | 2026-07-16T23:20:41.338739+00:00 | 1 | 11.0 MiB / 10.1 MiB | 1809.0 MB/s | 7/10 | 2,041,367 | 791.2s / 1,622,237 msg/s |
| Dekaf | 2026-07-16T23:21:08.3469369+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1809.0 MB/s | 7/11 | 2,112,691 | 818.2s / 1,477,399 msg/s |
| Dekaf | 2026-07-16T23:21:35.3511107+00:00 | 1 | 11.0 MiB / 10.4 MiB | 1809.0 MB/s | 7/11 | 2,190,207 | 845.2s / 1,635,775 msg/s |
| Dekaf | 2026-07-16T23:22:02.3638632+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1809.0 MB/s | 8/11 | 2,266,315 | 872.2s / 1,648,937 msg/s |
| Dekaf | 2026-07-16T23:52:31.4108541+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 582,794 msg/s |
| Dekaf | 2026-07-16T23:52:58.4231633+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1714.3 MB/s | 0/0 | 57,743 | 27.0s / 1,603,080 msg/s |
| Dekaf | 2026-07-16T23:53:25.4355102+00:00 | 1 | 14.0 MiB / 12.1 MiB | 1714.3 MB/s | 1/0 | 120,075 | 54.0s / 1,599,126 msg/s |
| Dekaf | 2026-07-16T23:53:52.4421705+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1718.3 MB/s | 1/0 | 183,310 | 81.0s / 1,608,103 msg/s |
| Dekaf | 2026-07-16T23:54:20.4522947+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1721.6 MB/s | 2/0 | 255,992 | 109.0s / 1,566,368 msg/s |
| Dekaf | 2026-07-16T23:54:47.4584657+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1725.1 MB/s | 2/1 | 324,626 | 136.0s / 1,624,040 msg/s |
| Dekaf | 2026-07-16T23:55:14.4675085+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1725.1 MB/s | 2/1 | 394,633 | 163.0s / 1,592,888 msg/s |
| Dekaf | 2026-07-16T23:55:42.4697115+00:00 | 1 | 13.0 MiB / 11.9 MiB | 1733.6 MB/s | 3/1 | 463,463 | 191.0s / 1,603,812 msg/s |
| Dekaf | 2026-07-16T23:56:09.4827471+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1743.3 MB/s | 3/1 | 523,238 | 218.1s / 1,533,875 msg/s |
| Dekaf | 2026-07-16T23:56:36.4935002+00:00 | 1 | 14.0 MiB / 11.6 MiB | 1743.3 MB/s | 4/1 | 578,866 | 245.1s / 1,576,044 msg/s |
| Dekaf | 2026-07-16T23:57:03.5026344+00:00 | 1 | 15.0 MiB / 14.2 MiB | 1743.3 MB/s | 5/1 | 634,322 | 272.1s / 1,577,857 msg/s |
| Dekaf | 2026-07-16T23:57:31.508666+00:00 | 1 | 15.0 MiB / 14.9 MiB | 1743.3 MB/s | 5/1 | 687,217 | 300.1s / 1,592,274 msg/s |
| Dekaf | 2026-07-16T23:57:58.5221119+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1747.1 MB/s | 6/1 | 745,262 | 327.1s / 1,598,857 msg/s |
| Dekaf | 2026-07-16T23:58:25.5333046+00:00 | 1 | 16.0 MiB / 15.5 MiB | 1765.5 MB/s | 6/1 | 806,324 | 354.1s / 1,608,976 msg/s |
| Dekaf | 2026-07-16T23:58:52.5429526+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1765.5 MB/s | 6/2 | 861,734 | 381.1s / 1,538,605 msg/s |
| Dekaf | 2026-07-16T23:59:20.5529552+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1765.5 MB/s | 7/2 | 914,729 | 409.1s / 1,610,888 msg/s |
| Dekaf | 2026-07-16T23:59:47.559957+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1765.5 MB/s | 7/2 | 968,132 | 436.1s / 1,575,844 msg/s |
| Dekaf | 2026-07-17T00:00:14.5656803+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1765.5 MB/s | 8/2 | 1,025,748 | 463.1s / 1,519,191 msg/s |
| Dekaf | 2026-07-17T00:00:41.5716572+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1765.5 MB/s | 8/2 | 1,081,749 | 490.1s / 1,595,934 msg/s |
| Dekaf | 2026-07-17T00:01:09.5842108+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1765.5 MB/s | 8/3 | 1,140,369 | 518.1s / 1,550,321 msg/s |
| Dekaf | 2026-07-17T00:01:36.5986239+00:00 | 1 | 13.0 MiB / 12.2 MiB | 1765.5 MB/s | 9/3 | 1,196,604 | 545.2s / 1,540,522 msg/s |
| Dekaf | 2026-07-17T00:02:03.6117587+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1765.5 MB/s | 9/3 | 1,249,772 | 572.2s / 1,508,510 msg/s |
| Dekaf | 2026-07-17T00:02:30.6210784+00:00 | 1 | 14.0 MiB / 13.9 MiB | 1765.5 MB/s | 10/3 | 1,299,681 | 599.2s / 1,498,829 msg/s |
| Dekaf | 2026-07-17T00:02:58.6331235+00:00 | 1 | 15.0 MiB / 14.6 MiB | 1765.5 MB/s | 10/3 | 1,361,139 | 627.2s / 1,564,532 msg/s |
| Dekaf | 2026-07-17T00:03:25.6441188+00:00 | 1 | 14.0 MiB / 13.0 MiB | 1765.5 MB/s | 10/4 | 1,421,485 | 654.2s / 1,594,097 msg/s |
| Dekaf | 2026-07-17T00:03:52.6583396+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1765.5 MB/s | 11/4 | 1,489,698 | 681.2s / 1,565,013 msg/s |
| Dekaf | 2026-07-17T00:04:19.6657421+00:00 | 1 | 10.0 MiB / 8.4 MiB | 1765.5 MB/s | 11/4 | 1,555,220 | 708.2s / 1,555,781 msg/s |
| Dekaf | 2026-07-17T00:04:47.6751308+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1765.5 MB/s | 11/5 | 1,627,725 | 736.2s / 1,588,126 msg/s |
| Dekaf | 2026-07-17T00:05:14.6862369+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1765.5 MB/s | 11/5 | 1,689,571 | 763.2s / 1,598,936 msg/s |
| Dekaf | 2026-07-17T00:05:41.6947317+00:00 | 1 | 13.0 MiB / 12.8 MiB | 1765.5 MB/s | 12/5 | 1,748,364 | 790.2s / 1,505,894 msg/s |
| Dekaf | 2026-07-17T00:06:09.6997925+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1765.5 MB/s | 13/5 | 1,799,089 | 818.2s / 1,534,391 msg/s |
| Dekaf | 2026-07-17T00:06:36.7076942+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1765.5 MB/s | 13/5 | 1,845,548 | 845.2s / 1,546,847 msg/s |
| Dekaf | 2026-07-17T00:07:03.7171673+00:00 | 1 | 15.0 MiB / 14.4 MiB | 1765.5 MB/s | 14/5 | 1,895,494 | 872.3s / 1,542,537 msg/s |
| Dekaf | 2026-07-17T00:07:30.7268627+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1765.5 MB/s | 14/5 | 1,943,937 | 899.3s / 1,546,103 msg/s |
| Dekaf (3conn) | 2026-07-17T00:07:59.2870572+00:00 | 1 | 48.0 MiB / 2.2 MiB | 1891.1 MB/s | 0/0 | 103 | 27.0s / 1,470,298 msg/s |
| Dekaf (3conn) | 2026-07-17T00:08:26.2995396+00:00 | 1 | 48.0 MiB / 5.1 MiB | 1891.1 MB/s | 0/1 | 144 | 54.0s / 1,525,387 msg/s |
| Dekaf (3conn) | 2026-07-17T00:08:53.3113894+00:00 | 1 | 48.0 MiB / 18.7 MiB | 2010.1 MB/s | 0/1 | 291 | 81.1s / 1,709,547 msg/s |
| Dekaf (3conn) | 2026-07-17T00:09:20.3240339+00:00 | 1 | 48.0 MiB / 7.4 MiB | 2010.1 MB/s | 0/2 | 458 | 108.1s / 1,709,562 msg/s |
| Dekaf (3conn) | 2026-07-17T00:09:48.3385369+00:00 | 1 | 42.0 MiB / 1.9 MiB | 2010.1 MB/s | 1/2 | 665 | 136.1s / 1,577,364 msg/s |
| Dekaf (3conn) | 2026-07-17T00:10:15.3580914+00:00 | 1 | 42.0 MiB / 7.9 MiB | 2010.1 MB/s | 1/2 | 843 | 163.1s / 1,796,108 msg/s |
| Dekaf (3conn) | 2026-07-17T00:10:42.3689694+00:00 | 1 | 36.0 MiB / 2.0 MiB | 2010.1 MB/s | 2/2 | 1,134 | 190.1s / 1,598,344 msg/s |
| Dekaf (3conn) | 2026-07-17T00:11:09.3745063+00:00 | 1 | 36.0 MiB / 2.4 MiB | 2010.1 MB/s | 2/2 | 1,418 | 217.1s / 1,446,001 msg/s |
| Dekaf (3conn) | 2026-07-17T00:11:37.3885007+00:00 | 1 | 30.0 MiB / 3.9 MiB | 2010.1 MB/s | 3/2 | 1,828 | 245.1s / 1,710,616 msg/s |
| Dekaf (3conn) | 2026-07-17T00:12:04.3907632+00:00 | 1 | 30.0 MiB / 1.5 MiB | 2010.1 MB/s | 3/3 | 2,287 | 272.1s / 1,711,118 msg/s |
| Dekaf (3conn) | 2026-07-17T00:12:31.4040685+00:00 | 1 | 30.0 MiB / 16.2 MiB | 2010.1 MB/s | 3/3 | 2,567 | 299.2s / 1,539,129 msg/s |
| Dekaf (3conn) | 2026-07-17T00:12:58.4166937+00:00 | 1 | 30.0 MiB / 5.2 MiB | 2010.1 MB/s | 3/4 | 2,873 | 326.2s / 1,364,078 msg/s |
| Dekaf (3conn) | 2026-07-17T00:13:26.4325971+00:00 | 1 | 30.0 MiB / 1.6 MiB | 2010.1 MB/s | 3/4 | 3,409 | 354.2s / 1,643,165 msg/s |
| Dekaf (3conn) | 2026-07-17T00:13:53.4473631+00:00 | 1 | 30.0 MiB / 8.2 MiB | 2010.1 MB/s | 3/5 | 3,836 | 381.2s / 1,623,277 msg/s |
| Dekaf (3conn) | 2026-07-17T00:14:20.4585251+00:00 | 1 | 33.0 MiB / 2.3 MiB | 2284.5 MB/s | 4/5 | 4,390 | 408.2s / 1,792,741 msg/s |
| Dekaf (3conn) | 2026-07-17T00:14:48.4755098+00:00 | 1 | 36.0 MiB / 6.8 MiB | 2284.5 MB/s | 4/5 | 5,121 | 436.2s / 2,278,104 msg/s |
| Dekaf (3conn) | 2026-07-17T00:15:15.494143+00:00 | 1 | 36.0 MiB / 8.1 MiB | 2711.7 MB/s | 5/5 | 6,562 | 463.2s / 2,315,316 msg/s |
| Dekaf (3conn) | 2026-07-17T00:15:42.5172798+00:00 | 1 | 36.0 MiB / 8.1 MiB | 2711.7 MB/s | 5/5 | 7,748 | 490.3s / 2,293,994 msg/s |
| Dekaf (3conn) | 2026-07-17T00:16:09.53102+00:00 | 1 | 39.0 MiB / 12.7 MiB | 2711.7 MB/s | 6/5 | 9,078 | 517.3s / 2,097,800 msg/s |
| Dekaf (3conn) | 2026-07-17T00:16:37.5363282+00:00 | 1 | 39.0 MiB / 5.2 MiB | 2711.7 MB/s | 6/6 | 10,395 | 545.3s / 2,130,949 msg/s |
| Dekaf (3conn) | 2026-07-17T00:17:04.5533616+00:00 | 1 | 33.0 MiB / 7.5 MiB | 2711.7 MB/s | 6/6 | 11,745 | 572.3s / 2,218,903 msg/s |
| Dekaf (3conn) | 2026-07-17T00:17:31.5605215+00:00 | 1 | 39.0 MiB / 10.0 MiB | 2711.7 MB/s | 6/7 | 13,230 | 599.3s / 2,228,913 msg/s |
| Dekaf (3conn) | 2026-07-17T00:17:58.580787+00:00 | 1 | 39.0 MiB / 4.0 MiB | 2741.4 MB/s | 6/7 | 14,666 | 626.3s / 2,210,643 msg/s |
| Dekaf (3conn) | 2026-07-17T00:18:26.5888175+00:00 | 1 | 42.0 MiB / 7.8 MiB | 2741.4 MB/s | 7/7 | 15,920 | 654.4s / 2,161,166 msg/s |
| Dekaf (3conn) | 2026-07-17T00:18:53.6075655+00:00 | 1 | 45.0 MiB / 10.8 MiB | 2741.4 MB/s | 8/7 | 17,236 | 681.4s / 2,316,466 msg/s |
| Dekaf (3conn) | 2026-07-17T00:19:20.6176258+00:00 | 1 | 48.0 MiB / 6.1 MiB | 2749.4 MB/s | 8/7 | 18,635 | 708.4s / 2,115,376 msg/s |
| Dekaf (3conn) | 2026-07-17T00:19:47.6241197+00:00 | 1 | 45.0 MiB / 19.0 MiB | 2749.4 MB/s | 8/8 | 19,892 | 735.4s / 2,096,695 msg/s |
| Dekaf (3conn) | 2026-07-17T00:20:15.6325051+00:00 | 1 | 39.0 MiB / 11.6 MiB | 2749.4 MB/s | 8/8 | 21,086 | 763.4s / 2,051,746 msg/s |
| Dekaf (3conn) | 2026-07-17T00:20:42.6455754+00:00 | 1 | 39.0 MiB / 2.5 MiB | 2749.4 MB/s | 9/8 | 22,412 | 790.4s / 2,174,040 msg/s |
| Dekaf (3conn) | 2026-07-17T00:21:09.6545161+00:00 | 1 | 33.0 MiB / 9.7 MiB | 2749.4 MB/s | 10/8 | 23,992 | 817.4s / 2,268,014 msg/s |
| Dekaf (3conn) | 2026-07-17T00:21:36.6695244+00:00 | 1 | 27.0 MiB / 6.8 MiB | 2749.4 MB/s | 10/8 | 25,770 | 844.4s / 2,063,035 msg/s |
| Dekaf (3conn) | 2026-07-17T00:22:04.68184+00:00 | 1 | 33.0 MiB / 8.2 MiB | 2749.4 MB/s | 10/9 | 27,323 | 872.5s / 2,189,016 msg/s |
| Dekaf (3conn) | 2026-07-17T00:22:31.6955858+00:00 | 1 | 36.0 MiB / 1.7 MiB | 2749.4 MB/s | 10/9 | 29,048 | 899.5s / 1,918,613 msg/s |
*2,600 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget, Idempotent)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-16T23:08:00.1891731+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-16T23:08:15.2020816+00:00 | 1 | capacity | succeeded | 15,013ms | 14.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-16T23:08:45.2274093+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-16T23:09:30.2574733+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:09:45.2682843+00:00 | 1 | capacity | failed | 15,010ms | 12.0 MiB / 9.4 MiB |
| Dekaf | 2026-07-16T23:10:15.2899904+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.2 MiB |
| Dekaf | 2026-07-16T23:10:30.300526+00:00 | 1 | capacity | succeeded | 15,010ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-16T23:11:00.3227441+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-16T23:11:15.3297785+00:00 | 1 | capacity | failed | 15,007ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-16T23:12:00.3601478+00:00 | 1 | capacity | succeeded | 15,011ms | 11.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-16T23:12:30.4001326+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-16T23:12:45.411461+00:00 | 1 | capacity | failed | 15,011ms | 11.0 MiB / 8.5 MiB |
| Dekaf | 2026-07-16T23:13:15.4308595+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-16T23:13:30.4440651+00:00 | 1 | capacity | failed | 15,013ms | 11.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:14:00.4657953+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.2 MiB |
| Dekaf | 2026-07-16T23:14:45.5001968+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-16T23:15:00.5117939+00:00 | 1 | capacity | succeeded | 15,011ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:15:30.5331139+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 12.0 MiB |
| Dekaf | 2026-07-16T23:15:45.5440433+00:00 | 1 | capacity | succeeded | 15,011ms | 13.0 MiB / 12.3 MiB |
| Dekaf | 2026-07-16T23:16:15.5620235+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-16T23:16:30.5735895+00:00 | 1 | capacity | failed | 15,011ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-16T23:17:15.6107388+00:00 | 1 | capacity | succeeded | 15,014ms | 11.0 MiB / 10.0 MiB |
| Dekaf | 2026-07-16T23:17:45.6351953+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-16T23:18:00.6469428+00:00 | 1 | capacity | failed | 15,011ms | 11.0 MiB / 6.8 MiB |
| Dekaf | 2026-07-16T23:18:30.6691739+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-16T23:18:45.6788507+00:00 | 1 | capacity | failed | 15,009ms | 11.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:19:15.6950653+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.6 MiB |
| Dekaf | 2026-07-16T23:20:00.7250285+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 11.0 MiB |
| Dekaf | 2026-07-16T23:20:15.7366567+00:00 | 1 | capacity | failed | 15,012ms | 11.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:20:45.7568772+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-16T23:21:00.7676745+00:00 | 1 | capacity | failed | 15,010ms | 11.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-16T23:21:30.7898243+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-16T23:21:45.8005448+00:00 | 1 | capacity | succeeded | 15,010ms | 12.0 MiB / 10.7 MiB |
| Dekaf | 2026-07-16T23:53:01.4986473+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.6 MiB |
| Dekaf | 2026-07-16T23:53:16.5095762+00:00 | 1 | capacity | succeeded | 15,011ms | 14.0 MiB / 13.4 MiB |
| Dekaf | 2026-07-16T23:53:46.5296284+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-16T23:54:01.5385233+00:00 | 1 | capacity | succeeded | 15,008ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:54:31.5604326+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:55:16.58951+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-16T23:55:31.6015882+00:00 | 1 | capacity | succeeded | 15,012ms | 13.0 MiB / 12.0 MiB |
| Dekaf | 2026-07-16T23:56:01.6278016+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 11.4 MiB |
| Dekaf | 2026-07-16T23:56:16.6397119+00:00 | 1 | capacity | succeeded | 15,011ms | 14.0 MiB / 12.3 MiB |
| Dekaf | 2026-07-16T23:56:46.6679985+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 12.9 MiB |
| Dekaf | 2026-07-16T23:57:01.6793243+00:00 | 1 | capacity | succeeded | 15,011ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-16T23:57:46.7094107+00:00 | 1 | capacity | succeeded | 15,010ms | 16.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-16T23:58:16.7342093+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 15.2 MiB |
| Dekaf | 2026-07-16T23:58:31.7462505+00:00 | 1 | capacity | failed | 15,012ms | 16.0 MiB / 17.1 MiB |
| Dekaf | 2026-07-16T23:59:01.7673153+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-16T23:59:16.776975+00:00 | 1 | capacity | succeeded | 15,009ms | 14.0 MiB / 12.9 MiB |
| Dekaf | 2026-07-16T23:59:46.8024363+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.7 MiB |
| Dekaf | 2026-07-17T00:00:31.8288008+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T00:00:46.8389818+00:00 | 1 | capacity | failed | 15,010ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-17T00:01:16.8607785+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.9 MiB |
| Dekaf | 2026-07-17T00:01:31.8942617+00:00 | 1 | capacity | succeeded | 15,033ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T00:02:01.9205063+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.0 MiB |
| Dekaf | 2026-07-17T00:02:16.9338104+00:00 | 1 | capacity | succeeded | 15,013ms | 14.0 MiB / 12.3 MiB |
| Dekaf | 2026-07-17T00:03:01.9648972+00:00 | 1 | capacity | failed | 15,009ms | 14.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-17T00:03:31.9930104+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T00:03:47.0014168+00:00 | 1 | capacity | succeeded | 15,008ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T00:04:17.025432+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.6 MiB |
| Dekaf | 2026-07-17T00:04:32.0370491+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-17T00:05:02.0587408+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T00:05:47.0898968+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T00:06:02.1017815+00:00 | 1 | capacity | succeeded | 15,011ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T00:06:32.1201517+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T00:06:47.1289983+00:00 | 1 | capacity | succeeded | 15,008ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-17T00:07:17.1505346+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 14.0 MiB |
| Dekaf (3conn) | 2026-07-17T00:08:17.4506959+00:00 | 1 | capacity | failed | 15,028ms | 48.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-07-17T00:08:47.5145492+00:00 | 1 | capacity | started | 0ms | 54.0 MiB / 11.0 MiB |
| Dekaf (3conn) | 2026-07-17T00:09:02.5385206+00:00 | 1 | capacity | failed | 15,023ms | 48.0 MiB / 4.7 MiB |
| Dekaf (3conn) | 2026-07-17T00:09:32.59336+00:00 | 1 | capacity | started | 0ms | 42.0 MiB / 7.3 MiB |
| Dekaf (3conn) | 2026-07-17T00:09:47.620623+00:00 | 1 | capacity | succeeded | 15,027ms | 42.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-17T00:10:17.6709358+00:00 | 1 | capacity | started | 0ms | 36.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-07-17T00:11:02.7444129+00:00 | 1 | capacity | started | 0ms | 30.0 MiB / 1.8 MiB |
| Dekaf (3conn) | 2026-07-17T00:11:17.7657155+00:00 | 1 | capacity | succeeded | 15,021ms | 30.0 MiB / 4.9 MiB |
| Dekaf (3conn) | 2026-07-17T00:11:47.8097826+00:00 | 1 | capacity | started | 0ms | 24.0 MiB / 5.6 MiB |
| Dekaf (3conn) | 2026-07-17T00:12:02.8300121+00:00 | 1 | capacity | failed | 15,020ms | 30.0 MiB / 6.2 MiB |
| Dekaf (3conn) | 2026-07-17T00:12:32.8910405+00:00 | 1 | capacity | started | 0ms | 33.0 MiB / 29.0 MiB |
| Dekaf (3conn) | 2026-07-17T00:12:47.921537+00:00 | 1 | capacity | failed | 15,028ms | 30.0 MiB / 6.3 MiB |
| Dekaf (3conn) | 2026-07-17T00:13:32.9946686+00:00 | 1 | capacity | failed | 15,027ms | 30.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-07-17T00:14:03.0384044+00:00 | 1 | capacity | started | 0ms | 33.0 MiB / 10.2 MiB |
| Dekaf (3conn) | 2026-07-17T00:14:18.0558787+00:00 | 1 | capacity | succeeded | 15,017ms | 33.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-17T00:14:48.0938914+00:00 | 1 | capacity | started | 0ms | 36.0 MiB / 8.2 MiB |
| Dekaf (3conn) | 2026-07-17T00:15:03.1140722+00:00 | 1 | capacity | succeeded | 15,020ms | 36.0 MiB / 5.5 MiB |
| Dekaf (3conn) | 2026-07-17T00:15:33.1658087+00:00 | 1 | capacity | started | 0ms | 39.0 MiB / 10.5 MiB |
| Dekaf (3conn) | 2026-07-17T00:16:18.2353007+00:00 | 1 | capacity | started | 0ms | 42.0 MiB / 3.5 MiB |
| Dekaf (3conn) | 2026-07-17T00:16:33.2538026+00:00 | 1 | capacity | failed | 15,018ms | 39.0 MiB / 39.4 MiB |
| Dekaf (3conn) | 2026-07-17T00:17:03.306692+00:00 | 1 | capacity | started | 0ms | 33.0 MiB / 11.2 MiB |
| Dekaf (3conn) | 2026-07-17T00:17:18.3231285+00:00 | 1 | capacity | failed | 15,016ms | 39.0 MiB / 8.0 MiB |
| Dekaf (3conn) | 2026-07-17T00:17:48.3586442+00:00 | 1 | capacity | started | 0ms | 42.0 MiB / 12.7 MiB |
| Dekaf (3conn) | 2026-07-17T00:18:03.3872358+00:00 | 1 | capacity | succeeded | 15,028ms | 42.0 MiB / 9.6 MiB |
| Dekaf (3conn) | 2026-07-17T00:18:48.4604676+00:00 | 1 | capacity | succeeded | 15,019ms | 45.0 MiB / 5.2 MiB |
| Dekaf (3conn) | 2026-07-17T00:19:18.5073797+00:00 | 1 | capacity | started | 0ms | 48.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-07-17T00:19:33.5266536+00:00 | 1 | capacity | failed | 15,019ms | 45.0 MiB / 0.6 MiB |
| Dekaf (3conn) | 2026-07-17T00:20:03.5779073+00:00 | 1 | capacity | started | 0ms | 39.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-17T00:20:18.5979604+00:00 | 1 | capacity | succeeded | 15,020ms | 39.0 MiB / 2.0 MiB |
| Dekaf (3conn) | 2026-07-17T00:20:48.6409698+00:00 | 1 | capacity | started | 0ms | 33.0 MiB / 7.7 MiB |
| Dekaf (3conn) | 2026-07-17T00:21:33.7263815+00:00 | 1 | capacity | started | 0ms | 27.0 MiB / 6.6 MiB |
| Dekaf (3conn) | 2026-07-17T00:21:48.7604156+00:00 | 1 | capacity | failed | 15,034ms | 33.0 MiB / 5.9 MiB |
| Dekaf (3conn) | 2026-07-17T00:22:18.8036819+00:00 | 1 | capacity | started | 0ms | 36.0 MiB / 8.5 MiB |
*17 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget, Idempotent)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 2,468 |
| Dekaf | 1 | 0.002–0.004ms | 2,884 |
| Dekaf | 1 | 0.004–0.008ms | 10,557 |
| Dekaf | 1 | 0.008–0.016ms | 53,198 |
| Dekaf | 1 | 0.016–0.032ms | 67,115 |
| Dekaf | 1 | 0.032–0.064ms | 61,410 |
| Dekaf | 1 | 0.064–0.128ms | 120,916 |
| Dekaf | 1 | 0.128–0.256ms | 282,039 |
| Dekaf | 1 | 0.256–0.512ms | 280,049 |
| Dekaf | 1 | 0.512–1.024ms | 44,342 |
| Dekaf | 1 | 1.024–2.048ms | 5,331 |
| Dekaf | 1 | 2.048–4.096ms | 3,529 |
| Dekaf | 1 | 4.096–8.192ms | 720 |
| Dekaf | 1 | 8.192–16.384ms | 39 |
| Dekaf | 1 | 0.001–0.002ms | 2,195 |
| Dekaf | 1 | 0.002–0.004ms | 2,660 |
| Dekaf | 1 | 0.004–0.008ms | 11,394 |
| Dekaf | 1 | 0.008–0.016ms | 45,217 |
| Dekaf | 1 | 0.016–0.032ms | 56,978 |
| Dekaf | 1 | 0.032–0.064ms | 63,850 |
| Dekaf | 1 | 0.064–0.128ms | 127,428 |
| Dekaf | 1 | 0.128–0.256ms | 349,859 |
| Dekaf | 1 | 0.256–0.512ms | 369,170 |
| Dekaf | 1 | 0.512–1.024ms | 51,221 |
| Dekaf | 1 | 1.024–2.048ms | 5,415 |
| Dekaf | 1 | 2.048–4.096ms | 4,084 |
| Dekaf | 1 | 4.096–8.192ms | 771 |
| Dekaf | 1 | 8.192–16.384ms | 36 |
| Dekaf | 1 | 32.768–65.536ms | 2 |
| Dekaf (3conn) | 1 | 0.001–0.002ms | 27 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 17 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 48 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 234 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 546 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 755 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 1,019 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 1,699 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 2,185 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 1,832 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 1,136 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 661 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 217 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 18 |

## Delivery Latency Outliers - Producer (Fire-and-Forget, Idempotent)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 171,058,000 | 2026-07-16T23:24:24.8373347+00:00 | 105.1ms | GC pause | - | - | 115.0s / 1,407,712 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 171,057,000 | 2026-07-16T23:24:24.8377803+00:00 | 103.1ms | GC pause | - | - | 115.0s / 1,407,712 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 171,061,000 | 2026-07-16T23:24:24.8408633+00:00 | 101.8ms | GC pause | - | - | 115.0s / 1,407,712 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 591,747,000 | 2026-07-16T23:29:18.2307835+00:00 | 101.2ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,937,000 | 2026-07-16T23:29:18.3783648+00:00 | 104.9ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,938,000 | 2026-07-16T23:29:18.3788953+00:00 | 104.4ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,941,000 | 2026-07-16T23:29:18.3802658+00:00 | 106.0ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,947,000 | 2026-07-16T23:29:18.3832442+00:00 | 107.9ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,948,000 | 2026-07-16T23:29:18.3837417+00:00 | 107.5ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,951,000 | 2026-07-16T23:29:18.3855501+00:00 | 106.9ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,957,000 | 2026-07-16T23:29:18.3885226+00:00 | 113.2ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,958,000 | 2026-07-16T23:29:18.3891408+00:00 | 112.6ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,961,000 | 2026-07-16T23:29:18.3913244+00:00 | 111.4ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,967,000 | 2026-07-16T23:29:18.3946012+00:00 | 111.4ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,968,000 | 2026-07-16T23:29:18.3949918+00:00 | 111.0ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,971,000 | 2026-07-16T23:29:18.3969497+00:00 | 115.7ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,977,000 | 2026-07-16T23:29:18.4002712+00:00 | 114.4ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,978,000 | 2026-07-16T23:29:18.401009+00:00 | 113.8ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,981,000 | 2026-07-16T23:29:18.4025414+00:00 | 112.3ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,987,000 | 2026-07-16T23:29:18.4058581+00:00 | 119.4ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,988,000 | 2026-07-16T23:29:18.4065053+00:00 | 118.8ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,991,000 | 2026-07-16T23:29:18.4084474+00:00 | 116.9ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,997,000 | 2026-07-16T23:29:18.4116086+00:00 | 114.7ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 591,998,000 | 2026-07-16T23:29:18.4120328+00:00 | 114.3ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,001,000 | 2026-07-16T23:29:18.4148363+00:00 | 121.1ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,007,000 | 2026-07-16T23:29:18.4189415+00:00 | 118.0ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,008,000 | 2026-07-16T23:29:18.4194465+00:00 | 117.5ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,011,000 | 2026-07-16T23:29:18.4215846+00:00 | 124.7ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,017,000 | 2026-07-16T23:29:18.4249389+00:00 | 122.7ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,018,000 | 2026-07-16T23:29:18.4254214+00:00 | 122.3ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,021,000 | 2026-07-16T23:29:18.4272524+00:00 | 120.6ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,027,000 | 2026-07-16T23:29:18.4303746+00:00 | 118.6ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,028,000 | 2026-07-16T23:29:18.4310286+00:00 | 122.0ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,031,000 | 2026-07-16T23:29:18.4328423+00:00 | 120.3ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,037,000 | 2026-07-16T23:29:18.4358968+00:00 | 119.1ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,038,000 | 2026-07-16T23:29:18.4365008+00:00 | 118.6ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,041,000 | 2026-07-16T23:29:18.4382506+00:00 | 120.1ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,047,000 | 2026-07-16T23:29:18.4412329+00:00 | 121.4ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,048,000 | 2026-07-16T23:29:18.4421676+00:00 | 120.5ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,051,000 | 2026-07-16T23:29:18.4436015+00:00 | 122.8ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,057,000 | 2026-07-16T23:29:18.4538003+00:00 | 116.0ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,058,000 | 2026-07-16T23:29:18.4543136+00:00 | 115.5ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,061,000 | 2026-07-16T23:29:18.4559413+00:00 | 113.9ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,067,000 | 2026-07-16T23:29:18.4590441+00:00 | 114.0ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,068,000 | 2026-07-16T23:29:18.459643+00:00 | 117.3ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,071,000 | 2026-07-16T23:29:18.461086+00:00 | 115.9ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,077,000 | 2026-07-16T23:29:18.4642211+00:00 | 116.4ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,078,000 | 2026-07-16T23:29:18.4647176+00:00 | 116.0ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,081,000 | 2026-07-16T23:29:18.4662249+00:00 | 118.2ms | GC pause | - | - | 408.3s / 1,375,050 msg/s | Gen2 +0 / pause +69.4ms |
| Confluent | 592,087,000 | 2026-07-16T23:29:18.4695169+00:00 | 118.1ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,088,000 | 2026-07-16T23:29:18.4700077+00:00 | 117.6ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,091,000 | 2026-07-16T23:29:18.4727634+00:00 | 120.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,097,000 | 2026-07-16T23:29:18.4787748+00:00 | 116.6ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,098,000 | 2026-07-16T23:29:18.4797706+00:00 | 115.6ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,101,000 | 2026-07-16T23:29:18.4829991+00:00 | 112.5ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,107,000 | 2026-07-16T23:29:18.4892513+00:00 | 109.9ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,108,000 | 2026-07-16T23:29:18.4899518+00:00 | 113.7ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,111,000 | 2026-07-16T23:29:18.4916344+00:00 | 112.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,117,000 | 2026-07-16T23:29:18.4954159+00:00 | 114.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,118,000 | 2026-07-16T23:29:18.4959189+00:00 | 113.7ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,121,000 | 2026-07-16T23:29:18.4978756+00:00 | 113.9ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,127,000 | 2026-07-16T23:29:18.5045886+00:00 | 113.4ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,128,000 | 2026-07-16T23:29:18.5050907+00:00 | 112.9ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,131,000 | 2026-07-16T23:29:18.5077125+00:00 | 113.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,137,000 | 2026-07-16T23:29:18.5141274+00:00 | 110.5ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,138,000 | 2026-07-16T23:29:18.5146176+00:00 | 110.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,141,000 | 2026-07-16T23:29:18.516602+00:00 | 108.1ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,147,000 | 2026-07-16T23:29:18.5196202+00:00 | 109.4ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,148,000 | 2026-07-16T23:29:18.5200766+00:00 | 108.9ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,151,000 | 2026-07-16T23:29:18.5216325+00:00 | 118.3ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,157,000 | 2026-07-16T23:29:18.5281212+00:00 | 112.9ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,158,000 | 2026-07-16T23:29:18.5286053+00:00 | 112.5ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,161,000 | 2026-07-16T23:29:18.5309378+00:00 | 111.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,167,000 | 2026-07-16T23:29:18.5388339+00:00 | 105.8ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,168,000 | 2026-07-16T23:29:18.5394363+00:00 | 105.3ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,171,000 | 2026-07-16T23:29:18.540833+00:00 | 103.9ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,177,000 | 2026-07-16T23:29:18.5439044+00:00 | 108.1ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,178,000 | 2026-07-16T23:29:18.544326+00:00 | 107.7ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,181,000 | 2026-07-16T23:29:18.5458177+00:00 | 106.3ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,187,000 | 2026-07-16T23:29:18.5524677+00:00 | 103.7ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,188,000 | 2026-07-16T23:29:18.5541861+00:00 | 102.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,191,000 | 2026-07-16T23:29:18.5561307+00:00 | 104.8ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,197,000 | 2026-07-16T23:29:18.5605952+00:00 | 109.8ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,198,000 | 2026-07-16T23:29:18.561092+00:00 | 109.3ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,201,000 | 2026-07-16T23:29:18.5636922+00:00 | 107.9ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,207,000 | 2026-07-16T23:29:18.5693025+00:00 | 106.7ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,208,000 | 2026-07-16T23:29:18.5702313+00:00 | 105.8ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,211,000 | 2026-07-16T23:29:18.5721494+00:00 | 104.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,217,000 | 2026-07-16T23:29:18.5767849+00:00 | 100.4ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,218,000 | 2026-07-16T23:29:18.5774252+00:00 | 103.3ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,221,000 | 2026-07-16T23:29:18.5798774+00:00 | 101.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,227,000 | 2026-07-16T23:29:18.5831559+00:00 | 101.4ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +157.9ms |
| Confluent | 592,231,000 | 2026-07-16T23:29:18.587381+00:00 | 102.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,237,000 | 2026-07-16T23:29:18.5913397+00:00 | 101.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,238,000 | 2026-07-16T23:29:18.5918359+00:00 | 100.5ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,241,000 | 2026-07-16T23:29:18.5947564+00:00 | 101.7ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,247,000 | 2026-07-16T23:29:18.5988155+00:00 | 101.8ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,248,000 | 2026-07-16T23:29:18.5996622+00:00 | 100.9ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,258,000 | 2026-07-16T23:29:18.606815+00:00 | 103.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,261,000 | 2026-07-16T23:29:18.6083874+00:00 | 101.7ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,267,000 | 2026-07-16T23:29:18.6129794+00:00 | 111.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,268,000 | 2026-07-16T23:29:18.6138177+00:00 | 110.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,271,000 | 2026-07-16T23:29:18.6153684+00:00 | 109.7ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,277,000 | 2026-07-16T23:29:18.6197672+00:00 | 109.6ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,278,000 | 2026-07-16T23:29:18.6202939+00:00 | 109.1ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,281,000 | 2026-07-16T23:29:18.6223897+00:00 | 108.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,287,000 | 2026-07-16T23:29:18.626841+00:00 | 106.6ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,288,000 | 2026-07-16T23:29:18.6273152+00:00 | 106.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,291,000 | 2026-07-16T23:29:18.6297895+00:00 | 103.8ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,297,000 | 2026-07-16T23:29:18.6333634+00:00 | 101.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,298,000 | 2026-07-16T23:29:18.6338785+00:00 | 106.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,301,000 | 2026-07-16T23:29:18.6358291+00:00 | 104.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,307,000 | 2026-07-16T23:29:18.6432379+00:00 | 108.5ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,308,000 | 2026-07-16T23:29:18.6437776+00:00 | 108.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,311,000 | 2026-07-16T23:29:18.6462478+00:00 | 106.6ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,317,000 | 2026-07-16T23:29:18.6502055+00:00 | 105.8ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,318,000 | 2026-07-16T23:29:18.6510029+00:00 | 105.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,321,000 | 2026-07-16T23:29:18.6528777+00:00 | 104.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,327,000 | 2026-07-16T23:29:18.6570553+00:00 | 106.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,328,000 | 2026-07-16T23:29:18.6577574+00:00 | 105.4ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,331,000 | 2026-07-16T23:29:18.6598831+00:00 | 103.3ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,337,000 | 2026-07-16T23:29:18.6634109+00:00 | 101.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,338,000 | 2026-07-16T23:29:18.6638908+00:00 | 100.5ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,341,000 | 2026-07-16T23:29:18.6654362+00:00 | 104.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,347,000 | 2026-07-16T23:29:18.6683265+00:00 | 102.7ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,348,000 | 2026-07-16T23:29:18.6688251+00:00 | 102.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,351,000 | 2026-07-16T23:29:18.673443+00:00 | 107.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,357,000 | 2026-07-16T23:29:18.6779934+00:00 | 103.8ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,358,000 | 2026-07-16T23:29:18.6786327+00:00 | 103.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,361,000 | 2026-07-16T23:29:18.6806705+00:00 | 101.3ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,367,000 | 2026-07-16T23:29:18.6848593+00:00 | 102.8ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,368,000 | 2026-07-16T23:29:18.6856414+00:00 | 102.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,371,000 | 2026-07-16T23:29:18.6876417+00:00 | 100.1ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,377,000 | 2026-07-16T23:29:18.6914637+00:00 | 100.8ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,378,000 | 2026-07-16T23:29:18.6920552+00:00 | 100.3ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,381,000 | 2026-07-16T23:29:18.6938724+00:00 | 102.5ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,387,000 | 2026-07-16T23:29:18.6977653+00:00 | 104.5ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,388,000 | 2026-07-16T23:29:18.6986402+00:00 | 103.6ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,391,000 | 2026-07-16T23:29:18.7003715+00:00 | 104.5ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,397,000 | 2026-07-16T23:29:18.7045427+00:00 | 108.9ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,398,000 | 2026-07-16T23:29:18.7050313+00:00 | 108.4ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,401,000 | 2026-07-16T23:29:18.7076539+00:00 | 105.9ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,407,000 | 2026-07-16T23:29:18.7118747+00:00 | 102.8ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,408,000 | 2026-07-16T23:29:18.7124095+00:00 | 105.7ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,411,000 | 2026-07-16T23:29:18.7141847+00:00 | 104.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,417,000 | 2026-07-16T23:29:18.7174128+00:00 | 105.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,418,000 | 2026-07-16T23:29:18.7221046+00:00 | 100.6ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,421,000 | 2026-07-16T23:29:18.7236889+00:00 | 101.7ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,487,000 | 2026-07-16T23:29:18.7886904+00:00 | 108.3ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,488,000 | 2026-07-16T23:29:18.7892212+00:00 | 108.4ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,491,000 | 2026-07-16T23:29:18.7911827+00:00 | 106.5ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,497,000 | 2026-07-16T23:29:18.795738+00:00 | 102.8ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,498,000 | 2026-07-16T23:29:18.7966892+00:00 | 101.9ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,501,000 | 2026-07-16T23:29:18.7987333+00:00 | 104.7ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,507,000 | 2026-07-16T23:29:18.8034044+00:00 | 103.3ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,508,000 | 2026-07-16T23:29:18.8038981+00:00 | 102.8ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,511,000 | 2026-07-16T23:29:18.805692+00:00 | 104.5ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,517,000 | 2026-07-16T23:29:18.808619+00:00 | 105.4ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,518,000 | 2026-07-16T23:29:18.8090638+00:00 | 105.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,521,000 | 2026-07-16T23:29:18.8105521+00:00 | 103.6ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,527,000 | 2026-07-16T23:29:18.8163948+00:00 | 101.5ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,528,000 | 2026-07-16T23:29:18.8168614+00:00 | 104.9ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,531,000 | 2026-07-16T23:29:18.8196998+00:00 | 102.1ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,537,000 | 2026-07-16T23:29:18.8234732+00:00 | 104.3ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,538,000 | 2026-07-16T23:29:18.8244056+00:00 | 103.4ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,541,000 | 2026-07-16T23:29:18.8260638+00:00 | 103.9ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,547,000 | 2026-07-16T23:29:18.830351+00:00 | 103.7ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,548,000 | 2026-07-16T23:29:18.8308022+00:00 | 103.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,551,000 | 2026-07-16T23:29:18.832825+00:00 | 101.3ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,557,000 | 2026-07-16T23:29:18.8366814+00:00 | 105.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,558,000 | 2026-07-16T23:29:18.8373185+00:00 | 104.3ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,561,000 | 2026-07-16T23:29:18.8392223+00:00 | 102.5ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,567,000 | 2026-07-16T23:29:18.8424349+00:00 | 103.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,568,000 | 2026-07-16T23:29:18.8429128+00:00 | 102.6ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,571,000 | 2026-07-16T23:29:18.8442592+00:00 | 105.9ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,577,000 | 2026-07-16T23:29:18.8471863+00:00 | 106.7ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,578,000 | 2026-07-16T23:29:18.850919+00:00 | 103.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,581,000 | 2026-07-16T23:29:18.8528817+00:00 | 105.5ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,587,000 | 2026-07-16T23:29:18.8582807+00:00 | 104.0ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,588,000 | 2026-07-16T23:29:18.8591566+00:00 | 103.1ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,591,000 | 2026-07-16T23:29:18.860856+00:00 | 101.5ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,597,000 | 2026-07-16T23:29:18.8654125+00:00 | 106.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,598,000 | 2026-07-16T23:29:18.8659665+00:00 | 106.6ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,601,000 | 2026-07-16T23:29:18.8682082+00:00 | 104.4ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,607,000 | 2026-07-16T23:29:18.8714948+00:00 | 103.7ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,608,000 | 2026-07-16T23:29:18.8719141+00:00 | 103.3ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,611,000 | 2026-07-16T23:29:18.8764723+00:00 | 101.1ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,617,000 | 2026-07-16T23:29:18.8800258+00:00 | 101.9ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,618,000 | 2026-07-16T23:29:18.8804442+00:00 | 101.5ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 592,621,000 | 2026-07-16T23:29:18.8818707+00:00 | 103.2ms | GC pause | - | - | 409.3s / 1,385,341 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 643,087,000 | 2026-07-16T23:29:54.9891631+00:00 | 100.7ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,088,000 | 2026-07-16T23:29:54.989798+00:00 | 100.0ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,091,000 | 2026-07-16T23:29:54.9913262+00:00 | 105.4ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,097,000 | 2026-07-16T23:29:54.9944451+00:00 | 105.1ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,098,000 | 2026-07-16T23:29:54.994853+00:00 | 104.7ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,101,000 | 2026-07-16T23:29:54.9967498+00:00 | 103.1ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,107,000 | 2026-07-16T23:29:55.0038329+00:00 | 102.4ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,108,000 | 2026-07-16T23:29:55.0044058+00:00 | 101.8ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,111,000 | 2026-07-16T23:29:55.007695+00:00 | 102.5ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,117,000 | 2026-07-16T23:29:55.0126925+00:00 | 103.3ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,118,000 | 2026-07-16T23:29:55.0132079+00:00 | 102.8ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,121,000 | 2026-07-16T23:29:55.0160171+00:00 | 105.0ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,127,000 | 2026-07-16T23:29:55.0206629+00:00 | 104.8ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,128,000 | 2026-07-16T23:29:55.0213098+00:00 | 104.2ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,131,000 | 2026-07-16T23:29:55.0233892+00:00 | 102.2ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,137,000 | 2026-07-16T23:29:55.0276743+00:00 | 116.4ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,138,000 | 2026-07-16T23:29:55.0282834+00:00 | 115.8ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,141,000 | 2026-07-16T23:29:55.0301572+00:00 | 114.0ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,147,000 | 2026-07-16T23:29:55.0340282+00:00 | 111.4ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,148,000 | 2026-07-16T23:29:55.0346727+00:00 | 110.8ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,151,000 | 2026-07-16T23:29:55.0366493+00:00 | 109.6ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,157,000 | 2026-07-16T23:29:55.0397259+00:00 | 110.2ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,158,000 | 2026-07-16T23:29:55.040207+00:00 | 109.8ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,161,000 | 2026-07-16T23:29:55.0417787+00:00 | 113.0ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,167,000 | 2026-07-16T23:29:55.0481302+00:00 | 110.4ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,168,000 | 2026-07-16T23:29:55.048623+00:00 | 109.9ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,171,000 | 2026-07-16T23:29:55.0505626+00:00 | 108.1ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,177,000 | 2026-07-16T23:29:55.0540753+00:00 | 108.2ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,178,000 | 2026-07-16T23:29:55.0545493+00:00 | 111.3ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,181,000 | 2026-07-16T23:29:55.0570956+00:00 | 108.8ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,187,000 | 2026-07-16T23:29:55.0610136+00:00 | 108.8ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,188,000 | 2026-07-16T23:29:55.0614835+00:00 | 108.4ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,191,000 | 2026-07-16T23:29:55.0630314+00:00 | 110.9ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,197,000 | 2026-07-16T23:29:55.065946+00:00 | 113.1ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,198,000 | 2026-07-16T23:29:55.0663628+00:00 | 112.7ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,201,000 | 2026-07-16T23:29:55.0682353+00:00 | 112.6ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,207,000 | 2026-07-16T23:29:55.076355+00:00 | 115.7ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,208,000 | 2026-07-16T23:29:55.0771339+00:00 | 115.0ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,211,000 | 2026-07-16T23:29:55.0789619+00:00 | 113.3ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,217,000 | 2026-07-16T23:29:55.0838995+00:00 | 109.9ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,218,000 | 2026-07-16T23:29:55.0846232+00:00 | 113.5ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,221,000 | 2026-07-16T23:29:55.0867118+00:00 | 111.5ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,227,000 | 2026-07-16T23:29:55.091688+00:00 | 111.3ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,228,000 | 2026-07-16T23:29:55.0924039+00:00 | 110.7ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,231,000 | 2026-07-16T23:29:55.0946915+00:00 | 109.4ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,237,000 | 2026-07-16T23:29:55.099798+00:00 | 110.0ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,238,000 | 2026-07-16T23:29:55.1003386+00:00 | 109.4ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,241,000 | 2026-07-16T23:29:55.1027212+00:00 | 110.5ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,247,000 | 2026-07-16T23:29:55.1080463+00:00 | 115.1ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,248,000 | 2026-07-16T23:29:55.1087879+00:00 | 114.4ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,251,000 | 2026-07-16T23:29:55.1107366+00:00 | 112.6ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,257,000 | 2026-07-16T23:29:55.1145405+00:00 | 110.3ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,258,000 | 2026-07-16T23:29:55.1157465+00:00 | 111.5ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,261,000 | 2026-07-16T23:29:55.1179108+00:00 | 109.4ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,267,000 | 2026-07-16T23:29:55.1232185+00:00 | 106.1ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,268,000 | 2026-07-16T23:29:55.1239286+00:00 | 105.4ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,271,000 | 2026-07-16T23:29:55.1268204+00:00 | 106.8ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,277,000 | 2026-07-16T23:29:55.1321713+00:00 | 112.0ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,278,000 | 2026-07-16T23:29:55.1388197+00:00 | 105.4ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,281,000 | 2026-07-16T23:29:55.1405999+00:00 | 104.8ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,287,000 | 2026-07-16T23:29:55.151457+00:00 | 102.9ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 643,288,000 | 2026-07-16T23:29:55.1520787+00:00 | 102.4ms | GC pause | - | - | 445.3s / 1,357,363 msg/s | Gen2 +0 / pause +59.2ms |
| Confluent | 644,168,000 | 2026-07-16T23:29:55.7938443+00:00 | 110.9ms | GC pause | - | - | 446.3s / 1,453,872 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 644,171,000 | 2026-07-16T23:29:55.7956921+00:00 | 109.2ms | GC pause | - | - | 446.3s / 1,453,872 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 644,177,000 | 2026-07-16T23:29:55.7986461+00:00 | 107.6ms | GC pause | - | - | 446.3s / 1,453,872 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 644,178,000 | 2026-07-16T23:29:55.7991237+00:00 | 107.2ms | GC pause | - | - | 446.3s / 1,453,872 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 1,275,881,000 | 2026-07-16T23:52:19.8042172+00:00 | 105.6ms | GC pause | - | - | 889.5s / 1,337,270 msg/s | Gen2 +0 / pause +117.4ms |
| Confluent | 1,275,878,000 | 2026-07-16T23:52:19.8045462+00:00 | 105.0ms | GC pause | - | - | 889.5s / 1,337,270 msg/s | Gen2 +0 / pause +117.4ms |
| Confluent | 1,275,887,000 | 2026-07-16T23:52:19.8106135+00:00 | 100.6ms | GC pause | - | - | 889.5s / 1,337,270 msg/s | Gen2 +0 / pause +117.4ms |
| Dekaf (3conn) | 910,397,000 | 2026-07-17T00:16:33.1721953+00:00 | 109.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 541.3s / 1,742,260 msg/s | Gen2 +0 / pause +1.5ms |
| Dekaf (3conn) | 910,407,000 | 2026-07-17T00:16:33.1774706+00:00 | 107.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 541.3s / 1,742,260 msg/s | Gen2 +0 / pause +1.5ms |
| Dekaf (3conn) | 910,417,000 | 2026-07-17T00:16:33.1840141+00:00 | 103.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 541.3s / 1,742,260 msg/s | Gen2 +0 / pause +1.5ms |
| Dekaf (3conn) | 1,280,187,000 | 2026-07-17T00:19:19.8716245+00:00 | 104.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 708.4s / 2,115,376 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,280,197,000 | 2026-07-17T00:19:19.8748997+00:00 | 102.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 708.4s / 2,115,376 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,280,207,000 | 2026-07-17T00:19:19.8782624+00:00 | 101.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 708.4s / 2,115,376 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,358,657,000 | 2026-07-17T00:19:56.8690194+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 745.4s / 2,170,003 msg/s | Gen2 +0 / pause +0.3ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*100 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.37x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent); comparison throughput is 1.11x.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.09 | 1109.77 | 1,187,732 | 1,194,593 | +2.3% | +0.23% | 1132.71 | 1,187,732 | 0 | 1.29 |
| Dekaf | 1.09 | 1104.44 | 1,088,648 | 1,099,387 | +2.5% | +0.26% | 1038.22 | 1,088,648 | 0 | 1.19 |
| Confluent | 1.75 | - | 878,563 | 878,366 | -0.7% | -0.08% | 837.86 | 878,563 | 0 | 1.54 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 320,558 | 356.17 | 1001.49 KB |
| Dekaf | 2 | 321,558 | 357.28 | 1000.19 KB |
| Dekaf | 3 | 326,029 | 362.24 | 1016.35 KB |
| Dekaf (3conn) | 1 | 341,704 | 379.66 | 1013.83 KB |
| Dekaf (3conn) | 2 | 347,314 | 385.89 | 1014.29 KB |
| Dekaf (3conn) | 3 | 357,606 | 397.33 | 1017.79 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-16T23:07:26.4903368+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 299,703 msg/s |
| Dekaf | 2026-07-16T23:07:44.5027231+00:00 | 3 | 16.0 MiB / 10.2 MiB | 364.3 MB/s | 0/0 | 8,442 | 18.0s / 999,290 msg/s |
| Dekaf | 2026-07-16T23:08:03.5142235+00:00 | 1 | 16.0 MiB / 11.0 MiB | 380.1 MB/s | 0/0 | 5,366 | 37.0s / 1,029,862 msg/s |
| Dekaf | 2026-07-16T23:08:21.518731+00:00 | 1 | 14.0 MiB / 12.9 MiB | 391.8 MB/s | 1/0 | 9,338 | 55.0s / 1,127,091 msg/s |
| Dekaf | 2026-07-16T23:08:39.5318178+00:00 | 2 | 12.0 MiB / 2.5 MiB | 404.2 MB/s | 2/0 | 11,599 | 73.0s / 1,106,481 msg/s |
| Dekaf | 2026-07-16T23:08:57.5378736+00:00 | 2 | 10.0 MiB / 6.8 MiB | 412.1 MB/s | 3/0 | 14,787 | 91.0s / 1,152,854 msg/s |
| Dekaf | 2026-07-16T23:09:15.5399138+00:00 | 3 | 10.0 MiB / 10.0 MiB | 422.9 MB/s | 3/1 | 51,200 | 109.0s / 1,092,937 msg/s |
| Dekaf | 2026-07-16T23:09:33.5500158+00:00 | 3 | 10.0 MiB / 6.2 MiB | 422.9 MB/s | 3/1 | 59,478 | 127.1s / 1,108,201 msg/s |
| Dekaf | 2026-07-16T23:09:52.5597262+00:00 | 1 | 10.0 MiB / 4.6 MiB | 413.1 MB/s | 3/2 | 25,202 | 146.1s / 1,044,000 msg/s |
| Dekaf | 2026-07-16T23:10:10.5652985+00:00 | 1 | 10.0 MiB / 5.4 MiB | 413.1 MB/s | 3/2 | 27,473 | 164.1s / 1,037,272 msg/s |
| Dekaf | 2026-07-16T23:10:28.5710456+00:00 | 2 | 10.0 MiB / 3.3 MiB | 412.1 MB/s | 3/2 | 28,218 | 182.1s / 1,084,682 msg/s |
| Dekaf | 2026-07-16T23:10:46.581647+00:00 | 2 | 8.0 MiB / 8.0 MiB | 412.1 MB/s | 4/2 | 32,513 | 200.1s / 1,083,359 msg/s |
| Dekaf | 2026-07-16T23:11:04.5917572+00:00 | 3 | 9.0 MiB / 5.1 MiB | 422.9 MB/s | 5/2 | 104,908 | 218.1s / 1,063,376 msg/s |
| Dekaf | 2026-07-16T23:11:22.5965772+00:00 | 3 | 9.0 MiB / 6.4 MiB | 422.9 MB/s | 5/2 | 111,242 | 236.1s / 1,126,515 msg/s |
| Dekaf | 2026-07-16T23:11:41.6053177+00:00 | 1 | 9.0 MiB / 8.9 MiB | 413.1 MB/s | 5/3 | 42,485 | 255.1s / 1,105,054 msg/s |
| Dekaf | 2026-07-16T23:11:59.6078563+00:00 | 1 | 9.0 MiB / 7.1 MiB | 413.1 MB/s | 5/3 | 45,423 | 273.1s / 1,122,184 msg/s |
| Dekaf | 2026-07-16T23:12:17.613179+00:00 | 2 | 8.0 MiB / 6.9 MiB | 414.1 MB/s | 6/3 | 53,667 | 291.1s / 1,077,630 msg/s |
| Dekaf | 2026-07-16T23:12:35.6196854+00:00 | 2 | 8.0 MiB / 6.0 MiB | 414.1 MB/s | 6/3 | 56,930 | 309.1s / 1,111,060 msg/s |
| Dekaf | 2026-07-16T23:12:53.6255846+00:00 | 3 | 7.0 MiB / 7.0 MiB | 422.9 MB/s | 6/4 | 155,179 | 327.1s / 1,110,786 msg/s |
| Dekaf | 2026-07-16T23:13:11.6307407+00:00 | 3 | 7.0 MiB / 4.6 MiB | 422.9 MB/s | 6/4 | 164,971 | 345.1s / 1,142,759 msg/s |
| Dekaf | 2026-07-16T23:13:30.6332318+00:00 | 1 | 8.0 MiB / 6.0 MiB | 413.1 MB/s | 7/4 | 61,681 | 364.1s / 1,161,614 msg/s |
| Dekaf | 2026-07-16T23:13:48.6365945+00:00 | 1 | 8.0 MiB / 2.9 MiB | 413.1 MB/s | 7/4 | 64,895 | 382.1s / 1,105,224 msg/s |
| Dekaf | 2026-07-16T23:14:06.6424678+00:00 | 2 | 9.0 MiB / 7.1 MiB | 414.1 MB/s | 7/4 | 67,933 | 400.1s / 1,146,224 msg/s |
| Dekaf | 2026-07-16T23:14:24.64507+00:00 | 2 | 9.0 MiB / 4.9 MiB | 414.1 MB/s | 7/5 | 69,073 | 418.2s / 1,121,257 msg/s |
| Dekaf | 2026-07-16T23:14:42.6521981+00:00 | 3 | 6.0 MiB / 5.2 MiB | 422.9 MB/s | 7/6 | 243,664 | 436.2s / 1,092,762 msg/s |
| Dekaf | 2026-07-16T23:15:00.6547161+00:00 | 3 | 6.0 MiB / 2.5 MiB | 422.9 MB/s | 7/6 | 258,559 | 454.2s / 1,066,145 msg/s |
| Dekaf | 2026-07-16T23:15:19.6619711+00:00 | 1 | 8.0 MiB / 7.4 MiB | 413.1 MB/s | 9/5 | 87,879 | 473.2s / 1,062,592 msg/s |
| Dekaf | 2026-07-16T23:15:37.6678315+00:00 | 1 | 8.0 MiB / 2.5 MiB | 413.1 MB/s | 9/5 | 93,244 | 491.2s / 1,159,339 msg/s |
| Dekaf | 2026-07-16T23:15:55.6723185+00:00 | 2 | 9.0 MiB / 5.2 MiB | 414.1 MB/s | 7/7 | 81,519 | 509.2s / 1,057,642 msg/s |
| Dekaf | 2026-07-16T23:16:13.6781158+00:00 | 2 | 10.0 MiB / 4.1 MiB | 414.1 MB/s | 7/7 | 83,276 | 527.2s / 1,130,477 msg/s |
| Dekaf | 2026-07-16T23:16:31.6806365+00:00 | 3 | 6.0 MiB / 4.7 MiB | 422.9 MB/s | 9/7 | 331,119 | 545.2s / 976,728 msg/s |
| Dekaf | 2026-07-16T23:16:49.68912+00:00 | 3 | 7.0 MiB / 2.7 MiB | 422.9 MB/s | 9/7 | 345,696 | 563.2s / 1,021,177 msg/s |
| Dekaf | 2026-07-16T23:17:08.6939098+00:00 | 1 | 7.0 MiB / 3.8 MiB | 413.1 MB/s | 11/6 | 113,736 | 582.2s / 1,156,660 msg/s |
| Dekaf | 2026-07-16T23:17:26.6983749+00:00 | 1 | 7.0 MiB / 2.6 MiB | 413.1 MB/s | 11/6 | 118,319 | 600.2s / 1,165,633 msg/s |
| Dekaf | 2026-07-16T23:17:44.7052177+00:00 | 2 | 10.0 MiB / 4.4 MiB | 414.1 MB/s | 7/9 | 96,294 | 618.2s / 1,096,105 msg/s |
| Dekaf | 2026-07-16T23:18:02.705578+00:00 | 2 | 9.0 MiB / 6.7 MiB | 414.1 MB/s | 7/10 | 97,769 | 636.2s / 1,164,249 msg/s |
| Dekaf | 2026-07-16T23:18:20.7086477+00:00 | 3 | 6.0 MiB / 5.1 MiB | 422.9 MB/s | 10/8 | 415,172 | 654.2s / 1,105,265 msg/s |
| Dekaf | 2026-07-16T23:18:38.7130252+00:00 | 3 | 7.0 MiB / 4.2 MiB | 427.6 MB/s | 11/8 | 426,149 | 672.2s / 1,111,552 msg/s |
| Dekaf | 2026-07-16T23:18:57.7189356+00:00 | 1 | 7.0 MiB / 5.1 MiB | 413.1 MB/s | 13/7 | 145,299 | 691.3s / 1,059,507 msg/s |
| Dekaf | 2026-07-16T23:19:15.7205955+00:00 | 1 | 7.0 MiB / 6.7 MiB | 413.1 MB/s | 13/7 | 147,826 | 709.3s / 1,105,869 msg/s |
| Dekaf | 2026-07-16T23:19:33.7324423+00:00 | 2 | 8.0 MiB / 1.5 MiB | 415.3 MB/s | 8/11 | 114,835 | 727.3s / 1,124,198 msg/s |
| Dekaf | 2026-07-16T23:19:51.7385172+00:00 | 2 | 8.0 MiB / 7.1 MiB | 415.3 MB/s | 9/11 | 119,589 | 745.3s / 1,150,445 msg/s |
| Dekaf | 2026-07-16T23:20:09.742984+00:00 | 3 | 5.0 MiB / 4.6 MiB | 427.6 MB/s | 11/10 | 499,119 | 763.3s / 1,114,673 msg/s |
| Dekaf | 2026-07-16T23:20:27.7478298+00:00 | 3 | 6.0 MiB / 6.0 MiB | 427.6 MB/s | 11/11 | 514,420 | 781.3s / 1,080,045 msg/s |
| Dekaf | 2026-07-16T23:20:46.7525428+00:00 | 1 | 7.0 MiB / 2.5 MiB | 413.1 MB/s | 13/9 | 168,663 | 800.3s / 1,097,959 msg/s |
| Dekaf | 2026-07-16T23:21:04.7557945+00:00 | 1 | 6.0 MiB / 2.2 MiB | 413.1 MB/s | 13/9 | 172,810 | 818.3s / 1,044,995 msg/s |
| Dekaf | 2026-07-16T23:21:22.7579155+00:00 | 2 | 10.0 MiB / 4.0 MiB | 415.3 MB/s | 11/11 | 133,344 | 836.3s / 1,177,441 msg/s |
| Dekaf | 2026-07-16T23:21:40.7607025+00:00 | 2 | 10.0 MiB / 4.1 MiB | 415.3 MB/s | 11/11 | 134,955 | 854.3s / 1,120,315 msg/s |
| Dekaf | 2026-07-16T23:21:58.7661956+00:00 | 3 | 6.0 MiB / 6.0 MiB | 428.0 MB/s | 12/12 | 596,564 | 872.3s / 1,129,498 msg/s |
| Dekaf | 2026-07-16T23:22:16.7719826+00:00 | 3 | 5.0 MiB / 5.0 MiB | 428.0 MB/s | 12/13 | 612,795 | 890.3s / 1,091,055 msg/s |
| Dekaf (3conn) | 2026-07-16T23:37:49.1980949+00:00 | 3 | 48.0 MiB / 22.6 MiB | 333.4 MB/s | 0/0 | 1,803 | 9.0s / 752,806 msg/s |
| Dekaf (3conn) | 2026-07-16T23:38:07.2059177+00:00 | 3 | 48.0 MiB / 48.0 MiB | 477.4 MB/s | 0/0 | 4,894 | 27.0s / 1,142,939 msg/s |
| Dekaf (3conn) | 2026-07-16T23:38:26.2385759+00:00 | 1 | 48.0 MiB / 27.0 MiB | 447.6 MB/s | 0/1 | 8,130 | 46.0s / 1,210,212 msg/s |
| Dekaf (3conn) | 2026-07-16T23:38:44.2596742+00:00 | 1 | 48.0 MiB / 3.6 MiB | 479.7 MB/s | 0/1 | 8,523 | 64.1s / 1,167,535 msg/s |
| Dekaf (3conn) | 2026-07-16T23:39:02.2673957+00:00 | 2 | 30.0 MiB / 6.3 MiB | 476.8 MB/s | 3/0 | 10,652 | 82.1s / 1,140,039 msg/s |
| Dekaf (3conn) | 2026-07-16T23:39:20.2761751+00:00 | 2 | 24.0 MiB / 6.5 MiB | 476.8 MB/s | 4/0 | 15,018 | 100.1s / 1,198,761 msg/s |
| Dekaf (3conn) | 2026-07-16T23:39:38.2925827+00:00 | 3 | 36.0 MiB / 36.0 MiB | 485.8 MB/s | 2/1 | 25,726 | 118.1s / 1,191,621 msg/s |
| Dekaf (3conn) | 2026-07-16T23:39:56.3104522+00:00 | 3 | 36.0 MiB / 34.4 MiB | 492.7 MB/s | 2/2 | 29,894 | 136.1s / 1,189,162 msg/s |
| Dekaf (3conn) | 2026-07-16T23:40:15.3210464+00:00 | 1 | 36.0 MiB / 7.7 MiB | 490.0 MB/s | 2/2 | 18,433 | 155.1s / 1,189,206 msg/s |
| Dekaf (3conn) | 2026-07-16T23:40:33.3329802+00:00 | 1 | 30.0 MiB / 1.9 MiB | 490.0 MB/s | 3/2 | 21,658 | 173.2s / 1,254,629 msg/s |
| Dekaf (3conn) | 2026-07-16T23:40:51.3399308+00:00 | 2 | 24.0 MiB / 4.3 MiB | 477.2 MB/s | 4/2 | 32,237 | 191.2s / 1,279,571 msg/s |
| Dekaf (3conn) | 2026-07-16T23:41:09.3571183+00:00 | 2 | 21.0 MiB / 8.3 MiB | 477.2 MB/s | 5/2 | 35,083 | 209.2s / 1,207,075 msg/s |
| Dekaf (3conn) | 2026-07-16T23:41:27.3668544+00:00 | 3 | 24.0 MiB / 16.4 MiB | 492.7 MB/s | 4/3 | 59,870 | 227.2s / 1,224,146 msg/s |
| Dekaf (3conn) | 2026-07-16T23:41:45.3792006+00:00 | 3 | 24.0 MiB / 24.0 MiB | 492.7 MB/s | 4/3 | 64,817 | 245.2s / 1,282,876 msg/s |
| Dekaf (3conn) | 2026-07-16T23:42:04.3947844+00:00 | 1 | 18.0 MiB / 15.6 MiB | 490.0 MB/s | 6/3 | 41,607 | 264.2s / 1,257,634 msg/s |
| Dekaf (3conn) | 2026-07-16T23:42:22.4145916+00:00 | 1 | 18.0 MiB / 3.9 MiB | 490.0 MB/s | 6/3 | 45,888 | 282.3s / 1,214,522 msg/s |
| Dekaf (3conn) | 2026-07-16T23:42:40.4238682+00:00 | 2 | 24.0 MiB / 19.7 MiB | 477.2 MB/s | 6/3 | 48,294 | 300.3s / 1,209,373 msg/s |
| Dekaf (3conn) | 2026-07-16T23:42:58.4321837+00:00 | 2 | 24.0 MiB / 20.7 MiB | 477.2 MB/s | 6/4 | 52,942 | 318.3s / 1,195,269 msg/s |
| Dekaf (3conn) | 2026-07-16T23:43:16.4373842+00:00 | 3 | 21.0 MiB / 5.4 MiB | 492.7 MB/s | 5/5 | 94,505 | 336.3s / 1,187,426 msg/s |
| Dekaf (3conn) | 2026-07-16T23:43:34.4449029+00:00 | 3 | 21.0 MiB / 17.4 MiB | 492.7 MB/s | 5/5 | 100,833 | 354.3s / 1,223,037 msg/s |
| Dekaf (3conn) | 2026-07-16T23:43:53.4541716+00:00 | 1 | 15.0 MiB / 15.0 MiB | 490.0 MB/s | 7/5 | 65,965 | 373.3s / 1,205,771 msg/s |
| Dekaf (3conn) | 2026-07-16T23:44:11.459713+00:00 | 1 | 15.0 MiB / 9.0 MiB | 490.0 MB/s | 7/5 | 71,815 | 391.4s / 1,171,927 msg/s |
| Dekaf (3conn) | 2026-07-16T23:44:29.473132+00:00 | 2 | 24.0 MiB / 3.6 MiB | 477.2 MB/s | 6/6 | 67,909 | 409.4s / 1,180,510 msg/s |
| Dekaf (3conn) | 2026-07-16T23:44:47.4766449+00:00 | 2 | 24.0 MiB / 24.0 MiB | 477.2 MB/s | 6/6 | 70,016 | 427.4s / 1,158,289 msg/s |
| Dekaf (3conn) | 2026-07-16T23:45:05.4863305+00:00 | 3 | 21.0 MiB / 21.0 MiB | 492.7 MB/s | 5/7 | 131,033 | 445.4s / 1,198,047 msg/s |
| Dekaf (3conn) | 2026-07-16T23:45:23.4972084+00:00 | 3 | 21.0 MiB / 11.2 MiB | 492.7 MB/s | 5/8 | 137,627 | 463.4s / 1,212,101 msg/s |
| Dekaf (3conn) | 2026-07-16T23:45:42.5003018+00:00 | 1 | 15.0 MiB / 5.1 MiB | 490.0 MB/s | 9/6 | 95,935 | 482.4s / 1,086,777 msg/s |
| Dekaf (3conn) | 2026-07-16T23:46:00.5063498+00:00 | 1 | 15.0 MiB / 15.0 MiB | 490.0 MB/s | 9/6 | 101,037 | 500.4s / 1,178,175 msg/s |
| Dekaf (3conn) | 2026-07-16T23:46:18.5149427+00:00 | 2 | 24.0 MiB / 4.4 MiB | 477.2 MB/s | 6/8 | 86,982 | 518.4s / 1,188,469 msg/s |
| Dekaf (3conn) | 2026-07-16T23:46:36.5247966+00:00 | 2 | 24.0 MiB / 5.6 MiB | 477.2 MB/s | 6/8 | 89,487 | 536.5s / 1,113,261 msg/s |
| Dekaf (3conn) | 2026-07-16T23:46:54.5372326+00:00 | 3 | 18.0 MiB / 9.2 MiB | 492.7 MB/s | 6/9 | 163,566 | 554.5s / 1,121,201 msg/s |
| Dekaf (3conn) | 2026-07-16T23:47:12.5486774+00:00 | 3 | 18.0 MiB / 3.1 MiB | 492.7 MB/s | 6/10 | 170,838 | 572.5s / 1,228,795 msg/s |
| Dekaf (3conn) | 2026-07-16T23:47:31.5578143+00:00 | 1 | 12.0 MiB / 3.3 MiB | 490.0 MB/s | 10/8 | 131,923 | 591.5s / 1,177,789 msg/s |
| Dekaf (3conn) | 2026-07-16T23:47:49.5710768+00:00 | 1 | 12.0 MiB / 10.9 MiB | 490.0 MB/s | 10/8 | 137,291 | 609.5s / 1,201,747 msg/s |
| Dekaf (3conn) | 2026-07-16T23:48:07.5807343+00:00 | 2 | 24.0 MiB / 1.7 MiB | 477.2 MB/s | 6/10 | 102,582 | 627.5s / 1,163,790 msg/s |
| Dekaf (3conn) | 2026-07-16T23:48:25.5945221+00:00 | 2 | 24.0 MiB / 17.9 MiB | 477.2 MB/s | 6/11 | 103,274 | 645.5s / 1,170,804 msg/s |
| Dekaf (3conn) | 2026-07-16T23:48:43.6102647+00:00 | 3 | 12.0 MiB / 11.7 MiB | 492.7 MB/s | 8/11 | 208,619 | 663.5s / 1,257,395 msg/s |
| Dekaf (3conn) | 2026-07-16T23:49:01.612635+00:00 | 3 | 12.0 MiB / 5.6 MiB | 492.7 MB/s | 8/11 | 216,893 | 681.5s / 1,212,236 msg/s |
| Dekaf (3conn) | 2026-07-16T23:49:20.6198742+00:00 | 1 | 12.0 MiB / 12.0 MiB | 490.0 MB/s | 10/10 | 159,898 | 700.5s / 1,233,678 msg/s |
| Dekaf (3conn) | 2026-07-16T23:49:38.6310158+00:00 | 1 | 12.0 MiB / 8.5 MiB | 490.0 MB/s | 10/11 | 164,723 | 718.5s / 1,216,094 msg/s |
| Dekaf (3conn) | 2026-07-16T23:49:56.6347829+00:00 | 2 | 18.0 MiB / 9.5 MiB | 477.2 MB/s | 8/12 | 113,835 | 736.5s / 1,240,664 msg/s |
| Dekaf (3conn) | 2026-07-16T23:50:14.6383207+00:00 | 2 | 18.0 MiB / 5.6 MiB | 477.2 MB/s | 8/12 | 116,240 | 754.5s / 1,237,599 msg/s |
| Dekaf (3conn) | 2026-07-16T23:50:32.6431035+00:00 | 3 | 9.0 MiB / 2.5 MiB | 492.7 MB/s | 9/13 | 264,683 | 772.6s / 1,238,528 msg/s |
| Dekaf (3conn) | 2026-07-16T23:50:50.6537885+00:00 | 3 | 9.0 MiB / 4.0 MiB | 492.7 MB/s | 9/13 | 274,194 | 790.6s / 1,188,896 msg/s |
| Dekaf (3conn) | 2026-07-16T23:51:09.6617603+00:00 | 1 | 12.0 MiB / 9.4 MiB | 490.0 MB/s | 10/13 | 183,670 | 809.6s / 1,111,593 msg/s |
| Dekaf (3conn) | 2026-07-16T23:51:27.6753308+00:00 | 1 | 9.0 MiB / 9.0 MiB | 490.0 MB/s | 10/13 | 188,341 | 827.6s / 1,160,726 msg/s |
| Dekaf (3conn) | 2026-07-16T23:51:45.6875424+00:00 | 2 | 18.0 MiB / 5.2 MiB | 477.2 MB/s | 10/13 | 129,765 | 845.6s / 1,206,448 msg/s |
| Dekaf (3conn) | 2026-07-16T23:52:03.6941014+00:00 | 2 | 18.0 MiB / 7.2 MiB | 477.2 MB/s | 10/13 | 130,165 | 863.6s / 1,207,834 msg/s |
| Dekaf (3conn) | 2026-07-16T23:52:21.6982122+00:00 | 3 | 6.0 MiB / 1.6 MiB | 492.7 MB/s | 12/13 | 320,453 | 881.6s / 1,211,442 msg/s |
| Dekaf (3conn) | 2026-07-16T23:52:39.7043399+00:00 | 3 | 6.0 MiB / 5.3 MiB | 492.7 MB/s | 12/13 | 333,866 | 899.6s / 1,181,141 msg/s |
*5,296 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-16T23:07:56.6763271+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.4 MiB |
| Dekaf | 2026-07-16T23:08:11.73023+00:00 | 1 | capacity | succeeded | 15,054ms | 14.0 MiB / 8.5 MiB |
| Dekaf | 2026-07-16T23:08:14.742512+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 5.5 MiB |
| Dekaf | 2026-07-16T23:08:29.7872897+00:00 | 1 | capacity | succeeded | 15,044ms | 12.0 MiB / 3.7 MiB |
| Dekaf | 2026-07-16T23:08:32.7942694+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 4.0 MiB |
| Dekaf | 2026-07-16T23:08:47.8505943+00:00 | 1 | capacity | succeeded | 15,056ms | 10.0 MiB / 7.7 MiB |
| Dekaf | 2026-07-16T23:08:50.8592193+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 7.6 MiB |
| Dekaf | 2026-07-16T23:09:05.906844+00:00 | 1 | capacity | failed | 15,047ms | 10.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-16T23:09:36.0401414+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-16T23:09:51.1036464+00:00 | 3 | capacity | failed | 15,063ms | 10.0 MiB / 2.2 MiB |
| Dekaf | 2026-07-16T23:10:21.1923221+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 9.0 MiB |
| Dekaf | 2026-07-16T23:10:36.2316169+00:00 | 3 | capacity | succeeded | 15,039ms | 8.0 MiB / 5.2 MiB |
| Dekaf | 2026-07-16T23:10:39.2402906+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 4.3 MiB |
| Dekaf | 2026-07-16T23:10:54.3327977+00:00 | 2 | capacity | succeeded | 15,043ms | 7.0 MiB / 5.3 MiB |
| Dekaf | 2026-07-16T23:11:12.3893432+00:00 | 2 | capacity | succeeded | 15,047ms | 8.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-16T23:11:24.4508119+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 6.9 MiB |
| Dekaf | 2026-07-16T23:11:42.4792318+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-16T23:12:09.6294477+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 5.5 MiB |
| Dekaf | 2026-07-16T23:12:27.6287582+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-16T23:12:42.6945624+00:00 | 2 | capacity | failed | 15,042ms | 8.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-16T23:13:12.788207+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-16T23:13:27.8311421+00:00 | 2 | capacity | succeeded | 15,042ms | 9.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-16T23:13:30.8459616+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-16T23:13:58.0180325+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-16T23:14:15.9735469+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-16T23:14:31.0562742+00:00 | 3 | capacity | failed | 15,082ms | 6.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-16T23:14:58.1388865+00:00 | 2 | capacity | failed | 15,042ms | 9.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-16T23:15:16.2031469+00:00 | 3 | capacity | succeeded | 15,045ms | 7.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-16T23:15:43.2977062+00:00 | 2 | capacity | failed | 15,056ms | 9.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-16T23:16:01.3388619+00:00 | 3 | capacity | succeeded | 15,044ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-16T23:16:13.3894296+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-16T23:16:31.5237442+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-16T23:16:49.5839372+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-16T23:17:04.6397465+00:00 | 1 | capacity | failed | 15,055ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-16T23:17:34.7216746+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-16T23:17:49.776758+00:00 | 1 | capacity | succeeded | 15,055ms | 6.0 MiB / 2.2 MiB |
| Dekaf | 2026-07-16T23:18:07.8376197+00:00 | 1 | capacity | succeeded | 15,052ms | 7.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-16T23:18:34.8303638+00:00 | 3 | capacity | succeeded | 15,034ms | 6.0 MiB / 3.5 MiB |
| Dekaf | 2026-07-16T23:18:43.8855801+00:00 | 2 | capacity | failed | 15,040ms | 9.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-16T23:19:13.9742944+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-16T23:19:29.0557678+00:00 | 2 | capacity | succeeded | 15,081ms | 7.0 MiB / 0.6 MiB |
| Dekaf | 2026-07-16T23:19:38.1589634+00:00 | 1 | capacity | failed | 15,046ms | 7.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-16T23:20:08.2725927+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-16T23:20:23.3256628+00:00 | 1 | capacity | failed | 15,053ms | 7.0 MiB / 0.7 MiB |
| Dekaf | 2026-07-16T23:20:53.4231545+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-16T23:21:08.4731954+00:00 | 1 | capacity | failed | 15,050ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-16T23:21:26.3921129+00:00 | 3 | capacity | failed | 15,058ms | 5.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-16T23:21:53.6440996+00:00 | 1 | capacity | failed | 15,041ms | 7.0 MiB / 2.2 MiB |
| Dekaf | 2026-07-16T23:22:11.5572868+00:00 | 3 | capacity | failed | 15,074ms | 5.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-16T23:38:10.4004593+00:00 | 2 | capacity | started | 0ms | 42.0 MiB / 11.8 MiB |
| Dekaf (3conn) | 2026-07-16T23:38:25.4848684+00:00 | 2 | capacity | succeeded | 15,084ms | 42.0 MiB / 24.6 MiB |
| Dekaf (3conn) | 2026-07-16T23:38:28.4886853+00:00 | 2 | capacity | started | 0ms | 36.0 MiB / 1.8 MiB |
| Dekaf (3conn) | 2026-07-16T23:38:46.5594623+00:00 | 2 | capacity | started | 0ms | 30.0 MiB / 18.3 MiB |
| Dekaf (3conn) | 2026-07-16T23:39:04.6476662+00:00 | 2 | capacity | started | 0ms | 24.0 MiB / 27.6 MiB |
| Dekaf (3conn) | 2026-07-16T23:39:19.7412653+00:00 | 2 | capacity | succeeded | 15,097ms | 24.0 MiB / 19.9 MiB |
| Dekaf (3conn) | 2026-07-16T23:39:31.7412303+00:00 | 3 | capacity | started | 0ms | 30.0 MiB / 34.9 MiB |
| Dekaf (3conn) | 2026-07-16T23:39:46.7978394+00:00 | 3 | capacity | failed | 15,056ms | 36.0 MiB / 29.0 MiB |
| Dekaf (3conn) | 2026-07-16T23:40:07.9077422+00:00 | 2 | capacity | started | 0ms | 21.0 MiB / 22.8 MiB |
| Dekaf (3conn) | 2026-07-16T23:40:17.000702+00:00 | 1 | capacity | started | 0ms | 30.0 MiB / 33.7 MiB |
| Dekaf (3conn) | 2026-07-16T23:40:32.0678735+00:00 | 1 | capacity | succeeded | 15,067ms | 30.0 MiB / 4.2 MiB |
| Dekaf (3conn) | 2026-07-16T23:40:50.0312288+00:00 | 3 | capacity | succeeded | 15,053ms | 24.0 MiB / 18.2 MiB |
| Dekaf (3conn) | 2026-07-16T23:40:53.0840335+00:00 | 2 | capacity | started | 0ms | 21.0 MiB / 16.8 MiB |
| Dekaf (3conn) | 2026-07-16T23:41:08.1280624+00:00 | 2 | capacity | succeeded | 15,044ms | 21.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-16T23:41:11.2747405+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 17.1 MiB |
| Dekaf (3conn) | 2026-07-16T23:41:29.3432041+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.4 MiB |
| Dekaf (3conn) | 2026-07-16T23:41:53.3373663+00:00 | 3 | capacity | succeeded | 15,055ms | 21.0 MiB / 11.4 MiB |
| Dekaf (3conn) | 2026-07-16T23:42:11.4085309+00:00 | 3 | capacity | failed | 15,059ms | 21.0 MiB / 8.9 MiB |
| Dekaf (3conn) | 2026-07-16T23:42:29.5937117+00:00 | 1 | capacity | failed | 15,061ms | 18.0 MiB / 14.1 MiB |
| Dekaf (3conn) | 2026-07-16T23:42:56.5806137+00:00 | 3 | capacity | failed | 15,040ms | 21.0 MiB / 16.8 MiB |
| Dekaf (3conn) | 2026-07-16T23:43:14.8279067+00:00 | 1 | capacity | succeeded | 15,040ms | 15.0 MiB / 5.6 MiB |
| Dekaf (3conn) | 2026-07-16T23:43:26.7633919+00:00 | 2 | capacity | started | 0ms | 21.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-16T23:43:41.8130721+00:00 | 2 | capacity | failed | 15,049ms | 24.0 MiB / 6.5 MiB |
| Dekaf (3conn) | 2026-07-16T23:44:11.9293422+00:00 | 2 | capacity | started | 0ms | 21.0 MiB / 16.9 MiB |
| Dekaf (3conn) | 2026-07-16T23:44:26.9555966+00:00 | 3 | capacity | failed | 15,053ms | 21.0 MiB / 7.2 MiB |
| Dekaf (3conn) | 2026-07-16T23:44:57.0670435+00:00 | 3 | capacity | started | 0ms | 18.0 MiB / 20.1 MiB |
| Dekaf (3conn) | 2026-07-16T23:45:06.2927608+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.4 MiB |
| Dekaf (3conn) | 2026-07-16T23:45:21.3402654+00:00 | 1 | capacity | failed | 15,047ms | 15.0 MiB / 7.0 MiB |
| Dekaf (3conn) | 2026-07-16T23:45:51.4851167+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 11.8 MiB |
| Dekaf (3conn) | 2026-07-16T23:46:06.5555918+00:00 | 1 | capacity | succeeded | 15,070ms | 12.0 MiB / 7.9 MiB |
| Dekaf (3conn) | 2026-07-16T23:46:27.4591488+00:00 | 3 | capacity | started | 0ms | 18.0 MiB / 6.0 MiB |
| Dekaf (3conn) | 2026-07-16T23:46:42.5292751+00:00 | 2 | capacity | failed | 15,053ms | 24.0 MiB / 9.2 MiB |
| Dekaf (3conn) | 2026-07-16T23:47:00.5925313+00:00 | 3 | capacity | failed | 15,055ms | 18.0 MiB / 18.1 MiB |
| Dekaf (3conn) | 2026-07-16T23:47:27.7553269+00:00 | 2 | capacity | failed | 15,117ms | 24.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-16T23:47:45.7862102+00:00 | 3 | capacity | succeeded | 15,052ms | 15.0 MiB / 3.7 MiB |
| Dekaf (3conn) | 2026-07-16T23:47:57.8664842+00:00 | 2 | capacity | started | 0ms | 21.0 MiB / 22.9 MiB |
| Dekaf (3conn) | 2026-07-16T23:48:12.9176546+00:00 | 2 | capacity | failed | 15,051ms | 24.0 MiB / 12.9 MiB |
| Dekaf (3conn) | 2026-07-16T23:48:40.2752838+00:00 | 1 | capacity | failed | 15,053ms | 12.0 MiB / 5.3 MiB |
| Dekaf (3conn) | 2026-07-16T23:48:58.0798791+00:00 | 2 | capacity | succeeded | 15,054ms | 21.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-07-16T23:49:10.3764481+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 1.6 MiB |
| Dekaf (3conn) | 2026-07-16T23:49:25.4303863+00:00 | 1 | capacity | failed | 15,053ms | 12.0 MiB / 11.9 MiB |
| Dekaf (3conn) | 2026-07-16T23:49:52.3054598+00:00 | 3 | capacity | succeeded | 15,041ms | 9.0 MiB / 5.0 MiB |
| Dekaf (3conn) | 2026-07-16T23:50:04.3495489+00:00 | 2 | capacity | started | 0ms | 21.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-07-16T23:50:19.3969016+00:00 | 2 | capacity | succeeded | 15,047ms | 21.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-16T23:50:49.5005176+00:00 | 2 | capacity | started | 0ms | 18.0 MiB / 9.8 MiB |
| Dekaf (3conn) | 2026-07-16T23:51:04.5437024+00:00 | 2 | capacity | succeeded | 15,043ms | 18.0 MiB / 2.7 MiB |
| Dekaf (3conn) | 2026-07-16T23:51:25.6196241+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-07-16T23:51:40.991934+00:00 | 1 | capacity | succeeded | 15,041ms | 9.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-07-16T23:51:52.7634095+00:00 | 2 | capacity | started | 0ms | 21.0 MiB / 6.9 MiB |
| Dekaf (3conn) | 2026-07-16T23:52:07.8271873+00:00 | 2 | capacity | failed | 15,063ms | 18.0 MiB / 6.6 MiB |
| Dekaf (3conn) | 2026-07-16T23:52:37.9480417+00:00 | 2 | capacity | started | 0ms | 15.0 MiB / 10.9 MiB |
*196 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 28 |
| Dekaf | 1 | 0.002–0.004ms | 48 |
| Dekaf | 1 | 0.004–0.008ms | 193 |
| Dekaf | 1 | 0.008–0.016ms | 567 |
| Dekaf | 1 | 0.016–0.032ms | 1,244 |
| Dekaf | 1 | 0.032–0.064ms | 1,583 |
| Dekaf | 1 | 0.064–0.128ms | 2,124 |
| Dekaf | 1 | 0.128–0.256ms | 3,593 |
| Dekaf | 1 | 0.256–0.512ms | 7,133 |
| Dekaf | 1 | 0.512–1.024ms | 11,042 |
| Dekaf | 1 | 1.024–2.048ms | 10,760 |
| Dekaf | 1 | 2.048–4.096ms | 5,759 |
| Dekaf | 1 | 4.096–8.192ms | 2,013 |
| Dekaf | 1 | 8.192–16.384ms | 515 |
| Dekaf | 1 | 16.384–32.768ms | 94 |
| Dekaf | 1 | 32.768–65.536ms | 1 |
| Dekaf | 2 | 0.001–0.002ms | 28 |
| Dekaf | 2 | 0.002–0.004ms | 35 |
| Dekaf | 2 | 0.004–0.008ms | 171 |
| Dekaf | 2 | 0.008–0.016ms | 470 |
| Dekaf | 2 | 0.016–0.032ms | 992 |
| Dekaf | 2 | 0.032–0.064ms | 1,254 |
| Dekaf | 2 | 0.064–0.128ms | 1,764 |
| Dekaf | 2 | 0.128–0.256ms | 2,964 |
| Dekaf | 2 | 0.256–0.512ms | 5,542 |
| Dekaf | 2 | 0.512–1.024ms | 8,407 |
| Dekaf | 2 | 1.024–2.048ms | 7,857 |
| Dekaf | 2 | 2.048–4.096ms | 4,185 |
| Dekaf | 2 | 4.096–8.192ms | 1,380 |
| Dekaf | 2 | 8.192–16.384ms | 279 |
| Dekaf | 2 | 16.384–32.768ms | 48 |
| Dekaf | 2 | 32.768–65.536ms | 1 |
| Dekaf | 3 | 0.001–0.002ms | 119 |
| Dekaf | 3 | 0.002–0.004ms | 128 |
| Dekaf | 3 | 0.004–0.008ms | 468 |
| Dekaf | 3 | 0.008–0.016ms | 1,345 |
| Dekaf | 3 | 0.016–0.032ms | 3,039 |
| Dekaf | 3 | 0.032–0.064ms | 4,409 |
| Dekaf | 3 | 0.064–0.128ms | 6,117 |
| Dekaf | 3 | 0.128–0.256ms | 11,061 |
| Dekaf | 3 | 0.256–0.512ms | 22,914 |
| Dekaf | 3 | 0.512–1.024ms | 37,489 |
| Dekaf | 3 | 1.024–2.048ms | 35,020 |
| Dekaf | 3 | 2.048–4.096ms | 17,330 |
| Dekaf | 3 | 4.096–8.192ms | 5,688 |
| Dekaf | 3 | 8.192–16.384ms | 1,266 |
| Dekaf | 3 | 16.384–32.768ms | 200 |
| Dekaf | 3 | 32.768–65.536ms | 5 |
| Dekaf (3conn) | 1 | 0.001–0.002ms | 23 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 40 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 105 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 422 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 1,156 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 1,486 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 1,896 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 2,786 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 4,720 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 7,406 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 9,075 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 7,134 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 2,730 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 774 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 335 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 71 |
| Dekaf (3conn) | 1 | 65.536–131.072ms | 1 |
| Dekaf (3conn) | 2 | 0.001–0.002ms | 14 |
| Dekaf (3conn) | 2 | 0.002–0.004ms | 26 |
| Dekaf (3conn) | 2 | 0.004–0.008ms | 54 |
| Dekaf (3conn) | 2 | 0.008–0.016ms | 245 |
| Dekaf (3conn) | 2 | 0.016–0.032ms | 752 |
| Dekaf (3conn) | 2 | 0.032–0.064ms | 883 |
| Dekaf (3conn) | 2 | 0.064–0.128ms | 1,204 |
| Dekaf (3conn) | 2 | 0.128–0.256ms | 1,930 |
| Dekaf (3conn) | 2 | 0.256–0.512ms | 3,122 |
| Dekaf (3conn) | 2 | 0.512–1.024ms | 4,867 |
| Dekaf (3conn) | 2 | 1.024–2.048ms | 6,004 |
| Dekaf (3conn) | 2 | 2.048–4.096ms | 4,812 |
| Dekaf (3conn) | 2 | 4.096–8.192ms | 1,788 |
| Dekaf (3conn) | 2 | 8.192–16.384ms | 428 |
| Dekaf (3conn) | 2 | 16.384–32.768ms | 164 |
| Dekaf (3conn) | 2 | 32.768–65.536ms | 23 |
| Dekaf (3conn) | 3 | 0.001–0.002ms | 39 |
| Dekaf (3conn) | 3 | 0.002–0.004ms | 62 |
| Dekaf (3conn) | 3 | 0.004–0.008ms | 181 |
| Dekaf (3conn) | 3 | 0.008–0.016ms | 712 |
| Dekaf (3conn) | 3 | 0.016–0.032ms | 2,030 |
| Dekaf (3conn) | 3 | 0.032–0.064ms | 2,374 |
| Dekaf (3conn) | 3 | 0.064–0.128ms | 3,286 |
| Dekaf (3conn) | 3 | 0.128–0.256ms | 5,056 |
| Dekaf (3conn) | 3 | 0.256–0.512ms | 8,604 |
| Dekaf (3conn) | 3 | 0.512–1.024ms | 13,132 |
| Dekaf (3conn) | 3 | 1.024–2.048ms | 15,200 |
| Dekaf (3conn) | 3 | 2.048–4.096ms | 11,855 |
| Dekaf (3conn) | 3 | 4.096–8.192ms | 4,096 |
| Dekaf (3conn) | 3 | 8.192–16.384ms | 976 |
| Dekaf (3conn) | 3 | 16.384–32.768ms | 377 |
| Dekaf (3conn) | 3 | 32.768–65.536ms | 76 |
| Dekaf (3conn) | 3 | 65.536–131.072ms | 1 |

## Delivery Latency Outliers - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 46,000 | 2026-07-16T23:07:26.6499151+00:00 | 130.0ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 54,000 | 2026-07-16T23:07:26.6592574+00:00 | 149.2ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 55,000 | 2026-07-16T23:07:26.660308+00:00 | 149.9ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 56,000 | 2026-07-16T23:07:26.6615333+00:00 | 146.9ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 58,000 | 2026-07-16T23:07:26.6640085+00:00 | 146.2ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 64,000 | 2026-07-16T23:07:26.6718343+00:00 | 165.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 65,000 | 2026-07-16T23:07:26.6761683+00:00 | 151.8ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 66,000 | 2026-07-16T23:07:26.6780668+00:00 | 159.2ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 67,000 | 2026-07-16T23:07:26.6790672+00:00 | 104.1ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 68,000 | 2026-07-16T23:07:26.680049+00:00 | 167.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 74,000 | 2026-07-16T23:07:26.6872601+00:00 | 165.5ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 75,000 | 2026-07-16T23:07:26.6887195+00:00 | 171.6ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 76,000 | 2026-07-16T23:07:26.6899415+00:00 | 162.8ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 77,000 | 2026-07-16T23:07:26.6913635+00:00 | 128.6ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 78,000 | 2026-07-16T23:07:26.6927196+00:00 | 166.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 84,000 | 2026-07-16T23:07:26.7005368+00:00 | 240.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 85,000 | 2026-07-16T23:07:26.701842+00:00 | 246.5ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 86,000 | 2026-07-16T23:07:26.7028892+00:00 | 238.0ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 87,000 | 2026-07-16T23:07:26.7042617+00:00 | 130.6ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 88,000 | 2026-07-16T23:07:26.7054787+00:00 | 242.8ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 94,000 | 2026-07-16T23:07:26.7137097+00:00 | 239.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 95,000 | 2026-07-16T23:07:26.7165745+00:00 | 248.1ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 96,000 | 2026-07-16T23:07:26.7177447+00:00 | 235.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 97,000 | 2026-07-16T23:07:26.7228126+00:00 | 153.9ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 98,000 | 2026-07-16T23:07:26.7269392+00:00 | 262.5ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 104,000 | 2026-07-16T23:07:26.7442619+00:00 | 240.0ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 105,000 | 2026-07-16T23:07:26.7456626+00:00 | 258.0ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 106,000 | 2026-07-16T23:07:26.7465479+00:00 | 237.7ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 107,000 | 2026-07-16T23:07:26.7509106+00:00 | 201.8ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 108,000 | 2026-07-16T23:07:26.7520573+00:00 | 251.7ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 114,000 | 2026-07-16T23:07:26.7655956+00:00 | 242.7ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 115,000 | 2026-07-16T23:07:26.7665075+00:00 | 273.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 116,000 | 2026-07-16T23:07:26.7723166+00:00 | 254.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 117,000 | 2026-07-16T23:07:26.7750331+00:00 | 197.9ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 118,000 | 2026-07-16T23:07:26.7759329+00:00 | 284.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 124,000 | 2026-07-16T23:07:26.800675+00:00 | 234.6ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 125,000 | 2026-07-16T23:07:26.8032226+00:00 | 292.2ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 126,000 | 2026-07-16T23:07:26.8089976+00:00 | 226.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 127,000 | 2026-07-16T23:07:26.8101503+00:00 | 185.1ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 128,000 | 2026-07-16T23:07:26.8110834+00:00 | 284.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 129,000 | 2026-07-16T23:07:26.8169529+00:00 | 118.5ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 130,000 | 2026-07-16T23:07:26.8181692+00:00 | 124.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 131,000 | 2026-07-16T23:07:26.8280147+00:00 | 120.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 132,000 | 2026-07-16T23:07:26.8294935+00:00 | 118.8ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 133,000 | 2026-07-16T23:07:26.8306521+00:00 | 129.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 134,000 | 2026-07-16T23:07:26.8315503+00:00 | 217.0ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 135,000 | 2026-07-16T23:07:26.8354029+00:00 | 268.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 136,000 | 2026-07-16T23:07:26.8365341+00:00 | 221.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 137,000 | 2026-07-16T23:07:26.837829+00:00 | 169.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 138,000 | 2026-07-16T23:07:26.8420347+00:00 | 277.8ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 139,000 | 2026-07-16T23:07:26.8432703+00:00 | 124.6ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 140,000 | 2026-07-16T23:07:26.8441644+00:00 | 133.8ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 141,000 | 2026-07-16T23:07:26.8484294+00:00 | 120.5ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 142,000 | 2026-07-16T23:07:26.8495892+00:00 | 119.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 143,000 | 2026-07-16T23:07:26.850913+00:00 | 126.6ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 144,000 | 2026-07-16T23:07:26.8543199+00:00 | 218.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 145,000 | 2026-07-16T23:07:26.8555356+00:00 | 272.9ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 146,000 | 2026-07-16T23:07:26.8564459+00:00 | 216.2ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 147,000 | 2026-07-16T23:07:26.859775+00:00 | 169.0ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 148,000 | 2026-07-16T23:07:26.8643596+00:00 | 264.0ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 149,000 | 2026-07-16T23:07:26.865775+00:00 | 115.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 150,000 | 2026-07-16T23:07:26.8751696+00:00 | 121.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 151,000 | 2026-07-16T23:07:26.8779781+00:00 | 114.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 152,000 | 2026-07-16T23:07:26.8788397+00:00 | 113.5ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 154,000 | 2026-07-16T23:07:26.8836446+00:00 | 211.8ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 155,000 | 2026-07-16T23:07:26.8849342+00:00 | 262.7ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 156,000 | 2026-07-16T23:07:26.942776+00:00 | 156.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 157,000 | 2026-07-16T23:07:26.9439597+00:00 | 145.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 158,000 | 2026-07-16T23:07:26.9449237+00:00 | 218.2ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 160,000 | 2026-07-16T23:07:26.9494669+00:00 | 100.6ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 164,000 | 2026-07-16T23:07:26.9563937+00:00 | 148.1ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 165,000 | 2026-07-16T23:07:26.9573589+00:00 | 226.2ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 166,000 | 2026-07-16T23:07:26.9653004+00:00 | 139.1ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 167,000 | 2026-07-16T23:07:26.9663496+00:00 | 163.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 168,000 | 2026-07-16T23:07:26.9675836+00:00 | 216.0ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 170,000 | 2026-07-16T23:07:26.9795498+00:00 | 102.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 173,000 | 2026-07-16T23:07:26.9908928+00:00 | 147.7ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 174,000 | 2026-07-16T23:07:26.9916837+00:00 | 210.0ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 175,000 | 2026-07-16T23:07:26.9975656+00:00 | 213.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 176,000 | 2026-07-16T23:07:26.9985459+00:00 | 209.7ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 177,000 | 2026-07-16T23:07:27.0004731+00:00 | 158.1ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 178,000 | 2026-07-16T23:07:27.0039831+00:00 | 207.0ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 179,000 | 2026-07-16T23:07:27.0052597+00:00 | 163.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 183,000 | 2026-07-16T23:07:27.0260672+00:00 | 142.7ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 184,000 | 2026-07-16T23:07:27.0400974+00:00 | 191.1ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 185,000 | 2026-07-16T23:07:27.0411748+00:00 | 172.7ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 186,000 | 2026-07-16T23:07:27.0420034+00:00 | 189.2ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 187,000 | 2026-07-16T23:07:27.0505213+00:00 | 271.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 188,000 | 2026-07-16T23:07:27.0517627+00:00 | 167.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 189,000 | 2026-07-16T23:07:27.0525766+00:00 | 136.8ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 191,000 | 2026-07-16T23:07:27.0610421+00:00 | 159.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 192,000 | 2026-07-16T23:07:27.0618531+00:00 | 158.5ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 193,000 | 2026-07-16T23:07:27.0630405+00:00 | 151.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 194,000 | 2026-07-16T23:07:27.0741138+00:00 | 214.5ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 195,000 | 2026-07-16T23:07:27.0751192+00:00 | 148.9ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 196,000 | 2026-07-16T23:07:27.075887+00:00 | 212.8ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 197,000 | 2026-07-16T23:07:27.0837648+00:00 | 260.2ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 198,000 | 2026-07-16T23:07:27.0846437+00:00 | 139.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 199,000 | 2026-07-16T23:07:27.0858007+00:00 | 141.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 201,000 | 2026-07-16T23:07:27.0972346+00:00 | 137.9ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 202,000 | 2026-07-16T23:07:27.0985105+00:00 | 145.2ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 203,000 | 2026-07-16T23:07:27.0992913+00:00 | 134.7ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 204,000 | 2026-07-16T23:07:27.1000864+00:00 | 212.2ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 205,000 | 2026-07-16T23:07:27.1010503+00:00 | 132.9ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 206,000 | 2026-07-16T23:07:27.1039465+00:00 | 208.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 207,000 | 2026-07-16T23:07:27.1051398+00:00 | 303.9ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 208,000 | 2026-07-16T23:07:27.1063194+00:00 | 136.1ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 209,000 | 2026-07-16T23:07:27.1143452+00:00 | 127.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 211,000 | 2026-07-16T23:07:27.1163597+00:00 | 131.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 212,000 | 2026-07-16T23:07:27.1173629+00:00 | 130.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 213,000 | 2026-07-16T23:07:27.1206378+00:00 | 121.1ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 214,000 | 2026-07-16T23:07:27.1222215+00:00 | 206.0ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 215,000 | 2026-07-16T23:07:27.1230243+00:00 | 131.0ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 216,000 | 2026-07-16T23:07:27.1302055+00:00 | 198.0ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 217,000 | 2026-07-16T23:07:27.1310707+00:00 | 289.8ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 218,000 | 2026-07-16T23:07:27.1322186+00:00 | 121.8ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 219,000 | 2026-07-16T23:07:27.1373947+00:00 | 118.6ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 221,000 | 2026-07-16T23:07:27.1397357+00:00 | 115.1ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 222,000 | 2026-07-16T23:07:27.1458042+00:00 | 109.1ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 223,000 | 2026-07-16T23:07:27.1466379+00:00 | 134.1ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 224,000 | 2026-07-16T23:07:27.1478581+00:00 | 188.6ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 225,000 | 2026-07-16T23:07:27.1578147+00:00 | 102.6ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 226,000 | 2026-07-16T23:07:27.160211+00:00 | 176.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 227,000 | 2026-07-16T23:07:27.161644+00:00 | 300.8ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 229,000 | 2026-07-16T23:07:27.2023819+00:00 | 101.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 233,000 | 2026-07-16T23:07:27.2104169+00:00 | 122.2ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 234,000 | 2026-07-16T23:07:27.2149373+00:00 | 139.8ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 236,000 | 2026-07-16T23:07:27.2221355+00:00 | 132.6ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 237,000 | 2026-07-16T23:07:27.2277568+00:00 | 327.0ms | GC pause | - | - | 2.0s / 510,034 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 239,000 | 2026-07-16T23:07:27.2332419+00:00 | 128.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 243,000 | 2026-07-16T23:07:27.2422296+00:00 | 119.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 244,000 | 2026-07-16T23:07:27.2439479+00:00 | 129.4ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 246,000 | 2026-07-16T23:07:27.2564578+00:00 | 129.1ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 247,000 | 2026-07-16T23:07:27.2577528+00:00 | 302.7ms | GC pause | - | - | 2.0s / 510,034 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 253,000 | 2026-07-16T23:07:27.297183+00:00 | 112.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 257,000 | 2026-07-16T23:07:27.307294+00:00 | 277.5ms | GC pause | - | - | 2.0s / 510,034 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 259,000 | 2026-07-16T23:07:27.3205849+00:00 | 109.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 263,000 | 2026-07-16T23:07:27.3324202+00:00 | 117.1ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 267,000 | 2026-07-16T23:07:27.3584015+00:00 | 263.3ms | GC pause | - | - | 2.0s / 510,034 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 269,000 | 2026-07-16T23:07:27.3610695+00:00 | 119.3ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 273,000 | 2026-07-16T23:07:27.379228+00:00 | 101.2ms | GC pause | - | - | 1.0s / 299,703 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 277,000 | 2026-07-16T23:07:27.4096402+00:00 | 222.1ms | GC pause | - | - | 2.0s / 510,034 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 279,000 | 2026-07-16T23:07:27.410985+00:00 | 130.8ms | GC pause | - | - | 2.0s / 510,034 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 283,000 | 2026-07-16T23:07:27.4216328+00:00 | 136.1ms | GC pause | - | - | 2.0s / 510,034 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 287,000 | 2026-07-16T23:07:27.431445+00:00 | 226.6ms | GC pause | - | - | 2.0s / 510,034 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 289,000 | 2026-07-16T23:07:27.4428191+00:00 | 125.5ms | GC pause | - | - | 2.0s / 510,034 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 293,000 | 2026-07-16T23:07:27.4514127+00:00 | 126.5ms | GC pause | - | - | 2.0s / 510,034 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 297,000 | 2026-07-16T23:07:27.4811902+00:00 | 213.4ms | GC pause | - | - | 2.0s / 510,034 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 299,000 | 2026-07-16T23:07:27.4839074+00:00 | 112.1ms | GC pause | - | - | 2.0s / 510,034 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf | 307,000 | 2026-07-16T23:07:27.5551338+00:00 | 151.9ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 317,000 | 2026-07-16T23:07:27.5700799+00:00 | 164.6ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 327,000 | 2026-07-16T23:07:27.586857+00:00 | 165.7ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 337,000 | 2026-07-16T23:07:27.6140651+00:00 | 159.6ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 347,000 | 2026-07-16T23:07:27.6362999+00:00 | 190.7ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 357,000 | 2026-07-16T23:07:27.6542568+00:00 | 184.9ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 367,000 | 2026-07-16T23:07:27.685729+00:00 | 179.3ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 377,000 | 2026-07-16T23:07:27.7083772+00:00 | 178.1ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 387,000 | 2026-07-16T23:07:27.7326157+00:00 | 161.8ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 397,000 | 2026-07-16T23:07:27.7586987+00:00 | 155.1ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 407,000 | 2026-07-16T23:07:27.7743452+00:00 | 149.4ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 417,000 | 2026-07-16T23:07:27.8112438+00:00 | 128.5ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 427,000 | 2026-07-16T23:07:27.8407072+00:00 | 111.5ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 447,000 | 2026-07-16T23:07:27.8771233+00:00 | 167.5ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 457,000 | 2026-07-16T23:07:27.8995658+00:00 | 158.3ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 467,000 | 2026-07-16T23:07:27.9142097+00:00 | 149.5ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 477,000 | 2026-07-16T23:07:27.9296038+00:00 | 179.0ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 487,000 | 2026-07-16T23:07:27.9393385+00:00 | 172.3ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 497,000 | 2026-07-16T23:07:27.9522979+00:00 | 181.3ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 499,000 | 2026-07-16T23:07:27.9534001+00:00 | 101.2ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 503,000 | 2026-07-16T23:07:27.9590291+00:00 | 103.2ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 507,000 | 2026-07-16T23:07:28.0147688+00:00 | 138.4ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 517,000 | 2026-07-16T23:07:28.0367161+00:00 | 128.7ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 527,000 | 2026-07-16T23:07:28.0565412+00:00 | 119.4ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 537,000 | 2026-07-16T23:07:28.0675971+00:00 | 131.6ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 547,000 | 2026-07-16T23:07:28.1007654+00:00 | 113.2ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 557,000 | 2026-07-16T23:07:28.1134645+00:00 | 119.2ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 567,000 | 2026-07-16T23:07:28.1345262+00:00 | 106.7ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 577,000 | 2026-07-16T23:07:28.1475591+00:00 | 107.7ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 587,000 | 2026-07-16T23:07:28.1652006+00:00 | 106.8ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 597,000 | 2026-07-16T23:07:28.1764335+00:00 | 112.1ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 607,000 | 2026-07-16T23:07:28.1949057+00:00 | 103.5ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 747,000 | 2026-07-16T23:07:28.4187979+00:00 | 108.6ms | throughput collapse | - | - | 2.0s / 510,034 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 758,000 | 2026-07-16T23:07:28.4381333+00:00 | 125.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 765,000 | 2026-07-16T23:07:28.4437035+00:00 | 121.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 767,000 | 2026-07-16T23:07:28.4442367+00:00 | 104.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 768,000 | 2026-07-16T23:07:28.4446292+00:00 | 120.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 775,000 | 2026-07-16T23:07:28.4564476+00:00 | 133.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 777,000 | 2026-07-16T23:07:28.4573948+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 778,000 | 2026-07-16T23:07:28.4576615+00:00 | 137.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 785,000 | 2026-07-16T23:07:28.4684093+00:00 | 133.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 787,000 | 2026-07-16T23:07:28.4693477+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 788,000 | 2026-07-16T23:07:28.4711512+00:00 | 130.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 795,000 | 2026-07-16T23:07:28.4815144+00:00 | 135.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 798,000 | 2026-07-16T23:07:28.4955917+00:00 | 121.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 805,000 | 2026-07-16T23:07:28.5141989+00:00 | 112.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 808,000 | 2026-07-16T23:07:28.5181438+00:00 | 121.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 815,000 | 2026-07-16T23:07:28.5352384+00:00 | 117.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 818,000 | 2026-07-16T23:07:28.5363655+00:00 | 116.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 825,000 | 2026-07-16T23:07:28.5509197+00:00 | 116.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 828,000 | 2026-07-16T23:07:28.553917+00:00 | 123.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 835,000 | 2026-07-16T23:07:28.5639868+00:00 | 123.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 838,000 | 2026-07-16T23:07:28.5653515+00:00 | 122.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 845,000 | 2026-07-16T23:07:28.5909695+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 848,000 | 2026-07-16T23:07:28.5944053+00:00 | 109.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 855,000 | 2026-07-16T23:07:28.6006436+00:00 | 109.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 858,000 | 2026-07-16T23:07:28.6029248+00:00 | 107.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 865,000 | 2026-07-16T23:07:28.6153772+00:00 | 103.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 868,000 | 2026-07-16T23:07:28.6174059+00:00 | 157.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 875,000 | 2026-07-16T23:07:28.6271485+00:00 | 152.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 878,000 | 2026-07-16T23:07:28.6295215+00:00 | 159.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 885,000 | 2026-07-16T23:07:28.6436633+00:00 | 152.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 888,000 | 2026-07-16T23:07:28.6511927+00:00 | 144.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 894,000 | 2026-07-16T23:07:28.6628708+00:00 | 112.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 895,000 | 2026-07-16T23:07:28.663159+00:00 | 141.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 896,000 | 2026-07-16T23:07:28.6672779+00:00 | 107.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 898,000 | 2026-07-16T23:07:28.6683317+00:00 | 146.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 904,000 | 2026-07-16T23:07:28.6780943+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 905,000 | 2026-07-16T23:07:28.6785257+00:00 | 147.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 906,000 | 2026-07-16T23:07:28.6847271+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 908,000 | 2026-07-16T23:07:28.6856416+00:00 | 140.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 914,000 | 2026-07-16T23:07:28.6933757+00:00 | 110.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 915,000 | 2026-07-16T23:07:28.6962529+00:00 | 144.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 916,000 | 2026-07-16T23:07:28.6964879+00:00 | 107.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 918,000 | 2026-07-16T23:07:28.6993525+00:00 | 157.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 924,000 | 2026-07-16T23:07:28.7077122+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 925,000 | 2026-07-16T23:07:28.7084328+00:00 | 157.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 926,000 | 2026-07-16T23:07:28.708994+00:00 | 127.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 928,000 | 2026-07-16T23:07:28.7105029+00:00 | 154.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 934,000 | 2026-07-16T23:07:28.7191961+00:00 | 129.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 935,000 | 2026-07-16T23:07:28.7196614+00:00 | 154.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 936,000 | 2026-07-16T23:07:28.7201403+00:00 | 128.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 938,000 | 2026-07-16T23:07:28.7263616+00:00 | 163.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 945,000 | 2026-07-16T23:07:28.7785417+00:00 | 118.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 948,000 | 2026-07-16T23:07:28.7812119+00:00 | 115.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 955,000 | 2026-07-16T23:07:28.7898122+00:00 | 121.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 958,000 | 2026-07-16T23:07:28.7927919+00:00 | 118.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 965,000 | 2026-07-16T23:07:28.8054049+00:00 | 117.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 968,000 | 2026-07-16T23:07:28.8086804+00:00 | 123.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 975,000 | 2026-07-16T23:07:28.8204479+00:00 | 115.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 978,000 | 2026-07-16T23:07:28.8261251+00:00 | 110.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 985,000 | 2026-07-16T23:07:28.8416636+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 988,000 | 2026-07-16T23:07:28.8513466+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 995,000 | 2026-07-16T23:07:28.8639249+00:00 | 110.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 998,000 | 2026-07-16T23:07:28.8705177+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,005,000 | 2026-07-16T23:07:28.8784157+00:00 | 119.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,008,000 | 2026-07-16T23:07:28.8821808+00:00 | 138.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,015,000 | 2026-07-16T23:07:28.8904996+00:00 | 144.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,018,000 | 2026-07-16T23:07:28.8954542+00:00 | 139.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,024,000 | 2026-07-16T23:07:28.9054951+00:00 | 109.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,025,000 | 2026-07-16T23:07:28.9061072+00:00 | 139.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,026,000 | 2026-07-16T23:07:28.9066809+00:00 | 108.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,034,000 | 2026-07-16T23:07:28.9195857+00:00 | 107.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,036,000 | 2026-07-16T23:07:28.9207953+00:00 | 115.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,044,000 | 2026-07-16T23:07:28.929716+00:00 | 109.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,046,000 | 2026-07-16T23:07:28.9321198+00:00 | 107.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,054,000 | 2026-07-16T23:07:28.9464265+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 566,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 47,000 | 2026-07-16T23:37:40.3326993+00:00 | 121.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 54,000 | 2026-07-16T23:37:40.34087+00:00 | 696.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 56,000 | 2026-07-16T23:37:40.3431339+00:00 | 694.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 57,000 | 2026-07-16T23:37:40.3444607+00:00 | 127.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 64,000 | 2026-07-16T23:37:40.3521337+00:00 | 731.5ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 66,000 | 2026-07-16T23:37:40.3545073+00:00 | 729.1ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 67,000 | 2026-07-16T23:37:40.3557016+00:00 | 147.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 71,000 | 2026-07-16T23:37:40.3606856+00:00 | 311.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 72,000 | 2026-07-16T23:37:40.3621218+00:00 | 309.8ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 74,000 | 2026-07-16T23:37:40.3645828+00:00 | 734.9ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 76,000 | 2026-07-16T23:37:40.3670521+00:00 | 732.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 77,000 | 2026-07-16T23:37:40.3682708+00:00 | 139.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 81,000 | 2026-07-16T23:37:40.3733626+00:00 | 320.1ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 82,000 | 2026-07-16T23:37:40.3744336+00:00 | 319.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 84,000 | 2026-07-16T23:37:40.3775725+00:00 | 747.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 85,000 | 2026-07-16T23:37:40.3786898+00:00 | 105.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 86,000 | 2026-07-16T23:37:40.3798042+00:00 | 745.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 87,000 | 2026-07-16T23:37:40.3816048+00:00 | 148.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 88,000 | 2026-07-16T23:37:40.3837665+00:00 | 120.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 91,000 | 2026-07-16T23:37:40.3874257+00:00 | 317.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 92,000 | 2026-07-16T23:37:40.3884185+00:00 | 316.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 94,000 | 2026-07-16T23:37:40.3910427+00:00 | 744.9ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 95,000 | 2026-07-16T23:37:40.3920958+00:00 | 120.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 96,000 | 2026-07-16T23:37:40.3937023+00:00 | 749.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 97,000 | 2026-07-16T23:37:40.3947645+00:00 | 175.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 98,000 | 2026-07-16T23:37:40.3958282+00:00 | 116.9ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 100,000 | 2026-07-16T23:37:40.399029+00:00 | 103.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 101,000 | 2026-07-16T23:37:40.4003348+00:00 | 364.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 102,000 | 2026-07-16T23:37:40.4018875+00:00 | 363.1ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 103,000 | 2026-07-16T23:37:40.403236+00:00 | 316.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 104,000 | 2026-07-16T23:37:40.4043615+00:00 | 756.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 105,000 | 2026-07-16T23:37:40.4058851+00:00 | 126.8ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 106,000 | 2026-07-16T23:37:40.407177+00:00 | 753.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 107,000 | 2026-07-16T23:37:40.4084433+00:00 | 184.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 108,000 | 2026-07-16T23:37:40.4098061+00:00 | 134.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 109,000 | 2026-07-16T23:37:40.411198+00:00 | 335.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 110,000 | 2026-07-16T23:37:40.4122667+00:00 | 105.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 111,000 | 2026-07-16T23:37:40.4137097+00:00 | 361.5ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 112,000 | 2026-07-16T23:37:40.4150539+00:00 | 360.1ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 113,000 | 2026-07-16T23:37:40.4161605+00:00 | 330.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 114,000 | 2026-07-16T23:37:40.4174025+00:00 | 749.9ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 115,000 | 2026-07-16T23:37:40.4185989+00:00 | 131.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 116,000 | 2026-07-16T23:37:40.4199386+00:00 | 753.9ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 117,000 | 2026-07-16T23:37:40.4211846+00:00 | 203.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 118,000 | 2026-07-16T23:37:40.4224344+00:00 | 127.8ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 119,000 | 2026-07-16T23:37:40.4238781+00:00 | 367.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 120,000 | 2026-07-16T23:37:40.4250156+00:00 | 116.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 121,000 | 2026-07-16T23:37:40.4263057+00:00 | 354.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 122,000 | 2026-07-16T23:37:40.4276137+00:00 | 352.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 123,000 | 2026-07-16T23:37:40.4289333+00:00 | 362.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 124,000 | 2026-07-16T23:37:40.4302113+00:00 | 751.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 125,000 | 2026-07-16T23:37:40.431765+00:00 | 130.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 126,000 | 2026-07-16T23:37:40.4328416+00:00 | 749.1ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 127,000 | 2026-07-16T23:37:40.4341355+00:00 | 234.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 128,000 | 2026-07-16T23:37:40.4352741+00:00 | 127.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 129,000 | 2026-07-16T23:37:40.4371786+00:00 | 379.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 130,000 | 2026-07-16T23:37:40.4385909+00:00 | 129.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 131,000 | 2026-07-16T23:37:40.4407672+00:00 | 347.1ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 132,000 | 2026-07-16T23:37:40.4433607+00:00 | 344.5ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 133,000 | 2026-07-16T23:37:40.4447006+00:00 | 397.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 134,000 | 2026-07-16T23:37:40.4458265+00:00 | 742.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 135,000 | 2026-07-16T23:37:40.447298+00:00 | 122.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 136,000 | 2026-07-16T23:37:40.4485693+00:00 | 752.1ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 137,000 | 2026-07-16T23:37:40.4498192+00:00 | 236.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 138,000 | 2026-07-16T23:37:40.4511675+00:00 | 127.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 139,000 | 2026-07-16T23:37:40.4524229+00:00 | 407.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 140,000 | 2026-07-16T23:37:40.4535142+00:00 | 125.9ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 141,000 | 2026-07-16T23:37:40.4550427+00:00 | 346.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 142,000 | 2026-07-16T23:37:40.4566363+00:00 | 345.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 143,000 | 2026-07-16T23:37:40.4579253+00:00 | 423.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 144,000 | 2026-07-16T23:37:40.4592817+00:00 | 751.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 145,000 | 2026-07-16T23:37:40.4605178+00:00 | 126.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 146,000 | 2026-07-16T23:37:40.4615876+00:00 | 749.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 147,000 | 2026-07-16T23:37:40.4628613+00:00 | 330.5ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 148,000 | 2026-07-16T23:37:40.4643614+00:00 | 122.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 149,000 | 2026-07-16T23:37:40.4657324+00:00 | 428.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 150,000 | 2026-07-16T23:37:40.4667913+00:00 | 132.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 151,000 | 2026-07-16T23:37:40.4682671+00:00 | 343.1ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 152,000 | 2026-07-16T23:37:40.4694484+00:00 | 341.9ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 153,000 | 2026-07-16T23:37:40.4707487+00:00 | 423.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 154,000 | 2026-07-16T23:37:40.4719973+00:00 | 748.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 155,000 | 2026-07-16T23:37:40.4734168+00:00 | 121.9ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 156,000 | 2026-07-16T23:37:40.4744863+00:00 | 767.9ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 157,000 | 2026-07-16T23:37:40.4757668+00:00 | 328.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 158,000 | 2026-07-16T23:37:40.477055+00:00 | 125.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 159,000 | 2026-07-16T23:37:40.4783614+00:00 | 427.9ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 160,000 | 2026-07-16T23:37:40.479734+00:00 | 148.5ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 161,000 | 2026-07-16T23:37:40.4813308+00:00 | 347.5ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 162,000 | 2026-07-16T23:37:40.4825501+00:00 | 346.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 163,000 | 2026-07-16T23:37:40.4837949+00:00 | 426.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 164,000 | 2026-07-16T23:37:40.4853937+00:00 | 785.7ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 165,000 | 2026-07-16T23:37:40.4867783+00:00 | 121.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 166,000 | 2026-07-16T23:37:40.488133+00:00 | 782.9ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 167,000 | 2026-07-16T23:37:40.4894879+00:00 | 329.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 168,000 | 2026-07-16T23:37:40.4908371+00:00 | 117.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 169,000 | 2026-07-16T23:37:40.4920211+00:00 | 424.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 170,000 | 2026-07-16T23:37:40.4934927+00:00 | 151.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 171,000 | 2026-07-16T23:37:40.4948059+00:00 | 357.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 172,000 | 2026-07-16T23:37:40.4960896+00:00 | 355.8ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 173,000 | 2026-07-16T23:37:40.4973631+00:00 | 426.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 174,000 | 2026-07-16T23:37:40.4988941+00:00 | 790.6ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 175,000 | 2026-07-16T23:37:40.5001084+00:00 | 112.5ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 176,000 | 2026-07-16T23:37:40.5016275+00:00 | 787.9ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 177,000 | 2026-07-16T23:37:40.5030913+00:00 | 331.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 178,000 | 2026-07-16T23:37:40.5044644+00:00 | 115.1ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 179,000 | 2026-07-16T23:37:40.5064803+00:00 | 433.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 180,000 | 2026-07-16T23:37:40.5075857+00:00 | 157.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 181,000 | 2026-07-16T23:37:40.5088693+00:00 | 372.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 182,000 | 2026-07-16T23:37:40.5101126+00:00 | 371.1ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 183,000 | 2026-07-16T23:37:40.5115918+00:00 | 428.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 184,000 | 2026-07-16T23:37:40.51284+00:00 | 790.1ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 185,000 | 2026-07-16T23:37:40.5140964+00:00 | 112.5ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 186,000 | 2026-07-16T23:37:40.5152572+00:00 | 787.7ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 187,000 | 2026-07-16T23:37:40.5165873+00:00 | 321.8ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 188,000 | 2026-07-16T23:37:40.5179329+00:00 | 108.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 189,000 | 2026-07-16T23:37:40.5194166+00:00 | 427.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 190,000 | 2026-07-16T23:37:40.5208659+00:00 | 164.1ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 191,000 | 2026-07-16T23:37:40.5221733+00:00 | 386.8ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 192,000 | 2026-07-16T23:37:40.5232951+00:00 | 385.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 193,000 | 2026-07-16T23:37:40.5244222+00:00 | 433.8ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 194,000 | 2026-07-16T23:37:40.5257227+00:00 | 791.9ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 195,000 | 2026-07-16T23:37:40.5273192+00:00 | 109.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 196,000 | 2026-07-16T23:37:40.5290613+00:00 | 788.6ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 197,000 | 2026-07-16T23:37:40.5304769+00:00 | 347.5ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 198,000 | 2026-07-16T23:37:40.531606+00:00 | 111.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 199,000 | 2026-07-16T23:37:40.5327562+00:00 | 432.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 200,000 | 2026-07-16T23:37:40.5340905+00:00 | 155.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 201,000 | 2026-07-16T23:37:40.5357303+00:00 | 404.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 202,000 | 2026-07-16T23:37:40.5373884+00:00 | 402.9ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 203,000 | 2026-07-16T23:37:40.5387942+00:00 | 430.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 204,000 | 2026-07-16T23:37:40.5400584+00:00 | 785.8ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 205,000 | 2026-07-16T23:37:40.5412764+00:00 | 121.9ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 206,000 | 2026-07-16T23:37:40.542489+00:00 | 783.4ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 207,000 | 2026-07-16T23:37:40.5442023+00:00 | 366.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 208,000 | 2026-07-16T23:37:40.5453399+00:00 | 117.9ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 209,000 | 2026-07-16T23:37:40.547005+00:00 | 427.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 210,000 | 2026-07-16T23:37:40.5481015+00:00 | 149.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 211,000 | 2026-07-16T23:37:40.5492055+00:00 | 414.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 212,000 | 2026-07-16T23:37:40.5503676+00:00 | 413.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 213,000 | 2026-07-16T23:37:40.5521365+00:00 | 422.5ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 214,000 | 2026-07-16T23:37:40.5532768+00:00 | 787.6ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 215,000 | 2026-07-16T23:37:40.5550098+00:00 | 130.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 216,000 | 2026-07-16T23:37:40.55627+00:00 | 784.6ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 217,000 | 2026-07-16T23:37:40.5574536+00:00 | 359.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 218,000 | 2026-07-16T23:37:40.5586137+00:00 | 126.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 219,000 | 2026-07-16T23:37:40.5603209+00:00 | 422.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 220,000 | 2026-07-16T23:37:40.5614644+00:00 | 157.9ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 221,000 | 2026-07-16T23:37:40.5632256+00:00 | 461.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 222,000 | 2026-07-16T23:37:40.564376+00:00 | 459.9ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 223,000 | 2026-07-16T23:37:40.5655196+00:00 | 420.1ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 224,000 | 2026-07-16T23:37:40.5666789+00:00 | 781.9ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 225,000 | 2026-07-16T23:37:40.568185+00:00 | 130.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 226,000 | 2026-07-16T23:37:40.5717924+00:00 | 785.5ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 227,000 | 2026-07-16T23:37:40.573267+00:00 | 363.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 228,000 | 2026-07-16T23:37:40.5746812+00:00 | 132.1ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 229,000 | 2026-07-16T23:37:40.5934599+00:00 | 401.5ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 230,000 | 2026-07-16T23:37:40.594561+00:00 | 159.8ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 231,000 | 2026-07-16T23:37:40.5960677+00:00 | 489.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 232,000 | 2026-07-16T23:37:40.6059868+00:00 | 479.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 233,000 | 2026-07-16T23:37:40.6076442+00:00 | 392.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 234,000 | 2026-07-16T23:37:40.6088005+00:00 | 771.5ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 235,000 | 2026-07-16T23:37:40.6251767+00:00 | 117.5ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 236,000 | 2026-07-16T23:37:40.6262494+00:00 | 754.0ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 237,000 | 2026-07-16T23:37:40.6273924+00:00 | 316.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 239,000 | 2026-07-16T23:37:40.647834+00:00 | 357.1ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 240,000 | 2026-07-16T23:37:40.6487137+00:00 | 151.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 241,000 | 2026-07-16T23:37:40.6685435+00:00 | 426.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 242,000 | 2026-07-16T23:37:40.6694772+00:00 | 425.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 243,000 | 2026-07-16T23:37:40.6705912+00:00 | 334.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 244,000 | 2026-07-16T23:37:40.6716992+00:00 | 716.8ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 246,000 | 2026-07-16T23:37:40.6886481+00:00 | 711.0ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 247,000 | 2026-07-16T23:37:40.689798+00:00 | 268.9ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 249,000 | 2026-07-16T23:37:40.7359388+00:00 | 272.9ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 250,000 | 2026-07-16T23:37:40.7372186+00:00 | 103.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 251,000 | 2026-07-16T23:37:40.7945335+00:00 | 331.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 252,000 | 2026-07-16T23:37:40.7956704+00:00 | 330.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 253,000 | 2026-07-16T23:37:40.7965685+00:00 | 222.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 254,000 | 2026-07-16T23:37:40.8042765+00:00 | 610.5ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 256,000 | 2026-07-16T23:37:40.8066261+00:00 | 608.2ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 257,000 | 2026-07-16T23:37:40.8134468+00:00 | 179.1ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 259,000 | 2026-07-16T23:37:40.8156083+00:00 | 212.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 261,000 | 2026-07-16T23:37:40.8204682+00:00 | 362.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 262,000 | 2026-07-16T23:37:40.8230159+00:00 | 360.1ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 263,000 | 2026-07-16T23:37:40.8297143+00:00 | 199.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 264,000 | 2026-07-16T23:37:40.8307056+00:00 | 601.4ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 266,000 | 2026-07-16T23:37:40.8338016+00:00 | 606.5ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 267,000 | 2026-07-16T23:37:40.8353798+00:00 | 165.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 269,000 | 2026-07-16T23:37:40.837786+00:00 | 199.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 271,000 | 2026-07-16T23:37:40.8399474+00:00 | 350.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 272,000 | 2026-07-16T23:37:40.8409296+00:00 | 349.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 273,000 | 2026-07-16T23:37:40.8537602+00:00 | 183.5ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 274,000 | 2026-07-16T23:37:40.8548515+00:00 | 591.8ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 276,000 | 2026-07-16T23:37:40.8784992+00:00 | 568.1ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 277,000 | 2026-07-16T23:37:40.879368+00:00 | 137.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 279,000 | 2026-07-16T23:37:40.9040606+00:00 | 135.9ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 281,000 | 2026-07-16T23:37:40.9063515+00:00 | 309.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 282,000 | 2026-07-16T23:37:40.9105136+00:00 | 305.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 283,000 | 2026-07-16T23:37:40.9114414+00:00 | 150.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 284,000 | 2026-07-16T23:37:40.912505+00:00 | 553.8ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 286,000 | 2026-07-16T23:37:40.9179084+00:00 | 548.4ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 287,000 | 2026-07-16T23:37:40.9189904+00:00 | 127.8ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 289,000 | 2026-07-16T23:37:40.9288041+00:00 | 149.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 291,000 | 2026-07-16T23:37:40.9309358+00:00 | 330.2ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 292,000 | 2026-07-16T23:37:40.9379508+00:00 | 323.2ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 293,000 | 2026-07-16T23:37:40.9391109+00:00 | 177.7ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 294,000 | 2026-07-16T23:37:40.9401262+00:00 | 538.1ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 296,000 | 2026-07-16T23:37:40.9449507+00:00 | 533.3ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 297,000 | 2026-07-16T23:37:40.9461595+00:00 | 119.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 299,000 | 2026-07-16T23:37:40.952524+00:00 | 178.6ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 301,000 | 2026-07-16T23:37:40.9587897+00:00 | 319.3ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 302,000 | 2026-07-16T23:37:40.9597397+00:00 | 318.4ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 303,000 | 2026-07-16T23:37:40.9607545+00:00 | 170.4ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 304,000 | 2026-07-16T23:37:40.9624763+00:00 | 539.9ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 306,000 | 2026-07-16T23:37:40.9762291+00:00 | 526.1ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 307,000 | 2026-07-16T23:37:40.9771122+00:00 | 132.3ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 309,000 | 2026-07-16T23:37:40.995211+00:00 | 148.5ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 311,000 | 2026-07-16T23:37:41.0022041+00:00 | 291.3ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 312,000 | 2026-07-16T23:37:41.0033185+00:00 | 290.2ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 313,000 | 2026-07-16T23:37:41.005235+00:00 | 151.1ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 314,000 | 2026-07-16T23:37:41.0117615+00:00 | 501.4ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 316,000 | 2026-07-16T23:37:41.0150434+00:00 | 498.1ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 317,000 | 2026-07-16T23:37:41.0190395+00:00 | 100.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 319,000 | 2026-07-16T23:37:41.0221353+00:00 | 149.5ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 321,000 | 2026-07-16T23:37:41.0263065+00:00 | 286.6ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 322,000 | 2026-07-16T23:37:41.0293678+00:00 | 299.1ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 323,000 | 2026-07-16T23:37:41.0332317+00:00 | 172.2ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 324,000 | 2026-07-16T23:37:41.0348852+00:00 | 494.0ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 326,000 | 2026-07-16T23:37:41.0383242+00:00 | 490.6ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 329,000 | 2026-07-16T23:37:41.0512146+00:00 | 176.8ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 331,000 | 2026-07-16T23:37:41.0620854+00:00 | 276.3ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 332,000 | 2026-07-16T23:37:41.0638856+00:00 | 274.5ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 333,000 | 2026-07-16T23:37:41.0670324+00:00 | 161.0ms | GC pause | - | - | 1.0s / 420,083 msg/s | Gen2 +3 / pause +1.7ms |
| Dekaf (3conn) | 334,000 | 2026-07-16T23:37:41.0691166+00:00 | 471.5ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 336,000 | 2026-07-16T23:37:41.0827824+00:00 | 463.5ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 339,000 | 2026-07-16T23:37:41.0924157+00:00 | 151.4ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 341,000 | 2026-07-16T23:37:41.0967176+00:00 | 340.0ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 342,000 | 2026-07-16T23:37:41.0996665+00:00 | 337.0ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 343,000 | 2026-07-16T23:37:41.1010582+00:00 | 161.4ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 349,000 | 2026-07-16T23:37:41.1177322+00:00 | 152.7ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 351,000 | 2026-07-16T23:37:41.119723+00:00 | 343.2ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 352,000 | 2026-07-16T23:37:41.1208013+00:00 | 356.9ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 361,000 | 2026-07-16T23:37:41.136059+00:00 | 360.2ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 362,000 | 2026-07-16T23:37:41.136806+00:00 | 359.4ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 371,000 | 2026-07-16T23:37:41.1504656+00:00 | 365.4ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 372,000 | 2026-07-16T23:37:41.1514429+00:00 | 364.4ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 380,000 | 2026-07-16T23:37:41.1669781+00:00 | 109.7ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 381,000 | 2026-07-16T23:37:41.1678351+00:00 | 361.1ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 390,000 | 2026-07-16T23:37:41.1806825+00:00 | 110.7ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 400,000 | 2026-07-16T23:37:41.1953827+00:00 | 101.0ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 405,000 | 2026-07-16T23:37:41.2015984+00:00 | 105.0ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 408,000 | 2026-07-16T23:37:41.2045163+00:00 | 102.0ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 410,000 | 2026-07-16T23:37:41.2119694+00:00 | 122.1ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 415,000 | 2026-07-16T23:37:41.2204431+00:00 | 104.5ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 418,000 | 2026-07-16T23:37:41.2250908+00:00 | 110.3ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 420,000 | 2026-07-16T23:37:41.2261143+00:00 | 122.3ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +4 / pause +2.4ms |
| Dekaf (3conn) | 500,000 | 2026-07-16T23:37:41.3675131+00:00 | 109.2ms | GC pause | - | - | 2.0s / 587,944 msg/s | Gen2 +1 / pause +0.6ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*71,164 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.61x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers; comparison throughput is 1.25x.
:::

## Producer → Consumer Round-Trip Steady State Throughput (15 minutes, 128B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Confluent | 1.65 | - | 123,286 | 1,657,166 | +22.7% | +160.73% | 15.05 | 123,286 | 0 | 0.20 |
| Dekaf | 1.28 | 3399.22 | 901,433 | 1,422,544 | +49.7% | +316.26% | 110.04 | 901,433 | 0 | 1.15 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer → Consumer Round-Trip Steady State

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 7,451 | 499.00 | 505.92 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer → Consumer Round-Trip Steady State

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-16T23:07:37.5391079+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 356,622 msg/s |
| Dekaf | 2026-07-16T23:07:38.5387929+00:00 | 1 | 16.0 MiB / 0.4 MiB | 65.4 MB/s | 0/0 | 0 | 1.0s / 356,622 msg/s |
| Dekaf | 2026-07-16T23:07:39.5393542+00:00 | 1 | 16.0 MiB / 2.7 MiB | 65.4 MB/s | 0/0 | 0 | 2.0s / 917,392 msg/s |
| Dekaf | 2026-07-16T23:07:40.5420748+00:00 | 1 | 16.0 MiB / 2.9 MiB | 278.8 MB/s | 0/0 | 0 | 3.0s / 1,488,943 msg/s |
| Dekaf | 2026-07-16T23:07:41.5435845+00:00 | 1 | 16.0 MiB / 1.9 MiB | 323.1 MB/s | 0/0 | 0 | 4.0s / 1,286,206 msg/s |
| Dekaf | 2026-07-16T23:07:42.5534995+00:00 | 1 | 16.0 MiB / 5.8 MiB | 323.1 MB/s | 0/0 | 0 | 5.0s / 1,442,104 msg/s |
| Dekaf | 2026-07-16T23:07:43.5557596+00:00 | 1 | 16.0 MiB / 2.9 MiB | 323.1 MB/s | 0/0 | 0 | 6.0s / 1,505,222 msg/s |
| Dekaf | 2026-07-16T23:07:44.5554204+00:00 | 1 | 16.0 MiB / 1.2 MiB | 323.1 MB/s | 0/0 | 0 | 7.0s / 1,134,543 msg/s |
| Dekaf | 2026-07-16T23:07:45.5560178+00:00 | 1 | 16.0 MiB / 1.6 MiB | 323.1 MB/s | 0/0 | 0 | 8.0s / 1,435,990 msg/s |
| Dekaf | 2026-07-16T23:07:46.5590725+00:00 | 1 | 16.0 MiB / 1.2 MiB | 323.1 MB/s | 0/0 | 0 | 9.0s / 1,409,288 msg/s |
| Dekaf | 2026-07-16T23:07:47.559107+00:00 | 1 | 16.0 MiB / 1.8 MiB | 323.1 MB/s | 0/0 | 0 | 10.0s / 1,368,555 msg/s |
| Dekaf | 2026-07-16T23:07:48.5608257+00:00 | 1 | 16.0 MiB / 1.7 MiB | 323.1 MB/s | 0/0 | 0 | 11.0s / 1,391,464 msg/s |
| Dekaf | 2026-07-16T23:07:49.5616029+00:00 | 1 | 16.0 MiB / 1.5 MiB | 331.0 MB/s | 0/0 | 0 | 12.0s / 1,435,800 msg/s |
| Dekaf | 2026-07-16T23:07:50.5628203+00:00 | 1 | 16.0 MiB / 0.9 MiB | 331.0 MB/s | 0/0 | 0 | 13.0s / 1,452,697 msg/s |
| Dekaf | 2026-07-16T23:07:51.5651603+00:00 | 1 | 16.0 MiB / 2.2 MiB | 331.0 MB/s | 0/0 | 0 | 14.0s / 1,780,734 msg/s |
| Dekaf | 2026-07-16T23:07:52.5690988+00:00 | 1 | 16.0 MiB / 2.1 MiB | 369.8 MB/s | 0/0 | 0 | 14.0s / 1,780,734 msg/s |

### Round-Trip Validation

| Client | Expected | Consumed | Missing | Duplicates | Corrupt | Out of Order | Wrong Partition | Unexpected | Timed Out | Result |
|--------|----------|----------|---------|------------|---------|--------------|-----------------|------------|-----------|--------|
| Confluent | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |
| Dekaf | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |

:::tip
**Dekaf uses 1.29x less CPU per message** than Confluent.Kafka for producer → consumer round-trip steady state; comparison throughput is 0.86x.
:::

## Producer (Transactional EOS), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 466.69 | 466.68 | 268 | 358 | +2.0% | +0.19% | 0.26 | 357 | 0 | 0.17 |
| Confluent | 302.83 | - | 129 | 172 | +0.3% | +0.03% | 0.12 | 173 | 0 | 0.05 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Transactional EOS), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 107,161 | 119.05 | 1.20 KB |
| Dekaf | 2 | 107,050 | 118.93 | 1.20 KB |
| Dekaf | 3 | 106,895 | 118.76 | 1.20 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Transactional EOS), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-16T23:07:41.2240538+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 200 msg/s |
| Dekaf | 2026-07-16T23:07:50.2318569+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.1 MB/s | 0/0 | 0 | 9.0s / 324 msg/s |
| Dekaf | 2026-07-16T23:07:59.2525402+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 18.0s / 333 msg/s |
| Dekaf | 2026-07-16T23:08:09.2568747+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 28.0s / 354 msg/s |
| Dekaf | 2026-07-16T23:08:18.260384+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 37.0s / 336 msg/s |
| Dekaf | 2026-07-16T23:08:27.2650821+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 46.0s / 362 msg/s |
| Dekaf | 2026-07-16T23:08:36.2774208+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 55.0s / 360 msg/s |
| Dekaf | 2026-07-16T23:08:45.2820911+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.1 MB/s | 0/0 | 0 | 64.0s / 345 msg/s |
| Dekaf | 2026-07-16T23:08:54.2883802+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 73.0s / 350 msg/s |
| Dekaf | 2026-07-16T23:09:03.3137531+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 82.0s / 357 msg/s |
| Dekaf | 2026-07-16T23:09:12.3215929+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 91.0s / 355 msg/s |
| Dekaf | 2026-07-16T23:09:21.3376314+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 100.0s / 358 msg/s |
| Dekaf | 2026-07-16T23:09:30.3553308+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 109.0s / 362 msg/s |
| Dekaf | 2026-07-16T23:09:39.3723767+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 118.0s / 362 msg/s |
| Dekaf | 2026-07-16T23:09:48.3970601+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 127.0s / 356 msg/s |
| Dekaf | 2026-07-16T23:09:58.4196421+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 137.0s / 359 msg/s |
| Dekaf | 2026-07-16T23:10:07.4295328+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 146.0s / 355 msg/s |
| Dekaf | 2026-07-16T23:10:16.4379051+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 155.0s / 357 msg/s |
| Dekaf | 2026-07-16T23:10:25.449973+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 164.0s / 369 msg/s |
| Dekaf | 2026-07-16T23:10:34.4540879+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 173.0s / 357 msg/s |
| Dekaf | 2026-07-16T23:10:43.4719606+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 182.0s / 366 msg/s |
| Dekaf | 2026-07-16T23:10:52.4943617+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 191.0s / 359 msg/s |
| Dekaf | 2026-07-16T23:11:01.5298157+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 200.0s / 367 msg/s |
| Dekaf | 2026-07-16T23:11:10.5330625+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 209.0s / 359 msg/s |
| Dekaf | 2026-07-16T23:11:19.5482379+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 218.0s / 368 msg/s |
| Dekaf | 2026-07-16T23:11:28.5531084+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 227.0s / 348 msg/s |
| Dekaf | 2026-07-16T23:11:37.5612524+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 236.0s / 349 msg/s |
| Dekaf | 2026-07-16T23:11:47.5656044+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 246.0s / 353 msg/s |
| Dekaf | 2026-07-16T23:11:56.5876806+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 255.0s / 349 msg/s |
| Dekaf | 2026-07-16T23:12:05.6078061+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 264.0s / 360 msg/s |
| Dekaf | 2026-07-16T23:12:14.6104834+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 273.0s / 354 msg/s |
| Dekaf | 2026-07-16T23:12:23.6227552+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 282.0s / 354 msg/s |
| Dekaf | 2026-07-16T23:12:32.6261463+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 291.0s / 366 msg/s |
| Dekaf | 2026-07-16T23:12:41.6509099+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 300.0s / 359 msg/s |
| Dekaf | 2026-07-16T23:12:50.6538628+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 309.0s / 368 msg/s |
| Dekaf | 2026-07-16T23:12:59.6553957+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 318.0s / 357 msg/s |
| Dekaf | 2026-07-16T23:13:08.6603445+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 327.0s / 364 msg/s |
| Dekaf | 2026-07-16T23:13:17.6652314+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 336.0s / 358 msg/s |
| Dekaf | 2026-07-16T23:13:26.6901658+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 345.0s / 360 msg/s |
| Dekaf | 2026-07-16T23:13:35.6944569+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 354.0s / 356 msg/s |
| Dekaf | 2026-07-16T23:13:45.7069943+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 364.0s / 366 msg/s |
| Dekaf | 2026-07-16T23:13:54.7269781+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 373.0s / 363 msg/s |
| Dekaf | 2026-07-16T23:14:03.7458281+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 382.0s / 368 msg/s |
| Dekaf | 2026-07-16T23:14:12.7526221+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 391.1s / 350 msg/s |
| Dekaf | 2026-07-16T23:14:21.7594235+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 400.1s / 354 msg/s |
| Dekaf | 2026-07-16T23:14:30.766699+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 409.1s / 357 msg/s |
| Dekaf | 2026-07-16T23:14:39.7694589+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 418.1s / 350 msg/s |
| Dekaf | 2026-07-16T23:14:48.7853449+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 427.1s / 355 msg/s |
| Dekaf | 2026-07-16T23:14:57.7920593+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 436.1s / 367 msg/s |
| Dekaf | 2026-07-16T23:15:06.8086124+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 445.1s / 358 msg/s |
| Dekaf | 2026-07-16T23:15:15.8214475+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 454.1s / 358 msg/s |
| Dekaf | 2026-07-16T23:15:24.8269771+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 463.1s / 370 msg/s |
| Dekaf | 2026-07-16T23:15:34.8316989+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 473.1s / 359 msg/s |
| Dekaf | 2026-07-16T23:15:43.8327082+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 482.1s / 369 msg/s |
| Dekaf | 2026-07-16T23:15:52.8379766+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 491.1s / 359 msg/s |
| Dekaf | 2026-07-16T23:16:01.8554707+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 500.1s / 365 msg/s |
| Dekaf | 2026-07-16T23:16:10.8571579+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 509.1s / 357 msg/s |
| Dekaf | 2026-07-16T23:16:19.8621158+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 518.1s / 367 msg/s |
| Dekaf | 2026-07-16T23:16:28.8758189+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 527.1s / 361 msg/s |
| Dekaf | 2026-07-16T23:16:37.8991734+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 536.1s / 360 msg/s |
| Dekaf | 2026-07-16T23:16:46.9021597+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 545.1s / 363 msg/s |
| Dekaf | 2026-07-16T23:16:55.9254186+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 554.1s / 343 msg/s |
| Dekaf | 2026-07-16T23:17:04.9356643+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 563.1s / 355 msg/s |
| Dekaf | 2026-07-16T23:17:13.9463063+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 572.1s / 349 msg/s |
| Dekaf | 2026-07-16T23:17:23.9517987+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 582.1s / 360 msg/s |
| Dekaf | 2026-07-16T23:17:32.9708164+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 592.1s / 352 msg/s |
| Dekaf | 2026-07-16T23:17:41.9723844+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 601.1s / 355 msg/s |
| Dekaf | 2026-07-16T23:17:50.9771427+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 610.1s / 365 msg/s |
| Dekaf | 2026-07-16T23:17:59.9802745+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 619.1s / 360 msg/s |
| Dekaf | 2026-07-16T23:18:08.9855751+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 628.1s / 367 msg/s |
| Dekaf | 2026-07-16T23:18:17.9891399+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 637.1s / 358 msg/s |
| Dekaf | 2026-07-16T23:18:27.0119144+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 646.1s / 365 msg/s |
| Dekaf | 2026-07-16T23:18:36.0144157+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 655.1s / 358 msg/s |
| Dekaf | 2026-07-16T23:18:45.0194558+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 664.1s / 372 msg/s |
| Dekaf | 2026-07-16T23:18:54.0297586+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 673.1s / 360 msg/s |
| Dekaf | 2026-07-16T23:19:03.0353822+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 682.1s / 360 msg/s |
| Dekaf | 2026-07-16T23:19:12.0394138+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 691.1s / 370 msg/s |
| Dekaf | 2026-07-16T23:19:22.0524406+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 701.1s / 358 msg/s |
| Dekaf | 2026-07-16T23:19:31.0704087+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 710.1s / 359 msg/s |
| Dekaf | 2026-07-16T23:19:40.0786147+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 719.1s / 348 msg/s |
| Dekaf | 2026-07-16T23:19:49.0844447+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 728.1s / 352 msg/s |
| Dekaf | 2026-07-16T23:19:58.0918469+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 737.1s / 348 msg/s |
| Dekaf | 2026-07-16T23:20:07.1173804+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 746.1s / 348 msg/s |
| Dekaf | 2026-07-16T23:20:16.1226194+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 755.1s / 353 msg/s |
| Dekaf | 2026-07-16T23:20:25.1381123+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 764.1s / 352 msg/s |
| Dekaf | 2026-07-16T23:20:34.1435912+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 773.1s / 371 msg/s |
| Dekaf | 2026-07-16T23:20:43.1655393+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 782.1s / 359 msg/s |
| Dekaf | 2026-07-16T23:20:52.187787+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 791.1s / 365 msg/s |
| Dekaf | 2026-07-16T23:21:01.2043045+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 800.1s / 358 msg/s |
| Dekaf | 2026-07-16T23:21:11.2102557+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 810.1s / 368 msg/s |
| Dekaf | 2026-07-16T23:21:20.2123035+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 819.1s / 362 msg/s |
| Dekaf | 2026-07-16T23:21:29.2305672+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 828.1s / 366 msg/s |
| Dekaf | 2026-07-16T23:21:38.251904+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 837.1s / 357 msg/s |
| Dekaf | 2026-07-16T23:21:47.2627921+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 846.1s / 360 msg/s |
| Dekaf | 2026-07-16T23:21:56.2752599+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 855.1s / 365 msg/s |
| Dekaf | 2026-07-16T23:22:05.2798999+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 864.1s / 359 msg/s |
| Dekaf | 2026-07-16T23:22:14.2848093+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 873.1s / 366 msg/s |
| Dekaf | 2026-07-16T23:22:23.2929654+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 882.1s / 344 msg/s |
| Dekaf | 2026-07-16T23:22:32.3468873+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 891.1s / 355 msg/s |
| Dekaf | 2026-07-16T23:22:41.3503172+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 899.1s / 355 msg/s |
*2,598 budget sample(s) omitted; rows sampled across the full timeline.*

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 155,300 | 116,500 | 38,800 | 116,500 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 321,100 | 240,900 | 80,200 | 240,900 | 0 | 0 | 0 | 0 | 0 | PASS |

:::note
Confluent.Kafka uses 1.54x less CPU per message for producer (transactional eos), 3 brokers; comparison throughput is 2.08x.
:::

## Consumer Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.68 | - | 1,956,116 | 1,981,253 | -0.8% | -0.01% | 1865.50 | - | 0 | 1.34 |
| Confluent | 1.05 | - | 1,376,214 | 1,373,009 | +1.2% | +0.12% | 1312.46 | - | 0 | 1.44 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

:::tip
**Dekaf uses 1.53x less CPU per message** than Confluent.Kafka for consumer; comparison throughput is 1.44x.
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.67 | - | 1,997,801 | 2,018,660 | -0.4% | -0.05% | 1905.25 | - | 0 | 1.33 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.44 | - | 3,560,189 | 3,589,173 | +10.3% | +0.88% | 3395.26 | - | 0 | 1.57 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.36 | - | 4,125,838 | 4,089,475 | -0.1% | +0.06% | 3934.71 | - | 0 | 1.49 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 24320 | 133 | 0 | 2814.62 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 296220 | 1 | 1 | 1508.88 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 320228 | 21 | 1 | 1532.04 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 214703 | 22 | 1 | 1011.22 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 259371 | 11 | 1 | 1274.83 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 251902 | 1 | 1 | 1245.04 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 206463 | 1 | 1 | 965.54 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 311141 | 1 | 1 | 1550.69 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 319832 | 3 | 1 | 1557.91 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 202631 | 13 | 1 | 949.00 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 5009 | 2 | 2 | 17.71 GB | 961 B |
| Confluent | Producer (Transactional EOS), 3 Brokers | 93 | 1 | 1 | 133.81 MB | 903 B |
| Dekaf | Consumer | 29609 | 1558 | 552 | 3322.64 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 87722 | 1618 | 440 | 3393.96 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 3 | 1 | 0 | 496.38 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 15 | 1 | 0 | 992.42 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 453 | 2 | 2 | 127.98 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 466 | 2 | 2 | 1.77 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 270 | 3 | 2 | 1.06 GB | 1 B |
| Dekaf | Producer (Acks All) | 458 | 2 | 2 | 1.88 GB | 2 B |
| Dekaf | Producer (Acks All) | 429 | 2 | 2 | 115.91 MB | 0 B |
| Dekaf | Producer (Acks All), 3 Brokers | 234 | 4 | 4 | 963.31 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 450 | 2 | 2 | 171.53 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 464 | 2 | 2 | 1.74 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 229 | 2 | 2 | 973.97 MB | 1 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 1210 | 6 | 1 | 5.64 GB | 306 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 45 | 2 | 1 | 151.63 MB | 495 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 389 | 5 | 3 | 1.54 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 295 | 8 | 4 | 1.30 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 495 | 4 | 3 | 1.69 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 286 | 6 | 5 | 1.38 GB | 1 B |

*Confluent.Kafka uses native librdkafka; .NET GC allocation counters exclude unmanaged allocations.*

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **CPU Isolation**: Brokers are pinned to dedicated cores and the client under test to its own cores, so the client — not the broker — is the measured bottleneck
- **RAM-backed Broker Logs**: Kafka log dirs are mounted on tmpfs so disk I/O never caps broker ingestion
- **Delivered Throughput**: producer tables report broker-confirmed throughput, measured as the end-offset delta across all partitions — not the client-side append rate, which can run far ahead of what the broker ever accepts
- **Median Interval Throughput**: table order and comparison ratios use median sampled client-side msg/s when available, which is less sensitive to short late-run stalls than the whole-run mean
- **Same-VM Pairing**: comparable Dekaf and Confluent scenarios run sequentially inside one job/VM; 1-broker producer acceptance lanes run twice in opposite client orders and publish a geometric-mean aggregate, while other lanes alternate order by workflow run number
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
