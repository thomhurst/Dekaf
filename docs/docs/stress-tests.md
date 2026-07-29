---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-29 11:07 UTC

:::info
The paired Dekaf vs Confluent comparison runs weekly (Sunday 2 AM UTC) and updates this page. 
Manual dispatches stay Dekaf-only unless full_run explicitly requests the same paired publish path. 
Tests measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,297,955 | 1,287,037–1,308,966 | 1.05 | 1.26x |
| Confluent | 2 | 1,033,120 | 983,513–1,085,228 | 1.73 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (confluent-first) | 1.04 | 1040.72 | 1,268,062 | 1,308,966 | -7.0% | -0.67% | 1209.32 | 1,268,062 | 0 | 1.31 |
| Dekaf (dekaf-first) | 1.07 | 1082.85 | 1,263,443 | 1,287,037 | +21.4% | +1.87% | 1204.91 | 1,263,443 | 0 | 1.35 |
| Dekaf (3conn) | 0.89 | 785.28 | 1,301,440 | 1,283,873 | +0.0% | +0.10% | 1241.15 | 1,301,440 | 0 | 1.15 |
| Confluent (confluent-first) | 1.85 | - | 934,260 | 1,085,228 | -35.3% | -3.42% | 890.98 | 934,260 | 0 | 1.73 |
| Confluent (dekaf-first) | 1.62 | - | 1,028,441 | 983,513 | +45.2% | +3.85% | 980.80 | 1,028,441 | 0 | 1.66 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,136,582 | 1262.82 | 998.18 KB |
| Dekaf | 1 | 1,121,540 | 1246.14 | 1007.86 KB |
| Dekaf (3conn) | 1 | 1,320,629 | 1467.35 | 881.66 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-29T09:51:21.9540045+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 414,493 msg/s |
| Dekaf | 2026-07-29T09:51:48.9643283+00:00 | 1 | 16.0 MiB / 15.9 MiB | 1228.4 MB/s | 0/0 | 18,758 | 27.0s / 1,130,610 msg/s |
| Dekaf | 2026-07-29T09:52:16.9711584+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1330.8 MB/s | 1/0 | 50,256 | 55.0s / 979,225 msg/s |
| Dekaf | 2026-07-29T09:52:43.9758877+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1330.8 MB/s | 2/0 | 80,200 | 82.0s / 1,056,344 msg/s |
| Dekaf | 2026-07-29T09:53:10.9834737+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1486.2 MB/s | 2/1 | 111,506 | 109.0s / 1,196,131 msg/s |
| Dekaf | 2026-07-29T09:53:37.9864966+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1594.5 MB/s | 2/1 | 147,861 | 136.0s / 917,667 msg/s |
| Dekaf | 2026-07-29T09:54:05.9937246+00:00 | 1 | 12.0 MiB / 1.4 MiB | 1594.5 MB/s | 2/1 | 182,248 | 164.0s / 886,962 msg/s |
| Dekaf | 2026-07-29T09:54:33.0010591+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1594.5 MB/s | 3/1 | 215,314 | 191.1s / 1,067,974 msg/s |
| Dekaf | 2026-07-29T09:55:00.0046235+00:00 | 1 | 13.0 MiB / 4.9 MiB | 1594.5 MB/s | 3/1 | 245,413 | 218.1s / 1,018,247 msg/s |
| Dekaf | 2026-07-29T09:55:27.0078761+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1594.5 MB/s | 4/1 | 275,844 | 245.1s / 1,081,600 msg/s |
| Dekaf | 2026-07-29T09:55:55.0134221+00:00 | 1 | 15.0 MiB / 14.9 MiB | 1594.5 MB/s | 4/1 | 308,026 | 273.1s / 1,232,489 msg/s |
| Dekaf | 2026-07-29T09:56:22.0194805+00:00 | 1 | 15.0 MiB / 11.3 MiB | 1594.5 MB/s | 5/1 | 325,514 | 300.1s / 1,009,566 msg/s |
| Dekaf | 2026-07-29T09:56:49.0312254+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1594.5 MB/s | 6/1 | 367,709 | 327.1s / 1,353,013 msg/s |
| Dekaf | 2026-07-29T09:57:16.040875+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1594.5 MB/s | 6/1 | 410,720 | 354.1s / 1,313,728 msg/s |
| Dekaf | 2026-07-29T09:57:44.0495982+00:00 | 1 | 13.0 MiB / 11.7 MiB | 1594.5 MB/s | 6/2 | 462,012 | 382.1s / 1,394,637 msg/s |
| Dekaf | 2026-07-29T09:58:11.0593732+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1594.5 MB/s | 6/2 | 514,087 | 409.1s / 1,329,141 msg/s |
| Dekaf | 2026-07-29T09:58:38.0690035+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1594.5 MB/s | 6/2 | 574,460 | 436.1s / 1,451,714 msg/s |
| Dekaf | 2026-07-29T09:59:05.0776077+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1594.5 MB/s | 6/3 | 634,922 | 463.1s / 1,292,989 msg/s |
| Dekaf | 2026-07-29T09:59:33.0868155+00:00 | 1 | 13.0 MiB / 12.6 MiB | 1596.6 MB/s | 6/3 | 698,447 | 491.1s / 1,503,496 msg/s |
| Dekaf | 2026-07-29T10:00:00.0941529+00:00 | 1 | 13.0 MiB / 12.1 MiB | 1722.3 MB/s | 6/3 | 765,349 | 518.1s / 1,412,989 msg/s |
| Dekaf | 2026-07-29T10:00:27.1029153+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1725.4 MB/s | 6/3 | 833,208 | 545.1s / 1,219,169 msg/s |
| Dekaf | 2026-07-29T10:00:55.1078307+00:00 | 1 | 11.0 MiB / 10.6 MiB | 1725.4 MB/s | 6/3 | 895,759 | 573.1s / 1,262,616 msg/s |
| Dekaf | 2026-07-29T10:01:22.1183159+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1725.4 MB/s | 7/3 | 959,276 | 600.2s / 1,299,453 msg/s |
| Dekaf | 2026-07-29T10:01:49.1237217+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1725.4 MB/s | 7/4 | 1,006,716 | 627.2s / 1,173,892 msg/s |
| Dekaf | 2026-07-29T10:02:16.1334835+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1725.4 MB/s | 7/4 | 1,059,165 | 654.2s / 1,285,427 msg/s |
| Dekaf | 2026-07-29T10:02:44.1452687+00:00 | 1 | 12.0 MiB / 2.8 MiB | 1725.4 MB/s | 7/4 | 1,124,949 | 682.2s / 1,296,486 msg/s |
| Dekaf | 2026-07-29T10:03:11.1539171+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1753.5 MB/s | 8/4 | 1,192,183 | 709.2s / 1,473,682 msg/s |
| Dekaf | 2026-07-29T10:03:38.1619057+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1753.5 MB/s | 8/4 | 1,259,563 | 736.2s / 1,398,221 msg/s |
| Dekaf | 2026-07-29T10:04:05.168387+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1753.5 MB/s | 8/5 | 1,322,145 | 763.2s / 1,351,046 msg/s |
| Dekaf | 2026-07-29T10:04:33.1794784+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1753.5 MB/s | 8/5 | 1,392,075 | 791.2s / 1,412,803 msg/s |
| Dekaf | 2026-07-29T10:05:00.1864213+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1753.5 MB/s | 8/6 | 1,456,366 | 818.2s / 1,432,090 msg/s |
| Dekaf | 2026-07-29T10:05:27.192166+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1753.5 MB/s | 8/6 | 1,522,578 | 845.2s / 1,194,469 msg/s |
| Dekaf | 2026-07-29T10:05:54.1987437+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1753.5 MB/s | 8/6 | 1,586,780 | 872.2s / 1,280,836 msg/s |
| Dekaf | 2026-07-29T10:36:23.3207809+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 572,416 msg/s |
| Dekaf | 2026-07-29T10:36:50.3315257+00:00 | 1 | 16.0 MiB / 15.4 MiB | 1653.1 MB/s | 0/0 | 32,573 | 27.0s / 1,456,677 msg/s |
| Dekaf | 2026-07-29T10:37:17.3439404+00:00 | 1 | 16.0 MiB / 14.4 MiB | 1653.1 MB/s | 0/1 | 73,013 | 54.0s / 1,367,984 msg/s |
| Dekaf | 2026-07-29T10:37:44.3537418+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1653.1 MB/s | 0/1 | 106,045 | 81.0s / 1,075,383 msg/s |
| Dekaf | 2026-07-29T10:38:12.3626178+00:00 | 1 | 14.0 MiB / 2.7 MiB | 1653.1 MB/s | 0/1 | 116,501 | 109.1s / 766,099 msg/s |
| Dekaf | 2026-07-29T10:38:39.3735321+00:00 | 1 | 12.0 MiB / 6.6 MiB | 1653.1 MB/s | 1/1 | 126,223 | 136.1s / 752,378 msg/s |
| Dekaf | 2026-07-29T10:39:06.3849395+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1653.1 MB/s | 1/2 | 171,511 | 163.1s / 1,402,293 msg/s |
| Dekaf | 2026-07-29T10:39:34.3953221+00:00 | 1 | 14.0 MiB / 11.7 MiB | 1653.1 MB/s | 1/2 | 226,253 | 191.1s / 1,478,327 msg/s |
| Dekaf | 2026-07-29T10:40:01.4155008+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1653.1 MB/s | 2/2 | 281,095 | 218.1s / 1,463,163 msg/s |
| Dekaf | 2026-07-29T10:40:28.4287064+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1653.1 MB/s | 2/2 | 333,591 | 245.1s / 1,417,644 msg/s |
| Dekaf | 2026-07-29T10:40:55.4449707+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1653.1 MB/s | 2/3 | 385,527 | 272.1s / 1,409,823 msg/s |
| Dekaf | 2026-07-29T10:41:23.4677245+00:00 | 1 | 15.0 MiB / 14.9 MiB | 1653.1 MB/s | 2/3 | 441,331 | 300.1s / 1,431,831 msg/s |
| Dekaf | 2026-07-29T10:41:50.4731391+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1675.6 MB/s | 2/3 | 497,877 | 327.1s / 1,294,164 msg/s |
| Dekaf | 2026-07-29T10:42:17.4903526+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1675.6 MB/s | 3/3 | 563,271 | 354.1s / 1,475,876 msg/s |
| Dekaf | 2026-07-29T10:42:44.5006429+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1675.6 MB/s | 4/3 | 639,488 | 381.1s / 1,513,605 msg/s |
| Dekaf | 2026-07-29T10:43:12.5098014+00:00 | 1 | 9.0 MiB / 8.2 MiB | 1675.6 MB/s | 4/3 | 720,576 | 409.1s / 1,411,765 msg/s |
| Dekaf | 2026-07-29T10:43:39.519505+00:00 | 1 | 9.0 MiB / 5.6 MiB | 1675.6 MB/s | 5/3 | 765,315 | 436.1s / 1,317,406 msg/s |
| Dekaf | 2026-07-29T10:44:06.5234073+00:00 | 1 | 9.0 MiB / 9.0 MiB | 1675.6 MB/s | 5/3 | 798,170 | 463.1s / 1,036,999 msg/s |
| Dekaf | 2026-07-29T10:44:33.5288331+00:00 | 1 | 8.0 MiB / 5.4 MiB | 1675.6 MB/s | 6/3 | 827,076 | 490.2s / 1,258,555 msg/s |
| Dekaf | 2026-07-29T10:45:01.5394074+00:00 | 1 | 9.0 MiB / 8.3 MiB | 1675.6 MB/s | 7/3 | 865,458 | 518.2s / 1,190,509 msg/s |
| Dekaf | 2026-07-29T10:45:28.5407219+00:00 | 1 | 10.0 MiB / 1.3 MiB | 1675.6 MB/s | 7/3 | 896,797 | 545.2s / 1,160,447 msg/s |
| Dekaf | 2026-07-29T10:45:55.5419439+00:00 | 1 | 10.0 MiB / 9.1 MiB | 1675.6 MB/s | 8/3 | 945,955 | 572.2s / 1,354,122 msg/s |
| Dekaf | 2026-07-29T10:46:22.5511063+00:00 | 1 | 10.0 MiB / 10.0 MiB | 1675.6 MB/s | 8/3 | 1,004,639 | 599.2s / 1,125,443 msg/s |
| Dekaf | 2026-07-29T10:46:50.5556376+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1675.6 MB/s | 9/3 | 1,073,855 | 627.2s / 1,098,818 msg/s |
| Dekaf | 2026-07-29T10:47:17.5630112+00:00 | 1 | 11.0 MiB / 10.9 MiB | 1675.6 MB/s | 9/4 | 1,126,765 | 654.2s / 1,012,003 msg/s |
| Dekaf | 2026-07-29T10:47:44.5691892+00:00 | 1 | 11.0 MiB / 10.5 MiB | 1675.6 MB/s | 9/4 | 1,177,655 | 681.2s / 990,859 msg/s |
| Dekaf | 2026-07-29T10:48:11.5729996+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1675.6 MB/s | 9/4 | 1,235,358 | 708.2s / 1,318,102 msg/s |
| Dekaf | 2026-07-29T10:48:39.5805969+00:00 | 1 | 11.0 MiB / 2.1 MiB | 1675.6 MB/s | 9/5 | 1,283,851 | 736.2s / 1,019,596 msg/s |
| Dekaf | 2026-07-29T10:49:06.5859923+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1675.6 MB/s | 9/5 | 1,336,996 | 763.2s / 1,338,724 msg/s |
| Dekaf | 2026-07-29T10:49:33.5957851+00:00 | 1 | 11.0 MiB / 10.5 MiB | 1675.6 MB/s | 9/5 | 1,393,976 | 790.2s / 1,037,317 msg/s |
| Dekaf | 2026-07-29T10:50:01.6056412+00:00 | 1 | 11.0 MiB / 10.1 MiB | 1675.6 MB/s | 9/5 | 1,460,225 | 818.2s / 1,239,758 msg/s |
| Dekaf | 2026-07-29T10:50:28.6091914+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1675.6 MB/s | 9/5 | 1,532,098 | 845.2s / 1,343,983 msg/s |
| Dekaf | 2026-07-29T10:50:55.6196792+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1675.6 MB/s | 9/6 | 1,600,945 | 872.2s / 1,304,274 msg/s |
| Dekaf | 2026-07-29T10:51:22.6311864+00:00 | 1 | 11.0 MiB / 10.7 MiB | 1675.6 MB/s | 9/6 | 1,662,343 | 899.3s / 1,169,030 msg/s |
| Dekaf (3conn) | 2026-07-29T10:51:51.6709078+00:00 | 1 | 16.0 MiB / 7.3 MiB | 1718.2 MB/s | 0/0 | 968 | 27.0s / 1,170,396 msg/s |
| Dekaf (3conn) | 2026-07-29T10:52:18.6869699+00:00 | 1 | 16.0 MiB / 4.5 MiB | 1718.2 MB/s | 0/1 | 1,854 | 54.0s / 1,184,733 msg/s |
| Dekaf (3conn) | 2026-07-29T10:52:45.6964717+00:00 | 1 | 16.0 MiB / 4.8 MiB | 1718.2 MB/s | 0/1 | 2,497 | 81.0s / 1,119,195 msg/s |
| Dekaf (3conn) | 2026-07-29T10:53:12.7159038+00:00 | 1 | 18.0 MiB / 2.9 MiB | 1718.2 MB/s | 0/1 | 3,037 | 108.0s / 1,075,347 msg/s |
| Dekaf (3conn) | 2026-07-29T10:53:40.7253206+00:00 | 1 | 18.0 MiB / 3.7 MiB | 1718.2 MB/s | 1/1 | 3,514 | 136.1s / 1,289,802 msg/s |
| Dekaf (3conn) | 2026-07-29T10:54:07.7356433+00:00 | 1 | 20.0 MiB / 5.1 MiB | 1718.2 MB/s | 1/1 | 4,553 | 163.1s / 1,208,524 msg/s |
| Dekaf (3conn) | 2026-07-29T10:54:34.7506989+00:00 | 1 | 18.0 MiB / 6.9 MiB | 2420.5 MB/s | 1/2 | 7,461 | 190.1s / 1,182,755 msg/s |
| Dekaf (3conn) | 2026-07-29T10:55:01.7661582+00:00 | 1 | 18.0 MiB / 4.1 MiB | 2420.5 MB/s | 1/2 | 8,777 | 217.1s / 1,474,756 msg/s |
| Dekaf (3conn) | 2026-07-29T10:55:29.7874311+00:00 | 1 | 15.0 MiB / 2.3 MiB | 2420.5 MB/s | 2/2 | 10,313 | 245.1s / 1,341,553 msg/s |
| Dekaf (3conn) | 2026-07-29T10:55:56.808478+00:00 | 1 | 13.0 MiB / 8.4 MiB | 2420.5 MB/s | 2/2 | 13,040 | 272.1s / 1,571,387 msg/s |
| Dekaf (3conn) | 2026-07-29T10:56:23.8195229+00:00 | 1 | 13.0 MiB / 9.4 MiB | 2420.5 MB/s | 3/2 | 16,133 | 299.1s / 1,187,512 msg/s |
| Dekaf (3conn) | 2026-07-29T10:56:50.8404812+00:00 | 1 | 13.0 MiB / 11.4 MiB | 2420.5 MB/s | 3/2 | 19,157 | 326.2s / 1,260,262 msg/s |
| Dekaf (3conn) | 2026-07-29T10:57:18.8488732+00:00 | 1 | 11.0 MiB / 9.1 MiB | 2420.5 MB/s | 4/2 | 24,664 | 354.2s / 1,403,202 msg/s |
| Dekaf (3conn) | 2026-07-29T10:57:45.8579242+00:00 | 1 | 9.0 MiB / 3.6 MiB | 2420.5 MB/s | 5/2 | 28,843 | 381.2s / 1,206,125 msg/s |
| Dekaf (3conn) | 2026-07-29T10:58:12.8686825+00:00 | 1 | 8.0 MiB / 2.4 MiB | 2420.5 MB/s | 5/2 | 37,701 | 408.2s / 1,223,616 msg/s |
| Dekaf (3conn) | 2026-07-29T10:58:40.8818613+00:00 | 1 | 9.0 MiB / 9.0 MiB | 2420.5 MB/s | 5/3 | 45,483 | 436.2s / 1,321,209 msg/s |
| Dekaf (3conn) | 2026-07-29T10:59:07.8893441+00:00 | 1 | 9.0 MiB / 4.2 MiB | 2420.5 MB/s | 5/3 | 53,930 | 463.2s / 1,329,988 msg/s |
| Dekaf (3conn) | 2026-07-29T10:59:34.8945168+00:00 | 1 | 9.0 MiB / 3.7 MiB | 2420.5 MB/s | 5/3 | 59,635 | 490.2s / 1,185,242 msg/s |
| Dekaf (3conn) | 2026-07-29T11:00:01.9097689+00:00 | 1 | 10.0 MiB / 2.6 MiB | 2420.5 MB/s | 6/3 | 65,985 | 517.2s / 1,553,548 msg/s |
| Dekaf (3conn) | 2026-07-29T11:00:29.9191399+00:00 | 1 | 11.0 MiB / 11.0 MiB | 2420.5 MB/s | 7/3 | 74,580 | 545.2s / 1,595,875 msg/s |
| Dekaf (3conn) | 2026-07-29T11:00:56.9386632+00:00 | 1 | 12.0 MiB / 4.0 MiB | 2420.5 MB/s | 7/3 | 81,744 | 572.2s / 1,378,413 msg/s |
| Dekaf (3conn) | 2026-07-29T11:01:23.9544899+00:00 | 1 | 11.0 MiB / 5.6 MiB | 2420.5 MB/s | 7/4 | 86,946 | 599.3s / 1,326,569 msg/s |
| Dekaf (3conn) | 2026-07-29T11:01:50.9687699+00:00 | 1 | 11.0 MiB / 2.7 MiB | 2420.5 MB/s | 7/4 | 93,246 | 626.3s / 1,296,585 msg/s |
| Dekaf (3conn) | 2026-07-29T11:02:18.978371+00:00 | 1 | 11.0 MiB / 4.0 MiB | 2420.5 MB/s | 7/4 | 100,942 | 654.3s / 1,441,359 msg/s |
| Dekaf (3conn) | 2026-07-29T11:02:45.9905032+00:00 | 1 | 11.0 MiB / 7.4 MiB | 2420.5 MB/s | 7/5 | 106,513 | 681.3s / 1,386,466 msg/s |
| Dekaf (3conn) | 2026-07-29T11:03:13.0025131+00:00 | 1 | 11.0 MiB / 6.8 MiB | 2420.5 MB/s | 7/5 | 113,713 | 708.3s / 1,120,100 msg/s |
| Dekaf (3conn) | 2026-07-29T11:03:40.0255139+00:00 | 1 | 11.0 MiB / 6.2 MiB | 2420.5 MB/s | 7/5 | 122,428 | 735.3s / 1,276,007 msg/s |
| Dekaf (3conn) | 2026-07-29T11:04:08.0343599+00:00 | 1 | 11.0 MiB / 11.0 MiB | 2420.5 MB/s | 7/5 | 127,460 | 763.3s / 1,134,374 msg/s |
| Dekaf (3conn) | 2026-07-29T11:04:35.0469953+00:00 | 1 | 11.0 MiB / 2.9 MiB | 2420.5 MB/s | 7/5 | 129,906 | 790.3s / 1,329,757 msg/s |
| Dekaf (3conn) | 2026-07-29T11:05:02.0516647+00:00 | 1 | 12.0 MiB / 1.0 MiB | 2420.5 MB/s | 8/5 | 132,429 | 817.3s / 1,029,746 msg/s |
| Dekaf (3conn) | 2026-07-29T11:05:29.0615439+00:00 | 1 | 13.0 MiB / 1.9 MiB | 2420.5 MB/s | 9/5 | 134,384 | 844.4s / 1,033,007 msg/s |
| Dekaf (3conn) | 2026-07-29T11:05:57.0744413+00:00 | 1 | 14.0 MiB / 3.0 MiB | 2420.5 MB/s | 9/5 | 135,712 | 872.4s / 942,705 msg/s |
| Dekaf (3conn) | 2026-07-29T11:06:24.0812686+00:00 | 1 | 14.0 MiB / 5.4 MiB | 2420.5 MB/s | 10/5 | 136,890 | 899.4s / 1,121,895 msg/s |
*2,600 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-29T09:51:52.111076+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 16.0 MiB |
| Dekaf | 2026-07-29T09:52:07.126176+00:00 | 1 | capacity | succeeded | 15,015ms | 14.0 MiB / 14.0 MiB |
| Dekaf | 2026-07-29T09:52:10.1283865+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T09:52:25.1393279+00:00 | 1 | capacity | succeeded | 15,010ms | 12.0 MiB / 4.8 MiB |
| Dekaf | 2026-07-29T09:52:55.1722176+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T09:53:10.1902178+00:00 | 1 | capacity | failed | 15,017ms | 12.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-29T09:54:10.2583641+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 10.2 MiB |
| Dekaf | 2026-07-29T09:54:25.2753469+00:00 | 1 | capacity | succeeded | 15,016ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T09:54:55.3048056+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 10.8 MiB |
| Dekaf | 2026-07-29T09:55:10.3184049+00:00 | 1 | capacity | succeeded | 15,013ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T09:55:40.3542079+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T09:55:55.3664529+00:00 | 1 | capacity | succeeded | 15,012ms | 15.0 MiB / 12.7 MiB |
| Dekaf | 2026-07-29T09:56:25.4060527+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-29T09:56:40.4387072+00:00 | 1 | capacity | succeeded | 15,032ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T09:57:10.475041+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.4 MiB |
| Dekaf | 2026-07-29T09:57:25.4870775+00:00 | 1 | capacity | failed | 15,011ms | 13.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T09:58:25.541691+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T09:58:40.5597099+00:00 | 1 | capacity | failed | 15,018ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T10:00:40.6801065+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T10:00:55.6933358+00:00 | 1 | capacity | succeeded | 15,013ms | 11.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T10:01:25.7257299+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T10:01:40.7383712+00:00 | 1 | capacity | failed | 15,012ms | 11.0 MiB / 8.2 MiB |
| Dekaf | 2026-07-29T10:02:40.7941772+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T10:02:55.8063623+00:00 | 1 | capacity | succeeded | 15,012ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T10:03:25.8344714+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.2 MiB |
| Dekaf | 2026-07-29T10:03:40.8456211+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T10:04:40.8948573+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-29T10:04:55.9291319+00:00 | 1 | capacity | failed | 15,034ms | 12.0 MiB / 9.0 MiB |
| Dekaf | 2026-07-29T10:36:53.4583733+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.7 MiB |
| Dekaf | 2026-07-29T10:37:08.4850727+00:00 | 1 | capacity | failed | 15,026ms | 16.0 MiB / 13.6 MiB |
| Dekaf | 2026-07-29T10:38:08.5697545+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 13.6 MiB |
| Dekaf | 2026-07-29T10:38:23.5897678+00:00 | 1 | capacity | succeeded | 15,019ms | 14.0 MiB / 13.7 MiB |
| Dekaf | 2026-07-29T10:38:26.5919555+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 12.8 MiB |
| Dekaf | 2026-07-29T10:38:41.6100637+00:00 | 1 | capacity | failed | 15,018ms | 14.0 MiB / 5.0 MiB |
| Dekaf | 2026-07-29T10:39:41.6764997+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T10:39:56.6912534+00:00 | 1 | capacity | succeeded | 15,014ms | 15.0 MiB / 13.7 MiB |
| Dekaf | 2026-07-29T10:40:26.7250013+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 13.4 MiB |
| Dekaf | 2026-07-29T10:40:41.7442122+00:00 | 1 | capacity | failed | 15,019ms | 15.0 MiB / 14.2 MiB |
| Dekaf | 2026-07-29T10:41:41.8129121+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 14.6 MiB |
| Dekaf | 2026-07-29T10:41:56.8305325+00:00 | 1 | capacity | succeeded | 15,017ms | 13.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T10:42:26.8530286+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T10:42:41.8651768+00:00 | 1 | capacity | succeeded | 15,012ms | 11.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-29T10:43:11.8890127+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.0 MiB |
| Dekaf | 2026-07-29T10:43:26.926774+00:00 | 1 | capacity | succeeded | 15,037ms | 9.0 MiB / 7.4 MiB |
| Dekaf | 2026-07-29T10:43:56.9612709+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-29T10:44:11.9767568+00:00 | 1 | capacity | succeeded | 15,015ms | 8.0 MiB / 7.2 MiB |
| Dekaf | 2026-07-29T10:44:42.004177+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-29T10:44:57.0209713+00:00 | 1 | capacity | succeeded | 15,016ms | 9.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-29T10:45:27.0511184+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 7.3 MiB |
| Dekaf | 2026-07-29T10:45:42.0668598+00:00 | 1 | capacity | succeeded | 15,015ms | 10.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-29T10:46:12.0875226+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 9.9 MiB |
| Dekaf | 2026-07-29T10:46:27.1013332+00:00 | 1 | capacity | succeeded | 15,014ms | 11.0 MiB / 9.9 MiB |
| Dekaf | 2026-07-29T10:46:57.129493+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.4 MiB |
| Dekaf | 2026-07-29T10:47:12.1455758+00:00 | 1 | capacity | failed | 15,016ms | 11.0 MiB / 10.2 MiB |
| Dekaf | 2026-07-29T10:48:12.2055796+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T10:48:27.2191036+00:00 | 1 | capacity | failed | 15,013ms | 11.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-29T10:50:27.3374929+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T10:50:42.348952+00:00 | 1 | capacity | failed | 15,011ms | 11.0 MiB / 11.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:51:54.8057114+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 4.0 MiB |
| Dekaf (3conn) | 2026-07-29T10:52:09.8356568+00:00 | 1 | capacity | failed | 15,030ms | 16.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:53:09.9614529+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 3.8 MiB |
| Dekaf (3conn) | 2026-07-29T10:53:24.9831985+00:00 | 1 | capacity | succeeded | 15,021ms | 18.0 MiB / 0.8 MiB |
| Dekaf (3conn) | 2026-07-29T10:53:55.0369456+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 4.2 MiB |
| Dekaf (3conn) | 2026-07-29T10:54:10.0587793+00:00 | 1 | capacity | failed | 15,021ms | 18.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:55:10.1531777+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 4.8 MiB |
| Dekaf (3conn) | 2026-07-29T10:55:25.1901595+00:00 | 1 | capacity | succeeded | 15,037ms | 15.0 MiB / 4.8 MiB |
| Dekaf (3conn) | 2026-07-29T10:55:55.2515978+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 7.0 MiB |
| Dekaf (3conn) | 2026-07-29T10:56:10.282246+00:00 | 1 | capacity | succeeded | 15,030ms | 13.0 MiB / 5.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:56:40.325692+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:56:55.3557002+00:00 | 1 | capacity | succeeded | 15,030ms | 11.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:57:25.4033294+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:57:40.4369463+00:00 | 1 | capacity | succeeded | 15,033ms | 9.0 MiB / 8.0 MiB |
| Dekaf (3conn) | 2026-07-29T10:58:10.4829903+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 4.8 MiB |
| Dekaf (3conn) | 2026-07-29T10:58:25.5089634+00:00 | 1 | capacity | failed | 15,025ms | 9.0 MiB / 7.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:59:25.5934481+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 3.3 MiB |
| Dekaf (3conn) | 2026-07-29T10:59:40.6216091+00:00 | 1 | capacity | succeeded | 15,028ms | 10.0 MiB / 7.7 MiB |
| Dekaf (3conn) | 2026-07-29T11:00:10.6749543+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 3.9 MiB |
| Dekaf (3conn) | 2026-07-29T11:00:25.7009972+00:00 | 1 | capacity | succeeded | 15,026ms | 11.0 MiB / 10.0 MiB |
| Dekaf (3conn) | 2026-07-29T11:00:55.7420569+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 0.9 MiB |
| Dekaf (3conn) | 2026-07-29T11:01:10.755915+00:00 | 1 | capacity | failed | 15,013ms | 11.0 MiB / 9.3 MiB |
| Dekaf (3conn) | 2026-07-29T11:02:10.8552076+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf (3conn) | 2026-07-29T11:02:25.8818816+00:00 | 1 | capacity | failed | 15,026ms | 11.0 MiB / 4.2 MiB |
| Dekaf (3conn) | 2026-07-29T11:04:26.0648414+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-07-29T11:04:41.0912812+00:00 | 1 | capacity | succeeded | 15,026ms | 12.0 MiB / 5.2 MiB |
| Dekaf (3conn) | 2026-07-29T11:05:11.138428+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 5.8 MiB |
| Dekaf (3conn) | 2026-07-29T11:05:26.1595544+00:00 | 1 | capacity | succeeded | 15,021ms | 13.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-07-29T11:05:56.2062034+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 1.3 MiB |
| Dekaf (3conn) | 2026-07-29T11:06:11.2397877+00:00 | 1 | capacity | succeeded | 15,033ms | 14.0 MiB / 8.1 MiB |

## Producer Admission Block Durations - Producer (Fire-and-Forget)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 940 |
| Dekaf | 1 | 0.002–0.004ms | 1,218 |
| Dekaf | 1 | 0.004–0.008ms | 4,204 |
| Dekaf | 1 | 0.008–0.016ms | 29,985 |
| Dekaf | 1 | 0.016–0.032ms | 32,829 |
| Dekaf | 1 | 0.032–0.064ms | 31,293 |
| Dekaf | 1 | 0.064–0.128ms | 54,796 |
| Dekaf | 1 | 0.128–0.256ms | 162,483 |
| Dekaf | 1 | 0.256–0.512ms | 258,193 |
| Dekaf | 1 | 0.512–1.024ms | 96,907 |
| Dekaf | 1 | 1.024–2.048ms | 19,150 |
| Dekaf | 1 | 2.048–4.096ms | 4,299 |
| Dekaf | 1 | 4.096–8.192ms | 1,300 |
| Dekaf | 1 | 8.192–16.384ms | 164 |
| Dekaf | 1 | 16.384–32.768ms | 6 |
| Dekaf (3conn) | 1 | 0.001–0.002ms | 65 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 67 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 175 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 578 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 2,077 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 6,434 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 4,620 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 6,596 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 7,449 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 7,262 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 5,095 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 1,388 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 278 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 31 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 1 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 1 |
| Dekaf | 1 | 0.001–0.002ms | 1,223 |
| Dekaf | 1 | 0.002–0.004ms | 1,390 |
| Dekaf | 1 | 0.004–0.008ms | 3,534 |
| Dekaf | 1 | 0.008–0.016ms | 16,615 |
| Dekaf | 1 | 0.016–0.032ms | 27,613 |
| Dekaf | 1 | 0.032–0.064ms | 34,980 |
| Dekaf | 1 | 0.064–0.128ms | 63,318 |
| Dekaf | 1 | 0.128–0.256ms | 173,905 |
| Dekaf | 1 | 0.256–0.512ms | 269,740 |
| Dekaf | 1 | 0.512–1.024ms | 82,670 |
| Dekaf | 1 | 1.024–2.048ms | 14,858 |
| Dekaf | 1 | 2.048–4.096ms | 4,630 |
| Dekaf | 1 | 4.096–8.192ms | 1,134 |
| Dekaf | 1 | 8.192–16.384ms | 148 |
| Dekaf | 1 | 16.384–32.768ms | 3 |
| Dekaf | 1 | 32.768–65.536ms | 1 |
| Dekaf | 1 | 65.536–131.072ms | 1 |

## Delivery Latency Outliers - Producer (Fire-and-Forget)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 19,770,000 | 2026-07-29T10:06:44.7220077+00:00 | 100.7ms | GC pause | - | - | 23.0s / 857,772 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 19,778,000 | 2026-07-29T10:06:44.725205+00:00 | 111.9ms | GC pause | - | - | 23.0s / 857,772 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 19,788,000 | 2026-07-29T10:06:44.7459696+00:00 | 101.5ms | GC pause | - | - | 23.0s / 857,772 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 171,231,000 | 2026-07-29T10:09:39.7399337+00:00 | 113.8ms | GC pause | - | - | 198.2s / 914,313 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 171,270,000 | 2026-07-29T10:09:39.779032+00:00 | 102.6ms | GC pause | - | - | 198.2s / 914,313 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 171,328,000 | 2026-07-29T10:09:39.8439728+00:00 | 127.0ms | GC pause | - | - | 198.2s / 914,313 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 171,331,000 | 2026-07-29T10:09:39.8455098+00:00 | 125.6ms | GC pause | - | - | 198.2s / 914,313 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 171,334,000 | 2026-07-29T10:09:39.8477639+00:00 | 103.2ms | GC pause | - | - | 198.2s / 914,313 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 171,353,000 | 2026-07-29T10:09:39.8586048+00:00 | 117.3ms | GC pause | - | - | 198.2s / 914,313 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 171,372,000 | 2026-07-29T10:09:39.8742518+00:00 | 126.0ms | GC pause | - | - | 198.2s / 914,313 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 171,377,000 | 2026-07-29T10:09:39.880533+00:00 | 146.3ms | GC pause | - | - | 198.2s / 914,313 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 171,402,000 | 2026-07-29T10:09:39.9174965+00:00 | 109.6ms | GC pause | - | - | 198.2s / 914,313 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 171,404,000 | 2026-07-29T10:09:39.9200713+00:00 | 106.3ms | GC pause | - | - | 198.2s / 914,313 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 171,431,000 | 2026-07-29T10:09:39.9440823+00:00 | 127.5ms | GC pause | - | - | 198.2s / 914,313 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 185,637,000 | 2026-07-29T10:09:56.1652117+00:00 | 111.0ms | GC pause | - | - | 214.2s / 864,299 msg/s | Gen2 +0 / pause +88.8ms |
| Confluent | 191,052,000 | 2026-07-29T10:10:02.7416871+00:00 | 108.8ms | GC pause | - | - | 221.2s / 395,861 msg/s | Gen2 +0 / pause +141.4ms |
| Confluent | 191,132,000 | 2026-07-29T10:10:02.9628331+00:00 | 103.0ms | GC pause | - | - | 221.2s / 395,861 msg/s | Gen2 +0 / pause +141.4ms |
| Confluent | 191,383,000 | 2026-07-29T10:10:03.5793686+00:00 | 126.5ms | GC pause | - | - | 222.2s / 382,758 msg/s | Gen2 +0 / pause +131.6ms |
| Confluent | 191,410,000 | 2026-07-29T10:10:03.62395+00:00 | 173.9ms | GC pause | - | - | 222.2s / 382,758 msg/s | Gen2 +0 / pause +131.6ms |
| Confluent | 191,425,000 | 2026-07-29T10:10:03.6517465+00:00 | 188.9ms | GC pause | - | - | 222.2s / 382,758 msg/s | Gen2 +0 / pause +131.6ms |
| Confluent | 191,428,000 | 2026-07-29T10:10:03.6587275+00:00 | 226.5ms | GC pause | - | - | 222.2s / 382,758 msg/s | Gen2 +0 / pause +131.6ms |
| Confluent | 191,442,000 | 2026-07-29T10:10:03.6932268+00:00 | 201.9ms | GC pause | - | - | 222.2s / 382,758 msg/s | Gen2 +0 / pause +131.6ms |
| Confluent | 191,453,000 | 2026-07-29T10:10:03.7263734+00:00 | 227.7ms | GC pause | - | - | 222.2s / 382,758 msg/s | Gen2 +0 / pause +131.6ms |
| Confluent | 191,469,000 | 2026-07-29T10:10:03.7703002+00:00 | 221.6ms | GC pause | - | - | 222.2s / 382,758 msg/s | Gen2 +0 / pause +131.6ms |
| Confluent | 191,483,000 | 2026-07-29T10:10:03.8003556+00:00 | 231.3ms | GC pause | - | - | 222.2s / 382,758 msg/s | Gen2 +0 / pause +131.6ms |
| Confluent | 191,519,000 | 2026-07-29T10:10:03.9084174+00:00 | 225.1ms | GC pause | - | - | 222.2s / 382,758 msg/s | Gen2 +0 / pause +131.6ms |
| Confluent | 191,527,000 | 2026-07-29T10:10:03.9260382+00:00 | 271.8ms | GC pause | - | - | 222.2s / 382,758 msg/s | Gen2 +0 / pause +131.6ms |
| Confluent | 191,540,000 | 2026-07-29T10:10:03.9769703+00:00 | 227.6ms | GC pause | - | - | 222.2s / 382,758 msg/s | Gen2 +0 / pause +131.6ms |
| Confluent | 191,558,000 | 2026-07-29T10:10:04.0324172+00:00 | 243.7ms | GC pause | - | - | 222.2s / 382,758 msg/s | Gen2 +0 / pause +131.6ms |
| Confluent | 191,569,000 | 2026-07-29T10:10:04.0631686+00:00 | 221.9ms | GC pause | - | - | 222.2s / 382,758 msg/s | Gen2 +0 / pause +131.6ms |
| Confluent | 191,586,000 | 2026-07-29T10:10:04.1385069+00:00 | 181.4ms | GC pause | - | - | 222.2s / 382,758 msg/s | Gen2 +0 / pause +131.6ms |
| Confluent | 191,614,000 | 2026-07-29T10:10:04.2191389+00:00 | 220.2ms | GC pause | - | - | 223.2s / 310,973 msg/s | Gen2 +0 / pause +240.3ms |
| Confluent | 191,641,000 | 2026-07-29T10:10:04.2873116+00:00 | 257.6ms | GC pause | - | - | 223.2s / 310,973 msg/s | Gen2 +0 / pause +240.3ms |
| Confluent | 191,648,000 | 2026-07-29T10:10:04.3099477+00:00 | 254.1ms | GC pause | - | - | 223.2s / 310,973 msg/s | Gen2 +0 / pause +240.3ms |
| Confluent | 191,706,000 | 2026-07-29T10:10:04.4621019+00:00 | 232.6ms | GC pause | - | - | 223.2s / 310,973 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 191,774,000 | 2026-07-29T10:10:04.6510484+00:00 | 250.8ms | GC pause | - | - | 223.2s / 310,973 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 191,784,000 | 2026-07-29T10:10:04.6707264+00:00 | 265.0ms | GC pause | - | - | 223.2s / 310,973 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 191,816,000 | 2026-07-29T10:10:04.7591033+00:00 | 275.2ms | GC pause | - | - | 223.2s / 310,973 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 191,820,000 | 2026-07-29T10:10:04.7683698+00:00 | 296.2ms | GC pause | - | - | 223.2s / 310,973 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 191,824,000 | 2026-07-29T10:10:04.7770576+00:00 | 288.1ms | GC pause | - | - | 223.2s / 310,973 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 191,875,000 | 2026-07-29T10:10:04.8918368+00:00 | 272.8ms | GC pause | - | - | 223.2s / 310,973 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 191,931,000 | 2026-07-29T10:10:05.0957447+00:00 | 246.7ms | GC pause | - | - | 223.2s / 310,973 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 191,967,000 | 2026-07-29T10:10:05.2834036+00:00 | 155.9ms | GC pause | - | - | 224.2s / 521,767 msg/s | Gen2 +0 / pause +199.9ms |
| Confluent | 191,997,000 | 2026-07-29T10:10:05.4189821+00:00 | 109.5ms | GC pause | - | - | 224.2s / 521,767 msg/s | Gen2 +0 / pause +91.2ms |
| Confluent | 192,118,000 | 2026-07-29T10:10:05.6543343+00:00 | 151.2ms | GC pause | - | - | 224.2s / 521,767 msg/s | Gen2 +0 / pause +91.2ms |
| Confluent | 192,258,000 | 2026-07-29T10:10:05.9167877+00:00 | 151.0ms | GC pause | - | - | 224.2s / 521,767 msg/s | Gen2 +0 / pause +91.2ms |
| Confluent | 192,274,000 | 2026-07-29T10:10:05.9349362+00:00 | 101.8ms | GC pause | - | - | 224.2s / 521,767 msg/s | Gen2 +0 / pause +91.2ms |
| Confluent | 192,276,000 | 2026-07-29T10:10:05.9366982+00:00 | 100.5ms | GC pause | - | - | 224.2s / 521,767 msg/s | Gen2 +0 / pause +91.2ms |
| Confluent | 192,278,000 | 2026-07-29T10:10:05.9408333+00:00 | 151.7ms | GC pause | - | - | 224.2s / 521,767 msg/s | Gen2 +0 / pause +91.2ms |
| Confluent | 192,301,000 | 2026-07-29T10:10:05.9709678+00:00 | 152.9ms | GC pause | - | - | 224.2s / 521,767 msg/s | Gen2 +0 / pause +91.2ms |
| Confluent | 192,331,000 | 2026-07-29T10:10:06.0131679+00:00 | 175.2ms | GC pause | - | - | 224.2s / 521,767 msg/s | Gen2 +0 / pause +91.2ms |
| Confluent | 193,141,000 | 2026-07-29T10:10:07.3269573+00:00 | 126.1ms | GC pause | - | - | 226.2s / 739,237 msg/s | Gen2 +0 / pause +227.1ms |
| Confluent | 193,168,000 | 2026-07-29T10:10:07.3652195+00:00 | 123.3ms | GC pause | - | - | 226.2s / 739,237 msg/s | Gen2 +0 / pause +227.1ms |
| Confluent | 193,191,000 | 2026-07-29T10:10:07.3921745+00:00 | 128.0ms | GC pause | - | - | 226.2s / 739,237 msg/s | Gen2 +0 / pause +227.1ms |
| Confluent | 193,201,000 | 2026-07-29T10:10:07.4046901+00:00 | 125.1ms | GC pause | - | - | 226.2s / 739,237 msg/s | Gen2 +0 / pause +227.1ms |
| Confluent | 193,209,000 | 2026-07-29T10:10:07.411745+00:00 | 101.4ms | GC pause | - | - | 226.2s / 739,237 msg/s | Gen2 +0 / pause +135.2ms |
| Confluent | 201,461,000 | 2026-07-29T10:10:16.4268834+00:00 | 102.5ms | GC pause | - | - | 235.2s / 886,663 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 201,575,000 | 2026-07-29T10:10:16.5424145+00:00 | 100.6ms | GC pause | - | - | 235.2s / 886,663 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 201,657,000 | 2026-07-29T10:10:16.6231098+00:00 | 118.7ms | GC pause | - | - | 235.2s / 886,663 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 201,697,000 | 2026-07-29T10:10:16.6630511+00:00 | 125.3ms | GC pause | - | - | 235.2s / 886,663 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 201,700,000 | 2026-07-29T10:10:16.6666194+00:00 | 108.5ms | GC pause | - | - | 235.2s / 886,663 msg/s | Gen2 +0 / pause +90.3ms |
| Confluent | 203,978,000 | 2026-07-29T10:10:19.2202015+00:00 | 100.1ms | GC pause | - | - | 237.2s / 870,462 msg/s | Gen2 +0 / pause +110.2ms |
| Confluent | 204,410,000 | 2026-07-29T10:10:19.7065196+00:00 | 106.9ms | GC pause | - | - | 238.2s / 807,611 msg/s | Gen2 +0 / pause +72.4ms |
| Confluent | 204,428,000 | 2026-07-29T10:10:19.7236358+00:00 | 137.9ms | GC pause | - | - | 238.2s / 807,611 msg/s | Gen2 +0 / pause +72.4ms |
| Confluent | 204,441,000 | 2026-07-29T10:10:19.7339948+00:00 | 142.7ms | GC pause | - | - | 238.2s / 807,611 msg/s | Gen2 +0 / pause +72.4ms |
| Confluent | 204,547,000 | 2026-07-29T10:10:19.8887724+00:00 | 111.2ms | GC pause | - | - | 238.2s / 807,611 msg/s | Gen2 +0 / pause +72.4ms |
| Confluent | 204,591,000 | 2026-07-29T10:10:19.9409649+00:00 | 110.0ms | GC pause | - | - | 238.2s / 807,611 msg/s | Gen2 +0 / pause +72.4ms |
| Confluent | 204,597,000 | 2026-07-29T10:10:19.9546534+00:00 | 102.3ms | GC pause | - | - | 238.2s / 807,611 msg/s | Gen2 +0 / pause +72.4ms |
| Confluent | 204,617,000 | 2026-07-29T10:10:19.9754805+00:00 | 111.5ms | GC pause | - | - | 238.2s / 807,611 msg/s | Gen2 +0 / pause +72.4ms |
| Confluent | 205,693,000 | 2026-07-29T10:10:21.1831127+00:00 | 104.4ms | GC pause | - | - | 239.2s / 937,745 msg/s | Gen2 +0 / pause +56.8ms |
| Confluent | 205,730,000 | 2026-07-29T10:10:21.2223598+00:00 | 112.9ms | GC pause | - | - | 239.2s / 937,745 msg/s | Gen2 +0 / pause +56.8ms |
| Confluent | 205,739,000 | 2026-07-29T10:10:21.2327488+00:00 | 107.3ms | GC pause | - | - | 239.2s / 937,745 msg/s | Gen2 +0 / pause +56.8ms |
| Confluent | 205,808,000 | 2026-07-29T10:10:21.3229603+00:00 | 156.1ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +120.7ms |
| Confluent | 205,814,000 | 2026-07-29T10:10:21.3279031+00:00 | 124.2ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +120.7ms |
| Confluent | 205,850,000 | 2026-07-29T10:10:21.3689563+00:00 | 147.7ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +120.7ms |
| Confluent | 205,866,000 | 2026-07-29T10:10:21.3869766+00:00 | 132.0ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +120.7ms |
| Confluent | 205,874,000 | 2026-07-29T10:10:21.3960432+00:00 | 138.1ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +120.7ms |
| Confluent | 205,881,000 | 2026-07-29T10:10:21.4029971+00:00 | 183.9ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +120.7ms |
| Confluent | 205,890,000 | 2026-07-29T10:10:21.4142722+00:00 | 162.2ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +120.7ms |
| Confluent | 205,892,000 | 2026-07-29T10:10:21.417299+00:00 | 139.3ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +120.7ms |
| Confluent | 205,912,000 | 2026-07-29T10:10:21.4431585+00:00 | 140.5ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +63.9ms |
| Confluent | 205,922,000 | 2026-07-29T10:10:21.4556037+00:00 | 142.9ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +63.9ms |
| Confluent | 205,972,000 | 2026-07-29T10:10:21.5197462+00:00 | 127.6ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +63.9ms |
| Confluent | 206,015,000 | 2026-07-29T10:10:21.5720289+00:00 | 118.1ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +63.9ms |
| Confluent | 206,022,000 | 2026-07-29T10:10:21.5807231+00:00 | 115.5ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +63.9ms |
| Confluent | 206,024,000 | 2026-07-29T10:10:21.5828923+00:00 | 114.0ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +63.9ms |
| Confluent | 206,032,000 | 2026-07-29T10:10:21.5945658+00:00 | 107.3ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +63.9ms |
| Confluent | 206,034,000 | 2026-07-29T10:10:21.5968786+00:00 | 110.3ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +63.9ms |
| Confluent | 206,051,000 | 2026-07-29T10:10:21.6197864+00:00 | 145.0ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +63.9ms |
| Confluent | 206,073,000 | 2026-07-29T10:10:21.6549096+00:00 | 113.1ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +63.9ms |
| Confluent | 206,087,000 | 2026-07-29T10:10:21.676044+00:00 | 126.1ms | GC pause | - | - | 240.2s / 773,121 msg/s | Gen2 +0 / pause +63.9ms |
| Confluent | 225,933,000 | 2026-07-29T10:10:44.4935426+00:00 | 109.1ms | GC pause | - | - | 263.2s / 902,505 msg/s | Gen2 +0 / pause +108.4ms |
| Confluent | 225,946,000 | 2026-07-29T10:10:44.5031527+00:00 | 110.8ms | GC pause | - | - | 263.2s / 902,505 msg/s | Gen2 +0 / pause +108.4ms |
| Confluent | 225,959,000 | 2026-07-29T10:10:44.5141847+00:00 | 110.5ms | GC pause | - | - | 263.2s / 902,505 msg/s | Gen2 +0 / pause +108.4ms |
| Confluent | 225,967,000 | 2026-07-29T10:10:44.5208085+00:00 | 120.3ms | GC pause | - | - | 263.2s / 902,505 msg/s | Gen2 +0 / pause +108.4ms |
| Confluent | 225,986,000 | 2026-07-29T10:10:44.5390904+00:00 | 111.5ms | GC pause | - | - | 263.2s / 902,505 msg/s | Gen2 +0 / pause +108.4ms |
| Confluent | 226,007,000 | 2026-07-29T10:10:44.5619104+00:00 | 127.3ms | GC pause | - | - | 263.2s / 902,505 msg/s | Gen2 +0 / pause +108.4ms |
| Confluent | 232,994,000 | 2026-07-29T10:10:52.4066211+00:00 | 104.6ms | GC pause | - | - | 271.2s / 858,031 msg/s | Gen2 +0 / pause +185.6ms |
| Confluent | 368,568,000 | 2026-07-29T10:13:07.7958181+00:00 | 103.4ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 368,576,000 | 2026-07-29T10:13:07.8063289+00:00 | 121.1ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 368,585,000 | 2026-07-29T10:13:07.8193073+00:00 | 111.8ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 368,734,000 | 2026-07-29T10:13:07.920059+00:00 | 102.2ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 368,737,000 | 2026-07-29T10:13:07.9221833+00:00 | 173.1ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 368,738,000 | 2026-07-29T10:13:07.9227448+00:00 | 172.6ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 368,858,000 | 2026-07-29T10:13:08.037421+00:00 | 173.2ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 368,887,000 | 2026-07-29T10:13:08.0767294+00:00 | 145.4ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 368,961,000 | 2026-07-29T10:13:08.1362961+00:00 | 167.9ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 368,979,000 | 2026-07-29T10:13:08.149195+00:00 | 115.5ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 368,990,000 | 2026-07-29T10:13:08.1550984+00:00 | 127.7ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 369,011,000 | 2026-07-29T10:13:08.1728723+00:00 | 172.0ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 369,050,000 | 2026-07-29T10:13:08.2291839+00:00 | 100.7ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 369,051,000 | 2026-07-29T10:13:08.2299921+00:00 | 149.4ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 369,097,000 | 2026-07-29T10:13:08.281237+00:00 | 162.9ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 369,142,000 | 2026-07-29T10:13:08.3340496+00:00 | 103.7ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 369,162,000 | 2026-07-29T10:13:08.3459701+00:00 | 105.6ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 369,173,000 | 2026-07-29T10:13:08.3521239+00:00 | 105.7ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 369,269,000 | 2026-07-29T10:13:08.4438193+00:00 | 102.4ms | GC pause | - | - | 406.3s / 1,114,304 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 369,272,000 | 2026-07-29T10:13:08.4460777+00:00 | 134.9ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +197.6ms |
| Confluent | 369,327,000 | 2026-07-29T10:13:08.496737+00:00 | 182.3ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +197.6ms |
| Confluent | 369,337,000 | 2026-07-29T10:13:08.5107606+00:00 | 181.1ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +197.6ms |
| Confluent | 369,359,000 | 2026-07-29T10:13:08.5264274+00:00 | 102.6ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +197.6ms |
| Confluent | 369,365,000 | 2026-07-29T10:13:08.5329387+00:00 | 109.9ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +197.6ms |
| Confluent | 369,385,000 | 2026-07-29T10:13:08.5586986+00:00 | 100.4ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +197.6ms |
| Confluent | 369,397,000 | 2026-07-29T10:13:08.577995+00:00 | 173.1ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +197.6ms |
| Confluent | 369,577,000 | 2026-07-29T10:13:08.7721963+00:00 | 158.4ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +106.5ms |
| Confluent | 369,608,000 | 2026-07-29T10:13:08.7980218+00:00 | 166.5ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +106.5ms |
| Confluent | 369,617,000 | 2026-07-29T10:13:08.8048445+00:00 | 165.0ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +106.5ms |
| Confluent | 369,657,000 | 2026-07-29T10:13:08.8360611+00:00 | 161.5ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +106.5ms |
| Confluent | 369,821,000 | 2026-07-29T10:13:09.0130137+00:00 | 136.9ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +106.5ms |
| Confluent | 369,871,000 | 2026-07-29T10:13:09.0550052+00:00 | 133.5ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +106.5ms |
| Confluent | 369,967,000 | 2026-07-29T10:13:09.1375799+00:00 | 131.9ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +106.5ms |
| Confluent | 369,978,000 | 2026-07-29T10:13:09.1475938+00:00 | 137.1ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +106.5ms |
| Confluent | 370,139,000 | 2026-07-29T10:13:09.2801082+00:00 | 103.8ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +106.5ms |
| Confluent | 370,188,000 | 2026-07-29T10:13:09.3201235+00:00 | 157.9ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +106.5ms |
| Confluent | 370,215,000 | 2026-07-29T10:13:09.3424475+00:00 | 128.6ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +106.5ms |
| Confluent | 370,218,000 | 2026-07-29T10:13:09.3444192+00:00 | 153.4ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +106.5ms |
| Confluent | 370,238,000 | 2026-07-29T10:13:09.3610995+00:00 | 152.6ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +106.5ms |
| Confluent | 370,240,000 | 2026-07-29T10:13:09.362449+00:00 | 117.2ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +106.5ms |
| Confluent | 370,283,000 | 2026-07-29T10:13:09.3970134+00:00 | 130.8ms | GC pause | - | - | 407.4s / 1,056,460 msg/s | Gen2 +0 / pause +106.5ms |
| Confluent | 370,321,000 | 2026-07-29T10:13:09.4428058+00:00 | 173.9ms | GC pause | - | - | 408.4s / 981,775 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 370,350,000 | 2026-07-29T10:13:09.4738724+00:00 | 121.3ms | GC pause | - | - | 408.4s / 981,775 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 370,420,000 | 2026-07-29T10:13:09.5410263+00:00 | 104.3ms | GC pause | - | - | 408.4s / 981,775 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 370,436,000 | 2026-07-29T10:13:09.5597136+00:00 | 109.4ms | GC pause | - | - | 408.4s / 981,775 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 370,461,000 | 2026-07-29T10:13:09.5844692+00:00 | 157.6ms | GC pause | - | - | 408.4s / 981,775 msg/s | Gen2 +0 / pause +76.6ms |
| Confluent | 370,469,000 | 2026-07-29T10:13:09.5931125+00:00 | 109.5ms | GC pause | - | - | 408.4s / 981,775 msg/s | Gen2 +0 / pause +76.6ms |
| Confluent | 370,491,000 | 2026-07-29T10:13:09.613222+00:00 | 153.7ms | GC pause | - | - | 408.4s / 981,775 msg/s | Gen2 +0 / pause +76.6ms |
| Confluent | 370,637,000 | 2026-07-29T10:13:09.770069+00:00 | 132.6ms | GC pause | - | - | 408.4s / 981,775 msg/s | Gen2 +0 / pause +76.6ms |
| Confluent | 370,697,000 | 2026-07-29T10:13:09.8363103+00:00 | 131.0ms | GC pause | - | - | 408.4s / 981,775 msg/s | Gen2 +0 / pause +76.6ms |
| Confluent | 373,768,000 | 2026-07-29T10:13:12.8889452+00:00 | 107.9ms | GC pause | - | - | 411.4s / 1,110,690 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 373,775,000 | 2026-07-29T10:13:12.8965835+00:00 | 126.7ms | GC pause | - | - | 411.4s / 1,110,690 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 373,776,000 | 2026-07-29T10:13:12.8971626+00:00 | 127.7ms | GC pause | - | - | 411.4s / 1,110,690 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 373,786,000 | 2026-07-29T10:13:12.9047285+00:00 | 124.1ms | GC pause | - | - | 411.4s / 1,110,690 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 373,795,000 | 2026-07-29T10:13:12.9127061+00:00 | 125.2ms | GC pause | - | - | 411.4s / 1,110,690 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 373,905,000 | 2026-07-29T10:13:13.0037226+00:00 | 130.0ms | GC pause | - | - | 411.4s / 1,110,690 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 374,069,000 | 2026-07-29T10:13:13.1708828+00:00 | 109.4ms | GC pause | - | - | 411.4s / 1,110,690 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 374,266,000 | 2026-07-29T10:13:13.3466652+00:00 | 117.6ms | GC pause | - | - | 411.4s / 1,110,690 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 374,285,000 | 2026-07-29T10:13:13.3636787+00:00 | 127.7ms | GC pause | - | - | 411.4s / 1,110,690 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 374,296,000 | 2026-07-29T10:13:13.373215+00:00 | 134.8ms | GC pause | - | - | 411.4s / 1,110,690 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 374,310,000 | 2026-07-29T10:13:13.3930126+00:00 | 102.0ms | GC pause | - | - | 411.4s / 1,110,690 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 374,359,000 | 2026-07-29T10:13:13.4353006+00:00 | 125.2ms | GC pause | - | - | 411.4s / 1,110,690 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 374,369,000 | 2026-07-29T10:13:13.4470103+00:00 | 119.3ms | GC pause | - | - | 411.4s / 1,110,690 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 374,411,000 | 2026-07-29T10:13:13.4858751+00:00 | 107.1ms | GC pause | - | - | 412.4s / 1,108,263 msg/s | Gen2 +0 / pause +148.8ms |
| Confluent | 374,441,000 | 2026-07-29T10:13:13.5185556+00:00 | 104.0ms | GC pause | - | - | 412.4s / 1,108,263 msg/s | Gen2 +0 / pause +148.8ms |
| Confluent | 374,499,000 | 2026-07-29T10:13:13.5897232+00:00 | 125.6ms | GC pause | - | - | 412.4s / 1,108,263 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 374,511,000 | 2026-07-29T10:13:13.6009994+00:00 | 103.8ms | GC pause | - | - | 412.4s / 1,108,263 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 374,579,000 | 2026-07-29T10:13:13.6714531+00:00 | 107.5ms | GC pause | - | - | 412.4s / 1,108,263 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 374,623,000 | 2026-07-29T10:13:13.7125484+00:00 | 100.6ms | GC pause | - | - | 412.4s / 1,108,263 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 374,968,000 | 2026-07-29T10:13:14.0522778+00:00 | 100.5ms | GC pause | - | - | 412.4s / 1,108,263 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 375,038,000 | 2026-07-29T10:13:14.1075395+00:00 | 102.1ms | GC pause | - | - | 412.4s / 1,108,263 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 375,268,000 | 2026-07-29T10:13:14.3088473+00:00 | 107.2ms | GC pause | - | - | 412.4s / 1,108,263 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 375,337,000 | 2026-07-29T10:13:14.3644882+00:00 | 118.3ms | GC pause | - | - | 412.4s / 1,108,263 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 375,351,000 | 2026-07-29T10:13:14.3742635+00:00 | 120.3ms | GC pause | - | - | 412.4s / 1,108,263 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 375,457,000 | 2026-07-29T10:13:14.4570176+00:00 | 116.5ms | GC pause | - | - | 412.4s / 1,108,263 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 375,587,000 | 2026-07-29T10:13:14.5682767+00:00 | 121.0ms | GC pause | - | - | 413.4s / 759,173 msg/s | Gen2 +0 / pause +181.3ms |
| Confluent | 375,638,000 | 2026-07-29T10:13:14.6167005+00:00 | 102.4ms | GC pause | - | - | 413.4s / 759,173 msg/s | Gen2 +0 / pause +102.5ms |
| Confluent | 377,246,000 | 2026-07-29T10:13:16.427036+00:00 | 156.3ms | GC pause | - | - | 415.4s / 957,506 msg/s | Gen2 +0 / pause +167.8ms |
| Confluent | 377,349,000 | 2026-07-29T10:13:16.5543016+00:00 | 115.0ms | GC pause | - | - | 415.4s / 957,506 msg/s | Gen2 +0 / pause +167.8ms |
| Confluent | 377,419,000 | 2026-07-29T10:13:16.6282865+00:00 | 100.1ms | GC pause | - | - | 415.4s / 957,506 msg/s | Gen2 +0 / pause +76.8ms |
| Confluent | 378,771,000 | 2026-07-29T10:13:17.9590625+00:00 | 106.6ms | GC pause | - | - | 416.4s / 1,087,568 msg/s | Gen2 +0 / pause +95.0ms |
| Confluent | 378,917,000 | 2026-07-29T10:13:18.0772496+00:00 | 122.3ms | GC pause | - | - | 416.4s / 1,087,568 msg/s | Gen2 +0 / pause +95.0ms |
| Confluent | 378,973,000 | 2026-07-29T10:13:18.1157394+00:00 | 108.9ms | GC pause | - | - | 416.4s / 1,087,568 msg/s | Gen2 +0 / pause +95.0ms |
| Confluent | 378,982,000 | 2026-07-29T10:13:18.1214417+00:00 | 101.7ms | GC pause | - | - | 416.4s / 1,087,568 msg/s | Gen2 +0 / pause +95.0ms |
| Confluent | 379,024,000 | 2026-07-29T10:13:18.1747028+00:00 | 108.0ms | GC pause | - | - | 416.4s / 1,087,568 msg/s | Gen2 +0 / pause +95.0ms |
| Confluent | 379,074,000 | 2026-07-29T10:13:18.2207203+00:00 | 114.4ms | GC pause | - | - | 416.4s / 1,087,568 msg/s | Gen2 +0 / pause +95.0ms |
| Confluent | 379,101,000 | 2026-07-29T10:13:18.2450356+00:00 | 131.6ms | GC pause | - | - | 416.4s / 1,087,568 msg/s | Gen2 +0 / pause +95.0ms |
| Confluent | 379,210,000 | 2026-07-29T10:13:18.3366409+00:00 | 128.8ms | GC pause | - | - | 416.4s / 1,087,568 msg/s | Gen2 +0 / pause +95.0ms |
| Confluent | 379,228,000 | 2026-07-29T10:13:18.3498959+00:00 | 148.7ms | GC pause | - | - | 416.4s / 1,087,568 msg/s | Gen2 +0 / pause +95.0ms |
| Confluent | 379,230,000 | 2026-07-29T10:13:18.3520314+00:00 | 139.7ms | GC pause | - | - | 416.4s / 1,087,568 msg/s | Gen2 +0 / pause +95.0ms |
| Confluent | 379,238,000 | 2026-07-29T10:13:18.3577582+00:00 | 157.7ms | GC pause | - | - | 416.4s / 1,087,568 msg/s | Gen2 +0 / pause +95.0ms |
| Confluent | 379,276,000 | 2026-07-29T10:13:18.400121+00:00 | 107.0ms | GC pause | - | - | 416.4s / 1,087,568 msg/s | Gen2 +0 / pause +95.0ms |
| Confluent | 379,321,000 | 2026-07-29T10:13:18.4499432+00:00 | 179.4ms | GC pause | - | - | 417.4s / 834,745 msg/s | Gen2 +0 / pause +196.6ms |
| Confluent | 379,359,000 | 2026-07-29T10:13:18.4833535+00:00 | 137.1ms | GC pause | - | - | 417.4s / 834,745 msg/s | Gen2 +0 / pause +196.6ms |
| Confluent | 379,399,000 | 2026-07-29T10:13:18.5432218+00:00 | 116.1ms | GC pause | - | - | 417.4s / 834,745 msg/s | Gen2 +0 / pause +196.6ms |
| Confluent | 379,406,000 | 2026-07-29T10:13:18.5506023+00:00 | 110.0ms | GC pause | - | - | 417.4s / 834,745 msg/s | Gen2 +0 / pause +196.6ms |
| Confluent | 380,257,000 | 2026-07-29T10:13:19.5823416+00:00 | 104.2ms | GC pause | - | - | 418.4s / 951,260 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 380,437,000 | 2026-07-29T10:13:19.7440207+00:00 | 116.6ms | GC pause | - | - | 418.4s / 951,260 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 380,548,000 | 2026-07-29T10:13:19.8259486+00:00 | 136.5ms | GC pause | - | - | 418.4s / 951,260 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 380,587,000 | 2026-07-29T10:13:19.8558032+00:00 | 147.5ms | GC pause | - | - | 418.4s / 951,260 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 380,621,000 | 2026-07-29T10:13:19.8866107+00:00 | 145.7ms | GC pause | - | - | 418.4s / 951,260 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 380,637,000 | 2026-07-29T10:13:19.8980817+00:00 | 149.0ms | GC pause | - | - | 418.4s / 951,260 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 380,751,000 | 2026-07-29T10:13:20.0288653+00:00 | 126.1ms | GC pause | - | - | 418.4s / 951,260 msg/s | Gen2 +0 / pause +91.1ms |
| Confluent | 381,281,000 | 2026-07-29T10:13:20.6391228+00:00 | 100.4ms | GC pause | - | - | 419.4s / 1,086,366 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 381,504,000 | 2026-07-29T10:13:20.8557026+00:00 | 106.4ms | GC pause | - | - | 419.4s / 1,086,366 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 381,514,000 | 2026-07-29T10:13:20.8660081+00:00 | 101.3ms | GC pause | - | - | 419.4s / 1,086,366 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 381,944,000 | 2026-07-29T10:13:21.2466633+00:00 | 100.9ms | GC pause | - | - | 419.4s / 1,086,366 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 382,044,000 | 2026-07-29T10:13:21.3266368+00:00 | 119.8ms | GC pause | - | - | 419.4s / 1,086,366 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 382,087,000 | 2026-07-29T10:13:21.3544559+00:00 | 124.9ms | GC pause | - | - | 419.4s / 1,086,366 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 382,088,000 | 2026-07-29T10:13:21.3550112+00:00 | 128.4ms | GC pause | - | - | 419.4s / 1,086,366 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 382,222,000 | 2026-07-29T10:13:21.4960069+00:00 | 110.6ms | GC pause | - | - | 420.4s / 1,016,836 msg/s | Gen2 +0 / pause +167.4ms |
| Confluent | 382,257,000 | 2026-07-29T10:13:21.533047+00:00 | 129.8ms | GC pause | - | - | 420.4s / 1,016,836 msg/s | Gen2 +0 / pause +167.4ms |
| Confluent | 382,278,000 | 2026-07-29T10:13:21.5617885+00:00 | 121.2ms | GC pause | - | - | 420.4s / 1,016,836 msg/s | Gen2 +0 / pause +167.4ms |
| Confluent | 382,501,000 | 2026-07-29T10:13:21.7588247+00:00 | 115.4ms | GC pause | - | - | 420.4s / 1,016,836 msg/s | Gen2 +0 / pause +97.6ms |
| Confluent | 382,604,000 | 2026-07-29T10:13:21.8690832+00:00 | 109.4ms | GC pause | - | - | 420.4s / 1,016,836 msg/s | Gen2 +0 / pause +97.6ms |
| Confluent | 382,634,000 | 2026-07-29T10:13:21.8948581+00:00 | 121.5ms | GC pause | - | - | 420.4s / 1,016,836 msg/s | Gen2 +0 / pause +97.6ms |
| Confluent | 382,668,000 | 2026-07-29T10:13:21.9204154+00:00 | 124.8ms | GC pause | - | - | 420.4s / 1,016,836 msg/s | Gen2 +0 / pause +97.6ms |
| Confluent | 383,097,000 | 2026-07-29T10:13:22.3615092+00:00 | 118.5ms | GC pause | - | - | 420.4s / 1,016,836 msg/s | Gen2 +0 / pause +97.6ms |
| Confluent | 383,117,000 | 2026-07-29T10:13:22.3829555+00:00 | 122.2ms | GC pause | - | - | 420.4s / 1,016,836 msg/s | Gen2 +0 / pause +97.6ms |
| Confluent | 383,148,000 | 2026-07-29T10:13:22.4145197+00:00 | 127.1ms | GC pause | - | - | 420.4s / 1,016,836 msg/s | Gen2 +0 / pause +97.6ms |
| Confluent | 383,354,000 | 2026-07-29T10:13:22.6244919+00:00 | 118.1ms | GC pause | - | - | 421.4s / 1,119,214 msg/s | Gen2 +0 / pause +79.7ms |
| Confluent | 384,323,000 | 2026-07-29T10:13:23.465907+00:00 | 104.7ms | GC pause | - | - | 421.4s / 1,119,214 msg/s | Gen2 +0 / pause +79.7ms |
| Confluent | 384,357,000 | 2026-07-29T10:13:23.4890308+00:00 | 141.1ms | GC pause | - | - | 422.4s / 1,111,178 msg/s | Gen2 +0 / pause +205.1ms |
| Confluent | 384,362,000 | 2026-07-29T10:13:23.4927917+00:00 | 103.5ms | GC pause | - | - | 422.4s / 1,111,178 msg/s | Gen2 +0 / pause +205.1ms |
| Confluent | 384,367,000 | 2026-07-29T10:13:23.5005011+00:00 | 131.6ms | GC pause | - | - | 422.4s / 1,111,178 msg/s | Gen2 +0 / pause +205.1ms |
| Confluent | 384,387,000 | 2026-07-29T10:13:23.5278052+00:00 | 124.5ms | GC pause | - | - | 422.4s / 1,111,178 msg/s | Gen2 +0 / pause +205.1ms |
| Confluent | 388,241,000 | 2026-07-29T10:13:27.351202+00:00 | 100.4ms | GC pause | - | - | 425.4s / 899,484 msg/s | Gen2 +0 / pause +100.2ms |
| Confluent | 391,801,000 | 2026-07-29T10:13:31.7197937+00:00 | 123.3ms | GC pause | - | - | 430.4s / 904,013 msg/s | Gen2 +0 / pause +92.0ms |
| Confluent | 391,824,000 | 2026-07-29T10:13:31.7383482+00:00 | 110.9ms | GC pause | - | - | 430.4s / 904,013 msg/s | Gen2 +0 / pause +92.0ms |
| Confluent | 391,838,000 | 2026-07-29T10:13:31.7526365+00:00 | 129.4ms | GC pause | - | - | 430.4s / 904,013 msg/s | Gen2 +0 / pause +92.0ms |
| Confluent | 391,898,000 | 2026-07-29T10:13:31.8058608+00:00 | 131.3ms | GC pause | - | - | 430.4s / 904,013 msg/s | Gen2 +0 / pause +92.0ms |
| Confluent | 391,912,000 | 2026-07-29T10:13:31.814361+00:00 | 108.0ms | GC pause | - | - | 430.4s / 904,013 msg/s | Gen2 +0 / pause +92.0ms |
| Confluent | 391,947,000 | 2026-07-29T10:13:31.8435824+00:00 | 136.2ms | GC pause | - | - | 430.4s / 904,013 msg/s | Gen2 +0 / pause +92.0ms |
| Confluent | 391,991,000 | 2026-07-29T10:13:31.8888925+00:00 | 136.6ms | GC pause | - | - | 430.4s / 904,013 msg/s | Gen2 +0 / pause +92.0ms |
| Confluent | 392,024,000 | 2026-07-29T10:13:31.9211707+00:00 | 105.1ms | GC pause | - | - | 430.4s / 904,013 msg/s | Gen2 +0 / pause +92.0ms |
| Confluent | 392,111,000 | 2026-07-29T10:13:31.9933838+00:00 | 131.7ms | GC pause | - | - | 430.4s / 904,013 msg/s | Gen2 +0 / pause +92.0ms |
| Confluent | 392,257,000 | 2026-07-29T10:13:32.1253017+00:00 | 122.6ms | GC pause | - | - | 430.4s / 904,013 msg/s | Gen2 +0 / pause +92.0ms |
| Confluent | 416,600,000 | 2026-07-29T10:14:01.0656946+00:00 | 102.3ms | GC pause | - | - | 459.4s / 1,031,131 msg/s | Gen2 +0 / pause +106.1ms |
| Confluent | 417,999,000 | 2026-07-29T10:14:02.6999929+00:00 | 102.6ms | GC pause | - | - | 461.4s / 1,171,734 msg/s | Gen2 +0 / pause +85.4ms |
| Confluent | 418,043,000 | 2026-07-29T10:14:02.7286138+00:00 | 116.3ms | GC pause | - | - | 461.4s / 1,171,734 msg/s | Gen2 +0 / pause +85.4ms |
| Confluent | 418,063,000 | 2026-07-29T10:14:02.740798+00:00 | 115.2ms | GC pause | - | - | 461.4s / 1,171,734 msg/s | Gen2 +0 / pause +85.4ms |
| Confluent | 418,080,000 | 2026-07-29T10:14:02.7536775+00:00 | 117.9ms | GC pause | - | - | 461.4s / 1,171,734 msg/s | Gen2 +0 / pause +85.4ms |
| Confluent | 418,165,000 | 2026-07-29T10:14:02.8256887+00:00 | 107.5ms | GC pause | - | - | 461.4s / 1,171,734 msg/s | Gen2 +0 / pause +85.4ms |
| Confluent | 419,258,000 | 2026-07-29T10:14:03.761176+00:00 | 160.2ms | GC pause | - | - | 462.4s / 1,057,597 msg/s | Gen2 +0 / pause +85.8ms |
| Confluent | 419,358,000 | 2026-07-29T10:14:03.8543851+00:00 | 156.5ms | GC pause | - | - | 462.4s / 1,057,597 msg/s | Gen2 +0 / pause +85.8ms |
| Confluent | 419,401,000 | 2026-07-29T10:14:03.8889016+00:00 | 158.0ms | GC pause | - | - | 462.4s / 1,057,597 msg/s | Gen2 +0 / pause +85.8ms |
| Confluent | 419,461,000 | 2026-07-29T10:14:03.9430552+00:00 | 149.6ms | GC pause | - | - | 462.4s / 1,057,597 msg/s | Gen2 +0 / pause +85.8ms |
| Confluent | 419,477,000 | 2026-07-29T10:14:03.95917+00:00 | 142.5ms | GC pause | - | - | 462.4s / 1,057,597 msg/s | Gen2 +0 / pause +85.8ms |
| Confluent | 419,541,000 | 2026-07-29T10:14:04.0143347+00:00 | 151.3ms | GC pause | - | - | 462.4s / 1,057,597 msg/s | Gen2 +0 / pause +85.8ms |
| Confluent | 419,557,000 | 2026-07-29T10:14:04.0256685+00:00 | 146.1ms | GC pause | - | - | 462.4s / 1,057,597 msg/s | Gen2 +0 / pause +85.8ms |
| Confluent | 419,598,000 | 2026-07-29T10:14:04.0772819+00:00 | 131.7ms | GC pause | - | - | 462.4s / 1,057,597 msg/s | Gen2 +0 / pause +85.8ms |
| Confluent | 419,707,000 | 2026-07-29T10:14:04.1700228+00:00 | 144.3ms | GC pause | - | - | 462.4s / 1,057,597 msg/s | Gen2 +0 / pause +85.8ms |
| Confluent | 419,808,000 | 2026-07-29T10:14:04.258214+00:00 | 161.8ms | GC pause | - | - | 462.4s / 1,057,597 msg/s | Gen2 +0 / pause +85.8ms |
| Confluent | 449,068,000 | 2026-07-29T10:14:36.6390495+00:00 | 109.0ms | GC pause | - | - | 495.4s / 917,505 msg/s | Gen2 +0 / pause +134.5ms |
| Confluent | 449,121,000 | 2026-07-29T10:14:36.6939619+00:00 | 107.3ms | GC pause | - | - | 495.4s / 917,505 msg/s | Gen2 +0 / pause +134.5ms |
| Confluent | 486,583,000 | 2026-07-29T10:15:15.9663915+00:00 | 100.8ms | GC pause | - | - | 534.4s / 940,640 msg/s | Gen2 +0 / pause +120.6ms |
| Confluent | 486,584,000 | 2026-07-29T10:15:15.9670999+00:00 | 100.2ms | GC pause | - | - | 534.4s / 940,640 msg/s | Gen2 +0 / pause +120.6ms |
| Confluent | 486,587,000 | 2026-07-29T10:15:15.971047+00:00 | 111.9ms | GC pause | - | - | 534.4s / 940,640 msg/s | Gen2 +0 / pause +120.6ms |
| Confluent | 617,028,000 | 2026-07-29T10:30:34.4439378+00:00 | 123.6ms | GC pause | - | - | 551.4s / 659,001 msg/s | Gen2 +0 / pause +122.2ms |
| Confluent | 617,082,000 | 2026-07-29T10:30:34.5239788+00:00 | 121.8ms | GC pause | - | - | 552.4s / 511,502 msg/s | Gen2 +0 / pause +284.1ms |
| Confluent | 617,091,000 | 2026-07-29T10:30:34.5372546+00:00 | 133.8ms | GC pause | - | - | 552.4s / 511,502 msg/s | Gen2 +0 / pause +284.1ms |
| Confluent | 617,114,000 | 2026-07-29T10:30:34.5665403+00:00 | 126.1ms | GC pause | - | - | 552.4s / 511,502 msg/s | Gen2 +0 / pause +284.1ms |
| Confluent | 617,179,000 | 2026-07-29T10:30:34.659265+00:00 | 151.3ms | GC pause | - | - | 552.4s / 511,502 msg/s | Gen2 +0 / pause +161.9ms |
| Confluent | 617,248,000 | 2026-07-29T10:30:34.7645793+00:00 | 176.2ms | GC pause | - | - | 552.4s / 511,502 msg/s | Gen2 +0 / pause +161.9ms |
| Confluent | 617,277,000 | 2026-07-29T10:30:34.8012582+00:00 | 191.7ms | GC pause | - | - | 552.4s / 511,502 msg/s | Gen2 +0 / pause +161.9ms |
| Confluent | 617,281,000 | 2026-07-29T10:30:34.8067122+00:00 | 209.0ms | GC pause | - | - | 552.4s / 511,502 msg/s | Gen2 +0 / pause +161.9ms |
| Confluent | 617,312,000 | 2026-07-29T10:30:34.8730188+00:00 | 193.6ms | GC pause | - | - | 552.4s / 511,502 msg/s | Gen2 +0 / pause +161.9ms |
| Confluent | 617,366,000 | 2026-07-29T10:30:35.0209823+00:00 | 123.9ms | GC pause | - | - | 552.4s / 511,502 msg/s | Gen2 +0 / pause +161.9ms |
| Confluent | 617,382,000 | 2026-07-29T10:30:35.0696676+00:00 | 100.6ms | GC pause | - | - | 552.4s / 511,502 msg/s | Gen2 +0 / pause +161.9ms |
| Confluent | 617,458,000 | 2026-07-29T10:30:35.210319+00:00 | 116.8ms | GC pause | - | - | 552.4s / 511,502 msg/s | Gen2 +0 / pause +161.9ms |
| Confluent | 625,235,000 | 2026-07-29T10:30:50.6910754+00:00 | 100.7ms | GC pause | - | - | 568.5s / 448,398 msg/s | Gen2 +0 / pause +189.4ms |
| Confluent | 626,245,000 | 2026-07-29T10:30:52.8155524+00:00 | 121.2ms | GC pause | - | - | 570.5s / 542,781 msg/s | Gen2 +0 / pause +149.3ms |
| Confluent | 626,251,000 | 2026-07-29T10:30:52.8442968+00:00 | 114.1ms | GC pause | - | - | 570.5s / 542,781 msg/s | Gen2 +0 / pause +149.3ms |
| Confluent | 626,265,000 | 2026-07-29T10:30:52.8668952+00:00 | 119.7ms | GC pause | - | - | 570.5s / 542,781 msg/s | Gen2 +0 / pause +149.3ms |
| Confluent | 626,373,000 | 2026-07-29T10:30:53.0237526+00:00 | 183.2ms | GC pause | - | - | 570.5s / 542,781 msg/s | Gen2 +0 / pause +149.3ms |
| Confluent | 626,390,000 | 2026-07-29T10:30:53.0527442+00:00 | 189.6ms | GC pause | - | - | 570.5s / 542,781 msg/s | Gen2 +0 / pause +149.3ms |
| Confluent | 626,428,000 | 2026-07-29T10:30:53.119786+00:00 | 218.4ms | GC pause | - | - | 570.5s / 542,781 msg/s | Gen2 +0 / pause +149.3ms |
| Confluent | 626,464,000 | 2026-07-29T10:30:53.1792726+00:00 | 237.8ms | GC pause | - | - | 570.5s / 542,781 msg/s | Gen2 +0 / pause +149.3ms |
| Confluent | 626,500,000 | 2026-07-29T10:30:53.2548628+00:00 | 229.3ms | GC pause | - | - | 570.5s / 542,781 msg/s | Gen2 +0 / pause +149.3ms |
| Confluent | 626,505,000 | 2026-07-29T10:30:53.2639059+00:00 | 225.2ms | GC pause | - | - | 570.5s / 542,781 msg/s | Gen2 +0 / pause +149.3ms |
| Confluent | 626,511,000 | 2026-07-29T10:30:53.2743009+00:00 | 249.9ms | GC pause | - | - | 570.5s / 542,781 msg/s | Gen2 +0 / pause +149.3ms |
| Confluent | 626,526,000 | 2026-07-29T10:30:53.3037194+00:00 | 231.5ms | GC pause | - | - | 570.5s / 542,781 msg/s | Gen2 +0 / pause +149.3ms |
| Confluent | 626,538,000 | 2026-07-29T10:30:53.3241275+00:00 | 292.2ms | GC pause | - | - | 570.5s / 542,781 msg/s | Gen2 +0 / pause +149.3ms |
| Confluent | 626,540,000 | 2026-07-29T10:30:53.3266998+00:00 | 254.1ms | GC pause | - | - | 570.5s / 542,781 msg/s | Gen2 +0 / pause +149.3ms |
| Confluent | 626,704,000 | 2026-07-29T10:30:53.7289342+00:00 | 217.0ms | GC pause | - | - | 571.5s / 390,437 msg/s | Gen2 +0 / pause +273.9ms |
| Confluent | 626,707,000 | 2026-07-29T10:30:53.7385923+00:00 | 249.4ms | GC pause | - | - | 571.5s / 390,437 msg/s | Gen2 +0 / pause +273.9ms |
| Confluent | 626,721,000 | 2026-07-29T10:30:53.7671699+00:00 | 237.2ms | GC pause | - | - | 571.5s / 390,437 msg/s | Gen2 +0 / pause +273.9ms |
| Confluent | 626,782,000 | 2026-07-29T10:30:53.8864022+00:00 | 164.2ms | GC pause | - | - | 571.5s / 390,437 msg/s | Gen2 +0 / pause +273.9ms |
| Confluent | 626,807,000 | 2026-07-29T10:30:53.9554239+00:00 | 223.3ms | GC pause | - | - | 571.5s / 390,437 msg/s | Gen2 +0 / pause +273.9ms |
| Confluent | 626,915,000 | 2026-07-29T10:30:54.2000155+00:00 | 136.8ms | GC pause | - | - | 571.5s / 390,437 msg/s | Gen2 +0 / pause +273.9ms |
| Confluent | 626,926,000 | 2026-07-29T10:30:54.2302057+00:00 | 136.5ms | GC pause | - | - | 571.5s / 390,437 msg/s | Gen2 +0 / pause +273.9ms |
| Confluent | 626,947,000 | 2026-07-29T10:30:54.2910309+00:00 | 202.2ms | GC pause | - | - | 571.5s / 390,437 msg/s | Gen2 +0 / pause +273.9ms |
| Confluent | 627,017,000 | 2026-07-29T10:30:54.5308793+00:00 | 125.6ms | GC pause | - | - | 572.5s / 465,124 msg/s | Gen2 +0 / pause +448.9ms |
| Confluent | 628,336,000 | 2026-07-29T10:30:57.3077362+00:00 | 120.3ms | GC pause | - | - | 574.5s / 462,088 msg/s | Gen2 +0 / pause +114.0ms |
| Confluent | 628,417,000 | 2026-07-29T10:30:57.5198353+00:00 | 103.1ms | GC pause | - | - | 574.5s / 462,088 msg/s | Gen2 +0 / pause +114.0ms |
| Confluent | 635,193,000 | 2026-07-29T10:31:11.8421619+00:00 | 107.4ms | GC pause | - | - | 589.5s / 462,371 msg/s | Gen2 +0 / pause +147.3ms |
| Confluent | 635,205,000 | 2026-07-29T10:31:11.8597947+00:00 | 105.9ms | GC pause | - | - | 589.5s / 462,371 msg/s | Gen2 +0 / pause +147.3ms |
| Confluent | 635,269,000 | 2026-07-29T10:31:11.9623592+00:00 | 147.7ms | GC pause | - | - | 589.5s / 462,371 msg/s | Gen2 +0 / pause +147.3ms |
| Confluent | 635,276,000 | 2026-07-29T10:31:11.9741152+00:00 | 144.5ms | GC pause | - | - | 589.5s / 462,371 msg/s | Gen2 +0 / pause +147.3ms |
| Confluent | 635,280,000 | 2026-07-29T10:31:11.9820868+00:00 | 156.3ms | GC pause | - | - | 589.5s / 462,371 msg/s | Gen2 +0 / pause +147.3ms |
| Confluent | 635,314,000 | 2026-07-29T10:31:12.0565167+00:00 | 128.0ms | GC pause | - | - | 589.5s / 462,371 msg/s | Gen2 +0 / pause +147.3ms |
| Confluent | 635,319,000 | 2026-07-29T10:31:12.0692514+00:00 | 116.7ms | GC pause | - | - | 589.5s / 462,371 msg/s | Gen2 +0 / pause +147.3ms |
| Confluent | 636,270,000 | 2026-07-29T10:31:14.2369363+00:00 | 118.2ms | GC pause | - | - | 591.5s / 428,588 msg/s | Gen2 +0 / pause +187.0ms |
| Confluent | 636,751,000 | 2026-07-29T10:31:15.3146526+00:00 | 124.3ms | GC pause | - | - | 592.5s / 446,596 msg/s | Gen2 +0 / pause +165.0ms |
| Confluent | 637,019,000 | 2026-07-29T10:31:15.9319135+00:00 | 115.4ms | GC pause | - | - | 593.5s / 424,578 msg/s | Gen2 +0 / pause +168.6ms |
| Confluent | 637,028,000 | 2026-07-29T10:31:15.9441234+00:00 | 172.1ms | GC pause | - | - | 593.5s / 424,578 msg/s | Gen2 +0 / pause +168.6ms |
| Confluent | 637,046,000 | 2026-07-29T10:31:15.977814+00:00 | 129.9ms | GC pause | - | - | 593.5s / 424,578 msg/s | Gen2 +0 / pause +168.6ms |
| Confluent | 637,068,000 | 2026-07-29T10:31:16.0238694+00:00 | 193.2ms | GC pause | - | - | 593.5s / 424,578 msg/s | Gen2 +0 / pause +168.6ms |
| Confluent | 637,069,000 | 2026-07-29T10:31:16.0295309+00:00 | 134.2ms | GC pause | - | - | 593.5s / 424,578 msg/s | Gen2 +0 / pause +168.6ms |
| Confluent | 637,089,000 | 2026-07-29T10:31:16.0929401+00:00 | 107.1ms | GC pause | - | - | 593.5s / 424,578 msg/s | Gen2 +0 / pause +168.6ms |
| Confluent | 638,173,000 | 2026-07-29T10:31:18.5452081+00:00 | 103.6ms | GC pause | - | - | 595.5s / 498,152 msg/s | Gen2 +0 / pause +152.6ms |
| Confluent | 638,237,000 | 2026-07-29T10:31:18.6700168+00:00 | 118.8ms | GC pause | - | - | 596.5s / 440,924 msg/s | Gen2 +0 / pause +325.9ms |
| Confluent | 638,246,000 | 2026-07-29T10:31:18.6840843+00:00 | 104.6ms | GC pause | - | - | 596.5s / 440,924 msg/s | Gen2 +0 / pause +173.3ms |
| Confluent | 638,303,000 | 2026-07-29T10:31:18.8075987+00:00 | 122.8ms | GC pause | - | - | 596.5s / 440,924 msg/s | Gen2 +0 / pause +173.3ms |
| Confluent | 638,351,000 | 2026-07-29T10:31:18.9284592+00:00 | 118.9ms | GC pause | - | - | 596.5s / 440,924 msg/s | Gen2 +0 / pause +173.3ms |
| Confluent | 638,566,000 | 2026-07-29T10:31:19.4097209+00:00 | 104.1ms | GC pause | - | - | 596.5s / 440,924 msg/s | Gen2 +0 / pause +173.3ms |
| Confluent | 641,737,000 | 2026-07-29T10:31:26.5395824+00:00 | 136.2ms | GC pause | - | - | 603.5s / 474,797 msg/s | Gen2 +0 / pause +135.3ms |
| Confluent | 641,741,000 | 2026-07-29T10:31:26.546535+00:00 | 132.0ms | GC pause | - | - | 603.5s / 474,797 msg/s | Gen2 +0 / pause +135.3ms |
| Confluent | 641,795,000 | 2026-07-29T10:31:26.6425128+00:00 | 173.6ms | GC pause | - | - | 604.5s / 404,716 msg/s | Gen2 +0 / pause +332.8ms |
| Confluent | 641,805,000 | 2026-07-29T10:31:26.6634019+00:00 | 164.0ms | GC pause | - | - | 604.5s / 404,716 msg/s | Gen2 +0 / pause +332.8ms |
| Confluent | 641,813,000 | 2026-07-29T10:31:26.671012+00:00 | 188.6ms | GC pause | - | - | 604.5s / 404,716 msg/s | Gen2 +0 / pause +332.8ms |
| Confluent | 641,934,000 | 2026-07-29T10:31:26.9109416+00:00 | 205.2ms | GC pause | - | - | 604.5s / 404,716 msg/s | Gen2 +0 / pause +197.5ms |
| Confluent | 641,986,000 | 2026-07-29T10:31:26.994812+00:00 | 255.1ms | GC pause | - | - | 604.5s / 404,716 msg/s | Gen2 +0 / pause +197.5ms |
| Confluent | 642,026,000 | 2026-07-29T10:31:27.0538708+00:00 | 257.2ms | GC pause | - | - | 604.5s / 404,716 msg/s | Gen2 +0 / pause +197.5ms |
| Confluent | 642,074,000 | 2026-07-29T10:31:27.1910681+00:00 | 223.8ms | GC pause | - | - | 604.5s / 404,716 msg/s | Gen2 +0 / pause +197.5ms |
| Confluent | 642,078,000 | 2026-07-29T10:31:27.202873+00:00 | 255.6ms | GC pause | - | - | 604.5s / 404,716 msg/s | Gen2 +0 / pause +197.5ms |
| Confluent | 642,080,000 | 2026-07-29T10:31:27.2106357+00:00 | 225.7ms | GC pause | - | - | 604.5s / 404,716 msg/s | Gen2 +0 / pause +197.5ms |
| Confluent | 642,092,000 | 2026-07-29T10:31:27.2577211+00:00 | 190.9ms | GC pause | - | - | 604.5s / 404,716 msg/s | Gen2 +0 / pause +197.5ms |
| Confluent | 642,097,000 | 2026-07-29T10:31:27.2766816+00:00 | 225.7ms | GC pause | - | - | 604.5s / 404,716 msg/s | Gen2 +0 / pause +197.5ms |
| Confluent | 642,175,000 | 2026-07-29T10:31:27.5141826+00:00 | 139.2ms | GC pause | - | - | 604.5s / 404,716 msg/s | Gen2 +0 / pause +197.5ms |
| Confluent | 642,327,000 | 2026-07-29T10:31:27.96364+00:00 | 107.3ms | GC pause | - | - | 605.5s / 406,353 msg/s | Gen2 +0 / pause +157.3ms |
| Confluent | 643,036,000 | 2026-07-29T10:31:29.5469068+00:00 | 100.6ms | GC pause | - | - | 606.5s / 449,439 msg/s | Gen2 +0 / pause +164.6ms |
| Confluent | 645,698,000 | 2026-07-29T10:31:35.4854864+00:00 | 105.4ms | GC pause | - | - | 612.5s / 514,939 msg/s | Gen2 +0 / pause +150.9ms |
| Confluent | 645,712,000 | 2026-07-29T10:31:35.511581+00:00 | 103.2ms | GC pause | - | - | 612.5s / 514,939 msg/s | Gen2 +0 / pause +150.9ms |
| Confluent | 645,721,000 | 2026-07-29T10:31:35.5268674+00:00 | 107.9ms | GC pause | - | - | 612.5s / 514,939 msg/s | Gen2 +0 / pause +150.9ms |
| Confluent | 645,776,000 | 2026-07-29T10:31:35.6197502+00:00 | 118.0ms | GC pause | - | - | 613.5s / 427,927 msg/s | Gen2 +0 / pause +321.4ms |
| Confluent | 645,825,000 | 2026-07-29T10:31:35.6841204+00:00 | 139.0ms | GC pause | - | - | 613.5s / 427,927 msg/s | Gen2 +0 / pause +321.4ms |
| Confluent | 645,866,000 | 2026-07-29T10:31:35.7359097+00:00 | 149.9ms | GC pause | - | - | 613.5s / 427,927 msg/s | Gen2 +0 / pause +170.5ms |
| Confluent | 645,876,000 | 2026-07-29T10:31:35.7507371+00:00 | 152.6ms | GC pause | - | - | 613.5s / 427,927 msg/s | Gen2 +0 / pause +170.5ms |
| Confluent | 645,884,000 | 2026-07-29T10:31:35.7607422+00:00 | 155.3ms | GC pause | - | - | 613.5s / 427,927 msg/s | Gen2 +0 / pause +170.5ms |
| Confluent | 645,921,000 | 2026-07-29T10:31:35.8686944+00:00 | 128.5ms | GC pause | - | - | 613.5s / 427,927 msg/s | Gen2 +0 / pause +170.5ms |
| Confluent | 647,027,000 | 2026-07-29T10:31:38.4489662+00:00 | 137.2ms | GC pause | - | - | 615.5s / 447,108 msg/s | Gen2 +0 / pause +206.5ms |
| Confluent | 647,114,000 | 2026-07-29T10:31:38.6307577+00:00 | 123.6ms | GC pause | - | - | 616.5s / 412,565 msg/s | Gen2 +0 / pause +408.9ms |
| Confluent | 649,946,000 | 2026-07-29T10:31:44.8525632+00:00 | 104.1ms | GC pause | - | - | 622.6s / 454,300 msg/s | Gen2 +0 / pause +130.4ms |
| Confluent | 649,996,000 | 2026-07-29T10:31:44.9366133+00:00 | 151.0ms | GC pause | - | - | 622.6s / 454,300 msg/s | Gen2 +0 / pause +130.4ms |
| Confluent | 650,074,000 | 2026-07-29T10:31:45.1358175+00:00 | 128.4ms | GC pause | - | - | 622.6s / 454,300 msg/s | Gen2 +0 / pause +130.4ms |
| Confluent | 650,111,000 | 2026-07-29T10:31:45.2170466+00:00 | 165.9ms | GC pause | - | - | 622.6s / 454,300 msg/s | Gen2 +0 / pause +130.4ms |
| Confluent | 650,144,000 | 2026-07-29T10:31:45.2661948+00:00 | 167.6ms | GC pause | - | - | 622.6s / 454,300 msg/s | Gen2 +0 / pause +130.4ms |
| Confluent | 650,156,000 | 2026-07-29T10:31:45.2826351+00:00 | 162.5ms | GC pause | - | - | 622.6s / 454,300 msg/s | Gen2 +0 / pause +130.4ms |
| Confluent | 650,204,000 | 2026-07-29T10:31:45.429706+00:00 | 123.3ms | GC pause | - | - | 622.6s / 454,300 msg/s | Gen2 +0 / pause +130.4ms |
| Confluent | 650,212,000 | 2026-07-29T10:31:45.4510901+00:00 | 112.0ms | GC pause | - | - | 622.6s / 454,300 msg/s | Gen2 +0 / pause +130.4ms |
| Confluent | 651,251,000 | 2026-07-29T10:31:47.634247+00:00 | 123.0ms | GC pause | - | - | 625.6s / 423,264 msg/s | Gen2 +0 / pause +361.2ms |
| Confluent | 651,253,000 | 2026-07-29T10:31:47.6384398+00:00 | 111.3ms | GC pause | - | - | 625.6s / 423,264 msg/s | Gen2 +0 / pause +361.2ms |
| Confluent | 654,498,000 | 2026-07-29T10:31:54.5990083+00:00 | 124.5ms | GC pause | - | - | 631.6s / 498,865 msg/s | Gen2 +0 / pause +162.5ms |
| Confluent | 654,544,000 | 2026-07-29T10:31:54.6815179+00:00 | 135.7ms | GC pause | - | - | 632.6s / 425,128 msg/s | Gen2 +0 / pause +300.4ms |
| Confluent | 654,567,000 | 2026-07-29T10:31:54.7220766+00:00 | 164.3ms | GC pause | - | - | 632.6s / 425,128 msg/s | Gen2 +0 / pause +300.4ms |
| Confluent | 654,570,000 | 2026-07-29T10:31:54.7295184+00:00 | 141.7ms | GC pause | - | - | 632.6s / 425,128 msg/s | Gen2 +0 / pause +300.4ms |
| Confluent | 654,589,000 | 2026-07-29T10:31:54.7773886+00:00 | 136.7ms | GC pause | - | - | 632.6s / 425,128 msg/s | Gen2 +0 / pause +137.9ms |
| Confluent | 654,590,000 | 2026-07-29T10:31:54.7789697+00:00 | 130.1ms | GC pause | - | - | 632.6s / 425,128 msg/s | Gen2 +0 / pause +137.9ms |
| Confluent | 654,595,000 | 2026-07-29T10:31:54.7863863+00:00 | 143.1ms | GC pause | - | - | 632.6s / 425,128 msg/s | Gen2 +0 / pause +137.9ms |
| Confluent | 654,599,000 | 2026-07-29T10:31:54.7932833+00:00 | 150.9ms | GC pause | - | - | 632.6s / 425,128 msg/s | Gen2 +0 / pause +137.9ms |
| Confluent | 654,638,000 | 2026-07-29T10:31:54.8497543+00:00 | 214.0ms | GC pause | - | - | 632.6s / 425,128 msg/s | Gen2 +0 / pause +137.9ms |
| Confluent | 654,697,000 | 2026-07-29T10:31:54.9921196+00:00 | 208.9ms | GC pause | - | - | 632.6s / 425,128 msg/s | Gen2 +0 / pause +137.9ms |
| Confluent | 654,714,000 | 2026-07-29T10:31:55.0248028+00:00 | 187.2ms | GC pause | - | - | 632.6s / 425,128 msg/s | Gen2 +0 / pause +137.9ms |
| Confluent | 654,769,000 | 2026-07-29T10:31:55.1638941+00:00 | 164.9ms | GC pause | - | - | 632.6s / 425,128 msg/s | Gen2 +0 / pause +137.9ms |
| Confluent | 654,796,000 | 2026-07-29T10:31:55.2485656+00:00 | 146.7ms | GC pause | - | - | 632.6s / 425,128 msg/s | Gen2 +0 / pause +137.9ms |
| Confluent | 654,808,000 | 2026-07-29T10:31:55.2717763+00:00 | 188.6ms | GC pause | - | - | 632.6s / 425,128 msg/s | Gen2 +0 / pause +137.9ms |
| Confluent | 654,817,000 | 2026-07-29T10:31:55.2899973+00:00 | 191.8ms | GC pause | - | - | 632.6s / 425,128 msg/s | Gen2 +0 / pause +137.9ms |
| Confluent | 654,971,000 | 2026-07-29T10:31:55.6607395+00:00 | 215.7ms | GC pause | - | - | 633.6s / 386,140 msg/s | Gen2 +0 / pause +332.4ms |
| Confluent | 655,004,000 | 2026-07-29T10:31:55.7541213+00:00 | 141.1ms | GC pause | - | - | 633.6s / 386,140 msg/s | Gen2 +0 / pause +332.4ms |
| Confluent | 655,021,000 | 2026-07-29T10:31:55.7976131+00:00 | 198.5ms | GC pause | - | - | 633.6s / 386,140 msg/s | Gen2 +0 / pause +194.5ms |
| Confluent | 655,038,000 | 2026-07-29T10:31:55.8436999+00:00 | 196.9ms | GC pause | - | - | 633.6s / 386,140 msg/s | Gen2 +0 / pause +194.5ms |
| Confluent | 655,059,000 | 2026-07-29T10:31:55.8768027+00:00 | 142.5ms | GC pause | - | - | 633.6s / 386,140 msg/s | Gen2 +0 / pause +194.5ms |
| Confluent | 655,123,000 | 2026-07-29T10:31:56.0344698+00:00 | 173.5ms | GC pause | - | - | 633.6s / 386,140 msg/s | Gen2 +0 / pause +194.5ms |
| Confluent | 655,208,000 | 2026-07-29T10:31:56.214817+00:00 | 282.0ms | GC pause | - | - | 633.6s / 386,140 msg/s | Gen2 +0 / pause +194.5ms |
| Confluent | 655,248,000 | 2026-07-29T10:31:56.3188528+00:00 | 279.0ms | GC pause | - | - | 633.6s / 386,140 msg/s | Gen2 +0 / pause +194.5ms |
| Confluent | 655,349,000 | 2026-07-29T10:31:56.6209259+00:00 | 150.8ms | GC pause | - | - | 634.6s / 395,677 msg/s | Gen2 +0 / pause +359.1ms |
| Confluent | 657,267,000 | 2026-07-29T10:32:00.9488445+00:00 | 107.5ms | GC pause | - | - | 638.6s / 519,318 msg/s | Gen2 +0 / pause +173.7ms |
| Confluent | 657,353,000 | 2026-07-29T10:32:01.1048853+00:00 | 102.7ms | GC pause | - | - | 638.6s / 519,318 msg/s | Gen2 +0 / pause +173.7ms |
| Confluent | 658,838,000 | 2026-07-29T10:32:04.4227163+00:00 | 113.2ms | GC pause | - | - | 641.6s / 440,210 msg/s | Gen2 +0 / pause +159.3ms |
| Confluent | 658,860,000 | 2026-07-29T10:32:04.4654977+00:00 | 112.3ms | GC pause | - | - | 641.6s / 440,210 msg/s | Gen2 +0 / pause +159.3ms |
| Confluent | 658,878,000 | 2026-07-29T10:32:04.5072937+00:00 | 124.2ms | GC pause | - | - | 641.6s / 440,210 msg/s | Gen2 +0 / pause +159.3ms |
| Confluent | 658,887,000 | 2026-07-29T10:32:04.5243055+00:00 | 120.0ms | GC pause | - | - | 641.6s / 440,210 msg/s | Gen2 +0 / pause +159.3ms |
| Confluent | 658,995,000 | 2026-07-29T10:32:04.8013108+00:00 | 101.8ms | GC pause | - | - | 642.6s / 375,926 msg/s | Gen2 +0 / pause +216.9ms |
| Confluent | 659,000,000 | 2026-07-29T10:32:04.8146978+00:00 | 108.8ms | GC pause | - | - | 642.6s / 375,926 msg/s | Gen2 +0 / pause +216.9ms |
| Confluent | 659,036,000 | 2026-07-29T10:32:04.874363+00:00 | 135.5ms | GC pause | - | - | 642.6s / 375,926 msg/s | Gen2 +0 / pause +216.9ms |
| Confluent | 659,060,000 | 2026-07-29T10:32:04.9272644+00:00 | 151.3ms | GC pause | - | - | 642.6s / 375,926 msg/s | Gen2 +0 / pause +216.9ms |
| Confluent | 659,090,000 | 2026-07-29T10:32:04.996894+00:00 | 155.8ms | GC pause | - | - | 642.6s / 375,926 msg/s | Gen2 +0 / pause +216.9ms |
| Confluent | 659,108,000 | 2026-07-29T10:32:05.0379278+00:00 | 197.9ms | GC pause | - | - | 642.6s / 375,926 msg/s | Gen2 +0 / pause +216.9ms |
| Confluent | 659,121,000 | 2026-07-29T10:32:05.0573288+00:00 | 201.0ms | GC pause | - | - | 642.6s / 375,926 msg/s | Gen2 +0 / pause +216.9ms |
| Confluent | 659,271,000 | 2026-07-29T10:32:05.494247+00:00 | 139.6ms | GC pause | - | - | 642.6s / 375,926 msg/s | Gen2 +0 / pause +216.9ms |
| Confluent | 659,329,000 | 2026-07-29T10:32:05.6288187+00:00 | 119.5ms | GC pause | - | - | 642.6s / 375,926 msg/s | Gen2 +0 / pause +216.9ms |
| Confluent | 662,258,000 | 2026-07-29T10:32:12.4303122+00:00 | 110.6ms | GC pause | - | - | 649.6s / 446,860 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 663,112,000 | 2026-07-29T10:32:14.2782999+00:00 | 118.9ms | GC pause | - | - | 651.6s / 467,176 msg/s | Gen2 +0 / pause +164.4ms |
| Confluent | 663,122,000 | 2026-07-29T10:32:14.2888475+00:00 | 127.6ms | GC pause | - | - | 651.6s / 467,176 msg/s | Gen2 +0 / pause +164.4ms |
| Confluent | 663,189,000 | 2026-07-29T10:32:14.3829663+00:00 | 184.6ms | GC pause | - | - | 651.6s / 467,176 msg/s | Gen2 +0 / pause +164.4ms |
| Confluent | 663,208,000 | 2026-07-29T10:32:14.423315+00:00 | 202.0ms | GC pause | - | - | 651.6s / 467,176 msg/s | Gen2 +0 / pause +164.4ms |
| Confluent | 663,215,000 | 2026-07-29T10:32:14.4392368+00:00 | 185.8ms | GC pause | - | - | 651.6s / 467,176 msg/s | Gen2 +0 / pause +164.4ms |
| Confluent | 663,216,000 | 2026-07-29T10:32:14.4415002+00:00 | 183.6ms | GC pause | - | - | 651.6s / 467,176 msg/s | Gen2 +0 / pause +164.4ms |
| Confluent | 663,251,000 | 2026-07-29T10:32:14.5698295+00:00 | 133.0ms | GC pause | - | - | 651.6s / 467,176 msg/s | Gen2 +0 / pause +164.4ms |
| Confluent | 665,125,000 | 2026-07-29T10:32:18.6298535+00:00 | 124.4ms | GC pause | - | - | 655.6s / 513,666 msg/s | Gen2 +0 / pause +153.8ms |
| Confluent | 665,133,000 | 2026-07-29T10:32:18.6428378+00:00 | 126.0ms | GC pause | - | - | 655.6s / 513,666 msg/s | Gen2 +0 / pause +153.8ms |
| Confluent | 665,149,000 | 2026-07-29T10:32:18.6762166+00:00 | 137.6ms | GC pause | - | - | 656.6s / 441,571 msg/s | Gen2 +0 / pause +340.7ms |
| Confluent | 665,158,000 | 2026-07-29T10:32:18.6982278+00:00 | 147.0ms | GC pause | - | - | 656.6s / 441,571 msg/s | Gen2 +0 / pause +340.7ms |
| Confluent | 665,167,000 | 2026-07-29T10:32:18.7194979+00:00 | 144.2ms | GC pause | - | - | 656.6s / 441,571 msg/s | Gen2 +0 / pause +340.7ms |
| Confluent | 665,548,000 | 2026-07-29T10:32:19.5699217+00:00 | 116.2ms | GC pause | - | - | 656.6s / 441,571 msg/s | Gen2 +0 / pause +186.9ms |
| Confluent | 665,616,000 | 2026-07-29T10:32:19.7329376+00:00 | 108.0ms | GC pause | - | - | 657.6s / 391,910 msg/s | Gen2 +0 / pause +373.8ms |
| Confluent | 666,639,000 | 2026-07-29T10:32:22.138283+00:00 | 101.4ms | GC pause | - | - | 659.6s / 456,748 msg/s | Gen2 +0 / pause +175.7ms |
| Confluent | 666,647,000 | 2026-07-29T10:32:22.1540614+00:00 | 112.1ms | GC pause | - | - | 659.6s / 456,748 msg/s | Gen2 +0 / pause +175.7ms |
| Confluent | 666,655,000 | 2026-07-29T10:32:22.1727916+00:00 | 105.9ms | GC pause | - | - | 659.6s / 456,748 msg/s | Gen2 +0 / pause +175.7ms |
| Confluent | 666,661,000 | 2026-07-29T10:32:22.1873007+00:00 | 116.9ms | GC pause | - | - | 659.6s / 456,748 msg/s | Gen2 +0 / pause +175.7ms |
| Confluent | 666,698,000 | 2026-07-29T10:32:22.2782104+00:00 | 110.3ms | GC pause | - | - | 659.6s / 456,748 msg/s | Gen2 +0 / pause +175.7ms |
| Confluent | 666,748,000 | 2026-07-29T10:32:22.3751171+00:00 | 114.2ms | GC pause | - | - | 659.6s / 456,748 msg/s | Gen2 +0 / pause +175.7ms |
| Confluent | 672,940,000 | 2026-07-29T10:32:35.8346041+00:00 | 108.7ms | GC pause | - | - | 673.6s / 406,839 msg/s | Gen2 +0 / pause +171.4ms |
| Confluent | 672,997,000 | 2026-07-29T10:32:35.9642162+00:00 | 100.5ms | GC pause | - | - | 673.6s / 406,839 msg/s | Gen2 +0 / pause +171.4ms |
| Confluent | 673,017,000 | 2026-07-29T10:32:35.9995563+00:00 | 113.2ms | GC pause | - | - | 673.6s / 406,839 msg/s | Gen2 +0 / pause +171.4ms |
| Confluent | 673,024,000 | 2026-07-29T10:32:36.0126413+00:00 | 107.2ms | GC pause | - | - | 673.6s / 406,839 msg/s | Gen2 +0 / pause +171.4ms |
| Confluent | 673,045,000 | 2026-07-29T10:32:36.0762375+00:00 | 101.7ms | GC pause | - | - | 673.6s / 406,839 msg/s | Gen2 +0 / pause +171.4ms |
| Confluent | 673,061,000 | 2026-07-29T10:32:36.1181572+00:00 | 105.0ms | GC pause | - | - | 673.6s / 406,839 msg/s | Gen2 +0 / pause +171.4ms |
| Confluent | 673,137,000 | 2026-07-29T10:32:36.317193+00:00 | 101.6ms | GC pause | - | - | 673.6s / 406,839 msg/s | Gen2 +0 / pause +171.4ms |
| Confluent | 673,197,000 | 2026-07-29T10:32:36.4495734+00:00 | 112.0ms | GC pause | - | - | 673.6s / 406,839 msg/s | Gen2 +0 / pause +171.4ms |
| Confluent | 674,334,000 | 2026-07-29T10:32:38.8849174+00:00 | 107.4ms | GC pause | - | - | 676.6s / 451,295 msg/s | Gen2 +0 / pause +172.5ms |
| Confluent | 674,384,000 | 2026-07-29T10:32:38.9838609+00:00 | 142.3ms | GC pause | - | - | 676.6s / 451,295 msg/s | Gen2 +0 / pause +172.5ms |
| Confluent | 674,403,000 | 2026-07-29T10:32:39.0254069+00:00 | 141.8ms | GC pause | - | - | 676.6s / 451,295 msg/s | Gen2 +0 / pause +172.5ms |
| Confluent | 674,515,000 | 2026-07-29T10:32:39.3011323+00:00 | 128.6ms | GC pause | - | - | 676.6s / 451,295 msg/s | Gen2 +0 / pause +172.5ms |
| Confluent | 676,743,000 | 2026-07-29T10:32:44.22963+00:00 | 127.4ms | GC pause | - | - | 681.7s / 452,331 msg/s | Gen2 +0 / pause +198.2ms |
| Confluent | 676,753,000 | 2026-07-29T10:32:44.2417788+00:00 | 118.2ms | GC pause | - | - | 681.7s / 452,331 msg/s | Gen2 +0 / pause +198.2ms |
| Confluent | 678,873,000 | 2026-07-29T10:32:49.1114953+00:00 | 101.7ms | GC pause | - | - | 686.7s / 477,346 msg/s | Gen2 +0 / pause +195.7ms |
| Confluent | 678,941,000 | 2026-07-29T10:32:49.2144279+00:00 | 169.8ms | GC pause | - | - | 686.7s / 477,346 msg/s | Gen2 +0 / pause +195.7ms |
| Confluent | 678,980,000 | 2026-07-29T10:32:49.3105784+00:00 | 165.3ms | GC pause | - | - | 686.7s / 477,346 msg/s | Gen2 +0 / pause +195.7ms |
| Confluent | 679,115,000 | 2026-07-29T10:32:49.5198941+00:00 | 226.9ms | GC pause | - | - | 686.7s / 477,346 msg/s | Gen2 +0 / pause +195.7ms |
| Confluent | 679,121,000 | 2026-07-29T10:32:49.5295207+00:00 | 254.2ms | GC pause | - | - | 686.7s / 477,346 msg/s | Gen2 +0 / pause +195.7ms |
| Confluent | 679,134,000 | 2026-07-29T10:32:49.5662374+00:00 | 216.6ms | GC pause | - | - | 686.7s / 477,346 msg/s | Gen2 +0 / pause +195.7ms |
| Confluent | 679,139,000 | 2026-07-29T10:32:49.5802858+00:00 | 209.7ms | GC pause | - | - | 686.7s / 477,346 msg/s | Gen2 +0 / pause +195.7ms |
| Confluent | 679,171,000 | 2026-07-29T10:32:49.6542098+00:00 | 244.4ms | GC pause | - | - | 687.7s / 353,217 msg/s | Gen2 +0 / pause +392.8ms |
| Confluent | 679,181,000 | 2026-07-29T10:32:49.671273+00:00 | 253.7ms | GC pause | - | - | 687.7s / 353,217 msg/s | Gen2 +0 / pause +392.8ms |
| Confluent | 679,222,000 | 2026-07-29T10:32:49.8475068+00:00 | 116.1ms | GC pause | - | - | 687.7s / 353,217 msg/s | Gen2 +0 / pause +197.0ms |
| Confluent | 679,447,000 | 2026-07-29T10:32:50.4641226+00:00 | 127.9ms | GC pause | - | - | 687.7s / 353,217 msg/s | Gen2 +0 / pause +197.0ms |
| Confluent | 679,832,000 | 2026-07-29T10:32:51.4235414+00:00 | 116.9ms | GC pause | - | - | 688.7s / 428,467 msg/s | Gen2 +0 / pause +180.0ms |
| Confluent | 679,851,000 | 2026-07-29T10:32:51.4616343+00:00 | 125.7ms | GC pause | - | - | 688.7s / 428,467 msg/s | Gen2 +0 / pause +180.0ms |
| Confluent | 679,854,000 | 2026-07-29T10:32:51.4662504+00:00 | 131.5ms | GC pause | - | - | 688.7s / 428,467 msg/s | Gen2 +0 / pause +180.0ms |
| Confluent | 679,870,000 | 2026-07-29T10:32:51.5050052+00:00 | 137.6ms | GC pause | - | - | 688.7s / 428,467 msg/s | Gen2 +0 / pause +180.0ms |
| Confluent | 680,798,000 | 2026-07-29T10:32:53.6209607+00:00 | 108.9ms | GC pause | - | - | 690.7s / 491,419 msg/s | Gen2 +0 / pause +162.0ms |
| Confluent | 680,814,000 | 2026-07-29T10:32:53.6500876+00:00 | 101.8ms | GC pause | - | - | 690.7s / 491,419 msg/s | Gen2 +0 / pause +162.0ms |
| Confluent | 680,899,000 | 2026-07-29T10:32:53.8215161+00:00 | 101.1ms | GC pause | - | - | 691.7s / 389,420 msg/s | Gen2 +0 / pause +359.5ms |
| Confluent | 681,018,000 | 2026-07-29T10:32:54.1130747+00:00 | 114.6ms | GC pause | - | - | 691.7s / 389,420 msg/s | Gen2 +0 / pause +197.4ms |
| Confluent | 682,536,000 | 2026-07-29T10:32:57.3856565+00:00 | 108.3ms | GC pause | - | - | 694.7s / 534,748 msg/s | Gen2 +0 / pause +183.4ms |
| Confluent | 682,546,000 | 2026-07-29T10:32:57.3960443+00:00 | 110.5ms | GC pause | - | - | 694.7s / 534,748 msg/s | Gen2 +0 / pause +183.4ms |
| Confluent | 682,560,000 | 2026-07-29T10:32:57.4266335+00:00 | 108.2ms | GC pause | - | - | 694.7s / 534,748 msg/s | Gen2 +0 / pause +183.4ms |
| Confluent | 682,603,000 | 2026-07-29T10:32:57.520521+00:00 | 123.9ms | GC pause | - | - | 694.7s / 534,748 msg/s | Gen2 +0 / pause +183.4ms |
| Confluent | 682,636,000 | 2026-07-29T10:32:57.5660032+00:00 | 148.0ms | GC pause | - | - | 694.7s / 534,748 msg/s | Gen2 +0 / pause +183.4ms |
| Confluent | 682,637,000 | 2026-07-29T10:32:57.5674945+00:00 | 177.3ms | GC pause | - | - | 694.7s / 534,748 msg/s | Gen2 +0 / pause +183.4ms |
| Confluent | 682,705,000 | 2026-07-29T10:32:57.6801927+00:00 | 245.6ms | GC pause | - | - | 695.7s / 404,879 msg/s | Gen2 +0 / pause +385.8ms |
| Confluent | 682,963,000 | 2026-07-29T10:32:58.3358231+00:00 | 234.9ms | GC pause | - | - | 695.7s / 404,879 msg/s | Gen2 +0 / pause +202.3ms |
| Confluent | 683,009,000 | 2026-07-29T10:32:58.4471438+00:00 | 219.0ms | GC pause | - | - | 695.7s / 404,879 msg/s | Gen2 +0 / pause +202.3ms |
| Confluent | 683,021,000 | 2026-07-29T10:32:58.4693011+00:00 | 288.2ms | GC pause | - | - | 695.7s / 404,879 msg/s | Gen2 +0 / pause +202.3ms |
| Confluent | 683,089,000 | 2026-07-29T10:32:58.6305384+00:00 | 255.7ms | GC pause | - | - | 696.7s / 408,684 msg/s | Gen2 +0 / pause +376.2ms |
| Confluent | 683,090,000 | 2026-07-29T10:32:58.6337675+00:00 | 258.5ms | GC pause | - | - | 696.7s / 408,684 msg/s | Gen2 +0 / pause +376.2ms |
| Confluent | 683,111,000 | 2026-07-29T10:32:58.6952345+00:00 | 301.3ms | GC pause | - | - | 696.7s / 408,684 msg/s | Gen2 +0 / pause +376.2ms |
| Confluent | 683,151,000 | 2026-07-29T10:32:58.7827422+00:00 | 334.0ms | GC pause | - | - | 696.7s / 408,684 msg/s | Gen2 +0 / pause +376.2ms |
| Confluent | 683,359,000 | 2026-07-29T10:32:59.2862286+00:00 | 240.6ms | GC pause | - | - | 696.7s / 408,684 msg/s | Gen2 +0 / pause +173.9ms |
| Confluent | 683,402,000 | 2026-07-29T10:32:59.38076+00:00 | 192.6ms | GC pause | - | - | 696.7s / 408,684 msg/s | Gen2 +0 / pause +173.9ms |
| Confluent | 683,475,000 | 2026-07-29T10:32:59.5440395+00:00 | 193.4ms | GC pause | - | - | 696.7s / 408,684 msg/s | Gen2 +0 / pause +173.9ms |
| Confluent | 683,511,000 | 2026-07-29T10:32:59.6464452+00:00 | 257.7ms | GC pause | - | - | 697.7s / 366,426 msg/s | Gen2 +0 / pause +376.6ms |
| Confluent | 683,530,000 | 2026-07-29T10:32:59.6887211+00:00 | 201.5ms | GC pause | - | - | 697.7s / 366,426 msg/s | Gen2 +0 / pause +376.6ms |
| Confluent | 683,575,000 | 2026-07-29T10:32:59.8018926+00:00 | 154.9ms | GC pause | - | - | 697.7s / 366,426 msg/s | Gen2 +0 / pause +376.6ms |
| Confluent | 683,580,000 | 2026-07-29T10:32:59.8175182+00:00 | 183.3ms | GC pause | - | - | 697.7s / 366,426 msg/s | Gen2 +0 / pause +376.6ms |
| Confluent | 683,583,000 | 2026-07-29T10:32:59.8243107+00:00 | 188.7ms | GC pause | - | - | 697.7s / 366,426 msg/s | Gen2 +0 / pause +376.6ms |
| Confluent | 683,594,000 | 2026-07-29T10:32:59.8805962+00:00 | 139.4ms | GC pause | - | - | 697.7s / 366,426 msg/s | Gen2 +0 / pause +202.7ms |
| Confluent | 683,677,000 | 2026-07-29T10:33:00.1372706+00:00 | 155.6ms | GC pause | - | - | 697.7s / 366,426 msg/s | Gen2 +0 / pause +202.7ms |
| Confluent | 684,271,000 | 2026-07-29T10:33:01.5074366+00:00 | 106.8ms | GC pause | - | - | 698.7s / 486,613 msg/s | Gen2 +0 / pause +188.0ms |
| Confluent | 684,355,000 | 2026-07-29T10:33:01.6607946+00:00 | 145.0ms | GC pause | - | - | 698.7s / 486,613 msg/s | Gen2 +0 / pause +188.0ms |
| Confluent | 684,357,000 | 2026-07-29T10:33:01.6632364+00:00 | 168.2ms | GC pause | - | - | 698.7s / 486,613 msg/s | Gen2 +0 / pause +188.0ms |
| Confluent | 684,521,000 | 2026-07-29T10:33:02.0286202+00:00 | 131.6ms | GC pause | - | - | 699.7s / 420,242 msg/s | Gen2 +0 / pause +181.1ms |
| Confluent | 684,555,000 | 2026-07-29T10:33:02.0828692+00:00 | 120.8ms | GC pause | - | - | 699.7s / 420,242 msg/s | Gen2 +0 / pause +181.1ms |
| Confluent | 684,597,000 | 2026-07-29T10:33:02.1910641+00:00 | 106.2ms | GC pause | - | - | 699.7s / 420,242 msg/s | Gen2 +0 / pause +181.1ms |
| Confluent | 684,621,000 | 2026-07-29T10:33:02.2616679+00:00 | 107.9ms | GC pause | - | - | 699.7s / 420,242 msg/s | Gen2 +0 / pause +181.1ms |
| Confluent | 685,971,000 | 2026-07-29T10:33:05.0897735+00:00 | 115.8ms | GC pause | - | - | 702.7s / 427,949 msg/s | Gen2 +0 / pause +224.5ms |
| Confluent | 685,975,000 | 2026-07-29T10:33:05.0984397+00:00 | 118.8ms | GC pause | - | - | 702.7s / 427,949 msg/s | Gen2 +0 / pause +224.5ms |
| Confluent | 686,004,000 | 2026-07-29T10:33:05.1743765+00:00 | 113.1ms | GC pause | - | - | 702.7s / 427,949 msg/s | Gen2 +0 / pause +224.5ms |
| Confluent | 686,029,000 | 2026-07-29T10:33:05.2152903+00:00 | 124.1ms | GC pause | - | - | 702.7s / 427,949 msg/s | Gen2 +0 / pause +224.5ms |
| Confluent | 686,056,000 | 2026-07-29T10:33:05.2707411+00:00 | 135.2ms | GC pause | - | - | 702.7s / 427,949 msg/s | Gen2 +0 / pause +224.5ms |
| Confluent | 686,111,000 | 2026-07-29T10:33:05.4036704+00:00 | 121.0ms | GC pause | - | - | 702.7s / 427,949 msg/s | Gen2 +0 / pause +224.5ms |
| Confluent | 691,965,000 | 2026-07-29T10:33:18.4246209+00:00 | 173.6ms | GC pause | - | - | 715.7s / 444,995 msg/s | Gen2 +0 / pause +179.7ms |
| Confluent | 692,121,000 | 2026-07-29T10:33:18.8700609+00:00 | 147.6ms | GC pause | - | - | 716.7s / 384,393 msg/s | Gen2 +0 / pause +231.9ms |
| Confluent | 694,758,000 | 2026-07-29T10:33:24.7978573+00:00 | 108.8ms | GC pause | - | - | 722.7s / 427,061 msg/s | Gen2 +0 / pause +344.7ms |
| Confluent | 694,802,000 | 2026-07-29T10:33:24.8832818+00:00 | 121.5ms | GC pause | - | - | 722.7s / 427,061 msg/s | Gen2 +0 / pause +179.8ms |
| Confluent | 703,278,000 | 2026-07-29T10:33:43.3539134+00:00 | 154.0ms | GC pause | - | - | 740.7s / 446,264 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 703,355,000 | 2026-07-29T10:33:43.5287598+00:00 | 130.0ms | GC pause | - | - | 740.7s / 446,264 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 703,367,000 | 2026-07-29T10:33:43.5570535+00:00 | 153.7ms | GC pause | - | - | 740.7s / 446,264 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 703,426,000 | 2026-07-29T10:33:43.7084385+00:00 | 110.1ms | GC pause | - | - | 740.7s / 446,264 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 703,518,000 | 2026-07-29T10:33:43.9437937+00:00 | 113.2ms | GC pause | - | - | 741.7s / 434,869 msg/s | Gen2 +0 / pause +196.1ms |
| Confluent | 703,840,000 | 2026-07-29T10:33:44.6425133+00:00 | 118.0ms | GC pause | - | - | 741.7s / 434,869 msg/s | Gen2 +0 / pause +196.1ms |
| Confluent | 704,987,000 | 2026-07-29T10:33:47.2150458+00:00 | 111.3ms | GC pause | - | - | 744.7s / 419,377 msg/s | Gen2 +0 / pause +221.6ms |
| Confluent | 705,430,000 | 2026-07-29T10:33:48.2518338+00:00 | 134.3ms | GC pause | - | - | 745.7s / 487,382 msg/s | Gen2 +0 / pause +147.9ms |
| Confluent | 705,444,000 | 2026-07-29T10:33:48.2797459+00:00 | 135.8ms | GC pause | - | - | 745.7s / 487,382 msg/s | Gen2 +0 / pause +147.9ms |
| Confluent | 705,511,000 | 2026-07-29T10:33:48.4384747+00:00 | 143.2ms | GC pause | - | - | 745.7s / 487,382 msg/s | Gen2 +0 / pause +147.9ms |
| Confluent | 705,528,000 | 2026-07-29T10:33:48.4656325+00:00 | 144.0ms | GC pause | - | - | 745.7s / 487,382 msg/s | Gen2 +0 / pause +147.9ms |
| Confluent | 705,534,000 | 2026-07-29T10:33:48.4716482+00:00 | 135.1ms | GC pause | - | - | 745.7s / 487,382 msg/s | Gen2 +0 / pause +147.9ms |
| Confluent | 705,557,000 | 2026-07-29T10:33:48.5121679+00:00 | 147.8ms | GC pause | - | - | 745.7s / 487,382 msg/s | Gen2 +0 / pause +147.9ms |
| Confluent | 705,568,000 | 2026-07-29T10:33:48.5299063+00:00 | 147.1ms | GC pause | - | - | 745.7s / 487,382 msg/s | Gen2 +0 / pause +147.9ms |
| Confluent | 705,605,000 | 2026-07-29T10:33:48.5855412+00:00 | 132.4ms | GC pause | - | - | 745.7s / 487,382 msg/s | Gen2 +0 / pause +147.9ms |
| Confluent | 705,671,000 | 2026-07-29T10:33:48.7296586+00:00 | 148.9ms | GC pause | - | - | 745.7s / 487,382 msg/s | Gen2 +0 / pause +147.9ms |
| Confluent | 705,713,000 | 2026-07-29T10:33:48.8592511+00:00 | 103.3ms | GC pause | - | - | 746.7s / 415,780 msg/s | Gen2 +0 / pause +342.7ms |
| Confluent | 711,398,000 | 2026-07-29T10:34:00.6020611+00:00 | 100.6ms | GC pause | - | - | 757.7s / 519,326 msg/s | Gen2 +0 / pause +158.9ms |
| Confluent | 711,428,000 | 2026-07-29T10:34:00.6519789+00:00 | 105.8ms | GC pause | - | - | 757.7s / 519,326 msg/s | Gen2 +0 / pause +158.9ms |
| Confluent | 711,924,000 | 2026-07-29T10:34:01.6777437+00:00 | 106.9ms | GC pause | - | - | 758.8s / 471,723 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 711,966,000 | 2026-07-29T10:34:01.7541768+00:00 | 123.4ms | GC pause | - | - | 758.8s / 471,723 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 716,638,000 | 2026-07-29T10:34:11.4903728+00:00 | 101.5ms | GC pause | - | - | 768.8s / 484,086 msg/s | Gen2 +0 / pause +181.0ms |
| Confluent | 727,248,000 | 2026-07-29T10:34:32.6710995+00:00 | 128.2ms | GC pause | - | - | 789.8s / 490,021 msg/s | Gen2 +0 / pause +192.5ms |
| Confluent | 731,020,000 | 2026-07-29T10:34:40.185094+00:00 | 108.5ms | GC pause | - | - | 797.8s / 487,379 msg/s | Gen2 +0 / pause +148.0ms |
| Confluent | 731,092,000 | 2026-07-29T10:34:40.3105674+00:00 | 101.5ms | GC pause | - | - | 797.8s / 487,379 msg/s | Gen2 +0 / pause +148.0ms |
| Confluent | 731,096,000 | 2026-07-29T10:34:40.3166264+00:00 | 111.4ms | GC pause | - | - | 797.8s / 487,379 msg/s | Gen2 +0 / pause +148.0ms |
| Confluent | 731,114,000 | 2026-07-29T10:34:40.3399103+00:00 | 135.8ms | GC pause | - | - | 797.8s / 487,379 msg/s | Gen2 +0 / pause +148.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*15,870 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.65x less CPU per message** than Confluent.Kafka for producer (fire-and-forget); comparison throughput is 1.26x.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.53 | 1447.12 | 893,515 | 904,411 | +10.1% | +0.92% | 852.12 | 893,515 | 0 | 1.37 |
| Dekaf (3conn) | 1.52 | 1342.54 | 882,677 | 878,757 | -22.1% | -2.14% | 841.79 | 882,677 | 0 | 1.34 |
| Confluent | 2.43 | - | 617,082 | 618,623 | +8.5% | +0.61% | 588.50 | 617,082 | 0 | 1.50 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 279,750 | 310.83 | 938.13 KB |
| Dekaf | 2 | 287,160 | 319.06 | 947.05 KB |
| Dekaf | 3 | 282,906 | 314.33 | 936.71 KB |
| Dekaf (3conn) | 1 | 301,204 | 334.67 | 869.13 KB |
| Dekaf (3conn) | 2 | 304,277 | 338.08 | 888.90 KB |
| Dekaf (3conn) | 3 | 293,208 | 325.78 | 878.01 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-29T09:51:22.5750196+00:00 | 3 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 253,973 msg/s |
| Dekaf | 2026-07-29T09:51:40.6016938+00:00 | 3 | 16.0 MiB / 4.3 MiB | 266.7 MB/s | 0/0 | 1,374 | 18.0s / 685,861 msg/s |
| Dekaf | 2026-07-29T09:51:59.6298872+00:00 | 1 | 16.0 MiB / 3.1 MiB | 302.3 MB/s | 0/0 | 2,965 | 37.1s / 811,014 msg/s |
| Dekaf | 2026-07-29T09:52:17.6655183+00:00 | 1 | 16.0 MiB / 3.4 MiB | 324.7 MB/s | 0/1 | 3,516 | 55.1s / 823,443 msg/s |
| Dekaf | 2026-07-29T09:52:35.6834931+00:00 | 2 | 16.0 MiB / 1.8 MiB | 332.9 MB/s | 0/1 | 4,865 | 73.1s / 821,339 msg/s |
| Dekaf | 2026-07-29T09:52:53.7073538+00:00 | 2 | 16.0 MiB / 1.7 MiB | 341.9 MB/s | 0/2 | 5,642 | 91.2s / 919,234 msg/s |
| Dekaf | 2026-07-29T09:53:11.7302079+00:00 | 3 | 16.0 MiB / 3.7 MiB | 344.3 MB/s | 0/2 | 5,249 | 109.2s / 855,489 msg/s |
| Dekaf | 2026-07-29T09:53:29.7491089+00:00 | 3 | 16.0 MiB / 2.4 MiB | 353.9 MB/s | 0/2 | 5,877 | 127.2s / 910,158 msg/s |
| Dekaf | 2026-07-29T09:53:47.7582535+00:00 | 3 | 16.0 MiB / 6.3 MiB | 360.7 MB/s | 0/2 | 6,545 | 145.2s / 961,279 msg/s |
| Dekaf | 2026-07-29T09:54:06.7999618+00:00 | 1 | 16.0 MiB / 6.6 MiB | 368.4 MB/s | 0/3 | 6,521 | 164.2s / 939,484 msg/s |
| Dekaf | 2026-07-29T09:54:24.82061+00:00 | 1 | 16.0 MiB / 4.4 MiB | 368.4 MB/s | 0/4 | 6,922 | 182.3s / 878,895 msg/s |
| Dekaf | 2026-07-29T09:54:42.8354798+00:00 | 2 | 14.0 MiB / 7.9 MiB | 397.2 MB/s | 1/3 | 10,759 | 200.3s / 944,851 msg/s |
| Dekaf | 2026-07-29T09:55:00.8565925+00:00 | 2 | 14.0 MiB / 3.5 MiB | 397.2 MB/s | 1/4 | 11,492 | 218.3s / 943,369 msg/s |
| Dekaf | 2026-07-29T09:55:18.8618091+00:00 | 3 | 10.0 MiB / 6.0 MiB | 378.3 MB/s | 2/2 | 11,129 | 236.4s / 962,565 msg/s |
| Dekaf | 2026-07-29T09:55:36.8710127+00:00 | 3 | 12.0 MiB / 2.0 MiB | 378.3 MB/s | 2/3 | 12,277 | 254.4s / 917,937 msg/s |
| Dekaf | 2026-07-29T09:55:55.9144589+00:00 | 1 | 16.0 MiB / 2.2 MiB | 368.4 MB/s | 0/4 | 8,551 | 273.4s / 923,218 msg/s |
| Dekaf | 2026-07-29T09:56:13.9484171+00:00 | 1 | 16.0 MiB / 5.8 MiB | 368.4 MB/s | 0/4 | 8,840 | 291.4s / 926,454 msg/s |
| Dekaf | 2026-07-29T09:56:31.9641575+00:00 | 1 | 16.0 MiB / 5.4 MiB | 368.4 MB/s | 0/4 | 8,964 | 309.5s / 918,169 msg/s |
| Dekaf | 2026-07-29T09:56:49.9875373+00:00 | 2 | 14.0 MiB / 9.4 MiB | 397.2 MB/s | 1/4 | 15,087 | 327.5s / 884,499 msg/s |
| Dekaf | 2026-07-29T09:57:08.0052724+00:00 | 2 | 14.0 MiB / 7.2 MiB | 397.2 MB/s | 1/4 | 15,569 | 345.5s / 993,326 msg/s |
| Dekaf | 2026-07-29T09:57:26.0308041+00:00 | 3 | 10.0 MiB / 5.6 MiB | 378.3 MB/s | 3/4 | 21,493 | 363.5s / 918,947 msg/s |
| Dekaf | 2026-07-29T09:57:44.0470919+00:00 | 3 | 10.0 MiB / 3.4 MiB | 378.3 MB/s | 3/4 | 23,167 | 381.5s / 918,669 msg/s |
| Dekaf | 2026-07-29T09:58:03.0700236+00:00 | 1 | 16.0 MiB / 2.2 MiB | 368.4 MB/s | 0/4 | 10,226 | 400.6s / 927,576 msg/s |
| Dekaf | 2026-07-29T09:58:21.0921947+00:00 | 1 | 16.0 MiB / 7.6 MiB | 368.4 MB/s | 0/4 | 10,775 | 418.6s / 915,335 msg/s |
| Dekaf | 2026-07-29T09:58:39.1270266+00:00 | 1 | 16.0 MiB / 2.8 MiB | 368.4 MB/s | 0/5 | 11,204 | 436.6s / 885,319 msg/s |
| Dekaf | 2026-07-29T09:58:57.158761+00:00 | 2 | 14.0 MiB / 6.0 MiB | 397.2 MB/s | 1/5 | 19,155 | 454.6s / 865,747 msg/s |
| Dekaf | 2026-07-29T09:59:15.1850546+00:00 | 2 | 14.0 MiB / 6.5 MiB | 397.2 MB/s | 1/5 | 19,805 | 472.7s / 922,873 msg/s |
| Dekaf | 2026-07-29T09:59:33.2028964+00:00 | 3 | 10.0 MiB / 7.9 MiB | 378.3 MB/s | 3/6 | 38,044 | 490.7s / 808,712 msg/s |
| Dekaf | 2026-07-29T09:59:51.2225299+00:00 | 3 | 10.0 MiB / 5.5 MiB | 378.3 MB/s | 3/6 | 40,329 | 508.7s / 778,524 msg/s |
| Dekaf | 2026-07-29T10:00:10.2365102+00:00 | 1 | 16.0 MiB / 8.7 MiB | 368.4 MB/s | 0/5 | 13,057 | 527.8s / 904,581 msg/s |
| Dekaf | 2026-07-29T10:00:28.2505373+00:00 | 1 | 16.0 MiB / 1.3 MiB | 368.4 MB/s | 0/5 | 13,659 | 545.8s / 880,155 msg/s |
| Dekaf | 2026-07-29T10:00:46.2611766+00:00 | 1 | 16.0 MiB / 2.4 MiB | 368.4 MB/s | 0/5 | 13,921 | 563.8s / 852,874 msg/s |
| Dekaf | 2026-07-29T10:01:04.2907321+00:00 | 2 | 14.0 MiB / 2.4 MiB | 397.2 MB/s | 1/5 | 23,833 | 581.8s / 761,176 msg/s |
| Dekaf | 2026-07-29T10:01:22.3106776+00:00 | 2 | 14.0 MiB / 2.8 MiB | 397.2 MB/s | 1/6 | 23,992 | 599.9s / 698,880 msg/s |
| Dekaf | 2026-07-29T10:01:40.3316752+00:00 | 3 | 10.0 MiB / 2.7 MiB | 378.3 MB/s | 3/6 | 51,764 | 617.9s / 788,230 msg/s |
| Dekaf | 2026-07-29T10:01:58.3577235+00:00 | 3 | 10.0 MiB / 6.6 MiB | 378.3 MB/s | 3/6 | 53,718 | 635.9s / 846,612 msg/s |
| Dekaf | 2026-07-29T10:02:17.3771279+00:00 | 1 | 16.0 MiB / 1.5 MiB | 368.4 MB/s | 0/5 | 15,030 | 654.9s / 763,085 msg/s |
| Dekaf | 2026-07-29T10:02:35.3932213+00:00 | 1 | 16.0 MiB / 7.6 MiB | 368.4 MB/s | 0/6 | 15,325 | 673.0s / 884,817 msg/s |
| Dekaf | 2026-07-29T10:02:53.4099738+00:00 | 2 | 14.0 MiB / 2.7 MiB | 397.2 MB/s | 1/6 | 27,221 | 691.0s / 925,383 msg/s |
| Dekaf | 2026-07-29T10:03:11.4350262+00:00 | 2 | 14.0 MiB / 3.0 MiB | 397.2 MB/s | 1/6 | 28,168 | 709.0s / 944,284 msg/s |
| Dekaf | 2026-07-29T10:03:29.4755676+00:00 | 2 | 14.0 MiB / 8.6 MiB | 397.2 MB/s | 1/6 | 28,802 | 727.0s / 957,602 msg/s |
| Dekaf | 2026-07-29T10:03:47.5090089+00:00 | 3 | 6.0 MiB / 4.9 MiB | 380.8 MB/s | 5/7 | 70,841 | 745.1s / 991,095 msg/s |
| Dekaf | 2026-07-29T10:04:05.5301283+00:00 | 3 | 7.0 MiB / 0.9 MiB | 387.6 MB/s | 6/7 | 75,446 | 763.1s / 1,015,168 msg/s |
| Dekaf | 2026-07-29T10:04:24.5432695+00:00 | 1 | 14.0 MiB / 1.5 MiB | 444.6 MB/s | 1/8 | 18,028 | 782.1s / 994,961 msg/s |
| Dekaf | 2026-07-29T10:04:42.5745659+00:00 | 1 | 14.0 MiB / 1.8 MiB | 444.6 MB/s | 1/8 | 18,307 | 800.1s / 988,433 msg/s |
| Dekaf | 2026-07-29T10:05:00.596043+00:00 | 2 | 14.0 MiB / 9.9 MiB | 451.2 MB/s | 1/6 | 30,973 | 818.1s / 963,040 msg/s |
| Dekaf | 2026-07-29T10:05:18.6089572+00:00 | 2 | 14.0 MiB / 10.7 MiB | 451.2 MB/s | 1/6 | 31,748 | 836.1s / 996,931 msg/s |
| Dekaf | 2026-07-29T10:05:36.6257234+00:00 | 2 | 12.0 MiB / 2.1 MiB | 451.2 MB/s | 2/6 | 32,452 | 854.2s / 975,750 msg/s |
| Dekaf | 2026-07-29T10:05:54.6538294+00:00 | 3 | 7.0 MiB / 1.9 MiB | 434.7 MB/s | 7/9 | 99,997 | 872.2s / 936,845 msg/s |
| Dekaf | 2026-07-29T10:06:12.6813701+00:00 | 3 | 7.0 MiB / 0.8 MiB | 434.7 MB/s | 7/9 | 103,378 | 890.2s / 947,363 msg/s |
| Dekaf (3conn) | 2026-07-29T10:21:44.7673569+00:00 | 3 | 16.0 MiB / 5.1 MiB | 373.0 MB/s | 0/0 | 425 | 9.0s / 996,077 msg/s |
| Dekaf (3conn) | 2026-07-29T10:22:02.7930401+00:00 | 3 | 16.0 MiB / 11.9 MiB | 390.1 MB/s | 0/0 | 1,743 | 27.1s / 884,603 msg/s |
| Dekaf (3conn) | 2026-07-29T10:22:21.8190164+00:00 | 1 | 14.0 MiB / 2.3 MiB | 443.3 MB/s | 1/0 | 2,585 | 46.1s / 991,646 msg/s |
| Dekaf (3conn) | 2026-07-29T10:22:39.842033+00:00 | 1 | 12.0 MiB / 2.2 MiB | 443.3 MB/s | 2/0 | 3,031 | 64.1s / 1,036,789 msg/s |
| Dekaf (3conn) | 2026-07-29T10:22:57.8735876+00:00 | 1 | 12.0 MiB / 9.7 MiB | 443.3 MB/s | 2/0 | 3,790 | 82.1s / 1,127,627 msg/s |
| Dekaf (3conn) | 2026-07-29T10:23:15.9197835+00:00 | 2 | 12.0 MiB / 7.7 MiB | 435.5 MB/s | 2/0 | 4,600 | 100.2s / 1,030,545 msg/s |
| Dekaf (3conn) | 2026-07-29T10:23:33.9478183+00:00 | 2 | 12.0 MiB / 3.7 MiB | 435.5 MB/s | 2/1 | 5,262 | 118.2s / 1,084,007 msg/s |
| Dekaf (3conn) | 2026-07-29T10:23:51.9684699+00:00 | 3 | 12.0 MiB / 4.5 MiB | 414.3 MB/s | 2/1 | 6,564 | 136.2s / 1,054,143 msg/s |
| Dekaf (3conn) | 2026-07-29T10:24:09.9830148+00:00 | 3 | 12.0 MiB / 3.4 MiB | 414.3 MB/s | 2/1 | 7,305 | 154.2s / 1,119,385 msg/s |
| Dekaf (3conn) | 2026-07-29T10:24:29.0241636+00:00 | 1 | 12.0 MiB / 3.9 MiB | 448.1 MB/s | 2/1 | 8,568 | 173.2s / 1,133,266 msg/s |
| Dekaf (3conn) | 2026-07-29T10:24:47.0339921+00:00 | 1 | 12.0 MiB / 1.7 MiB | 448.1 MB/s | 2/2 | 8,983 | 191.3s / 1,007,758 msg/s |
| Dekaf (3conn) | 2026-07-29T10:25:05.0430071+00:00 | 1 | 12.0 MiB / 0.9 MiB | 448.1 MB/s | 2/2 | 9,495 | 209.3s / 1,014,436 msg/s |
| Dekaf (3conn) | 2026-07-29T10:25:23.0605104+00:00 | 2 | 12.0 MiB / 1.6 MiB | 454.5 MB/s | 2/2 | 8,344 | 227.3s / 772,757 msg/s |
| Dekaf (3conn) | 2026-07-29T10:25:41.0934211+00:00 | 2 | 12.0 MiB / 1.6 MiB | 454.5 MB/s | 2/2 | 8,449 | 245.3s / 778,542 msg/s |
| Dekaf (3conn) | 2026-07-29T10:25:59.109035+00:00 | 3 | 12.0 MiB / 1.3 MiB | 424.4 MB/s | 2/2 | 9,977 | 263.3s / 795,912 msg/s |
| Dekaf (3conn) | 2026-07-29T10:26:17.1335467+00:00 | 3 | 12.0 MiB / 1.9 MiB | 424.4 MB/s | 2/2 | 10,358 | 281.4s / 876,572 msg/s |
| Dekaf (3conn) | 2026-07-29T10:26:36.1683038+00:00 | 1 | 10.0 MiB / 1.8 MiB | 448.1 MB/s | 3/3 | 13,734 | 300.4s / 924,476 msg/s |
| Dekaf (3conn) | 2026-07-29T10:26:54.1890456+00:00 | 1 | 10.0 MiB / 2.2 MiB | 448.1 MB/s | 3/3 | 14,740 | 318.4s / 949,923 msg/s |
| Dekaf (3conn) | 2026-07-29T10:27:12.2071725+00:00 | 2 | 10.0 MiB / 6.9 MiB | 538.4 MB/s | 3/2 | 10,088 | 336.4s / 1,017,171 msg/s |
| Dekaf (3conn) | 2026-07-29T10:27:30.2182111+00:00 | 2 | 10.0 MiB / 2.5 MiB | 538.4 MB/s | 3/2 | 10,622 | 354.5s / 1,045,847 msg/s |
| Dekaf (3conn) | 2026-07-29T10:27:48.2376793+00:00 | 2 | 8.0 MiB / 0.9 MiB | 538.4 MB/s | 4/2 | 11,170 | 372.5s / 937,178 msg/s |
| Dekaf (3conn) | 2026-07-29T10:28:06.2459786+00:00 | 3 | 8.0 MiB / 1.9 MiB | 444.9 MB/s | 4/2 | 14,841 | 390.5s / 1,096,908 msg/s |
| Dekaf (3conn) | 2026-07-29T10:28:24.2606446+00:00 | 3 | 9.0 MiB / 2.1 MiB | 444.9 MB/s | 4/2 | 15,684 | 408.5s / 980,867 msg/s |
| Dekaf (3conn) | 2026-07-29T10:28:43.2829983+00:00 | 1 | 10.0 MiB / 5.5 MiB | 487.7 MB/s | 3/4 | 18,519 | 427.5s / 984,469 msg/s |
| Dekaf (3conn) | 2026-07-29T10:29:01.306674+00:00 | 1 | 10.0 MiB / 0.4 MiB | 487.7 MB/s | 3/4 | 18,996 | 445.5s / 1,033,254 msg/s |
| Dekaf (3conn) | 2026-07-29T10:29:19.3182531+00:00 | 2 | 10.0 MiB / 3.7 MiB | 538.4 MB/s | 6/2 | 14,165 | 463.6s / 947,249 msg/s |
| Dekaf (3conn) | 2026-07-29T10:29:37.3321569+00:00 | 2 | 10.0 MiB / 2.7 MiB | 538.4 MB/s | 6/2 | 14,608 | 481.6s / 1,099,364 msg/s |
| Dekaf (3conn) | 2026-07-29T10:29:55.3463469+00:00 | 2 | 11.0 MiB / 1.8 MiB | 538.4 MB/s | 6/2 | 15,248 | 499.6s / 978,277 msg/s |
| Dekaf (3conn) | 2026-07-29T10:30:13.3813249+00:00 | 3 | 11.0 MiB / 8.7 MiB | 444.9 MB/s | 7/2 | 20,579 | 517.6s / 910,155 msg/s |
| Dekaf (3conn) | 2026-07-29T10:30:31.4019444+00:00 | 3 | 11.0 MiB / 0.6 MiB | 444.9 MB/s | 7/2 | 20,953 | 535.6s / 827,883 msg/s |
| Dekaf (3conn) | 2026-07-29T10:30:50.4324326+00:00 | 1 | 10.0 MiB / 2.1 MiB | 487.7 MB/s | 3/5 | 23,055 | 554.6s / 868,685 msg/s |
| Dekaf (3conn) | 2026-07-29T10:31:08.4524234+00:00 | 1 | 10.0 MiB / 1.7 MiB | 487.7 MB/s | 3/5 | 23,725 | 572.7s / 886,531 msg/s |
| Dekaf (3conn) | 2026-07-29T10:31:26.471306+00:00 | 2 | 13.0 MiB / 1.6 MiB | 538.4 MB/s | 8/2 | 17,340 | 590.7s / 806,913 msg/s |
| Dekaf (3conn) | 2026-07-29T10:31:44.4902344+00:00 | 2 | 12.0 MiB / 3.6 MiB | 538.4 MB/s | 8/3 | 17,429 | 608.7s / 794,599 msg/s |
| Dekaf (3conn) | 2026-07-29T10:32:02.5089497+00:00 | 2 | 12.0 MiB / 2.7 MiB | 538.4 MB/s | 8/3 | 17,480 | 626.7s / 685,968 msg/s |
| Dekaf (3conn) | 2026-07-29T10:32:20.5348209+00:00 | 3 | 10.0 MiB / 5.6 MiB | 444.9 MB/s | 8/3 | 23,069 | 644.7s / 732,062 msg/s |
| Dekaf (3conn) | 2026-07-29T10:32:38.5753051+00:00 | 3 | 8.0 MiB / 5.8 MiB | 444.9 MB/s | 9/3 | 23,579 | 662.8s / 837,363 msg/s |
| Dekaf (3conn) | 2026-07-29T10:32:57.6093016+00:00 | 1 | 10.0 MiB / 10.0 MiB | 487.7 MB/s | 3/5 | 27,377 | 681.8s / 793,750 msg/s |
| Dekaf (3conn) | 2026-07-29T10:33:15.6287991+00:00 | 1 | 10.0 MiB / 5.3 MiB | 487.7 MB/s | 3/5 | 28,127 | 699.8s / 773,679 msg/s |
| Dekaf (3conn) | 2026-07-29T10:33:33.6564475+00:00 | 2 | 12.0 MiB / 1.7 MiB | 538.4 MB/s | 8/4 | 18,533 | 717.9s / 717,434 msg/s |
| Dekaf (3conn) | 2026-07-29T10:33:51.6720519+00:00 | 2 | 12.0 MiB / 1.2 MiB | 538.4 MB/s | 8/4 | 18,628 | 735.9s / 688,190 msg/s |
| Dekaf (3conn) | 2026-07-29T10:34:09.6840259+00:00 | 3 | 9.0 MiB / 2.3 MiB | 444.9 MB/s | 10/4 | 26,094 | 753.9s / 729,242 msg/s |
| Dekaf (3conn) | 2026-07-29T10:34:27.7084752+00:00 | 3 | 8.0 MiB / 2.2 MiB | 444.9 MB/s | 11/4 | 26,815 | 771.9s / 722,714 msg/s |
| Dekaf (3conn) | 2026-07-29T10:34:45.729447+00:00 | 3 | 8.0 MiB / 2.7 MiB | 444.9 MB/s | 12/4 | 27,472 | 789.9s / 750,585 msg/s |
| Dekaf (3conn) | 2026-07-29T10:35:04.7504662+00:00 | 1 | 10.0 MiB / 0.8 MiB | 487.7 MB/s | 3/6 | 30,240 | 809.0s / 712,402 msg/s |
| Dekaf (3conn) | 2026-07-29T10:35:22.771198+00:00 | 1 | 10.0 MiB / 2.6 MiB | 487.7 MB/s | 3/6 | 30,344 | 827.0s / 703,935 msg/s |
| Dekaf (3conn) | 2026-07-29T10:35:40.7910785+00:00 | 2 | 12.0 MiB / 1.0 MiB | 538.4 MB/s | 8/5 | 19,151 | 845.0s / 637,055 msg/s |
| Dekaf (3conn) | 2026-07-29T10:35:58.8019159+00:00 | 2 | 12.0 MiB / 4.3 MiB | 538.4 MB/s | 8/5 | 19,205 | 863.0s / 793,647 msg/s |
| Dekaf (3conn) | 2026-07-29T10:36:16.8129622+00:00 | 3 | 8.0 MiB / 2.3 MiB | 444.9 MB/s | 12/5 | 31,770 | 881.0s / 781,104 msg/s |
| Dekaf (3conn) | 2026-07-29T10:36:34.826464+00:00 | 3 | 8.0 MiB / 0.5 MiB | 444.9 MB/s | 12/5 | 32,610 | 899.1s / 794,957 msg/s |
*5,290 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-29T09:51:52.8889165+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-29T09:51:53.0753364+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 1.0 MiB |
| Dekaf | 2026-07-29T09:52:08.043408+00:00 | 3 | capacity | failed | 15,154ms | 16.0 MiB / 6.5 MiB |
| Dekaf | 2026-07-29T09:52:08.1923595+00:00 | 1 | capacity | failed | 15,117ms | 16.0 MiB / 6.9 MiB |
| Dekaf | 2026-07-29T09:52:38.2497326+00:00 | 3 | capacity | started | 0ms | 18.0 MiB / 2.3 MiB |
| Dekaf | 2026-07-29T09:52:38.3681872+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-29T09:52:40.7898927+00:00 | 3 | capacity | failed | 2,540ms | 16.0 MiB / 13.4 MiB |
| Dekaf | 2026-07-29T09:52:52.5211767+00:00 | 1 | capacity | failed | 14,151ms | 16.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-29T09:53:21.1941926+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 1.3 MiB |
| Dekaf | 2026-07-29T09:53:36.2627896+00:00 | 2 | capacity | succeeded | 15,067ms | 14.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-29T09:53:37.7764091+00:00 | 1 | capacity | failed | 15,059ms | 16.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-29T09:53:54.3945872+00:00 | 2 | capacity | failed | 15,120ms | 14.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-29T09:54:08.0660515+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-29T09:54:41.5176033+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-29T09:54:54.7905319+00:00 | 2 | capacity | started | 0ms | 15.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-29T09:54:58.3097368+00:00 | 2 | capacity | failed | 3,519ms | 14.0 MiB / 3.3 MiB |
| Dekaf | 2026-07-29T09:54:59.6195556+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-29T09:55:17.6968161+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 2.6 MiB |
| Dekaf | 2026-07-29T09:55:32.790789+00:00 | 3 | capacity | failed | 15,093ms | 12.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-29T09:56:18.1185353+00:00 | 3 | capacity | succeeded | 15,177ms | 10.0 MiB / 4.5 MiB |
| Dekaf | 2026-07-29T09:56:21.1320049+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 2.8 MiB |
| Dekaf | 2026-07-29T09:56:59.1026913+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 0.9 MiB |
| Dekaf | 2026-07-29T09:57:14.2044144+00:00 | 2 | capacity | failed | 15,101ms | 14.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-29T09:57:50.1502987+00:00 | 3 | capacity | failed | 13,564ms | 10.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T09:58:11.0967684+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 4.8 MiB |
| Dekaf | 2026-07-29T09:58:26.1716469+00:00 | 1 | capacity | failed | 15,075ms | 16.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-29T09:58:35.3534414+00:00 | 3 | capacity | failed | 15,057ms | 10.0 MiB / 1.8 MiB |
| Dekaf | 2026-07-29T10:01:19.3021833+00:00 | 2 | capacity | failed | 3,522ms | 14.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-29T10:02:27.7172696+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-29T10:02:36.8581813+00:00 | 3 | capacity | started | 0ms | 11.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-29T10:02:38.8860093+00:00 | 3 | capacity | failed | 2,027ms | 10.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-29T10:03:09.0987396+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 3.3 MiB |
| Dekaf | 2026-07-29T10:03:15.0217093+00:00 | 1 | capacity | succeeded | 15,120ms | 14.0 MiB / 2.0 MiB |
| Dekaf | 2026-07-29T10:03:24.1832781+00:00 | 3 | capacity | succeeded | 15,084ms | 8.0 MiB / 3.0 MiB |
| Dekaf | 2026-07-29T10:03:33.1129916+00:00 | 1 | capacity | failed | 15,078ms | 14.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-29T10:03:42.2715931+00:00 | 3 | capacity | succeeded | 15,071ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T10:04:00.3660896+00:00 | 3 | capacity | succeeded | 15,076ms | 6.0 MiB / 0.8 MiB |
| Dekaf | 2026-07-29T10:04:03.2857095+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-29T10:04:18.3397399+00:00 | 1 | capacity | failed | 15,054ms | 14.0 MiB / 1.9 MiB |
| Dekaf | 2026-07-29T10:04:18.4858237+00:00 | 3 | capacity | failed | 15,102ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-29T10:05:03.7054935+00:00 | 3 | capacity | succeeded | 15,074ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T10:05:20.8709478+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-29T10:05:36.0050807+00:00 | 2 | capacity | succeeded | 15,134ms | 12.0 MiB / 4.4 MiB |
| Dekaf | 2026-07-29T10:05:39.0147641+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T10:05:54.1082689+00:00 | 2 | capacity | succeeded | 15,093ms | 10.0 MiB / 3.3 MiB |
| Dekaf | 2026-07-29T10:05:57.1264161+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 8.8 MiB |
| Dekaf | 2026-07-29T10:06:19.0796345+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 0.9 MiB |
| Dekaf | 2026-07-29T10:06:19.1376699+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 2.8 MiB |
| Dekaf (3conn) | 2026-07-29T10:22:06.2202835+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 6.8 MiB |
| Dekaf (3conn) | 2026-07-29T10:22:06.247695+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 1.2 MiB |
| Dekaf (3conn) | 2026-07-29T10:22:21.3165455+00:00 | 3 | capacity | succeeded | 15,096ms | 14.0 MiB / 4.3 MiB |
| Dekaf (3conn) | 2026-07-29T10:22:21.3193244+00:00 | 2 | capacity | succeeded | 15,071ms | 14.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-29T10:22:24.3245398+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 6.9 MiB |
| Dekaf (3conn) | 2026-07-29T10:22:24.3422487+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-07-29T10:22:39.4196218+00:00 | 2 | capacity | succeeded | 15,095ms | 12.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:22:39.4694914+00:00 | 3 | capacity | succeeded | 15,127ms | 12.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:23:09.5971802+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 2.6 MiB |
| Dekaf (3conn) | 2026-07-29T10:23:09.6841247+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:23:24.6962079+00:00 | 2 | capacity | failed | 15,099ms | 12.0 MiB / 2.5 MiB |
| Dekaf (3conn) | 2026-07-29T10:23:24.7850794+00:00 | 3 | capacity | failed | 15,101ms | 12.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-07-29T10:24:25.0480006+00:00 | 2 | capacity | started | 0ms | 13.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:24:25.2134737+00:00 | 3 | capacity | started | 0ms | 13.0 MiB / 0.8 MiB |
| Dekaf (3conn) | 2026-07-29T10:24:40.1164138+00:00 | 2 | capacity | failed | 15,070ms | 12.0 MiB / 4.8 MiB |
| Dekaf (3conn) | 2026-07-29T10:24:40.3137032+00:00 | 3 | capacity | failed | 15,100ms | 12.0 MiB / 3.0 MiB |
| Dekaf (3conn) | 2026-07-29T10:25:25.2696263+00:00 | 1 | capacity | succeeded | 15,076ms | 10.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:25:55.5065585+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-07-29T10:26:40.826526+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 6.3 MiB |
| Dekaf (3conn) | 2026-07-29T10:26:55.9341186+00:00 | 2 | capacity | succeeded | 15,107ms | 10.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:26:56.186853+00:00 | 3 | capacity | succeeded | 15,075ms | 10.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-07-29T10:27:25.9843241+00:00 | 1 | capacity | failed | 15,053ms | 10.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:27:26.0873142+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-07-29T10:27:41.1557468+00:00 | 2 | capacity | succeeded | 15,068ms | 8.0 MiB / 0.5 MiB |
| Dekaf (3conn) | 2026-07-29T10:27:41.4460546+00:00 | 3 | capacity | succeeded | 15,122ms | 8.0 MiB / 0.9 MiB |
| Dekaf (3conn) | 2026-07-29T10:28:11.5665243+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 0.2 MiB |
| Dekaf (3conn) | 2026-07-29T10:28:26.3723465+00:00 | 2 | capacity | succeeded | 15,074ms | 9.0 MiB / 7.6 MiB |
| Dekaf (3conn) | 2026-07-29T10:28:56.5861902+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 1.0 MiB |
| Dekaf (3conn) | 2026-07-29T10:28:56.8016748+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 1.8 MiB |
| Dekaf (3conn) | 2026-07-29T10:29:11.8999299+00:00 | 3 | capacity | succeeded | 15,098ms | 10.0 MiB / 7.2 MiB |
| Dekaf (3conn) | 2026-07-29T10:29:26.5741719+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-07-29T10:29:41.7770225+00:00 | 2 | capacity | started | 0ms | 11.0 MiB / 0.9 MiB |
| Dekaf (3conn) | 2026-07-29T10:29:42.0856955+00:00 | 3 | capacity | started | 0ms | 11.0 MiB / 3.0 MiB |
| Dekaf (3conn) | 2026-07-29T10:29:57.1767559+00:00 | 3 | capacity | succeeded | 15,091ms | 11.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-07-29T10:30:27.0260928+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 0.2 MiB |
| Dekaf (3conn) | 2026-07-29T10:30:42.1205938+00:00 | 2 | capacity | succeeded | 15,094ms | 12.0 MiB / 0.8 MiB |
| Dekaf (3conn) | 2026-07-29T10:30:42.4451947+00:00 | 3 | capacity | succeeded | 15,102ms | 12.0 MiB / 2.0 MiB |
| Dekaf (3conn) | 2026-07-29T10:31:12.7249283+00:00 | 3 | capacity | started | 0ms | 13.0 MiB / 0.5 MiB |
| Dekaf (3conn) | 2026-07-29T10:31:16.7574493+00:00 | 3 | capacity | failed | 4,032ms | 12.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-07-29T10:32:17.1756954+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 0.3 MiB |
| Dekaf (3conn) | 2026-07-29T10:32:27.7321999+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:32:35.2876126+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 0.5 MiB |
| Dekaf (3conn) | 2026-07-29T10:32:42.8123072+00:00 | 2 | capacity | failed | 15,080ms | 12.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-07-29T10:33:20.6777069+00:00 | 3 | capacity | started | 0ms | 11.0 MiB / 0.8 MiB |
| Dekaf (3conn) | 2026-07-29T10:33:35.7814213+00:00 | 3 | capacity | succeeded | 15,103ms | 11.0 MiB / 0.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:33:58.3114963+00:00 | 1 | capacity | failed | 15,081ms | 10.0 MiB / 0.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:34:05.9851981+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 4.5 MiB |
| Dekaf (3conn) | 2026-07-29T10:34:24.0995846+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-29T10:34:39.2198897+00:00 | 3 | capacity | succeeded | 15,120ms | 8.0 MiB / 2.8 MiB |
| Dekaf (3conn) | 2026-07-29T10:34:58.7014534+00:00 | 2 | capacity | failed | 15,091ms | 12.0 MiB / 7.6 MiB |
| Dekaf (3conn) | 2026-07-29T10:35:09.4093627+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:36:24.8440777+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 1.9 MiB |
*51 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf (3conn) | 1 | 0.001–0.002ms | 2 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 4 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 8 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 27 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 94 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 175 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 220 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 227 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 381 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 545 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 686 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 625 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 351 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 110 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 7 |
| Dekaf (3conn) | 1 | 65.536–131.072ms | 2 |
| Dekaf (3conn) | 1 | 131.072–262.144ms | 1 |
| Dekaf (3conn) | 2 | 0.002–0.004ms | 1 |
| Dekaf (3conn) | 2 | 0.004–0.008ms | 1 |
| Dekaf (3conn) | 2 | 0.008–0.016ms | 6 |
| Dekaf (3conn) | 2 | 0.016–0.032ms | 20 |
| Dekaf (3conn) | 2 | 0.032–0.064ms | 63 |
| Dekaf (3conn) | 2 | 0.064–0.128ms | 103 |
| Dekaf (3conn) | 2 | 0.128–0.256ms | 165 |
| Dekaf (3conn) | 2 | 0.256–0.512ms | 176 |
| Dekaf (3conn) | 2 | 0.512–1.024ms | 272 |
| Dekaf (3conn) | 2 | 1.024–2.048ms | 378 |
| Dekaf (3conn) | 2 | 2.048–4.096ms | 422 |
| Dekaf (3conn) | 2 | 4.096–8.192ms | 374 |
| Dekaf (3conn) | 2 | 8.192–16.384ms | 195 |
| Dekaf (3conn) | 2 | 16.384–32.768ms | 58 |
| Dekaf (3conn) | 2 | 32.768–65.536ms | 5 |
| Dekaf (3conn) | 3 | 0.002–0.004ms | 2 |
| Dekaf (3conn) | 3 | 0.004–0.008ms | 3 |
| Dekaf (3conn) | 3 | 0.008–0.016ms | 14 |
| Dekaf (3conn) | 3 | 0.016–0.032ms | 36 |
| Dekaf (3conn) | 3 | 0.032–0.064ms | 95 |
| Dekaf (3conn) | 3 | 0.064–0.128ms | 152 |
| Dekaf (3conn) | 3 | 0.128–0.256ms | 189 |
| Dekaf (3conn) | 3 | 0.256–0.512ms | 231 |
| Dekaf (3conn) | 3 | 0.512–1.024ms | 366 |
| Dekaf (3conn) | 3 | 1.024–2.048ms | 564 |
| Dekaf (3conn) | 3 | 2.048–4.096ms | 720 |
| Dekaf (3conn) | 3 | 4.096–8.192ms | 643 |
| Dekaf (3conn) | 3 | 8.192–16.384ms | 428 |
| Dekaf (3conn) | 3 | 16.384–32.768ms | 146 |
| Dekaf (3conn) | 3 | 32.768–65.536ms | 10 |
| Dekaf (3conn) | 3 | 65.536–131.072ms | 2 |
| Dekaf | 1 | 0.002–0.004ms | 1 |
| Dekaf | 1 | 0.008–0.016ms | 3 |
| Dekaf | 1 | 0.016–0.032ms | 10 |
| Dekaf | 1 | 0.032–0.064ms | 34 |
| Dekaf | 1 | 0.064–0.128ms | 68 |
| Dekaf | 1 | 0.128–0.256ms | 75 |
| Dekaf | 1 | 0.256–0.512ms | 119 |
| Dekaf | 1 | 0.512–1.024ms | 176 |
| Dekaf | 1 | 1.024–2.048ms | 307 |
| Dekaf | 1 | 2.048–4.096ms | 387 |
| Dekaf | 1 | 4.096–8.192ms | 463 |
| Dekaf | 1 | 8.192–16.384ms | 289 |
| Dekaf | 1 | 16.384–32.768ms | 102 |
| Dekaf | 1 | 32.768–65.536ms | 13 |
| Dekaf | 1 | 65.536–131.072ms | 2 |
| Dekaf | 2 | 0.001–0.002ms | 3 |
| Dekaf | 2 | 0.002–0.004ms | 3 |
| Dekaf | 2 | 0.004–0.008ms | 4 |
| Dekaf | 2 | 0.008–0.016ms | 5 |
| Dekaf | 2 | 0.016–0.032ms | 23 |
| Dekaf | 2 | 0.032–0.064ms | 90 |
| Dekaf | 2 | 0.064–0.128ms | 154 |
| Dekaf | 2 | 0.128–0.256ms | 164 |
| Dekaf | 2 | 0.256–0.512ms | 204 |
| Dekaf | 2 | 0.512–1.024ms | 382 |
| Dekaf | 2 | 1.024–2.048ms | 546 |
| Dekaf | 2 | 2.048–4.096ms | 726 |
| Dekaf | 2 | 4.096–8.192ms | 757 |
| Dekaf | 2 | 8.192–16.384ms | 472 |
| Dekaf | 2 | 16.384–32.768ms | 140 |
| Dekaf | 2 | 32.768–65.536ms | 17 |
| Dekaf | 3 | 0.001–0.002ms | 8 |
| Dekaf | 3 | 0.002–0.004ms | 10 |
| Dekaf | 3 | 0.004–0.008ms | 15 |
| Dekaf | 3 | 0.008–0.016ms | 43 |
| Dekaf | 3 | 0.016–0.032ms | 160 |
| Dekaf | 3 | 0.032–0.064ms | 388 |
| Dekaf | 3 | 0.064–0.128ms | 713 |
| Dekaf | 3 | 0.128–0.256ms | 811 |
| Dekaf | 3 | 0.256–0.512ms | 936 |
| Dekaf | 3 | 0.512–1.024ms | 1,644 |
| Dekaf | 3 | 1.024–2.048ms | 2,467 |
| Dekaf | 3 | 2.048–4.096ms | 2,821 |
| Dekaf | 3 | 4.096–8.192ms | 2,175 |
| Dekaf | 3 | 8.192–16.384ms | 1,074 |
| Dekaf | 3 | 16.384–32.768ms | 275 |
| Dekaf | 3 | 32.768–65.536ms | 44 |
| Dekaf | 3 | 65.536–131.072ms | 1 |

## Delivery Latency Outliers - Producer (Fire-and-Forget), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 105,000 | 2026-07-29T09:51:22.9765804+00:00 | 172.7ms | throughput collapse | - | - | 1.0s / 253,973 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 108,000 | 2026-07-29T09:51:22.9831618+00:00 | 166.1ms | throughput collapse | - | - | 1.0s / 253,973 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 118,000 | 2026-07-29T09:51:23.0175909+00:00 | 225.4ms | throughput collapse | - | - | 1.0s / 253,973 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 125,000 | 2026-07-29T09:51:23.1004569+00:00 | 141.6ms | throughput collapse | - | - | 1.0s / 253,973 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 128,000 | 2026-07-29T09:51:23.1053158+00:00 | 136.7ms | throughput collapse | - | - | 1.0s / 253,973 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 132,000 | 2026-07-29T09:51:23.1173438+00:00 | 183.7ms | throughput collapse | - | - | 1.0s / 253,973 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 175,000 | 2026-07-29T09:51:23.261069+00:00 | 178.0ms | throughput collapse | - | - | 1.0s / 253,973 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 178,000 | 2026-07-29T09:51:23.2768279+00:00 | 162.3ms | throughput collapse | - | - | 1.0s / 253,973 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 188,000 | 2026-07-29T09:51:23.3318513+00:00 | 156.8ms | throughput collapse | - | - | 1.0s / 253,973 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 192,000 | 2026-07-29T09:51:23.3440818+00:00 | 144.6ms | throughput collapse | - | - | 1.0s / 253,973 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 205,000 | 2026-07-29T09:51:23.4024458+00:00 | 102.6ms | throughput collapse | - | - | 1.0s / 253,973 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 221,000 | 2026-07-29T09:51:23.4747466+00:00 | 103.6ms | throughput collapse | - | - | 1.0s / 253,973 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 228,000 | 2026-07-29T09:51:23.4878419+00:00 | 122.8ms | throughput collapse | - | - | 1.0s / 253,973 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 296,000 | 2026-07-29T09:51:23.7376282+00:00 | 101.0ms | GC pause | - | - | 2.0s / 354,342 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 324,000 | 2026-07-29T09:51:23.7974706+00:00 | 149.6ms | GC pause | - | - | 2.0s / 354,342 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 328,000 | 2026-07-29T09:51:23.8055605+00:00 | 103.5ms | GC pause | - | - | 2.0s / 354,342 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 426,000 | 2026-07-29T09:51:24.0342982+00:00 | 203.5ms | GC pause | - | - | 2.0s / 354,342 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 429,000 | 2026-07-29T09:51:24.0394074+00:00 | 161.5ms | GC pause | - | - | 2.0s / 354,342 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 434,000 | 2026-07-29T09:51:24.0474051+00:00 | 272.9ms | GC pause | - | - | 2.0s / 354,342 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 436,000 | 2026-07-29T09:51:24.0518438+00:00 | 268.5ms | GC pause | - | - | 2.0s / 354,342 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 437,000 | 2026-07-29T09:51:24.0547597+00:00 | 150.3ms | GC pause | - | - | 2.0s / 354,342 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 443,000 | 2026-07-29T09:51:24.073402+00:00 | 134.3ms | GC pause | - | - | 2.0s / 354,342 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 447,000 | 2026-07-29T09:51:24.0902539+00:00 | 121.9ms | GC pause | - | - | 2.0s / 354,342 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 463,000 | 2026-07-29T09:51:24.1745659+00:00 | 142.1ms | GC pause | - | - | 2.0s / 354,342 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 466,000 | 2026-07-29T09:51:24.1940194+00:00 | 144.1ms | GC pause | - | - | 2.0s / 354,342 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 491,000 | 2026-07-29T09:51:24.3338098+00:00 | 129.0ms | GC pause | - | - | 2.0s / 354,342 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 505,000 | 2026-07-29T09:51:24.3602188+00:00 | 166.8ms | GC pause | - | - | 2.0s / 354,342 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 508,000 | 2026-07-29T09:51:24.3651352+00:00 | 161.9ms | GC pause | - | - | 2.0s / 354,342 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 538,000 | 2026-07-29T09:51:24.4309183+00:00 | 126.6ms | GC pause | - | - | 2.0s / 354,342 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 548,000 | 2026-07-29T09:51:24.470449+00:00 | 104.5ms | GC pause | - | - | 2.0s / 354,342 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 594,000 | 2026-07-29T09:51:24.5970567+00:00 | 192.0ms | GC pause | - | - | 3.0s / 366,931 msg/s | Gen2 +1 / pause +2.6ms |
| Dekaf | 624,000 | 2026-07-29T09:51:24.6612884+00:00 | 190.7ms | throughput collapse | - | - | 3.0s / 366,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 634,000 | 2026-07-29T09:51:24.7214463+00:00 | 156.3ms | throughput collapse | - | - | 3.0s / 366,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 660,000 | 2026-07-29T09:51:24.8204435+00:00 | 117.0ms | throughput collapse | - | - | 3.0s / 366,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 666,000 | 2026-07-29T09:51:24.8482669+00:00 | 106.7ms | throughput collapse | - | - | 3.0s / 366,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 837,000 | 2026-07-29T09:51:25.1520833+00:00 | 112.3ms | throughput collapse | - | - | 3.0s / 366,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 877,000 | 2026-07-29T09:51:25.2285039+00:00 | 205.6ms | throughput collapse | - | - | 3.0s / 366,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 889,000 | 2026-07-29T09:51:25.2843894+00:00 | 203.6ms | throughput collapse | - | - | 3.0s / 366,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 893,000 | 2026-07-29T09:51:25.3236437+00:00 | 177.6ms | throughput collapse | - | - | 3.0s / 366,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 903,000 | 2026-07-29T09:51:25.3983964+00:00 | 135.7ms | throughput collapse | - | - | 3.0s / 366,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 907,000 | 2026-07-29T09:51:25.4155623+00:00 | 118.5ms | throughput collapse | - | - | 3.0s / 366,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 923,000 | 2026-07-29T09:51:25.4451651+00:00 | 179.9ms | throughput collapse | - | - | 3.0s / 366,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 953,000 | 2026-07-29T09:51:25.5434053+00:00 | 125.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 463,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 969,000 | 2026-07-29T09:51:25.6069739+00:00 | 115.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 463,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 973,000 | 2026-07-29T09:51:25.6211492+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 463,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,012,000 | 2026-07-29T09:51:25.7040013+00:00 | 108.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 463,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,047,000 | 2026-07-29T09:51:25.7726708+00:00 | 131.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 463,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,067,000 | 2026-07-29T09:51:25.8239163+00:00 | 115.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 463,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,118,000 | 2026-07-29T09:51:25.9451863+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 463,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,155,000 | 2026-07-29T09:51:25.9887213+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 463,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,259,000 | 2026-07-29T09:51:26.1710789+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 463,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,277,000 | 2026-07-29T09:51:26.1987051+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 463,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,279,000 | 2026-07-29T09:51:26.2040988+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 463,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,287,000 | 2026-07-29T09:51:26.2143553+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 463,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,349,000 | 2026-07-29T09:51:26.3452215+00:00 | 155.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 463,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,357,000 | 2026-07-29T09:51:26.3517419+00:00 | 149.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 463,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,359,000 | 2026-07-29T09:51:26.362418+00:00 | 162.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 463,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,365,000 | 2026-07-29T09:51:26.3741059+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 463,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,381,000 | 2026-07-29T09:51:26.3945365+00:00 | 132.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 463,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,409,000 | 2026-07-29T09:51:26.5263706+00:00 | 151.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 457,051 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,503,000 | 2026-07-29T09:51:26.7515878+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 457,051 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,529,000 | 2026-07-29T09:51:26.8178821+00:00 | 168.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 457,051 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,533,000 | 2026-07-29T09:51:26.8213024+00:00 | 164.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 457,051 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,537,000 | 2026-07-29T09:51:26.8236985+00:00 | 162.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 457,051 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,557,000 | 2026-07-29T09:51:26.8558696+00:00 | 178.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 457,051 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,583,000 | 2026-07-29T09:51:26.9915608+00:00 | 107.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 457,051 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,609,000 | 2026-07-29T09:51:27.0457101+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 457,051 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,645,000 | 2026-07-29T09:51:27.132912+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 457,051 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,765,000 | 2026-07-29T09:51:27.3511238+00:00 | 105.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 457,051 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,768,000 | 2026-07-29T09:51:27.3535417+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 457,051 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,771,000 | 2026-07-29T09:51:27.3620024+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 457,051 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,795,000 | 2026-07-29T09:51:27.3854251+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 457,051 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,843,000 | 2026-07-29T09:51:27.4871669+00:00 | 121.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 457,051 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,853,000 | 2026-07-29T09:51:27.4972069+00:00 | 136.7ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 494,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,857,000 | 2026-07-29T09:51:27.5019174+00:00 | 132.0ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 494,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,859,000 | 2026-07-29T09:51:27.5221197+00:00 | 111.8ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 494,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,873,000 | 2026-07-29T09:51:27.5610854+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 494,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,879,000 | 2026-07-29T09:51:27.5682311+00:00 | 109.8ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 494,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,002,000 | 2026-07-29T09:51:27.7843663+00:00 | 130.3ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 494,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,011,000 | 2026-07-29T09:51:27.7942061+00:00 | 121.0ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 494,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,021,000 | 2026-07-29T09:51:27.8115562+00:00 | 103.6ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 494,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,023,000 | 2026-07-29T09:51:27.8137854+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 494,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,091,000 | 2026-07-29T09:51:27.9625928+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 494,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,095,000 | 2026-07-29T09:51:27.9652599+00:00 | 113.8ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 494,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,187,000 | 2026-07-29T09:51:28.1768031+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 494,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,189,000 | 2026-07-29T09:51:28.1788926+00:00 | 131.0ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 494,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,199,000 | 2026-07-29T09:51:28.1998461+00:00 | 148.0ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 494,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,213,000 | 2026-07-29T09:51:28.2294668+00:00 | 126.1ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 494,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,227,000 | 2026-07-29T09:51:28.2896048+00:00 | 124.3ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 494,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,342,000 | 2026-07-29T09:51:28.5225045+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 677,323 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,345,000 | 2026-07-29T09:51:28.5256091+00:00 | 123.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 677,323 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,351,000 | 2026-07-29T09:51:28.532287+00:00 | 117.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 677,323 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,355,000 | 2026-07-29T09:51:28.5356945+00:00 | 113.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 677,323 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,361,000 | 2026-07-29T09:51:28.5412599+00:00 | 149.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 677,323 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,375,000 | 2026-07-29T09:51:28.5551637+00:00 | 150.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 677,323 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,411,000 | 2026-07-29T09:51:28.6719266+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 677,323 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,412,000 | 2026-07-29T09:51:28.6724989+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 677,323 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,428,000 | 2026-07-29T09:51:28.7114626+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 677,323 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,432,000 | 2026-07-29T09:51:28.7208051+00:00 | 106.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 677,323 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,951,000 | 2026-07-29T09:51:29.4208392+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 677,323 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,955,000 | 2026-07-29T09:51:29.4257475+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 677,323 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,277,000 | 2026-07-29T09:51:29.8732072+00:00 | 125.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 645,719 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,283,000 | 2026-07-29T09:51:29.8903613+00:00 | 107.9ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 645,719 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,287,000 | 2026-07-29T09:51:29.8957725+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 645,719 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,294,000 | 2026-07-29T09:51:29.9044763+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 645,719 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,186,000 | 2026-07-29T09:51:31.2501392+00:00 | 197.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 606,308 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,206,000 | 2026-07-29T09:51:31.2798498+00:00 | 199.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 606,308 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,214,000 | 2026-07-29T09:51:31.2873748+00:00 | 193.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 606,308 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,216,000 | 2026-07-29T09:51:31.3007747+00:00 | 180.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 606,308 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,224,000 | 2026-07-29T09:51:31.3133765+00:00 | 174.9ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 606,308 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,262,000 | 2026-07-29T09:51:31.4888911+00:00 | 120.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 606,308 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,281,000 | 2026-07-29T09:51:31.5168754+00:00 | 129.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 590,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,285,000 | 2026-07-29T09:51:31.5228363+00:00 | 113.6ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 590,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,291,000 | 2026-07-29T09:51:31.5319396+00:00 | 122.6ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 590,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,301,000 | 2026-07-29T09:51:31.5462103+00:00 | 116.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 590,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,305,000 | 2026-07-29T09:51:31.5530329+00:00 | 111.5ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 590,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,308,000 | 2026-07-29T09:51:31.5563714+00:00 | 111.3ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 590,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,312,000 | 2026-07-29T09:51:31.5629003+00:00 | 109.5ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 590,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,044,000 | 2026-07-29T09:51:32.8332604+00:00 | 123.0ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 523,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,060,000 | 2026-07-29T09:51:32.9281276+00:00 | 113.5ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 523,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,064,000 | 2026-07-29T09:51:32.9396164+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 523,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,076,000 | 2026-07-29T09:51:32.9585224+00:00 | 100.7ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 523,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,114,000 | 2026-07-29T09:51:33.0515957+00:00 | 119.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 523,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,116,000 | 2026-07-29T09:51:33.0533908+00:00 | 118.0ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 523,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,146,000 | 2026-07-29T09:51:33.0830691+00:00 | 151.3ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 523,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,150,000 | 2026-07-29T09:51:33.0862123+00:00 | 146.1ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 523,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,329,000 | 2026-07-29T09:51:33.439034+00:00 | 112.0ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 523,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,337,000 | 2026-07-29T09:51:33.4501985+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 523,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,339,000 | 2026-07-29T09:51:33.4523828+00:00 | 124.7ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 523,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,359,000 | 2026-07-29T09:51:33.4681718+00:00 | 116.2ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 523,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,367,000 | 2026-07-29T09:51:33.4781041+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 523,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,369,000 | 2026-07-29T09:51:33.4825524+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 523,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,581,000 | 2026-07-29T09:51:33.8241662+00:00 | 147.1ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 563,766 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,582,000 | 2026-07-29T09:51:33.8259579+00:00 | 145.3ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 563,766 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,585,000 | 2026-07-29T09:51:33.8335343+00:00 | 142.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 563,766 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,598,000 | 2026-07-29T09:51:33.8597287+00:00 | 144.9ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 563,766 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,601,000 | 2026-07-29T09:51:33.8665734+00:00 | 139.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 563,766 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,611,000 | 2026-07-29T09:51:33.8841377+00:00 | 125.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 563,766 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,612,000 | 2026-07-29T09:51:33.8908629+00:00 | 118.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 563,766 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,625,000 | 2026-07-29T09:51:33.9097446+00:00 | 112.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 563,766 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,710,000 | 2026-07-29T09:51:34.0972576+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 563,766 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,714,000 | 2026-07-29T09:51:34.1006885+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 563,766 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,840,000 | 2026-07-29T09:51:34.3007882+00:00 | 116.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 563,766 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,854,000 | 2026-07-29T09:51:34.3233684+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 563,766 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,880,000 | 2026-07-29T09:51:34.375691+00:00 | 124.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 563,766 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,910,000 | 2026-07-29T09:51:34.443846+00:00 | 105.7ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 563,766 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,546,000 | 2026-07-29T09:51:35.379766+00:00 | 129.7ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 678,528 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,564,000 | 2026-07-29T09:51:35.3971394+00:00 | 120.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 678,528 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,566,000 | 2026-07-29T09:51:35.3989249+00:00 | 118.8ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 678,528 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,576,000 | 2026-07-29T09:51:35.4066291+00:00 | 111.1ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 678,528 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,610,000 | 2026-07-29T09:51:35.4826358+00:00 | 103.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 678,528 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,616,000 | 2026-07-29T09:51:35.499018+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 678,528 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,634,000 | 2026-07-29T09:51:35.5411455+00:00 | 115.9ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 610,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,640,000 | 2026-07-29T09:51:35.5466508+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 610,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,870,000 | 2026-07-29T09:51:35.9026625+00:00 | 128.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 610,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,871,000 | 2026-07-29T09:51:35.9035503+00:00 | 158.0ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 610,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,874,000 | 2026-07-29T09:51:35.9056593+00:00 | 125.2ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 610,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,880,000 | 2026-07-29T09:51:35.9125097+00:00 | 118.4ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 610,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,882,000 | 2026-07-29T09:51:35.9248501+00:00 | 143.7ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 610,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,888,000 | 2026-07-29T09:51:35.9331932+00:00 | 140.9ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 610,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,891,000 | 2026-07-29T09:51:35.9387915+00:00 | 135.3ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 610,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,894,000 | 2026-07-29T09:51:35.9423983+00:00 | 109.8ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 610,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,902,000 | 2026-07-29T09:51:35.9582948+00:00 | 126.3ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 610,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,143,000 | 2026-07-29T09:51:36.3913532+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 610,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,446,000 | 2026-07-29T09:51:36.8226631+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 571,447 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,454,000 | 2026-07-29T09:51:36.8302953+00:00 | 146.8ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 571,447 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,456,000 | 2026-07-29T09:51:36.8391865+00:00 | 137.9ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 571,447 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,470,000 | 2026-07-29T09:51:36.8606106+00:00 | 124.5ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 571,447 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,474,000 | 2026-07-29T09:51:36.8906619+00:00 | 126.9ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 571,447 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,500,000 | 2026-07-29T09:51:36.9363692+00:00 | 147.0ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 571,447 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,506,000 | 2026-07-29T09:51:36.9699205+00:00 | 113.4ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 571,447 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,526,000 | 2026-07-29T09:51:37.0190047+00:00 | 115.9ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 571,447 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,136,000 | 2026-07-29T09:51:37.9906535+00:00 | 106.6ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 671,341 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,393,000 | 2026-07-29T09:51:38.3972618+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 671,341 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,657,000 | 2026-07-29T09:51:38.7979097+00:00 | 118.4ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 604,924 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,693,000 | 2026-07-29T09:51:38.8640764+00:00 | 119.9ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 604,924 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,697,000 | 2026-07-29T09:51:38.879307+00:00 | 108.2ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 604,924 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,703,000 | 2026-07-29T09:51:38.8860031+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 604,924 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,852,000 | 2026-07-29T09:51:39.1451268+00:00 | 116.6ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 604,924 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,855,000 | 2026-07-29T09:51:39.1505274+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 604,924 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,858,000 | 2026-07-29T09:51:39.1528111+00:00 | 110.3ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 604,924 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,862,000 | 2026-07-29T09:51:39.1585278+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 604,924 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,901,000 | 2026-07-29T09:51:39.2177851+00:00 | 133.8ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 604,924 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,941,000 | 2026-07-29T09:51:39.3259601+00:00 | 118.3ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 604,924 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,370,000 | 2026-07-29T09:51:39.9479215+00:00 | 113.7ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 685,861 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,374,000 | 2026-07-29T09:51:39.9565663+00:00 | 118.1ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 685,861 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,386,000 | 2026-07-29T09:51:39.9697246+00:00 | 113.1ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 685,861 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,685,000 | 2026-07-29T09:51:40.3937793+00:00 | 118.3ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 685,861 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,701,000 | 2026-07-29T09:51:40.418461+00:00 | 131.2ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 685,861 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,702,000 | 2026-07-29T09:51:40.4190362+00:00 | 130.6ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 685,861 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,708,000 | 2026-07-29T09:51:40.4291865+00:00 | 116.1ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 685,861 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,046,000 | 2026-07-29T09:51:40.9609123+00:00 | 149.0ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 592,777 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,054,000 | 2026-07-29T09:51:40.9696883+00:00 | 146.8ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 592,777 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,056,000 | 2026-07-29T09:51:40.9727198+00:00 | 159.2ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 592,777 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,060,000 | 2026-07-29T09:51:40.9765023+00:00 | 155.8ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 592,777 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,644,000 | 2026-07-29T09:51:41.8716988+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 814,150 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,200,000 | 2026-07-29T09:51:42.5532177+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 629,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,410,000 | 2026-07-29T09:51:42.8864058+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 629,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,424,000 | 2026-07-29T09:51:42.9025006+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 629,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,434,000 | 2026-07-29T09:51:42.9125239+00:00 | 145.7ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 629,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,436,000 | 2026-07-29T09:51:42.9142981+00:00 | 143.9ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 629,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,446,000 | 2026-07-29T09:51:42.9354928+00:00 | 137.4ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 629,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,450,000 | 2026-07-29T09:51:42.967747+00:00 | 117.0ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 629,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,734,000 | 2026-07-29T09:51:43.4127528+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 629,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,051,000 | 2026-07-29T09:51:43.8739393+00:00 | 109.2ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 694,823 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,052,000 | 2026-07-29T09:51:43.8749194+00:00 | 108.2ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 694,823 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,071,000 | 2026-07-29T09:51:43.8949347+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 694,823 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,892,000 | 2026-07-29T09:51:46.4009128+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 722,521 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,261,000 | 2026-07-29T09:51:46.8795676+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 793,672 msg/s | Gen2 +0 / pause +6.2ms |
| Dekaf | 15,042,000 | 2026-07-29T09:51:47.8859628+00:00 | 131.1ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 700,013 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,052,000 | 2026-07-29T09:51:47.8926262+00:00 | 130.5ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 700,013 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,071,000 | 2026-07-29T09:51:47.9231124+00:00 | 112.0ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 700,013 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,075,000 | 2026-07-29T09:51:47.9286859+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 700,013 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,348,000 | 2026-07-29T09:51:48.3377397+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 700,013 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,444,000 | 2026-07-29T09:51:51.3370708+00:00 | 124.0ms | broker/backlog (no scale or GC event) | - | - | 29.1s / 696,329 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,454,000 | 2026-07-29T09:51:51.3450639+00:00 | 115.7ms | broker/backlog (no scale or GC event) | - | - | 29.1s / 696,329 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,494,000 | 2026-07-29T09:51:51.4134037+00:00 | 111.9ms | broker/backlog (no scale or GC event) | - | - | 29.1s / 696,329 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,141,000 | 2026-07-29T09:51:52.3624133+00:00 | 131.5ms | broker/backlog (no scale or GC event) | - | - | 30.1s / 659,692 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,155,000 | 2026-07-29T09:51:52.3935088+00:00 | 112.5ms | broker/backlog (no scale or GC event) | - | - | 30.1s / 659,692 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,162,000 | 2026-07-29T09:51:52.3993656+00:00 | 121.0ms | broker/backlog (no scale or GC event) | - | - | 30.1s / 659,692 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,449,000 | 2026-07-29T09:51:52.8784097+00:00 | 111.5ms | broker/backlog (no scale or GC event) | 3:capacity/started, 3:capacity/failed | - | 31.1s / 639,100 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,459,000 | 2026-07-29T09:51:52.8956172+00:00 | 101.9ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 31.1s / 639,100 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,563,000 | 2026-07-29T09:51:57.4113288+00:00 | 100.2ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/failed, 1:capacity/failed | - | 35.1s / 700,923 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,173,000 | 2026-07-29T09:52:01.9072+00:00 | 107.0ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/failed, 1:capacity/failed | - | 40.1s / 796,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,183,000 | 2026-07-29T09:52:01.9147815+00:00 | 100.9ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/failed, 1:capacity/failed | - | 40.1s / 796,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,328,000 | 2026-07-29T09:52:10.3877156+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 48.1s / 813,570 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 32,335,000 | 2026-07-29T09:52:10.3916447+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 48.1s / 813,570 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 32,351,000 | 2026-07-29T09:52:10.4029158+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 48.1s / 813,570 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 32,355,000 | 2026-07-29T09:52:10.4062511+00:00 | 113.3ms | broker/backlog (no scale or GC event) | - | - | 48.1s / 813,570 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 40,242,000 | 2026-07-29T09:52:19.8937673+00:00 | 110.5ms | broker/backlog (no scale or GC event) | - | - | 58.1s / 786,518 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 40,633,000 | 2026-07-29T09:52:20.395309+00:00 | 113.6ms | broker/backlog (no scale or GC event) | - | - | 58.1s / 786,518 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 41,813,000 | 2026-07-29T09:52:21.9140547+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 60.1s / 796,130 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,259,000 | 2026-07-29T09:52:24.887617+00:00 | 106.9ms | broker/backlog (no scale or GC event) | - | - | 63.1s / 788,138 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 53,514,000 | 2026-07-29T09:52:35.8870397+00:00 | 107.0ms | broker/backlog (no scale or GC event) | - | - | 74.1s / 817,375 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 68,321,000 | 2026-07-29T09:52:52.4269567+00:00 | 100.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 90.2s / 835,971 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 79,137,000 | 2026-07-29T09:53:04.4462744+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 102.2s / 782,591 msg/s | Gen2 +0 / pause +1.8ms |
| Dekaf | 79,438,000 | 2026-07-29T09:53:04.8634875+00:00 | 143.5ms | broker/backlog (no scale or GC event) | - | - | 103.2s / 777,516 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 79,441,000 | 2026-07-29T09:53:04.8658771+00:00 | 141.3ms | broker/backlog (no scale or GC event) | - | - | 103.2s / 777,516 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 79,451,000 | 2026-07-29T09:53:04.8879616+00:00 | 126.9ms | broker/backlog (no scale or GC event) | - | - | 103.2s / 777,516 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 79,455,000 | 2026-07-29T09:53:04.9013553+00:00 | 113.5ms | broker/backlog (no scale or GC event) | - | - | 103.2s / 777,516 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 84,944,000 | 2026-07-29T09:53:11.4212572+00:00 | 112.2ms | broker/backlog (no scale or GC event) | - | - | 109.2s / 855,489 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 100,811,000 | 2026-07-29T09:53:29.4082629+00:00 | 104.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/failed | - | 127.2s / 910,158 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 100,812,000 | 2026-07-29T09:53:29.4120955+00:00 | 100.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/failed | - | 127.2s / 910,158 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 124,041,000 | 2026-07-29T09:53:53.8830033+00:00 | 108.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 152.2s / 895,063 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 178,021,000 | 2026-07-29T09:54:50.9050854+00:00 | 103.5ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 209.3s / 907,068 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 243,869,000 | 2026-07-29T09:56:02.9188526+00:00 | 119.2ms | broker/backlog (no scale or GC event) | 3:capacity/started, 3:capacity/succeeded | - | 281.4s / 777,671 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 252,029,000 | 2026-07-29T09:56:12.3705688+00:00 | 115.0ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 290.4s / 701,680 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 252,033,000 | 2026-07-29T09:56:12.3840066+00:00 | 102.8ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 290.4s / 701,680 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 252,053,000 | 2026-07-29T09:56:12.41935+00:00 | 104.8ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 290.4s / 701,680 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 252,073,000 | 2026-07-29T09:56:12.4552635+00:00 | 100.3ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 290.4s / 701,680 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 285,793,000 | 2026-07-29T09:56:51.4072942+00:00 | 125.0ms | broker/backlog (no scale or GC event) | - | - | 329.5s / 656,269 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 330,501,000 | 2026-07-29T09:57:41.4027626+00:00 | 102.4ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 379.5s / 872,284 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 435,306,000 | 2026-07-29T09:59:37.3916134+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 495.7s / 818,435 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 472,644,000 | 2026-07-29T10:00:21.3659289+00:00 | 127.7ms | broker/backlog (no scale or GC event) | - | - | 539.8s / 810,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 472,646,000 | 2026-07-29T10:00:21.3678265+00:00 | 125.9ms | broker/backlog (no scale or GC event) | - | - | 539.8s / 810,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 612,412,000 | 2026-07-29T10:03:05.8634179+00:00 | 112.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 704.0s / 873,336 msg/s | Gen2 +0 / pause +0.0ms |
| Confluent | 233,053,000 | 2026-07-29T10:12:36.4091026+00:00 | 104.0ms | GC pause | - | - | 374.4s / 509,525 msg/s | Gen2 +0 / pause +184.3ms |
| Confluent | 266,080,000 | 2026-07-29T10:13:33.0108768+00:00 | 108.4ms | GC pause | - | - | 430.4s / 425,826 msg/s | Gen2 +0 / pause +111.3ms |
| Confluent | 266,082,000 | 2026-07-29T10:13:33.0173325+00:00 | 104.8ms | GC pause | - | - | 430.4s / 425,826 msg/s | Gen2 +0 / pause +111.3ms |
| Confluent | 277,253,000 | 2026-07-29T10:13:57.4327394+00:00 | 109.2ms | GC pause | - | - | 455.4s / 461,764 msg/s | Gen2 +0 / pause +124.3ms |
| Confluent | 293,993,000 | 2026-07-29T10:14:32.928686+00:00 | 222.9ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,000,000 | 2026-07-29T10:14:32.9435395+00:00 | 208.9ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,002,000 | 2026-07-29T10:14:32.9481098+00:00 | 205.0ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,020,000 | 2026-07-29T10:14:32.9828299+00:00 | 192.4ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,022,000 | 2026-07-29T10:14:32.9856639+00:00 | 188.7ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,023,000 | 2026-07-29T10:14:32.9871686+00:00 | 188.3ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,030,000 | 2026-07-29T10:14:33.0024021+00:00 | 185.9ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,033,000 | 2026-07-29T10:14:33.00746+00:00 | 181.1ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,040,000 | 2026-07-29T10:14:33.0188963+00:00 | 174.3ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,043,000 | 2026-07-29T10:14:33.0228519+00:00 | 170.9ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,050,000 | 2026-07-29T10:14:33.0349485+00:00 | 159.9ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,053,000 | 2026-07-29T10:14:33.041023+00:00 | 154.0ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,062,000 | 2026-07-29T10:14:33.0593537+00:00 | 136.2ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,070,000 | 2026-07-29T10:14:33.0751018+00:00 | 141.0ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,073,000 | 2026-07-29T10:14:33.0814776+00:00 | 137.4ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,083,000 | 2026-07-29T10:14:33.0990495+00:00 | 132.9ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,090,000 | 2026-07-29T10:14:33.1130463+00:00 | 122.3ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,092,000 | 2026-07-29T10:14:33.1169267+00:00 | 114.9ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,100,000 | 2026-07-29T10:14:33.134083+00:00 | 106.6ms | GC pause | - | - | 490.5s / 458,792 msg/s | Gen2 +0 / pause +80.5ms |
| Confluent | 294,172,000 | 2026-07-29T10:14:33.3392628+00:00 | 110.1ms | GC pause | - | - | 491.5s / 471,716 msg/s | Gen2 +0 / pause +88.3ms |
| Confluent | 294,180,000 | 2026-07-29T10:14:33.3569198+00:00 | 110.4ms | GC pause | - | - | 491.5s / 471,716 msg/s | Gen2 +0 / pause +88.3ms |
| Confluent | 294,183,000 | 2026-07-29T10:14:33.3649367+00:00 | 104.6ms | GC pause | - | - | 491.5s / 471,716 msg/s | Gen2 +0 / pause +88.3ms |
| Confluent | 303,358,000 | 2026-07-29T10:14:52.4115464+00:00 | 114.0ms | GC pause | - | - | 510.5s / 437,111 msg/s | Gen2 +0 / pause +102.0ms |
| Confluent | 303,364,000 | 2026-07-29T10:14:52.4409025+00:00 | 110.4ms | GC pause | - | - | 510.5s / 437,111 msg/s | Gen2 +0 / pause +102.0ms |
| Confluent | 303,368,000 | 2026-07-29T10:14:52.4490575+00:00 | 102.6ms | GC pause | - | - | 510.5s / 437,111 msg/s | Gen2 +0 / pause +102.0ms |
| Confluent | 303,371,000 | 2026-07-29T10:14:52.4524303+00:00 | 106.8ms | GC pause | - | - | 510.5s / 437,111 msg/s | Gen2 +0 / pause +102.0ms |
| Confluent | 303,374,000 | 2026-07-29T10:14:52.4573154+00:00 | 102.2ms | GC pause | - | - | 510.5s / 437,111 msg/s | Gen2 +0 / pause +102.0ms |
| Confluent | 303,377,000 | 2026-07-29T10:14:52.4603256+00:00 | 100.2ms | GC pause | - | - | 510.5s / 437,111 msg/s | Gen2 +0 / pause +102.0ms |
| Confluent | 303,387,000 | 2026-07-29T10:14:52.4753687+00:00 | 116.7ms | GC pause | - | - | 510.5s / 437,111 msg/s | Gen2 +0 / pause +102.0ms |
| Confluent | 303,397,000 | 2026-07-29T10:14:52.4903964+00:00 | 104.4ms | GC pause | - | - | 510.5s / 437,111 msg/s | Gen2 +0 / pause +102.0ms |
| Confluent | 303,398,000 | 2026-07-29T10:14:52.4913565+00:00 | 103.5ms | GC pause | - | - | 510.5s / 437,111 msg/s | Gen2 +0 / pause +102.0ms |
| Confluent | 303,407,000 | 2026-07-29T10:14:52.5011052+00:00 | 102.6ms | GC pause | - | - | 510.5s / 437,111 msg/s | Gen2 +0 / pause +102.0ms |
| Confluent | 358,907,000 | 2026-07-29T10:16:34.9746346+00:00 | 100.6ms | GC pause | - | - | 612.7s / 499,585 msg/s | Gen2 +0 / pause +180.8ms |
| Confluent | 358,914,000 | 2026-07-29T10:16:34.9926291+00:00 | 111.4ms | GC pause | - | - | 612.7s / 499,585 msg/s | Gen2 +0 / pause +180.8ms |
| Confluent | 358,917,000 | 2026-07-29T10:16:34.9963805+00:00 | 107.5ms | GC pause | - | - | 612.7s / 499,585 msg/s | Gen2 +0 / pause +180.8ms |
| Confluent | 358,924,000 | 2026-07-29T10:16:35.0127639+00:00 | 102.5ms | GC pause | - | - | 612.7s / 499,585 msg/s | Gen2 +0 / pause +180.8ms |
| Confluent | 358,937,000 | 2026-07-29T10:16:35.0338401+00:00 | 123.8ms | GC pause | - | - | 612.7s / 499,585 msg/s | Gen2 +0 / pause +180.8ms |
| Confluent | 358,941,000 | 2026-07-29T10:16:35.0399757+00:00 | 118.2ms | GC pause | - | - | 612.7s / 499,585 msg/s | Gen2 +0 / pause +180.8ms |
| Confluent | 358,944,000 | 2026-07-29T10:16:35.0446824+00:00 | 112.6ms | GC pause | - | - | 612.7s / 499,585 msg/s | Gen2 +0 / pause +180.8ms |
| Confluent | 358,948,000 | 2026-07-29T10:16:35.0499418+00:00 | 108.8ms | GC pause | - | - | 612.7s / 499,585 msg/s | Gen2 +0 / pause +180.8ms |
| Confluent | 358,951,000 | 2026-07-29T10:16:35.0557996+00:00 | 103.1ms | GC pause | - | - | 612.7s / 499,585 msg/s | Gen2 +0 / pause +180.8ms |
| Confluent | 358,964,000 | 2026-07-29T10:16:35.0775282+00:00 | 103.0ms | GC pause | - | - | 612.7s / 499,585 msg/s | Gen2 +0 / pause +180.8ms |
| Confluent | 359,324,000 | 2026-07-29T10:16:35.8742838+00:00 | 100.5ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,334,000 | 2026-07-29T10:16:35.8957873+00:00 | 103.1ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,344,000 | 2026-07-29T10:16:35.912014+00:00 | 132.6ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,351,000 | 2026-07-29T10:16:35.9226524+00:00 | 134.0ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,354,000 | 2026-07-29T10:16:35.928638+00:00 | 142.8ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,358,000 | 2026-07-29T10:16:35.9371766+00:00 | 127.4ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,367,000 | 2026-07-29T10:16:35.9644593+00:00 | 110.7ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,374,000 | 2026-07-29T10:16:35.976397+00:00 | 135.3ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,384,000 | 2026-07-29T10:16:36.0007634+00:00 | 129.0ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,394,000 | 2026-07-29T10:16:36.0162326+00:00 | 129.6ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,397,000 | 2026-07-29T10:16:36.0235128+00:00 | 116.0ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,398,000 | 2026-07-29T10:16:36.029335+00:00 | 110.3ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,401,000 | 2026-07-29T10:16:36.0335856+00:00 | 159.9ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,407,000 | 2026-07-29T10:16:36.0383017+00:00 | 248.1ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,408,000 | 2026-07-29T10:16:36.0390602+00:00 | 247.4ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,411,000 | 2026-07-29T10:16:36.0448583+00:00 | 245.6ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,418,000 | 2026-07-29T10:16:36.0545092+00:00 | 240.6ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,421,000 | 2026-07-29T10:16:36.0579938+00:00 | 237.3ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,424,000 | 2026-07-29T10:16:36.0637295+00:00 | 122.2ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,431,000 | 2026-07-29T10:16:36.0779549+00:00 | 259.7ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,437,000 | 2026-07-29T10:16:36.0905226+00:00 | 259.4ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,441,000 | 2026-07-29T10:16:36.0964307+00:00 | 253.8ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,444,000 | 2026-07-29T10:16:36.1034342+00:00 | 121.8ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,448,000 | 2026-07-29T10:16:36.1097325+00:00 | 274.6ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,451,000 | 2026-07-29T10:16:36.1159151+00:00 | 268.7ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,454,000 | 2026-07-29T10:16:36.1226613+00:00 | 118.2ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,464,000 | 2026-07-29T10:16:36.1366323+00:00 | 108.2ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,467,000 | 2026-07-29T10:16:36.1432453+00:00 | 266.6ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,471,000 | 2026-07-29T10:16:36.1484223+00:00 | 299.7ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,477,000 | 2026-07-29T10:16:36.1608413+00:00 | 300.6ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,481,000 | 2026-07-29T10:16:36.1652611+00:00 | 297.4ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,488,000 | 2026-07-29T10:16:36.1755503+00:00 | 309.2ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,491,000 | 2026-07-29T10:16:36.1871167+00:00 | 297.8ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,494,000 | 2026-07-29T10:16:36.1950423+00:00 | 112.4ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,497,000 | 2026-07-29T10:16:36.1992305+00:00 | 342.6ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,498,000 | 2026-07-29T10:16:36.2034192+00:00 | 338.5ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,511,000 | 2026-07-29T10:16:36.2277071+00:00 | 339.3ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,514,000 | 2026-07-29T10:16:36.2354019+00:00 | 100.1ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,518,000 | 2026-07-29T10:16:36.2415004+00:00 | 333.5ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,528,000 | 2026-07-29T10:16:36.259437+00:00 | 324.9ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,534,000 | 2026-07-29T10:16:36.2729491+00:00 | 121.9ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,544,000 | 2026-07-29T10:16:36.2884829+00:00 | 113.0ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,547,000 | 2026-07-29T10:16:36.2943756+00:00 | 323.4ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,557,000 | 2026-07-29T10:16:36.3123544+00:00 | 320.1ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,558,000 | 2026-07-29T10:16:36.3143935+00:00 | 318.1ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,564,000 | 2026-07-29T10:16:36.3269661+00:00 | 158.2ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,567,000 | 2026-07-29T10:16:36.3385707+00:00 | 303.2ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,568,000 | 2026-07-29T10:16:36.3394726+00:00 | 302.4ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,574,000 | 2026-07-29T10:16:36.35158+00:00 | 141.1ms | GC pause | - | - | 613.7s / 492,719 msg/s | Gen2 +0 / pause +168.7ms |
| Confluent | 359,578,000 | 2026-07-29T10:16:36.35601+00:00 | 296.4ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,584,000 | 2026-07-29T10:16:36.3657271+00:00 | 135.8ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,588,000 | 2026-07-29T10:16:36.3759204+00:00 | 289.4ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,591,000 | 2026-07-29T10:16:36.3800751+00:00 | 295.1ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,594,000 | 2026-07-29T10:16:36.3935417+00:00 | 113.7ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,604,000 | 2026-07-29T10:16:36.4118807+00:00 | 115.6ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,607,000 | 2026-07-29T10:16:36.4162574+00:00 | 280.9ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,614,000 | 2026-07-29T10:16:36.430398+00:00 | 101.5ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,621,000 | 2026-07-29T10:16:36.4528577+00:00 | 268.2ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,638,000 | 2026-07-29T10:16:36.4940624+00:00 | 267.7ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +288.4ms |
| Confluent | 359,641,000 | 2026-07-29T10:16:36.5035903+00:00 | 259.4ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,648,000 | 2026-07-29T10:16:36.514642+00:00 | 250.5ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,667,000 | 2026-07-29T10:16:36.5568677+00:00 | 228.1ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,668,000 | 2026-07-29T10:16:36.5630219+00:00 | 222.2ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,677,000 | 2026-07-29T10:16:36.5816522+00:00 | 218.0ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,678,000 | 2026-07-29T10:16:36.582448+00:00 | 217.3ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,697,000 | 2026-07-29T10:16:36.6353819+00:00 | 222.9ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,701,000 | 2026-07-29T10:16:36.6448318+00:00 | 219.2ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,707,000 | 2026-07-29T10:16:36.6564525+00:00 | 225.6ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,717,000 | 2026-07-29T10:16:36.6760675+00:00 | 225.6ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,718,000 | 2026-07-29T10:16:36.6785625+00:00 | 223.1ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,721,000 | 2026-07-29T10:16:36.6839946+00:00 | 217.9ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,727,000 | 2026-07-29T10:16:36.6932762+00:00 | 220.4ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,728,000 | 2026-07-29T10:16:36.6942367+00:00 | 225.4ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,738,000 | 2026-07-29T10:16:36.7088362+00:00 | 217.2ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,748,000 | 2026-07-29T10:16:36.7257674+00:00 | 236.8ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,761,000 | 2026-07-29T10:16:36.750281+00:00 | 232.1ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,767,000 | 2026-07-29T10:16:36.764977+00:00 | 225.5ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,768,000 | 2026-07-29T10:16:36.7666783+00:00 | 260.7ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,774,000 | 2026-07-29T10:16:36.7849916+00:00 | 102.9ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,777,000 | 2026-07-29T10:16:36.7924126+00:00 | 245.7ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,781,000 | 2026-07-29T10:16:36.7988622+00:00 | 257.3ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,784,000 | 2026-07-29T10:16:36.8025086+00:00 | 127.3ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,788,000 | 2026-07-29T10:16:36.8136807+00:00 | 248.2ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,791,000 | 2026-07-29T10:16:36.8169484+00:00 | 251.9ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,797,000 | 2026-07-29T10:16:36.8264457+00:00 | 253.0ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,798,000 | 2026-07-29T10:16:36.8285044+00:00 | 251.0ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,801,000 | 2026-07-29T10:16:36.8375638+00:00 | 242.2ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,804,000 | 2026-07-29T10:16:36.840855+00:00 | 172.6ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,807,000 | 2026-07-29T10:16:36.8450836+00:00 | 237.7ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,808,000 | 2026-07-29T10:16:36.8476285+00:00 | 240.7ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,811,000 | 2026-07-29T10:16:36.8548931+00:00 | 233.6ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,817,000 | 2026-07-29T10:16:36.8655299+00:00 | 231.3ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,818,000 | 2026-07-29T10:16:36.8664871+00:00 | 230.4ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,828,000 | 2026-07-29T10:16:36.8995478+00:00 | 210.9ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,837,000 | 2026-07-29T10:16:36.9117385+00:00 | 211.3ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,838,000 | 2026-07-29T10:16:36.912959+00:00 | 210.2ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,847,000 | 2026-07-29T10:16:36.9290031+00:00 | 207.2ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,848,000 | 2026-07-29T10:16:36.9338699+00:00 | 202.4ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,854,000 | 2026-07-29T10:16:36.9416596+00:00 | 232.1ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,868,000 | 2026-07-29T10:16:36.963942+00:00 | 214.9ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,871,000 | 2026-07-29T10:16:36.9670195+00:00 | 212.1ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,874,000 | 2026-07-29T10:16:36.9698805+00:00 | 328.6ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,877,000 | 2026-07-29T10:16:36.9739932+00:00 | 213.3ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,884,000 | 2026-07-29T10:16:36.9857793+00:00 | 319.1ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,891,000 | 2026-07-29T10:16:36.9975977+00:00 | 199.4ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,898,000 | 2026-07-29T10:16:37.0083086+00:00 | 193.3ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,914,000 | 2026-07-29T10:16:37.0340816+00:00 | 317.3ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,918,000 | 2026-07-29T10:16:37.0393872+00:00 | 185.8ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,931,000 | 2026-07-29T10:16:37.0574652+00:00 | 182.0ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,934,000 | 2026-07-29T10:16:37.0610588+00:00 | 315.4ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,937,000 | 2026-07-29T10:16:37.0665622+00:00 | 176.7ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,938,000 | 2026-07-29T10:16:37.0676891+00:00 | 175.6ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,948,000 | 2026-07-29T10:16:37.0839194+00:00 | 184.7ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,954,000 | 2026-07-29T10:16:37.0945092+00:00 | 315.7ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,957,000 | 2026-07-29T10:16:37.0982288+00:00 | 182.1ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,958,000 | 2026-07-29T10:16:37.0996668+00:00 | 187.4ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,961,000 | 2026-07-29T10:16:37.1048257+00:00 | 182.4ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,967,000 | 2026-07-29T10:16:37.1132845+00:00 | 179.8ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,971,000 | 2026-07-29T10:16:37.1197224+00:00 | 266.0ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,974,000 | 2026-07-29T10:16:37.1237713+00:00 | 352.0ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,978,000 | 2026-07-29T10:16:37.1316192+00:00 | 265.9ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,981,000 | 2026-07-29T10:16:37.1364289+00:00 | 288.0ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,987,000 | 2026-07-29T10:16:37.1471542+00:00 | 277.7ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,988,000 | 2026-07-29T10:16:37.1481361+00:00 | 276.8ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,991,000 | 2026-07-29T10:16:37.1532349+00:00 | 271.9ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 359,997,000 | 2026-07-29T10:16:37.1625888+00:00 | 270.7ms | GC pause | - | - | 614.7s / 541,483 msg/s | Gen2 +0 / pause +119.6ms |
| Confluent | 360,001,000 | 2026-07-29T10:16:37.1689246+00:00 | 326.1ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,004,000 | 2026-07-29T10:16:37.1741386+00:00 | 352.5ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,007,000 | 2026-07-29T10:16:37.1805623+00:00 | 361.9ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,008,000 | 2026-07-29T10:16:37.1813797+00:00 | 362.2ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,011,000 | 2026-07-29T10:16:37.1862846+00:00 | 357.9ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,014,000 | 2026-07-29T10:16:37.192675+00:00 | 341.4ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,017,000 | 2026-07-29T10:16:37.1965309+00:00 | 348.7ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,018,000 | 2026-07-29T10:16:37.2003937+00:00 | 344.9ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,021,000 | 2026-07-29T10:16:37.2042935+00:00 | 347.2ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,024,000 | 2026-07-29T10:16:37.2090236+00:00 | 350.1ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,028,000 | 2026-07-29T10:16:37.2157096+00:00 | 355.6ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,038,000 | 2026-07-29T10:16:37.2333452+00:00 | 361.2ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,044,000 | 2026-07-29T10:16:37.2418597+00:00 | 364.7ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,047,000 | 2026-07-29T10:16:37.2462269+00:00 | 360.7ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,051,000 | 2026-07-29T10:16:37.2541938+00:00 | 358.1ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,054,000 | 2026-07-29T10:16:37.2583166+00:00 | 445.0ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,061,000 | 2026-07-29T10:16:37.271446+00:00 | 344.4ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,064,000 | 2026-07-29T10:16:37.2744078+00:00 | 444.8ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,068,000 | 2026-07-29T10:16:37.2825544+00:00 | 360.2ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,077,000 | 2026-07-29T10:16:37.2980224+00:00 | 354.9ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,078,000 | 2026-07-29T10:16:37.2999957+00:00 | 353.0ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,084,000 | 2026-07-29T10:16:37.3091005+00:00 | 478.1ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,087,000 | 2026-07-29T10:16:37.3143421+00:00 | 350.9ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,097,000 | 2026-07-29T10:16:37.3300551+00:00 | 345.6ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,098,000 | 2026-07-29T10:16:37.3317182+00:00 | 344.0ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,101,000 | 2026-07-29T10:16:37.336345+00:00 | 339.6ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,107,000 | 2026-07-29T10:16:37.3467786+00:00 | 337.3ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,108,000 | 2026-07-29T10:16:37.3476407+00:00 | 349.0ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,114,000 | 2026-07-29T10:16:37.3601337+00:00 | 462.2ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,117,000 | 2026-07-29T10:16:37.3663664+00:00 | 355.1ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,128,000 | 2026-07-29T10:16:37.3932395+00:00 | 348.1ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,134,000 | 2026-07-29T10:16:37.4035965+00:00 | 449.2ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,137,000 | 2026-07-29T10:16:37.4088657+00:00 | 343.8ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,138,000 | 2026-07-29T10:16:37.4096973+00:00 | 343.1ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,144,000 | 2026-07-29T10:16:37.4200399+00:00 | 443.3ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,147,000 | 2026-07-29T10:16:37.4255904+00:00 | 328.0ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,151,000 | 2026-07-29T10:16:37.4325892+00:00 | 338.3ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,154,000 | 2026-07-29T10:16:37.4384098+00:00 | 427.4ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,158,000 | 2026-07-29T10:16:37.4510387+00:00 | 371.9ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,164,000 | 2026-07-29T10:16:37.4634143+00:00 | 429.5ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,167,000 | 2026-07-29T10:16:37.4662951+00:00 | 373.4ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,177,000 | 2026-07-29T10:16:37.4870002+00:00 | 359.1ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +280.4ms |
| Confluent | 360,181,000 | 2026-07-29T10:16:37.4957218+00:00 | 351.0ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,184,000 | 2026-07-29T10:16:37.499147+00:00 | 418.3ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,187,000 | 2026-07-29T10:16:37.5064697+00:00 | 342.0ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,188,000 | 2026-07-29T10:16:37.5099953+00:00 | 340.9ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,194,000 | 2026-07-29T10:16:37.519633+00:00 | 405.6ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,198,000 | 2026-07-29T10:16:37.5285035+00:00 | 323.6ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,201,000 | 2026-07-29T10:16:37.5364783+00:00 | 317.6ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,204,000 | 2026-07-29T10:16:37.5427283+00:00 | 426.0ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,207,000 | 2026-07-29T10:16:37.5484236+00:00 | 319.9ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,211,000 | 2026-07-29T10:16:37.5599043+00:00 | 334.0ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,214,000 | 2026-07-29T10:16:37.5656446+00:00 | 404.9ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,217,000 | 2026-07-29T10:16:37.5702169+00:00 | 330.4ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,224,000 | 2026-07-29T10:16:37.5825652+00:00 | 389.2ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,227,000 | 2026-07-29T10:16:37.5858195+00:00 | 323.7ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,231,000 | 2026-07-29T10:16:37.5916759+00:00 | 321.4ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,237,000 | 2026-07-29T10:16:37.6014472+00:00 | 321.9ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,238,000 | 2026-07-29T10:16:37.6033467+00:00 | 320.1ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,248,000 | 2026-07-29T10:16:37.6213569+00:00 | 319.1ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,254,000 | 2026-07-29T10:16:37.6285206+00:00 | 357.2ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,258,000 | 2026-07-29T10:16:37.6348723+00:00 | 338.0ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,264,000 | 2026-07-29T10:16:37.6538399+00:00 | 344.8ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,267,000 | 2026-07-29T10:16:37.6614875+00:00 | 316.5ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,268,000 | 2026-07-29T10:16:37.6632783+00:00 | 314.8ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,277,000 | 2026-07-29T10:16:37.6788341+00:00 | 300.8ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,284,000 | 2026-07-29T10:16:37.6979512+00:00 | 326.7ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,291,000 | 2026-07-29T10:16:37.7093495+00:00 | 280.7ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,297,000 | 2026-07-29T10:16:37.722487+00:00 | 273.2ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,298,000 | 2026-07-29T10:16:37.723466+00:00 | 272.3ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,301,000 | 2026-07-29T10:16:37.7287651+00:00 | 267.5ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,307,000 | 2026-07-29T10:16:37.7385804+00:00 | 259.3ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,317,000 | 2026-07-29T10:16:37.7748101+00:00 | 230.3ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,318,000 | 2026-07-29T10:16:37.7833633+00:00 | 221.8ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,321,000 | 2026-07-29T10:16:37.7858554+00:00 | 220.0ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,324,000 | 2026-07-29T10:16:37.7967314+00:00 | 246.7ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,328,000 | 2026-07-29T10:16:37.8173999+00:00 | 207.0ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,331,000 | 2026-07-29T10:16:37.8252925+00:00 | 199.3ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,338,000 | 2026-07-29T10:16:37.8448739+00:00 | 185.8ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,341,000 | 2026-07-29T10:16:37.865483+00:00 | 165.5ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,347,000 | 2026-07-29T10:16:37.8743537+00:00 | 157.9ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,351,000 | 2026-07-29T10:16:37.8844746+00:00 | 154.1ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,358,000 | 2026-07-29T10:16:37.9161364+00:00 | 126.4ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Confluent | 360,364,000 | 2026-07-29T10:16:37.9379151+00:00 | 132.6ms | GC pause | - | - | 615.7s / 462,152 msg/s | Gen2 +0 / pause +160.8ms |
| Dekaf (3conn) | 194,000 | 2026-07-29T10:21:36.2384915+00:00 | 100.4ms | GC pause | - | - | 1.0s / 455,379 msg/s | Gen2 +1 / pause +2.0ms |
| Dekaf (3conn) | 207,000 | 2026-07-29T10:21:36.2585804+00:00 | 103.8ms | GC pause | - | - | 1.0s / 455,379 msg/s | Gen2 +1 / pause +2.0ms |
| Dekaf (3conn) | 291,000 | 2026-07-29T10:21:36.4311646+00:00 | 105.0ms | GC pause | - | - | 1.0s / 455,379 msg/s | Gen2 +1 / pause +2.0ms |
| Dekaf (3conn) | 292,000 | 2026-07-29T10:21:36.4345144+00:00 | 104.4ms | GC pause | - | - | 1.0s / 455,379 msg/s | Gen2 +1 / pause +2.0ms |
| Dekaf (3conn) | 401,000 | 2026-07-29T10:21:36.6290372+00:00 | 129.1ms | GC pause | - | - | 1.0s / 455,379 msg/s | Gen2 +1 / pause +2.0ms |
| Dekaf (3conn) | 402,000 | 2026-07-29T10:21:36.6301002+00:00 | 128.1ms | GC pause | - | - | 1.0s / 455,379 msg/s | Gen2 +1 / pause +2.0ms |
| Dekaf (3conn) | 405,000 | 2026-07-29T10:21:36.6336754+00:00 | 126.0ms | GC pause | - | - | 1.0s / 455,379 msg/s | Gen2 +1 / pause +2.0ms |
| Dekaf (3conn) | 408,000 | 2026-07-29T10:21:36.6387145+00:00 | 120.9ms | GC pause | - | - | 1.0s / 455,379 msg/s | Gen2 +1 / pause +2.0ms |
| Dekaf (3conn) | 411,000 | 2026-07-29T10:21:36.643788+00:00 | 115.8ms | GC pause | - | - | 1.0s / 455,379 msg/s | Gen2 +1 / pause +2.0ms |
| Dekaf (3conn) | 412,000 | 2026-07-29T10:21:36.6454751+00:00 | 114.2ms | GC pause | - | - | 1.0s / 455,379 msg/s | Gen2 +1 / pause +2.0ms |
| Dekaf (3conn) | 415,000 | 2026-07-29T10:21:36.6518333+00:00 | 115.4ms | GC pause | - | - | 1.0s / 455,379 msg/s | Gen2 +1 / pause +2.0ms |
| Dekaf (3conn) | 418,000 | 2026-07-29T10:21:36.6568086+00:00 | 110.4ms | GC pause | - | - | 1.0s / 455,379 msg/s | Gen2 +1 / pause +2.0ms |
| Dekaf (3conn) | 421,000 | 2026-07-29T10:21:36.6659171+00:00 | 101.3ms | GC pause | - | - | 1.0s / 455,379 msg/s | Gen2 +1 / pause +2.0ms |
| Dekaf (3conn) | 422,000 | 2026-07-29T10:21:36.6671867+00:00 | 100.0ms | GC pause | - | - | 1.0s / 455,379 msg/s | Gen2 +1 / pause +2.0ms |
| Dekaf (3conn) | 806,000 | 2026-07-29T10:21:37.5323416+00:00 | 140.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 466,005 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 807,000 | 2026-07-29T10:21:37.5328554+00:00 | 134.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 466,005 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 814,000 | 2026-07-29T10:21:37.5590657+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 466,005 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,000 | 2026-07-29T10:21:37.5659469+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 466,005 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 817,000 | 2026-07-29T10:21:37.566594+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 466,005 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 824,000 | 2026-07-29T10:21:37.5830902+00:00 | 132.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 466,005 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,000 | 2026-07-29T10:21:37.5840089+00:00 | 131.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 466,005 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 834,000 | 2026-07-29T10:21:37.6051643+00:00 | 110.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 466,005 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 836,000 | 2026-07-29T10:21:37.6079276+00:00 | 107.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 466,005 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 840,000 | 2026-07-29T10:21:37.6163819+00:00 | 120.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 466,005 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 843,000 | 2026-07-29T10:21:37.6227824+00:00 | 118.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 466,005 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,131,000 | 2026-07-29T10:21:38.1807401+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 687,999 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,132,000 | 2026-07-29T10:21:38.1813159+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 687,999 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,301,000 | 2026-07-29T10:21:40.992076+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 748,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,302,000 | 2026-07-29T10:21:40.9943036+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 748,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,574,000 | 2026-07-29T10:21:41.324996+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 748,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,576,000 | 2026-07-29T10:21:41.3256787+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 748,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,584,000 | 2026-07-29T10:21:41.3305035+00:00 | 107.0ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 748,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,586,000 | 2026-07-29T10:21:41.332428+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 748,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,587,000 | 2026-07-29T10:21:41.3330323+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 748,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,594,000 | 2026-07-29T10:21:41.3391843+00:00 | 113.2ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 748,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,596,000 | 2026-07-29T10:21:41.3407805+00:00 | 111.6ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 748,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,604,000 | 2026-07-29T10:21:41.3508302+00:00 | 108.2ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 748,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,606,000 | 2026-07-29T10:21:41.3520642+00:00 | 111.1ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 748,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,614,000 | 2026-07-29T10:21:41.3571782+00:00 | 110.8ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 748,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,616,000 | 2026-07-29T10:21:41.3591939+00:00 | 108.8ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 748,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,940,000 | 2026-07-29T10:21:41.8259527+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,958,000 | 2026-07-29T10:21:41.8415373+00:00 | 136.3ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,960,000 | 2026-07-29T10:21:41.8424579+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,965,000 | 2026-07-29T10:21:41.8469207+00:00 | 134.6ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,968,000 | 2026-07-29T10:21:41.849226+00:00 | 132.3ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,971,000 | 2026-07-29T10:21:41.851596+00:00 | 114.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,972,000 | 2026-07-29T10:21:41.8521645+00:00 | 114.4ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,975,000 | 2026-07-29T10:21:41.8542858+00:00 | 139.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,978,000 | 2026-07-29T10:21:41.8563551+00:00 | 137.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,981,000 | 2026-07-29T10:21:41.8587974+00:00 | 119.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,982,000 | 2026-07-29T10:21:41.8608284+00:00 | 117.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,983,000 | 2026-07-29T10:21:41.8620447+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,985,000 | 2026-07-29T10:21:41.8639645+00:00 | 132.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,988,000 | 2026-07-29T10:21:41.8777376+00:00 | 119.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,989,000 | 2026-07-29T10:21:41.8794212+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,345,000 | 2026-07-29T10:21:42.3686638+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,348,000 | 2026-07-29T10:21:42.3732341+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,355,000 | 2026-07-29T10:21:42.3811544+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,358,000 | 2026-07-29T10:21:42.3839011+00:00 | 100.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,365,000 | 2026-07-29T10:21:42.3889428+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,368,000 | 2026-07-29T10:21:42.3916448+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,370,000 | 2026-07-29T10:21:42.3955382+00:00 | 100.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,405,000 | 2026-07-29T10:21:42.4747666+00:00 | 110.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,408,000 | 2026-07-29T10:21:42.4780522+00:00 | 108.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,415,000 | 2026-07-29T10:21:42.4858346+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,428,000 | 2026-07-29T10:21:42.4961536+00:00 | 108.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,435,000 | 2026-07-29T10:21:42.5008142+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,438,000 | 2026-07-29T10:21:42.5036207+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 783,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,285,000 | 2026-07-29T10:21:45.2997588+00:00 | 117.3ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 922,684 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,288,000 | 2026-07-29T10:21:45.3016767+00:00 | 115.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 922,684 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,295,000 | 2026-07-29T10:21:45.3093673+00:00 | 108.5ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 922,684 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,298,000 | 2026-07-29T10:21:45.3112624+00:00 | 108.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 922,684 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,305,000 | 2026-07-29T10:21:45.3154583+00:00 | 104.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 922,684 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,308,000 | 2026-07-29T10:21:45.3171113+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 922,684 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,315,000 | 2026-07-29T10:21:45.3214517+00:00 | 107.3ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 922,684 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,318,000 | 2026-07-29T10:21:45.3236133+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 922,684 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,325,000 | 2026-07-29T10:21:45.3282863+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 922,684 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,294,000 | 2026-07-29T10:21:47.4496208+00:00 | 113.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 876,941 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,296,000 | 2026-07-29T10:21:47.4506604+00:00 | 112.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 876,941 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,304,000 | 2026-07-29T10:21:47.4613874+00:00 | 111.9ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 876,941 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,306,000 | 2026-07-29T10:21:47.4634004+00:00 | 109.9ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 876,941 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,307,000 | 2026-07-29T10:21:47.4639425+00:00 | 107.9ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 876,941 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,314,000 | 2026-07-29T10:21:47.469153+00:00 | 120.3ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 876,941 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,316,000 | 2026-07-29T10:21:47.4742101+00:00 | 115.2ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 876,941 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,317,000 | 2026-07-29T10:21:47.4746353+00:00 | 105.3ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 876,941 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,324,000 | 2026-07-29T10:21:47.4846688+00:00 | 116.5ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 876,941 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,326,000 | 2026-07-29T10:21:47.485625+00:00 | 115.5ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 876,941 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,327,000 | 2026-07-29T10:21:47.4886129+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 876,941 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,432,000 | 2026-07-29T10:21:47.6677245+00:00 | 137.9ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 876,941 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,441,000 | 2026-07-29T10:21:47.6715842+00:00 | 134.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 876,941 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,442,000 | 2026-07-29T10:21:47.6722163+00:00 | 133.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 876,941 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,451,000 | 2026-07-29T10:21:47.6849507+00:00 | 140.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,452,000 | 2026-07-29T10:21:47.6885096+00:00 | 137.1ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,461,000 | 2026-07-29T10:21:47.6953565+00:00 | 130.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,462,000 | 2026-07-29T10:21:47.696124+00:00 | 135.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,471,000 | 2026-07-29T10:21:47.7050255+00:00 | 128.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,472,000 | 2026-07-29T10:21:47.7054268+00:00 | 127.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,481,000 | 2026-07-29T10:21:47.7128725+00:00 | 122.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,482,000 | 2026-07-29T10:21:47.7132802+00:00 | 121.9ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,491,000 | 2026-07-29T10:21:47.7163976+00:00 | 119.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,492,000 | 2026-07-29T10:21:47.7168932+00:00 | 120.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,501,000 | 2026-07-29T10:21:47.7229099+00:00 | 118.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,203,000 | 2026-07-29T10:21:48.3680978+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,209,000 | 2026-07-29T10:21:48.3704855+00:00 | 114.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,213,000 | 2026-07-29T10:21:48.3721165+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,219,000 | 2026-07-29T10:21:48.3744142+00:00 | 110.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,223,000 | 2026-07-29T10:21:48.3766745+00:00 | 108.1ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,229,000 | 2026-07-29T10:21:48.3797047+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,233,000 | 2026-07-29T10:21:48.382699+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,027,734 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,434,000 | 2026-07-29T10:21:49.8351541+00:00 | 132.0ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,436,000 | 2026-07-29T10:21:49.8362558+00:00 | 130.9ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,444,000 | 2026-07-29T10:21:49.847788+00:00 | 209.7ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,446,000 | 2026-07-29T10:21:49.8499084+00:00 | 207.6ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,454,000 | 2026-07-29T10:21:49.8577451+00:00 | 195.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,456,000 | 2026-07-29T10:21:49.8622094+00:00 | 190.7ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,464,000 | 2026-07-29T10:21:49.8688574+00:00 | 196.4ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,466,000 | 2026-07-29T10:21:49.8697893+00:00 | 195.5ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,474,000 | 2026-07-29T10:21:49.8738851+00:00 | 191.4ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,476,000 | 2026-07-29T10:21:49.8749192+00:00 | 190.4ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,477,000 | 2026-07-29T10:21:49.8752472+00:00 | 177.7ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,484,000 | 2026-07-29T10:21:49.8930144+00:00 | 173.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,486,000 | 2026-07-29T10:21:49.9099761+00:00 | 156.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,487,000 | 2026-07-29T10:21:49.9102961+00:00 | 142.6ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,494,000 | 2026-07-29T10:21:49.9342475+00:00 | 135.6ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,496,000 | 2026-07-29T10:21:49.9350756+00:00 | 147.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,497,000 | 2026-07-29T10:21:49.9358073+00:00 | 129.5ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,504,000 | 2026-07-29T10:21:49.9709971+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,506,000 | 2026-07-29T10:21:49.9717277+00:00 | 110.7ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,809,000 | 2026-07-29T10:21:50.3887732+00:00 | 127.0ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,813,000 | 2026-07-29T10:21:50.3918888+00:00 | 123.9ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,819,000 | 2026-07-29T10:21:50.3970677+00:00 | 125.9ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,823,000 | 2026-07-29T10:21:50.3999584+00:00 | 128.7ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,829,000 | 2026-07-29T10:21:50.4040729+00:00 | 130.4ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,830,000 | 2026-07-29T10:21:50.4046613+00:00 | 116.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,833,000 | 2026-07-29T10:21:50.4065903+00:00 | 144.1ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,839,000 | 2026-07-29T10:21:50.41065+00:00 | 142.1ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,840,000 | 2026-07-29T10:21:50.4114355+00:00 | 115.6ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,843,000 | 2026-07-29T10:21:50.413708+00:00 | 143.4ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,849,000 | 2026-07-29T10:21:50.4167102+00:00 | 142.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,850,000 | 2026-07-29T10:21:50.4171535+00:00 | 135.6ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,853,000 | 2026-07-29T10:21:50.4293929+00:00 | 129.9ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,859,000 | 2026-07-29T10:21:50.4618416+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 683,757 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,347,000 | 2026-07-29T10:21:54.2710959+00:00 | 128.2ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 857,443 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,487,000 | 2026-07-29T10:21:54.4927956+00:00 | 152.3ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 857,443 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,497,000 | 2026-07-29T10:21:54.499858+00:00 | 151.8ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 857,443 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,507,000 | 2026-07-29T10:21:54.5094596+00:00 | 142.2ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 857,443 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,517,000 | 2026-07-29T10:21:54.5157474+00:00 | 145.1ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 857,443 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,524,000 | 2026-07-29T10:21:54.5276188+00:00 | 107.8ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 857,443 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,526,000 | 2026-07-29T10:21:54.53035+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 857,443 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,527,000 | 2026-07-29T10:21:54.5310012+00:00 | 139.8ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 857,443 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,534,000 | 2026-07-29T10:21:54.5422987+00:00 | 102.8ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 857,443 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,537,000 | 2026-07-29T10:21:54.5478306+00:00 | 123.0ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 857,443 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,547,000 | 2026-07-29T10:21:54.5605285+00:00 | 120.6ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 857,443 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 19,160,000 | 2026-07-29T10:21:58.3256596+00:00 | 123.5ms | broker/backlog (no scale or GC event) | - | - | 23.1s / 753,560 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 19,170,000 | 2026-07-29T10:21:58.3326059+00:00 | 130.9ms | broker/backlog (no scale or GC event) | - | - | 23.1s / 753,560 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 19,180,000 | 2026-07-29T10:21:58.3440133+00:00 | 122.1ms | broker/backlog (no scale or GC event) | - | - | 23.1s / 753,560 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 19,190,000 | 2026-07-29T10:21:58.3562308+00:00 | 112.2ms | broker/backlog (no scale or GC event) | - | - | 23.1s / 753,560 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 19,200,000 | 2026-07-29T10:21:58.3612611+00:00 | 119.3ms | broker/backlog (no scale or GC event) | - | - | 23.1s / 753,560 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 23,004,000 | 2026-07-29T10:22:02.3572091+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 27.1s / 884,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 23,006,000 | 2026-07-29T10:22:02.3583592+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 27.1s / 884,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 23,014,000 | 2026-07-29T10:22:02.3642877+00:00 | 109.3ms | broker/backlog (no scale or GC event) | - | - | 27.1s / 884,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 23,016,000 | 2026-07-29T10:22:02.3653541+00:00 | 108.2ms | broker/backlog (no scale or GC event) | - | - | 27.1s / 884,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 23,024,000 | 2026-07-29T10:22:02.3693212+00:00 | 109.4ms | broker/backlog (no scale or GC event) | - | - | 27.1s / 884,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 23,026,000 | 2026-07-29T10:22:02.3705264+00:00 | 117.1ms | broker/backlog (no scale or GC event) | - | - | 27.1s / 884,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 23,034,000 | 2026-07-29T10:22:02.3746655+00:00 | 116.5ms | broker/backlog (no scale or GC event) | - | - | 27.1s / 884,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 23,036,000 | 2026-07-29T10:22:02.3754985+00:00 | 115.7ms | broker/backlog (no scale or GC event) | - | - | 27.1s / 884,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 23,044,000 | 2026-07-29T10:22:02.3807583+00:00 | 111.2ms | broker/backlog (no scale or GC event) | - | - | 27.1s / 884,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 24,981,000 | 2026-07-29T10:22:04.3517325+00:00 | 109.4ms | broker/backlog (no scale or GC event) | - | - | 29.1s / 951,683 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 24,982,000 | 2026-07-29T10:22:04.3522814+00:00 | 116.2ms | broker/backlog (no scale or GC event) | - | - | 29.1s / 951,683 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 24,991,000 | 2026-07-29T10:22:04.3590754+00:00 | 113.8ms | broker/backlog (no scale or GC event) | - | - | 29.1s / 951,683 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 24,992,000 | 2026-07-29T10:22:04.3595277+00:00 | 113.3ms | broker/backlog (no scale or GC event) | - | - | 29.1s / 951,683 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 31,840,000 | 2026-07-29T10:22:11.3937512+00:00 | 101.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded, 2:capacity/succeeded | - | 36.1s / 940,505 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 33,107,000 | 2026-07-29T10:22:12.951516+00:00 | 106.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded, 2:capacity/succeeded | - | 38.1s / 748,647 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 36,242,000 | 2026-07-29T10:22:16.3764152+00:00 | 107.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded, 2:capacity/succeeded | - | 41.1s / 902,506 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 143,596,000 | 2026-07-29T10:24:02.3610959+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 147.2s / 932,053 msg/s | Gen2 +0 / pause +1.4ms |
| Dekaf (3conn) | 143,604,000 | 2026-07-29T10:24:02.3641677+00:00 | 105.4ms | broker/backlog (no scale or GC event) | - | - | 147.2s / 932,053 msg/s | Gen2 +0 / pause +1.4ms |
| Dekaf (3conn) | 143,606,000 | 2026-07-29T10:24:02.3650476+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 147.2s / 932,053 msg/s | Gen2 +0 / pause +1.4ms |
| Dekaf (3conn) | 184,424,000 | 2026-07-29T10:24:41.7639028+00:00 | 132.8ms | broker/backlog (no scale or GC event) | - | - | 186.3s / 893,452 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 184,426,000 | 2026-07-29T10:24:41.7652788+00:00 | 131.4ms | broker/backlog (no scale or GC event) | - | - | 186.3s / 893,452 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 184,434,000 | 2026-07-29T10:24:41.7726213+00:00 | 129.5ms | broker/backlog (no scale or GC event) | - | - | 186.3s / 893,452 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 184,436,000 | 2026-07-29T10:24:41.77307+00:00 | 129.1ms | broker/backlog (no scale or GC event) | - | - | 186.3s / 893,452 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 184,437,000 | 2026-07-29T10:24:41.7738815+00:00 | 132.6ms | broker/backlog (no scale or GC event) | - | - | 186.3s / 893,452 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,405,000 | 2026-07-29T10:30:50.8995978+00:00 | 185.5ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,408,000 | 2026-07-29T10:30:50.9041084+00:00 | 181.0ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,410,000 | 2026-07-29T10:30:50.9062529+00:00 | 199.6ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,412,000 | 2026-07-29T10:30:50.9078914+00:00 | 180.1ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,413,000 | 2026-07-29T10:30:50.9083458+00:00 | 211.1ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,415,000 | 2026-07-29T10:30:50.9102654+00:00 | 177.9ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,418,000 | 2026-07-29T10:30:50.9139367+00:00 | 177.7ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,419,000 | 2026-07-29T10:30:50.9143778+00:00 | 210.8ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,420,000 | 2026-07-29T10:30:50.914795+00:00 | 197.4ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,421,000 | 2026-07-29T10:30:50.9153854+00:00 | 177.4ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,422,000 | 2026-07-29T10:30:50.9159933+00:00 | 178.3ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,423,000 | 2026-07-29T10:30:50.9165762+00:00 | 208.6ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,425,000 | 2026-07-29T10:30:50.9177116+00:00 | 173.9ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,428,000 | 2026-07-29T10:30:50.918807+00:00 | 172.8ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,429,000 | 2026-07-29T10:30:50.9192309+00:00 | 212.2ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,430,000 | 2026-07-29T10:30:50.9199255+00:00 | 195.2ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,431,000 | 2026-07-29T10:30:50.9202115+00:00 | 174.1ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,432,000 | 2026-07-29T10:30:50.9204843+00:00 | 173.8ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,433,000 | 2026-07-29T10:30:50.9215753+00:00 | 209.9ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,435,000 | 2026-07-29T10:30:50.9236921+00:00 | 167.9ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 532,438,000 | 2026-07-29T10:30:50.9250548+00:00 | 166.5ms | broker/backlog (no scale or GC event) | - | - | 555.6s / 714,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 577,066,000 | 2026-07-29T10:31:44.344919+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 608.7s / 794,599 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 577,067,000 | 2026-07-29T10:31:44.3463396+00:00 | 122.7ms | broker/backlog (no scale or GC event) | - | - | 608.7s / 794,599 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 577,074,000 | 2026-07-29T10:31:44.3635126+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 608.7s / 794,599 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 577,076,000 | 2026-07-29T10:31:44.3661656+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 608.7s / 794,599 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 577,077,000 | 2026-07-29T10:31:44.3674745+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 608.7s / 794,599 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 606,785,000 | 2026-07-29T10:32:23.5538888+00:00 | 104.2ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 648.7s / 722,344 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 606,787,000 | 2026-07-29T10:32:23.5558818+00:00 | 124.0ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 648.7s / 722,344 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 606,788,000 | 2026-07-29T10:32:23.5593487+00:00 | 105.4ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 648.7s / 722,344 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 606,791,000 | 2026-07-29T10:32:23.5629578+00:00 | 102.6ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 648.7s / 722,344 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 606,792,000 | 2026-07-29T10:32:23.5651595+00:00 | 100.4ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 648.7s / 722,344 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 606,794,000 | 2026-07-29T10:32:23.5682228+00:00 | 111.6ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 648.7s / 722,344 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 606,796,000 | 2026-07-29T10:32:23.569158+00:00 | 110.7ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 648.7s / 722,344 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 606,797,000 | 2026-07-29T10:32:23.5695175+00:00 | 110.3ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 648.7s / 722,344 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 606,804,000 | 2026-07-29T10:32:23.5746654+00:00 | 105.2ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 648.7s / 722,344 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 606,806,000 | 2026-07-29T10:32:23.5773435+00:00 | 104.1ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 648.7s / 722,344 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 606,807,000 | 2026-07-29T10:32:23.5781271+00:00 | 103.4ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 648.7s / 722,344 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 606,814,000 | 2026-07-29T10:32:23.5836664+00:00 | 100.6ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 648.7s / 722,344 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*952 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.59x less CPU per message** than Confluent.Kafka for producer (fire-and-forget), 3 brokers; comparison throughput is 1.46x.
:::

## Producer (Acks All) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,387,862 | 1,355,036–1,421,483 | 1.07 | 1.33x |
| Confluent | 2 | 1,045,260 | 993,151–1,100,104 | 1.62 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (confluent-first) | 1.08 | 1106.30 | 1,353,025 | 1,421,483 | -8.9% | -0.77% | 1290.35 | 1,353,025 | 0 | 1.46 |
| Dekaf (dekaf-first) | 1.07 | 1086.40 | 1,338,463 | 1,355,036 | -12.3% | -1.18% | 1276.46 | 1,338,463 | 0 | 1.43 |
| Confluent (confluent-first) | 1.54 | - | 1,085,749 | 1,100,104 | +0.5% | +0.08% | 1035.45 | 1,085,749 | 0 | 1.67 |
| Confluent (dekaf-first) | 1.70 | - | 982,078 | 993,151 | +28.8% | +2.92% | 936.58 | 982,078 | 0 | 1.67 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Acks All)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,190,196 | 1322.42 | 1017.07 KB |
| Dekaf | 1 | 1,184,432 | 1316.01 | 1011.01 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Acks All)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-29T09:51:14.9825017+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 357,177 msg/s |
| Dekaf | 2026-07-29T09:51:32.9881472+00:00 | 1 | 16.0 MiB / 11.1 MiB | 1596.8 MB/s | 0/0 | 17,106 | 18.0s / 1,289,874 msg/s |
| Dekaf | 2026-07-29T09:51:50.990891+00:00 | 1 | 16.0 MiB / 15.4 MiB | 1596.8 MB/s | 0/0 | 40,351 | 36.0s / 1,467,041 msg/s |
| Dekaf | 2026-07-29T09:52:09.9999604+00:00 | 1 | 14.0 MiB / 13.0 MiB | 1596.8 MB/s | 1/0 | 70,470 | 55.0s / 1,372,140 msg/s |
| Dekaf | 2026-07-29T09:52:28.0081511+00:00 | 1 | 14.0 MiB / 13.9 MiB | 1667.1 MB/s | 1/0 | 107,387 | 73.0s / 1,576,380 msg/s |
| Dekaf | 2026-07-29T09:52:46.0186817+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1667.1 MB/s | 2/0 | 144,983 | 91.0s / 1,470,584 msg/s |
| Dekaf | 2026-07-29T09:53:04.0237036+00:00 | 1 | 12.0 MiB / 3.1 MiB | 1667.1 MB/s | 2/0 | 182,638 | 109.0s / 1,309,521 msg/s |
| Dekaf | 2026-07-29T09:53:22.0297161+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1667.1 MB/s | 2/0 | 214,396 | 127.0s / 1,518,746 msg/s |
| Dekaf | 2026-07-29T09:53:40.0349502+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1667.1 MB/s | 2/1 | 248,149 | 145.1s / 1,413,673 msg/s |
| Dekaf | 2026-07-29T09:53:59.0417732+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1667.1 MB/s | 2/1 | 285,180 | 164.1s / 1,431,959 msg/s |
| Dekaf | 2026-07-29T09:54:17.0489535+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1667.1 MB/s | 2/1 | 322,973 | 182.1s / 1,459,222 msg/s |
| Dekaf | 2026-07-29T09:54:35.0526454+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1667.1 MB/s | 2/1 | 361,396 | 200.1s / 1,471,207 msg/s |
| Dekaf | 2026-07-29T09:54:53.0605127+00:00 | 1 | 13.0 MiB / 12.8 MiB | 1667.1 MB/s | 3/1 | 397,587 | 218.1s / 1,534,686 msg/s |
| Dekaf | 2026-07-29T09:55:11.0683388+00:00 | 1 | 13.0 MiB / 12.6 MiB | 1684.9 MB/s | 3/1 | 435,813 | 236.1s / 1,540,946 msg/s |
| Dekaf | 2026-07-29T09:55:29.0729023+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1684.9 MB/s | 3/1 | 472,146 | 254.1s / 1,556,156 msg/s |
| Dekaf | 2026-07-29T09:55:48.078508+00:00 | 1 | 13.0 MiB / 9.2 MiB | 1684.9 MB/s | 3/2 | 511,431 | 273.1s / 1,407,695 msg/s |
| Dekaf | 2026-07-29T09:56:06.0837319+00:00 | 1 | 13.0 MiB / 12.6 MiB | 1684.9 MB/s | 3/2 | 546,093 | 291.1s / 1,509,312 msg/s |
| Dekaf | 2026-07-29T09:56:24.0869267+00:00 | 1 | 13.0 MiB / 12.6 MiB | 1684.9 MB/s | 3/2 | 577,785 | 309.1s / 1,373,592 msg/s |
| Dekaf | 2026-07-29T09:56:42.0916109+00:00 | 1 | 11.0 MiB / 10.3 MiB | 1684.9 MB/s | 3/2 | 612,934 | 327.1s / 1,465,799 msg/s |
| Dekaf | 2026-07-29T09:57:00.099208+00:00 | 1 | 13.0 MiB / 11.1 MiB | 1684.9 MB/s | 3/3 | 643,407 | 345.1s / 1,316,428 msg/s |
| Dekaf | 2026-07-29T09:57:18.0994197+00:00 | 1 | 13.0 MiB / 12.1 MiB | 1684.9 MB/s | 3/3 | 675,007 | 363.1s / 1,114,658 msg/s |
| Dekaf | 2026-07-29T09:57:37.1035733+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1684.9 MB/s | 3/3 | 710,093 | 382.1s / 1,413,649 msg/s |
| Dekaf | 2026-07-29T09:57:55.1100167+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1684.9 MB/s | 3/3 | 744,797 | 400.1s / 1,320,238 msg/s |
| Dekaf | 2026-07-29T09:58:13.1192672+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1684.9 MB/s | 3/3 | 777,917 | 418.1s / 1,379,455 msg/s |
| Dekaf | 2026-07-29T09:58:31.124113+00:00 | 1 | 13.0 MiB / 12.6 MiB | 1684.9 MB/s | 3/3 | 807,040 | 436.1s / 1,204,811 msg/s |
| Dekaf | 2026-07-29T09:58:49.1290494+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1684.9 MB/s | 3/3 | 834,382 | 454.1s / 1,161,627 msg/s |
| Dekaf | 2026-07-29T09:59:07.1368649+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1684.9 MB/s | 4/3 | 865,479 | 472.1s / 1,294,708 msg/s |
| Dekaf | 2026-07-29T09:59:26.1460357+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1684.9 MB/s | 4/3 | 898,598 | 491.1s / 1,324,816 msg/s |
| Dekaf | 2026-07-29T09:59:44.1498078+00:00 | 1 | 15.0 MiB / 14.6 MiB | 1684.9 MB/s | 4/3 | 927,353 | 509.1s / 1,352,029 msg/s |
| Dekaf | 2026-07-29T10:00:02.1513474+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1684.9 MB/s | 4/4 | 958,380 | 527.1s / 1,255,062 msg/s |
| Dekaf | 2026-07-29T10:00:20.1609626+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1684.9 MB/s | 4/4 | 973,947 | 545.2s / 912,841 msg/s |
| Dekaf | 2026-07-29T10:00:38.1640328+00:00 | 1 | 14.0 MiB / 13.3 MiB | 1684.9 MB/s | 4/4 | 993,001 | 563.2s / 1,283,429 msg/s |
| Dekaf | 2026-07-29T10:00:56.1679834+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1684.9 MB/s | 4/4 | 1,020,330 | 581.2s / 1,400,863 msg/s |
| Dekaf | 2026-07-29T10:01:15.1788724+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1684.9 MB/s | 5/4 | 1,055,456 | 600.2s / 1,424,029 msg/s |
| Dekaf | 2026-07-29T10:01:33.1795634+00:00 | 1 | 10.0 MiB / 3.8 MiB | 1684.9 MB/s | 5/4 | 1,079,443 | 618.2s / 1,094,540 msg/s |
| Dekaf | 2026-07-29T10:01:51.1809483+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1684.9 MB/s | 5/5 | 1,101,861 | 636.2s / 1,342,329 msg/s |
| Dekaf | 2026-07-29T10:02:09.1808198+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1684.9 MB/s | 5/5 | 1,127,672 | 654.2s / 1,238,845 msg/s |
| Dekaf | 2026-07-29T10:02:27.1839766+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1684.9 MB/s | 5/5 | 1,158,532 | 672.2s / 1,275,296 msg/s |
| Dekaf | 2026-07-29T10:02:46.1887858+00:00 | 1 | 13.0 MiB / 12.7 MiB | 1684.9 MB/s | 5/5 | 1,194,701 | 691.2s / 1,422,922 msg/s |
| Dekaf | 2026-07-29T10:03:04.1913296+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1684.9 MB/s | 6/5 | 1,228,692 | 709.2s / 1,280,463 msg/s |
| Dekaf | 2026-07-29T10:03:22.1959483+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1684.9 MB/s | 6/5 | 1,259,573 | 727.2s / 1,341,334 msg/s |
| Dekaf | 2026-07-29T10:03:40.2017469+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1684.9 MB/s | 6/5 | 1,292,438 | 745.2s / 1,478,876 msg/s |
| Dekaf | 2026-07-29T10:03:58.2060304+00:00 | 1 | 13.0 MiB / 12.8 MiB | 1684.9 MB/s | 6/6 | 1,327,338 | 763.2s / 1,262,967 msg/s |
| Dekaf | 2026-07-29T10:04:16.2112403+00:00 | 1 | 13.0 MiB / 12.7 MiB | 1684.9 MB/s | 6/6 | 1,358,649 | 781.2s / 1,275,185 msg/s |
| Dekaf | 2026-07-29T10:04:35.2174426+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1684.9 MB/s | 6/6 | 1,390,252 | 800.2s / 1,195,215 msg/s |
| Dekaf | 2026-07-29T10:04:53.224483+00:00 | 1 | 13.0 MiB / 10.7 MiB | 1684.9 MB/s | 6/6 | 1,415,042 | 818.2s / 1,198,796 msg/s |
| Dekaf | 2026-07-29T10:05:11.2267033+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1684.9 MB/s | 6/7 | 1,442,918 | 836.2s / 1,303,872 msg/s |
| Dekaf | 2026-07-29T10:05:29.2313915+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1684.9 MB/s | 6/7 | 1,471,065 | 854.2s / 1,208,205 msg/s |
| Dekaf | 2026-07-29T10:05:47.239228+00:00 | 1 | 13.0 MiB / 12.5 MiB | 1684.9 MB/s | 6/8 | 1,504,187 | 872.3s / 1,324,940 msg/s |
| Dekaf | 2026-07-29T10:06:05.2441976+00:00 | 1 | 13.0 MiB / 9.3 MiB | 1684.9 MB/s | 6/8 | 1,536,711 | 890.3s / 1,207,829 msg/s |
| Dekaf | 2026-07-29T10:36:25.3487124+00:00 | 1 | 16.0 MiB / 14.4 MiB | 1470.7 MB/s | 0/0 | 8,010 | 9.0s / 1,295,628 msg/s |
| Dekaf | 2026-07-29T10:36:43.3598872+00:00 | 1 | 16.0 MiB / 14.5 MiB | 1495.7 MB/s | 0/0 | 25,652 | 27.0s / 1,282,699 msg/s |
| Dekaf | 2026-07-29T10:37:01.3678364+00:00 | 1 | 14.0 MiB / 13.2 MiB | 1495.7 MB/s | 0/0 | 49,358 | 45.0s / 1,255,152 msg/s |
| Dekaf | 2026-07-29T10:37:19.3735247+00:00 | 1 | 16.0 MiB / 14.3 MiB | 1495.7 MB/s | 0/1 | 66,411 | 63.0s / 1,193,079 msg/s |
| Dekaf | 2026-07-29T10:37:37.3822006+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1495.7 MB/s | 0/1 | 85,736 | 81.0s / 1,177,057 msg/s |
| Dekaf | 2026-07-29T10:37:55.3857092+00:00 | 1 | 16.0 MiB / 14.0 MiB | 1495.7 MB/s | 0/1 | 105,578 | 99.0s / 1,315,992 msg/s |
| Dekaf | 2026-07-29T10:38:14.3932273+00:00 | 1 | 18.0 MiB / 13.8 MiB | 1495.7 MB/s | 0/1 | 127,395 | 118.1s / 1,365,777 msg/s |
| Dekaf | 2026-07-29T10:38:32.3994204+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1525.5 MB/s | 0/2 | 147,383 | 136.1s / 1,215,011 msg/s |
| Dekaf | 2026-07-29T10:38:50.407692+00:00 | 1 | 16.0 MiB / 10.9 MiB | 1530.2 MB/s | 0/2 | 165,192 | 154.1s / 1,465,694 msg/s |
| Dekaf | 2026-07-29T10:39:08.4147109+00:00 | 1 | 16.0 MiB / 15.9 MiB | 1530.8 MB/s | 0/2 | 183,012 | 172.1s / 1,177,781 msg/s |
| Dekaf | 2026-07-29T10:39:26.4251484+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1576.1 MB/s | 0/2 | 204,311 | 190.1s / 1,434,853 msg/s |
| Dekaf | 2026-07-29T10:39:44.4409617+00:00 | 1 | 16.0 MiB / 14.6 MiB | 1595.0 MB/s | 0/2 | 226,344 | 208.1s / 1,464,734 msg/s |
| Dekaf | 2026-07-29T10:40:03.4496339+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1614.1 MB/s | 0/2 | 250,597 | 227.1s / 1,493,758 msg/s |
| Dekaf | 2026-07-29T10:40:21.4622612+00:00 | 1 | 16.0 MiB / 13.9 MiB | 1614.1 MB/s | 0/2 | 275,947 | 245.1s / 1,464,476 msg/s |
| Dekaf | 2026-07-29T10:40:39.4715637+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1614.1 MB/s | 1/2 | 308,796 | 263.1s / 1,474,680 msg/s |
| Dekaf | 2026-07-29T10:40:57.4803972+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1624.8 MB/s | 1/2 | 343,573 | 281.1s / 1,448,815 msg/s |
| Dekaf | 2026-07-29T10:41:15.4894661+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1624.8 MB/s | 1/2 | 378,600 | 299.1s / 1,472,874 msg/s |
| Dekaf | 2026-07-29T10:41:34.4964055+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1624.8 MB/s | 2/2 | 419,666 | 318.1s / 1,421,544 msg/s |
| Dekaf | 2026-07-29T10:41:52.4987166+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1624.8 MB/s | 2/2 | 452,404 | 336.1s / 1,439,056 msg/s |
| Dekaf | 2026-07-29T10:42:10.5007129+00:00 | 1 | 12.0 MiB / 9.6 MiB | 1624.8 MB/s | 2/3 | 486,223 | 354.1s / 1,457,726 msg/s |
| Dekaf | 2026-07-29T10:42:28.5097397+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1624.8 MB/s | 2/3 | 524,199 | 372.1s / 1,460,494 msg/s |
| Dekaf | 2026-07-29T10:42:46.5133923+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1624.8 MB/s | 2/3 | 563,600 | 390.1s / 1,513,324 msg/s |
| Dekaf | 2026-07-29T10:43:04.5226973+00:00 | 1 | 13.0 MiB / 12.5 MiB | 1624.8 MB/s | 2/3 | 605,563 | 408.2s / 1,500,451 msg/s |
| Dekaf | 2026-07-29T10:43:23.5307717+00:00 | 1 | 13.0 MiB / 10.4 MiB | 1624.8 MB/s | 3/3 | 647,387 | 427.2s / 1,531,132 msg/s |
| Dekaf | 2026-07-29T10:43:41.5397154+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1624.8 MB/s | 3/3 | 686,560 | 445.2s / 1,510,191 msg/s |
| Dekaf | 2026-07-29T10:43:59.5457897+00:00 | 1 | 14.0 MiB / 12.6 MiB | 1624.8 MB/s | 3/3 | 724,226 | 463.2s / 1,482,094 msg/s |
| Dekaf | 2026-07-29T10:44:17.5554667+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1624.8 MB/s | 4/3 | 762,677 | 481.2s / 1,527,755 msg/s |
| Dekaf | 2026-07-29T10:44:35.5617188+00:00 | 1 | 15.0 MiB / 14.6 MiB | 1624.8 MB/s | 4/3 | 799,091 | 499.2s / 1,521,030 msg/s |
| Dekaf | 2026-07-29T10:44:53.5700509+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1624.8 MB/s | 4/4 | 836,518 | 517.2s / 1,484,613 msg/s |
| Dekaf | 2026-07-29T10:45:12.5819769+00:00 | 1 | 14.0 MiB / 12.9 MiB | 1624.8 MB/s | 4/4 | 874,081 | 536.2s / 1,473,647 msg/s |
| Dekaf | 2026-07-29T10:45:30.5876832+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1624.8 MB/s | 4/4 | 911,430 | 554.2s / 1,493,990 msg/s |
| Dekaf | 2026-07-29T10:45:48.5947054+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1624.8 MB/s | 4/4 | 949,459 | 572.2s / 1,518,415 msg/s |
| Dekaf | 2026-07-29T10:46:06.5979031+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1637.0 MB/s | 5/4 | 991,442 | 590.2s / 1,518,623 msg/s |
| Dekaf | 2026-07-29T10:46:24.6028155+00:00 | 1 | 12.0 MiB / 11.3 MiB | 1637.0 MB/s | 5/4 | 1,032,205 | 608.2s / 1,314,886 msg/s |
| Dekaf | 2026-07-29T10:46:42.6046828+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1637.0 MB/s | 5/4 | 1,067,555 | 626.2s / 1,304,896 msg/s |
| Dekaf | 2026-07-29T10:47:01.6087573+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1637.0 MB/s | 5/5 | 1,104,166 | 645.2s / 1,430,734 msg/s |
| Dekaf | 2026-07-29T10:47:19.6118278+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1637.0 MB/s | 5/5 | 1,145,688 | 663.2s / 1,511,007 msg/s |
| Dekaf | 2026-07-29T10:47:37.6171064+00:00 | 1 | 12.0 MiB / 10.9 MiB | 1637.0 MB/s | 5/5 | 1,186,721 | 681.2s / 1,375,264 msg/s |
| Dekaf | 2026-07-29T10:47:55.6195701+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1637.0 MB/s | 5/5 | 1,224,418 | 699.2s / 1,434,642 msg/s |
| Dekaf | 2026-07-29T10:48:13.627167+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1637.0 MB/s | 5/6 | 1,262,844 | 717.3s / 1,443,970 msg/s |
| Dekaf | 2026-07-29T10:48:31.6323274+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1637.0 MB/s | 5/6 | 1,287,229 | 735.3s / 1,122,356 msg/s |
| Dekaf | 2026-07-29T10:48:50.6356907+00:00 | 1 | 12.0 MiB / 10.2 MiB | 1637.0 MB/s | 5/6 | 1,316,891 | 754.3s / 1,138,090 msg/s |
| Dekaf | 2026-07-29T10:49:08.6402545+00:00 | 1 | 12.0 MiB / 10.7 MiB | 1637.0 MB/s | 5/6 | 1,347,955 | 772.3s / 1,110,023 msg/s |
| Dekaf | 2026-07-29T10:49:26.6424163+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1637.0 MB/s | 5/6 | 1,378,165 | 790.3s / 1,094,161 msg/s |
| Dekaf | 2026-07-29T10:49:44.6467866+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1637.0 MB/s | 5/6 | 1,410,292 | 808.3s / 1,097,838 msg/s |
| Dekaf | 2026-07-29T10:50:02.6539489+00:00 | 1 | 10.0 MiB / 7.4 MiB | 1637.0 MB/s | 5/6 | 1,441,724 | 826.3s / 1,058,817 msg/s |
| Dekaf | 2026-07-29T10:50:20.6586941+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1637.0 MB/s | 5/7 | 1,466,641 | 844.3s / 1,103,238 msg/s |
| Dekaf | 2026-07-29T10:50:39.6679484+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1637.0 MB/s | 5/7 | 1,500,454 | 863.3s / 1,140,852 msg/s |
| Dekaf | 2026-07-29T10:50:57.6743484+00:00 | 1 | 12.0 MiB / 11.3 MiB | 1637.0 MB/s | 5/7 | 1,531,686 | 881.3s / 1,118,207 msg/s |
| Dekaf | 2026-07-29T10:51:15.6821853+00:00 | 1 | 12.0 MiB / 10.7 MiB | 1637.0 MB/s | 5/7 | 1,563,467 | 899.3s / 1,128,857 msg/s |
*1,700 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Acks All)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-29T09:51:45.0919075+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.0 MiB |
| Dekaf | 2026-07-29T09:52:00.1062437+00:00 | 1 | capacity | succeeded | 15,014ms | 14.0 MiB / 13.8 MiB |
| Dekaf | 2026-07-29T09:52:30.1304983+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.0 MiB |
| Dekaf | 2026-07-29T09:52:45.1438424+00:00 | 1 | capacity | succeeded | 15,013ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T09:53:15.1705106+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T09:53:30.1816247+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 8.6 MiB |
| Dekaf | 2026-07-29T09:54:30.2335026+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T09:54:45.2494837+00:00 | 1 | capacity | succeeded | 15,015ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T09:55:15.2761818+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T09:55:30.2904471+00:00 | 1 | capacity | failed | 15,014ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T09:56:30.3585875+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 11.6 MiB |
| Dekaf | 2026-07-29T09:56:45.3724844+00:00 | 1 | capacity | failed | 15,013ms | 13.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T09:58:45.4847121+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 11.9 MiB |
| Dekaf | 2026-07-29T09:59:00.5046346+00:00 | 1 | capacity | succeeded | 15,019ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T09:59:30.5364679+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 12.6 MiB |
| Dekaf | 2026-07-29T09:59:45.5495553+00:00 | 1 | capacity | failed | 15,013ms | 14.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-29T10:00:45.6130135+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 12.6 MiB |
| Dekaf | 2026-07-29T10:01:00.6235904+00:00 | 1 | capacity | succeeded | 15,010ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T10:01:30.648963+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 2.0 MiB |
| Dekaf | 2026-07-29T10:01:45.6622684+00:00 | 1 | capacity | failed | 15,013ms | 12.0 MiB / 1.8 MiB |
| Dekaf | 2026-07-29T10:02:45.712914+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T10:03:00.7257572+00:00 | 1 | capacity | succeeded | 15,012ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T10:03:30.7504846+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 11.8 MiB |
| Dekaf | 2026-07-29T10:03:45.7599449+00:00 | 1 | capacity | failed | 15,009ms | 13.0 MiB / 11.7 MiB |
| Dekaf | 2026-07-29T10:04:45.8276905+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.4 MiB |
| Dekaf | 2026-07-29T10:05:00.8476092+00:00 | 1 | capacity | failed | 15,020ms | 13.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T10:05:30.8771966+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.6 MiB |
| Dekaf | 2026-07-29T10:05:45.8908905+00:00 | 1 | capacity | failed | 15,013ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T10:36:46.4670177+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.9 MiB |
| Dekaf | 2026-07-29T10:37:01.4830581+00:00 | 1 | capacity | failed | 15,016ms | 16.0 MiB / 13.3 MiB |
| Dekaf | 2026-07-29T10:38:01.560904+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-29T10:38:16.5873348+00:00 | 1 | capacity | failed | 15,026ms | 16.0 MiB / 16.1 MiB |
| Dekaf | 2026-07-29T10:40:16.7517852+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-29T10:40:31.7663449+00:00 | 1 | capacity | succeeded | 15,014ms | 14.0 MiB / 13.6 MiB |
| Dekaf | 2026-07-29T10:41:01.7907015+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T10:41:16.8015241+00:00 | 1 | capacity | succeeded | 15,010ms | 12.0 MiB / 11.0 MiB |
| Dekaf | 2026-07-29T10:41:46.8467631+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T10:42:01.8587354+00:00 | 1 | capacity | failed | 15,012ms | 12.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-29T10:43:01.91019+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.9 MiB |
| Dekaf | 2026-07-29T10:43:16.9222448+00:00 | 1 | capacity | succeeded | 15,012ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T10:43:46.9504713+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 10.6 MiB |
| Dekaf | 2026-07-29T10:44:01.9628332+00:00 | 1 | capacity | succeeded | 15,012ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T10:44:31.9900313+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T10:44:47.0034001+00:00 | 1 | capacity | failed | 15,013ms | 14.0 MiB / 12.9 MiB |
| Dekaf | 2026-07-29T10:45:47.0548443+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T10:46:02.0664084+00:00 | 1 | capacity | succeeded | 15,011ms | 12.0 MiB / 10.9 MiB |
| Dekaf | 2026-07-29T10:46:32.091717+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T10:46:47.1016853+00:00 | 1 | capacity | failed | 15,009ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-29T10:47:47.1486785+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.5 MiB |
| Dekaf | 2026-07-29T10:48:02.1579351+00:00 | 1 | capacity | failed | 15,009ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T10:50:02.27535+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T10:50:17.2968287+00:00 | 1 | capacity | failed | 15,021ms | 12.0 MiB / 8.4 MiB |

## Producer Admission Block Durations - Producer (Acks All)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 1,364 |
| Dekaf | 1 | 0.002–0.004ms | 1,743 |
| Dekaf | 1 | 0.004–0.008ms | 5,160 |
| Dekaf | 1 | 0.008–0.016ms | 38,135 |
| Dekaf | 1 | 0.016–0.032ms | 42,254 |
| Dekaf | 1 | 0.032–0.064ms | 38,380 |
| Dekaf | 1 | 0.064–0.128ms | 68,729 |
| Dekaf | 1 | 0.128–0.256ms | 178,841 |
| Dekaf | 1 | 0.256–0.512ms | 197,445 |
| Dekaf | 1 | 0.512–1.024ms | 76,160 |
| Dekaf | 1 | 1.024–2.048ms | 18,074 |
| Dekaf | 1 | 2.048–4.096ms | 3,680 |
| Dekaf | 1 | 4.096–8.192ms | 698 |
| Dekaf | 1 | 8.192–16.384ms | 31 |
| Dekaf | 1 | 16.384–32.768ms | 2 |
| Dekaf | 1 | 0.001–0.002ms | 1,663 |
| Dekaf | 1 | 0.002–0.004ms | 1,906 |
| Dekaf | 1 | 0.004–0.008ms | 5,018 |
| Dekaf | 1 | 0.008–0.016ms | 28,713 |
| Dekaf | 1 | 0.016–0.032ms | 39,063 |
| Dekaf | 1 | 0.032–0.064ms | 45,065 |
| Dekaf | 1 | 0.064–0.128ms | 82,602 |
| Dekaf | 1 | 0.128–0.256ms | 198,714 |
| Dekaf | 1 | 0.256–0.512ms | 216,229 |
| Dekaf | 1 | 0.512–1.024ms | 55,574 |
| Dekaf | 1 | 1.024–2.048ms | 9,733 |
| Dekaf | 1 | 2.048–4.096ms | 3,329 |
| Dekaf | 1 | 4.096–8.192ms | 511 |
| Dekaf | 1 | 8.192–16.384ms | 26 |
| Dekaf | 1 | 16.384–32.768ms | 6 |
| Dekaf | 1 | 32.768–65.536ms | 1 |

:::tip
**Dekaf uses 1.51x less CPU per message** than Confluent.Kafka for producer (acks all); comparison throughput is 1.33x.
:::

## Producer (Acks All), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.21 | 1152.60 | 1,031,777 | 1,085,943 | +35.0% | +3.25% | 983.98 | 1,031,777 | 0 | 1.25 |
| Confluent | 1.77 | - | 899,783 | 907,948 | -2.0% | -0.17% | 858.10 | 899,783 | 0 | 1.60 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Acks All), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 322,959 | 358.84 | 937.21 KB |
| Dekaf | 2 | 326,332 | 362.58 | 932.34 KB |
| Dekaf | 3 | 328,673 | 365.19 | 961.93 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Acks All), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-29T09:51:26.6284658+00:00 | 2 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 242,851 msg/s |
| Dekaf | 2026-07-29T09:51:35.6331307+00:00 | 3 | 16.0 MiB / 16.0 MiB | 275.2 MB/s | 0/0 | 3,217 | 9.0s / 685,311 msg/s |
| Dekaf | 2026-07-29T09:51:44.637201+00:00 | 3 | 16.0 MiB / 2.2 MiB | 340.5 MB/s | 0/0 | 5,339 | 18.0s / 866,218 msg/s |
| Dekaf | 2026-07-29T09:51:54.637135+00:00 | 1 | 16.0 MiB / 1.4 MiB | 335.6 MB/s | 0/0 | 1,096 | 28.0s / 720,340 msg/s |
| Dekaf | 2026-07-29T09:52:03.6404835+00:00 | 1 | 16.0 MiB / 7.8 MiB | 335.6 MB/s | 0/0 | 1,249 | 37.0s / 881,261 msg/s |
| Dekaf | 2026-07-29T09:52:12.645079+00:00 | 1 | 14.0 MiB / 2.3 MiB | 335.6 MB/s | 1/0 | 1,383 | 46.0s / 870,466 msg/s |
| Dekaf | 2026-07-29T09:52:21.6514052+00:00 | 1 | 14.0 MiB / 0.8 MiB | 335.6 MB/s | 1/0 | 1,639 | 55.0s / 849,987 msg/s |
| Dekaf | 2026-07-29T09:52:30.6539131+00:00 | 2 | 16.0 MiB / 12.2 MiB | 346.5 MB/s | 0/1 | 3,630 | 64.0s / 1,011,079 msg/s |
| Dekaf | 2026-07-29T09:52:39.6540587+00:00 | 2 | 16.0 MiB / 5.4 MiB | 347.8 MB/s | 0/1 | 3,700 | 73.1s / 872,974 msg/s |
| Dekaf | 2026-07-29T09:52:48.6584622+00:00 | 2 | 16.0 MiB / 5.6 MiB | 347.8 MB/s | 0/1 | 3,919 | 82.1s / 923,269 msg/s |
| Dekaf | 2026-07-29T09:52:57.6607655+00:00 | 2 | 14.0 MiB / 7.5 MiB | 347.8 MB/s | 1/1 | 3,919 | 91.1s / 937,862 msg/s |
| Dekaf | 2026-07-29T09:53:06.6642841+00:00 | 3 | 8.0 MiB / 1.4 MiB | 353.2 MB/s | 4/0 | 12,555 | 100.1s / 827,040 msg/s |
| Dekaf | 2026-07-29T09:53:15.6671683+00:00 | 3 | 8.0 MiB / 7.1 MiB | 353.2 MB/s | 4/0 | 13,286 | 109.1s / 885,274 msg/s |
| Dekaf | 2026-07-29T09:53:24.6711793+00:00 | 3 | 8.0 MiB / 4.1 MiB | 353.2 MB/s | 4/1 | 13,668 | 118.1s / 709,554 msg/s |
| Dekaf | 2026-07-29T09:53:33.6769462+00:00 | 3 | 8.0 MiB / 1.5 MiB | 353.2 MB/s | 4/1 | 13,982 | 127.1s / 743,912 msg/s |
| Dekaf | 2026-07-29T09:53:43.6799735+00:00 | 1 | 8.0 MiB / 0.9 MiB | 339.2 MB/s | 4/1 | 5,451 | 137.1s / 879,890 msg/s |
| Dekaf | 2026-07-29T09:53:52.6850945+00:00 | 1 | 8.0 MiB / 0.4 MiB | 339.2 MB/s | 4/1 | 5,721 | 146.1s / 932,472 msg/s |
| Dekaf | 2026-07-29T09:54:01.6893802+00:00 | 1 | 8.0 MiB / 3.0 MiB | 339.2 MB/s | 4/1 | 6,018 | 155.1s / 725,594 msg/s |
| Dekaf | 2026-07-29T09:54:10.6937116+00:00 | 1 | 9.0 MiB / 1.7 MiB | 339.2 MB/s | 5/1 | 6,225 | 164.1s / 816,096 msg/s |
| Dekaf | 2026-07-29T09:54:19.6972281+00:00 | 2 | 12.0 MiB / 3.2 MiB | 347.8 MB/s | 2/2 | 4,083 | 173.1s / 686,145 msg/s |
| Dekaf | 2026-07-29T09:54:28.6992248+00:00 | 2 | 12.0 MiB / 1.1 MiB | 347.8 MB/s | 2/2 | 4,109 | 182.1s / 735,565 msg/s |
| Dekaf | 2026-07-29T09:54:37.7019096+00:00 | 2 | 12.0 MiB / 1.3 MiB | 347.8 MB/s | 2/2 | 4,131 | 191.1s / 803,934 msg/s |
| Dekaf | 2026-07-29T09:54:46.7042683+00:00 | 2 | 12.0 MiB / 3.9 MiB | 347.8 MB/s | 2/2 | 4,170 | 200.1s / 852,521 msg/s |
| Dekaf | 2026-07-29T09:54:55.7078937+00:00 | 3 | 9.0 MiB / 2.1 MiB | 358.9 MB/s | 5/1 | 19,196 | 209.1s / 870,638 msg/s |
| Dekaf | 2026-07-29T09:55:04.7083935+00:00 | 3 | 9.0 MiB / 1.1 MiB | 365.2 MB/s | 5/1 | 19,858 | 218.1s / 920,770 msg/s |
| Dekaf | 2026-07-29T09:55:13.7155618+00:00 | 3 | 7.0 MiB / 1.6 MiB | 365.2 MB/s | 5/1 | 20,571 | 227.1s / 757,183 msg/s |
| Dekaf | 2026-07-29T09:55:22.717023+00:00 | 3 | 7.0 MiB / 6.3 MiB | 365.2 MB/s | 5/1 | 21,054 | 236.1s / 939,581 msg/s |
| Dekaf | 2026-07-29T09:55:32.7193895+00:00 | 1 | 9.0 MiB / 0.4 MiB | 395.0 MB/s | 5/2 | 8,808 | 246.1s / 961,887 msg/s |
| Dekaf | 2026-07-29T09:55:41.7217542+00:00 | 1 | 9.0 MiB / 3.5 MiB | 395.0 MB/s | 5/2 | 9,200 | 255.1s / 854,623 msg/s |
| Dekaf | 2026-07-29T09:55:50.7247001+00:00 | 1 | 9.0 MiB / 6.9 MiB | 395.0 MB/s | 5/2 | 9,530 | 264.1s / 1,029,059 msg/s |
| Dekaf | 2026-07-29T09:55:59.7256035+00:00 | 1 | 9.0 MiB / 2.0 MiB | 395.0 MB/s | 5/2 | 10,014 | 273.1s / 956,691 msg/s |
| Dekaf | 2026-07-29T09:56:08.72768+00:00 | 2 | 11.0 MiB / 4.3 MiB | 385.9 MB/s | 4/2 | 4,243 | 282.1s / 934,238 msg/s |
| Dekaf | 2026-07-29T09:56:17.7295995+00:00 | 2 | 11.0 MiB / 1.6 MiB | 385.9 MB/s | 4/2 | 4,243 | 291.1s / 834,140 msg/s |
| Dekaf | 2026-07-29T09:56:26.738057+00:00 | 2 | 11.0 MiB / 1.0 MiB | 385.9 MB/s | 4/2 | 4,250 | 300.1s / 917,596 msg/s |
| Dekaf | 2026-07-29T09:56:35.7398997+00:00 | 2 | 11.0 MiB / 0.9 MiB | 385.9 MB/s | 4/2 | 4,428 | 309.1s / 958,999 msg/s |
| Dekaf | 2026-07-29T09:56:44.7414974+00:00 | 2 | 9.0 MiB / 0.6 MiB | 385.9 MB/s | 4/2 | 4,569 | 318.1s / 969,172 msg/s |
| Dekaf | 2026-07-29T09:56:53.7456491+00:00 | 3 | 9.0 MiB / 0.7 MiB | 396.1 MB/s | 5/3 | 29,388 | 327.1s / 918,375 msg/s |
| Dekaf | 2026-07-29T09:57:02.7479196+00:00 | 3 | 9.0 MiB / 1.8 MiB | 396.1 MB/s | 5/3 | 30,102 | 336.1s / 1,027,053 msg/s |
| Dekaf | 2026-07-29T09:57:11.7526565+00:00 | 3 | 9.0 MiB / 9.0 MiB | 411.1 MB/s | 5/3 | 31,216 | 345.2s / 1,049,317 msg/s |
| Dekaf | 2026-07-29T09:57:20.7537063+00:00 | 3 | 9.0 MiB / 3.6 MiB | 416.0 MB/s | 5/3 | 32,369 | 354.2s / 1,103,718 msg/s |
| Dekaf | 2026-07-29T09:57:30.7549548+00:00 | 1 | 9.0 MiB / 7.4 MiB | 399.8 MB/s | 5/3 | 13,881 | 364.2s / 1,033,009 msg/s |
| Dekaf | 2026-07-29T09:57:39.7583505+00:00 | 1 | 9.0 MiB / 7.1 MiB | 401.3 MB/s | 5/3 | 14,433 | 373.2s / 1,142,941 msg/s |
| Dekaf | 2026-07-29T09:57:48.7637383+00:00 | 1 | 9.0 MiB / 8.9 MiB | 423.4 MB/s | 5/3 | 15,327 | 382.2s / 1,058,633 msg/s |
| Dekaf | 2026-07-29T09:57:57.7677053+00:00 | 1 | 9.0 MiB / 3.5 MiB | 423.4 MB/s | 5/3 | 15,846 | 391.2s / 1,052,430 msg/s |
| Dekaf | 2026-07-29T09:58:06.7690494+00:00 | 2 | 8.0 MiB / 4.7 MiB | 433.2 MB/s | 5/3 | 5,533 | 400.2s / 1,124,515 msg/s |
| Dekaf | 2026-07-29T09:58:15.7688899+00:00 | 2 | 8.0 MiB / 1.8 MiB | 433.2 MB/s | 5/3 | 6,043 | 409.2s / 1,146,766 msg/s |
| Dekaf | 2026-07-29T09:58:24.7699126+00:00 | 2 | 8.0 MiB / 7.2 MiB | 433.2 MB/s | 6/3 | 6,805 | 418.2s / 1,177,941 msg/s |
| Dekaf | 2026-07-29T09:58:33.7738896+00:00 | 2 | 8.0 MiB / 4.9 MiB | 433.2 MB/s | 6/3 | 8,314 | 427.2s / 1,113,526 msg/s |
| Dekaf | 2026-07-29T09:58:42.7768986+00:00 | 3 | 9.0 MiB / 7.2 MiB | 447.2 MB/s | 5/4 | 57,623 | 436.2s / 1,097,145 msg/s |
| Dekaf | 2026-07-29T09:58:51.7792266+00:00 | 3 | 9.0 MiB / 6.9 MiB | 447.2 MB/s | 5/4 | 61,631 | 445.2s / 1,157,160 msg/s |
| Dekaf | 2026-07-29T09:59:00.7805771+00:00 | 3 | 7.0 MiB / 7.0 MiB | 447.2 MB/s | 6/4 | 66,170 | 454.2s / 1,154,533 msg/s |
| Dekaf | 2026-07-29T09:59:09.7805183+00:00 | 3 | 7.0 MiB / 7.0 MiB | 447.2 MB/s | 6/4 | 71,537 | 463.2s / 1,109,926 msg/s |
| Dekaf | 2026-07-29T09:59:19.782921+00:00 | 1 | 9.0 MiB / 7.0 MiB | 427.3 MB/s | 5/4 | 24,098 | 473.2s / 1,123,323 msg/s |
| Dekaf | 2026-07-29T09:59:28.7839213+00:00 | 1 | 9.0 MiB / 7.1 MiB | 427.3 MB/s | 5/4 | 24,831 | 482.2s / 1,168,118 msg/s |
| Dekaf | 2026-07-29T09:59:37.7858935+00:00 | 1 | 9.0 MiB / 9.0 MiB | 427.3 MB/s | 5/4 | 25,133 | 491.2s / 1,132,738 msg/s |
| Dekaf | 2026-07-29T09:59:46.7875424+00:00 | 1 | 9.0 MiB / 5.3 MiB | 427.3 MB/s | 5/4 | 26,247 | 500.2s / 1,008,705 msg/s |
| Dekaf | 2026-07-29T09:59:55.7885285+00:00 | 2 | 8.0 MiB / 4.9 MiB | 433.2 MB/s | 6/5 | 13,005 | 509.2s / 1,116,787 msg/s |
| Dekaf | 2026-07-29T10:00:04.7895113+00:00 | 2 | 8.0 MiB / 5.3 MiB | 433.2 MB/s | 6/5 | 13,813 | 518.2s / 1,172,052 msg/s |
| Dekaf | 2026-07-29T10:00:13.7890533+00:00 | 2 | 8.0 MiB / 3.7 MiB | 433.2 MB/s | 6/5 | 15,182 | 527.2s / 1,165,983 msg/s |
| Dekaf | 2026-07-29T10:00:22.790925+00:00 | 2 | 9.0 MiB / 5.3 MiB | 433.2 MB/s | 6/5 | 15,541 | 536.2s / 1,004,133 msg/s |
| Dekaf | 2026-07-29T10:00:31.7935606+00:00 | 3 | 6.0 MiB / 5.9 MiB | 447.2 MB/s | 7/5 | 120,110 | 545.2s / 1,147,498 msg/s |
| Dekaf | 2026-07-29T10:00:40.7953739+00:00 | 3 | 6.0 MiB / 6.0 MiB | 447.2 MB/s | 7/5 | 125,896 | 554.2s / 1,185,633 msg/s |
| Dekaf | 2026-07-29T10:00:49.7960335+00:00 | 3 | 6.0 MiB / 5.3 MiB | 447.2 MB/s | 7/6 | 131,478 | 563.2s / 1,158,154 msg/s |
| Dekaf | 2026-07-29T10:00:58.7978372+00:00 | 3 | 6.0 MiB / 5.3 MiB | 447.2 MB/s | 7/6 | 138,354 | 572.2s / 1,181,707 msg/s |
| Dekaf | 2026-07-29T10:01:08.79748+00:00 | 1 | 9.0 MiB / 8.1 MiB | 427.3 MB/s | 5/4 | 30,407 | 582.2s / 1,150,245 msg/s |
| Dekaf | 2026-07-29T10:01:17.7968871+00:00 | 1 | 9.0 MiB / 6.7 MiB | 427.3 MB/s | 5/4 | 31,320 | 591.2s / 1,193,389 msg/s |
| Dekaf | 2026-07-29T10:01:26.7973841+00:00 | 1 | 9.0 MiB / 5.2 MiB | 427.3 MB/s | 5/4 | 31,510 | 600.2s / 1,249,913 msg/s |
| Dekaf | 2026-07-29T10:01:35.7992878+00:00 | 1 | 9.0 MiB / 1.0 MiB | 431.7 MB/s | 5/4 | 32,583 | 609.2s / 1,221,472 msg/s |
| Dekaf | 2026-07-29T10:01:44.8001522+00:00 | 1 | 9.0 MiB / 3.5 MiB | 431.7 MB/s | 5/4 | 33,205 | 618.3s / 1,201,764 msg/s |
| Dekaf | 2026-07-29T10:01:53.8022138+00:00 | 2 | 7.0 MiB / 7.0 MiB | 433.2 MB/s | 7/6 | 21,293 | 627.3s / 1,214,660 msg/s |
| Dekaf | 2026-07-29T10:02:02.8040279+00:00 | 2 | 7.0 MiB / 2.5 MiB | 433.2 MB/s | 7/6 | 21,732 | 636.3s / 1,182,392 msg/s |
| Dekaf | 2026-07-29T10:02:11.8053516+00:00 | 2 | 9.0 MiB / 7.2 MiB | 433.2 MB/s | 7/7 | 22,107 | 645.3s / 1,092,950 msg/s |
| Dekaf | 2026-07-29T10:02:20.8059615+00:00 | 2 | 9.0 MiB / 0.0 MiB | 433.2 MB/s | 7/7 | 22,481 | 654.3s / 1,076,291 msg/s |
| Dekaf | 2026-07-29T10:02:29.8086538+00:00 | 3 | 6.0 MiB / 6.0 MiB | 447.8 MB/s | 7/7 | 195,087 | 663.3s / 1,201,720 msg/s |
| Dekaf | 2026-07-29T10:02:38.8113607+00:00 | 3 | 6.0 MiB / 6.0 MiB | 447.8 MB/s | 7/7 | 201,633 | 672.3s / 1,186,608 msg/s |
| Dekaf | 2026-07-29T10:02:47.8111851+00:00 | 3 | 6.0 MiB / 4.5 MiB | 447.8 MB/s | 7/7 | 208,202 | 681.3s / 1,149,909 msg/s |
| Dekaf | 2026-07-29T10:02:56.8144541+00:00 | 3 | 6.0 MiB / 6.0 MiB | 447.8 MB/s | 7/7 | 213,188 | 690.3s / 1,210,077 msg/s |
| Dekaf | 2026-07-29T10:03:06.8202693+00:00 | 1 | 10.0 MiB / 2.4 MiB | 431.7 MB/s | 6/4 | 36,919 | 700.3s / 1,203,275 msg/s |
| Dekaf | 2026-07-29T10:03:15.8240817+00:00 | 1 | 10.0 MiB / 4.2 MiB | 431.7 MB/s | 6/4 | 37,000 | 709.3s / 1,240,486 msg/s |
| Dekaf | 2026-07-29T10:03:24.8248985+00:00 | 1 | 11.0 MiB / 2.2 MiB | 431.7 MB/s | 6/4 | 37,331 | 718.3s / 1,153,914 msg/s |
| Dekaf | 2026-07-29T10:03:33.8268031+00:00 | 1 | 11.0 MiB / 2.2 MiB | 431.7 MB/s | 7/4 | 37,431 | 727.3s / 1,163,303 msg/s |
| Dekaf | 2026-07-29T10:03:42.8262489+00:00 | 2 | 9.0 MiB / 8.1 MiB | 433.2 MB/s | 7/8 | 28,273 | 736.3s / 1,201,226 msg/s |
| Dekaf | 2026-07-29T10:03:51.8277909+00:00 | 2 | 10.0 MiB / 2.9 MiB | 437.2 MB/s | 7/8 | 28,462 | 745.3s / 1,183,953 msg/s |
| Dekaf | 2026-07-29T10:04:00.8290382+00:00 | 2 | 9.0 MiB / 2.8 MiB | 437.2 MB/s | 7/8 | 28,846 | 754.3s / 1,118,968 msg/s |
| Dekaf | 2026-07-29T10:04:09.8311937+00:00 | 2 | 10.0 MiB / 2.1 MiB | 437.2 MB/s | 8/8 | 28,982 | 763.3s / 1,220,436 msg/s |
| Dekaf | 2026-07-29T10:04:18.8327209+00:00 | 3 | 7.0 MiB / 3.4 MiB | 448.1 MB/s | 7/7 | 263,890 | 772.3s / 1,201,033 msg/s |
| Dekaf | 2026-07-29T10:04:27.8328982+00:00 | 3 | 6.0 MiB / 5.9 MiB | 448.1 MB/s | 7/8 | 268,291 | 781.3s / 996,542 msg/s |
| Dekaf | 2026-07-29T10:04:36.8347344+00:00 | 3 | 6.0 MiB / 5.7 MiB | 448.1 MB/s | 7/8 | 270,104 | 790.3s / 954,452 msg/s |
| Dekaf | 2026-07-29T10:04:45.8356902+00:00 | 3 | 6.0 MiB / 5.1 MiB | 448.1 MB/s | 7/8 | 273,445 | 799.3s / 1,104,926 msg/s |
| Dekaf | 2026-07-29T10:04:55.8352997+00:00 | 1 | 8.0 MiB / 5.1 MiB | 437.4 MB/s | 8/4 | 40,014 | 809.3s / 1,146,946 msg/s |
| Dekaf | 2026-07-29T10:05:04.8362865+00:00 | 1 | 9.0 MiB / 2.6 MiB | 437.4 MB/s | 8/5 | 40,110 | 818.3s / 896,506 msg/s |
| Dekaf | 2026-07-29T10:05:13.8364108+00:00 | 1 | 9.0 MiB / 7.7 MiB | 437.4 MB/s | 8/5 | 40,310 | 827.3s / 1,107,466 msg/s |
| Dekaf | 2026-07-29T10:05:22.8420997+00:00 | 1 | 9.0 MiB / 4.1 MiB | 437.4 MB/s | 8/5 | 40,450 | 836.3s / 1,138,267 msg/s |
| Dekaf | 2026-07-29T10:05:31.8449973+00:00 | 2 | 10.0 MiB / 3.1 MiB | 437.2 MB/s | 8/9 | 29,780 | 845.3s / 1,045,778 msg/s |
| Dekaf | 2026-07-29T10:05:40.8464949+00:00 | 2 | 10.0 MiB / 2.1 MiB | 437.2 MB/s | 8/9 | 29,870 | 854.3s / 1,092,096 msg/s |
| Dekaf | 2026-07-29T10:05:49.8473402+00:00 | 2 | 10.0 MiB / 5.0 MiB | 437.2 MB/s | 8/9 | 30,046 | 863.3s / 1,035,200 msg/s |
| Dekaf | 2026-07-29T10:05:58.8489782+00:00 | 2 | 10.0 MiB / 7.3 MiB | 437.2 MB/s | 8/9 | 30,244 | 872.3s / 986,574 msg/s |
| Dekaf | 2026-07-29T10:06:07.8489143+00:00 | 3 | 6.0 MiB / 5.0 MiB | 448.1 MB/s | 7/8 | 307,369 | 881.3s / 1,113,706 msg/s |
| Dekaf | 2026-07-29T10:06:16.85024+00:00 | 3 | 6.0 MiB / 4.7 MiB | 448.1 MB/s | 7/8 | 310,860 | 890.3s / 1,214,077 msg/s |
| Dekaf | 2026-07-29T10:06:25.8538982+00:00 | 3 | 6.0 MiB / 4.5 MiB | 448.1 MB/s | 7/8 | 316,087 | 899.3s / 1,165,329 msg/s |
*2,598 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Acks All), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-29T09:51:56.9361194+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T09:51:56.9490922+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 8.6 MiB |
| Dekaf | 2026-07-29T09:51:57.0125979+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 7.7 MiB |
| Dekaf | 2026-07-29T09:52:12.0009296+00:00 | 2 | capacity | failed | 15,064ms | 16.0 MiB / 8.9 MiB |
| Dekaf | 2026-07-29T09:52:12.0191476+00:00 | 1 | capacity | succeeded | 15,069ms | 14.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-29T09:52:12.0791205+00:00 | 3 | capacity | succeeded | 15,066ms | 14.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-29T09:52:15.0251961+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-29T09:52:15.0859921+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-29T09:52:30.1039436+00:00 | 1 | capacity | succeeded | 15,078ms | 12.0 MiB / 9.2 MiB |
| Dekaf | 2026-07-29T09:52:30.1739448+00:00 | 3 | capacity | succeeded | 15,087ms | 12.0 MiB / 3.5 MiB |
| Dekaf | 2026-07-29T09:52:33.109203+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 5.2 MiB |
| Dekaf | 2026-07-29T09:52:33.1795554+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-29T09:52:42.1261371+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-29T09:52:48.1530077+00:00 | 1 | capacity | succeeded | 15,043ms | 10.0 MiB / 8.8 MiB |
| Dekaf | 2026-07-29T09:52:48.2226424+00:00 | 3 | capacity | succeeded | 15,043ms | 10.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T09:52:51.17246+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-29T09:52:51.2309506+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 1.1 MiB |
| Dekaf | 2026-07-29T09:52:57.1779478+00:00 | 2 | capacity | succeeded | 15,051ms | 14.0 MiB / 2.0 MiB |
| Dekaf | 2026-07-29T09:53:00.1839179+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-29T09:53:06.2240354+00:00 | 1 | capacity | succeeded | 15,052ms | 8.0 MiB / 6.7 MiB |
| Dekaf | 2026-07-29T09:53:06.3276685+00:00 | 3 | capacity | succeeded | 15,096ms | 8.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-29T09:53:09.2295636+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-29T09:53:09.3359093+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 5.8 MiB |
| Dekaf | 2026-07-29T09:53:15.2529587+00:00 | 2 | capacity | succeeded | 15,069ms | 12.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-29T09:53:24.3211043+00:00 | 1 | capacity | failed | 15,090ms | 8.0 MiB / 2.3 MiB |
| Dekaf | 2026-07-29T09:53:24.3888361+00:00 | 3 | capacity | failed | 15,052ms | 8.0 MiB / 2.3 MiB |
| Dekaf | 2026-07-29T09:53:45.4012385+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 4.4 MiB |
| Dekaf | 2026-07-29T09:53:54.4212577+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 1.4 MiB |
| Dekaf | 2026-07-29T09:54:00.447229+00:00 | 2 | capacity | failed | 15,045ms | 12.0 MiB / 0.9 MiB |
| Dekaf | 2026-07-29T09:54:09.4809813+00:00 | 1 | capacity | succeeded | 15,059ms | 9.0 MiB / 1.8 MiB |
| Dekaf | 2026-07-29T09:54:24.6278193+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 1.4 MiB |
| Dekaf | 2026-07-29T09:54:39.5944926+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 1.4 MiB |
| Dekaf | 2026-07-29T09:54:39.689326+00:00 | 3 | capacity | succeeded | 15,061ms | 9.0 MiB / 6.8 MiB |
| Dekaf | 2026-07-29T09:54:54.6532003+00:00 | 1 | capacity | failed | 15,058ms | 9.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-29T09:55:00.681055+00:00 | 2 | capacity | started | 0ms | 13.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-29T09:55:09.8204285+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-29T09:55:15.7356393+00:00 | 2 | capacity | succeeded | 15,054ms | 13.0 MiB / 0.8 MiB |
| Dekaf | 2026-07-29T09:55:24.8789489+00:00 | 3 | capacity | failed | 15,059ms | 9.0 MiB / 4.2 MiB |
| Dekaf | 2026-07-29T09:55:45.8827961+00:00 | 2 | capacity | started | 0ms | 11.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-29T09:55:54.9169858+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 6.2 MiB |
| Dekaf | 2026-07-29T09:55:54.9840409+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-29T09:56:00.9339815+00:00 | 2 | capacity | succeeded | 15,051ms | 11.0 MiB / 1.9 MiB |
| Dekaf | 2026-07-29T09:56:09.9609905+00:00 | 1 | capacity | failed | 15,044ms | 9.0 MiB / 2.3 MiB |
| Dekaf | 2026-07-29T09:56:10.0300617+00:00 | 3 | capacity | failed | 15,046ms | 9.0 MiB / 1.6 MiB |
| Dekaf | 2026-07-29T09:56:31.0388952+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 2.8 MiB |
| Dekaf | 2026-07-29T09:56:46.0909794+00:00 | 2 | capacity | failed | 15,052ms | 11.0 MiB / 1.0 MiB |
| Dekaf | 2026-07-29T09:57:46.2882517+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 5.8 MiB |
| Dekaf | 2026-07-29T09:58:01.3558943+00:00 | 2 | capacity | succeeded | 15,067ms | 9.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-29T09:58:04.3651425+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 3.3 MiB |
| Dekaf | 2026-07-29T09:58:10.3821384+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-29T09:58:10.4187238+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-29T09:58:12.9258282+00:00 | 3 | capacity | failed | 2,507ms | 9.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-29T09:58:19.4112938+00:00 | 2 | capacity | succeeded | 15,046ms | 8.0 MiB / 1.9 MiB |
| Dekaf | 2026-07-29T09:58:25.4205182+00:00 | 1 | capacity | failed | 15,038ms | 9.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-29T09:58:43.016655+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 7.6 MiB |
| Dekaf | 2026-07-29T09:58:49.4834938+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 3.7 MiB |
| Dekaf | 2026-07-29T09:58:58.0516741+00:00 | 3 | capacity | succeeded | 15,035ms | 7.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-29T09:59:01.0582809+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-29T09:59:04.5190295+00:00 | 2 | capacity | failed | 15,035ms | 8.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-29T09:59:16.103671+00:00 | 3 | capacity | failed | 15,045ms | 7.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-29T09:59:34.6097417+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-29T09:59:49.6496538+00:00 | 2 | capacity | failed | 15,039ms | 8.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-29T10:00:16.2759215+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 5.2 MiB |
| Dekaf | 2026-07-29T10:00:19.728552+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 3.3 MiB |
| Dekaf | 2026-07-29T10:00:31.3458206+00:00 | 3 | capacity | succeeded | 15,069ms | 6.0 MiB / 4.4 MiB |
| Dekaf | 2026-07-29T10:00:34.3511164+00:00 | 3 | capacity | started | 0ms | 5.0 MiB / 5.0 MiB |
| Dekaf | 2026-07-29T10:00:34.8013102+00:00 | 2 | capacity | failed | 15,072ms | 8.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-29T10:00:49.3959165+00:00 | 3 | capacity | failed | 15,044ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-29T10:01:04.8924344+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-29T10:01:19.9390695+00:00 | 2 | capacity | succeeded | 15,046ms | 9.0 MiB / 5.5 MiB |
| Dekaf | 2026-07-29T10:01:49.5550352+00:00 | 3 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T10:01:50.0287189+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 7.4 MiB |
| Dekaf | 2026-07-29T10:02:04.586718+00:00 | 3 | capacity | failed | 15,031ms | 6.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-29T10:02:05.0669707+00:00 | 2 | capacity | failed | 15,038ms | 9.0 MiB / 1.6 MiB |
| Dekaf | 2026-07-29T10:02:26.124159+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-29T10:02:41.1593598+00:00 | 1 | capacity | succeeded | 15,035ms | 10.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-29T10:03:05.2177616+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 7.7 MiB |
| Dekaf | 2026-07-29T10:03:11.2499889+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 2.6 MiB |
| Dekaf | 2026-07-29T10:03:20.2595231+00:00 | 2 | capacity | failed | 15,041ms | 9.0 MiB / 5.8 MiB |
| Dekaf | 2026-07-29T10:03:26.3278664+00:00 | 1 | capacity | succeeded | 15,077ms | 11.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-29T10:03:50.3885667+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 2.0 MiB |
| Dekaf | 2026-07-29T10:03:56.4124502+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-29T10:04:04.9656052+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T10:04:05.4240551+00:00 | 2 | capacity | succeeded | 15,035ms | 10.0 MiB / 4.8 MiB |
| Dekaf | 2026-07-29T10:04:11.45346+00:00 | 1 | capacity | succeeded | 15,041ms | 9.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-29T10:04:19.9993502+00:00 | 3 | capacity | failed | 15,033ms | 6.0 MiB / 1.0 MiB |
| Dekaf | 2026-07-29T10:04:35.5139193+00:00 | 2 | capacity | started | 0ms | 11.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-29T10:04:41.5375026+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 3.3 MiB |
| Dekaf | 2026-07-29T10:04:50.5521061+00:00 | 2 | capacity | failed | 15,038ms | 10.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-29T10:04:56.5948537+00:00 | 1 | capacity | failed | 15,057ms | 9.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-29T10:05:50.7422802+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-29T10:05:56.7397342+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-29T10:06:05.8084698+00:00 | 2 | capacity | succeeded | 15,066ms | 8.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-29T10:06:11.8153906+00:00 | 1 | capacity | failed | 15,075ms | 9.0 MiB / 2.2 MiB |

## Producer Admission Block Durations - Producer (Acks All), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 11 |
| Dekaf | 1 | 0.002–0.004ms | 8 |
| Dekaf | 1 | 0.004–0.008ms | 38 |
| Dekaf | 1 | 0.008–0.016ms | 103 |
| Dekaf | 1 | 0.016–0.032ms | 232 |
| Dekaf | 1 | 0.032–0.064ms | 371 |
| Dekaf | 1 | 0.064–0.128ms | 529 |
| Dekaf | 1 | 0.128–0.256ms | 871 |
| Dekaf | 1 | 0.256–0.512ms | 1,648 |
| Dekaf | 1 | 0.512–1.024ms | 2,373 |
| Dekaf | 1 | 1.024–2.048ms | 1,786 |
| Dekaf | 1 | 2.048–4.096ms | 1,182 |
| Dekaf | 1 | 4.096–8.192ms | 672 |
| Dekaf | 1 | 8.192–16.384ms | 342 |
| Dekaf | 1 | 16.384–32.768ms | 118 |
| Dekaf | 1 | 32.768–65.536ms | 8 |
| Dekaf | 2 | 0.001–0.002ms | 9 |
| Dekaf | 2 | 0.002–0.004ms | 7 |
| Dekaf | 2 | 0.004–0.008ms | 32 |
| Dekaf | 2 | 0.008–0.016ms | 108 |
| Dekaf | 2 | 0.016–0.032ms | 233 |
| Dekaf | 2 | 0.032–0.064ms | 310 |
| Dekaf | 2 | 0.064–0.128ms | 491 |
| Dekaf | 2 | 0.128–0.256ms | 830 |
| Dekaf | 2 | 0.256–0.512ms | 1,566 |
| Dekaf | 2 | 0.512–1.024ms | 2,219 |
| Dekaf | 2 | 1.024–2.048ms | 1,299 |
| Dekaf | 2 | 2.048–4.096ms | 696 |
| Dekaf | 2 | 4.096–8.192ms | 318 |
| Dekaf | 2 | 8.192–16.384ms | 138 |
| Dekaf | 2 | 16.384–32.768ms | 63 |
| Dekaf | 2 | 32.768–65.536ms | 4 |
| Dekaf | 3 | 0.001–0.002ms | 96 |
| Dekaf | 3 | 0.002–0.004ms | 85 |
| Dekaf | 3 | 0.004–0.008ms | 234 |
| Dekaf | 3 | 0.008–0.016ms | 763 |
| Dekaf | 3 | 0.016–0.032ms | 1,771 |
| Dekaf | 3 | 0.032–0.064ms | 2,980 |
| Dekaf | 3 | 0.064–0.128ms | 4,637 |
| Dekaf | 3 | 0.128–0.256ms | 8,247 |
| Dekaf | 3 | 0.256–0.512ms | 16,105 |
| Dekaf | 3 | 0.512–1.024ms | 22,915 |
| Dekaf | 3 | 1.024–2.048ms | 13,717 |
| Dekaf | 3 | 2.048–4.096ms | 5,683 |
| Dekaf | 3 | 4.096–8.192ms | 2,454 |
| Dekaf | 3 | 8.192–16.384ms | 991 |
| Dekaf | 3 | 16.384–32.768ms | 307 |
| Dekaf | 3 | 32.768–65.536ms | 17 |
| Dekaf | 3 | 65.536–131.072ms | 1 |

## Delivery Latency Outliers - Producer (Acks All), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 26,000 | 2026-07-29T09:51:26.8432514+00:00 | 111.1ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 31,000 | 2026-07-29T09:51:26.8511489+00:00 | 172.6ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 36,000 | 2026-07-29T09:51:26.8630329+00:00 | 105.4ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 41,000 | 2026-07-29T09:51:26.8714082+00:00 | 186.1ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 42,000 | 2026-07-29T09:51:26.8737622+00:00 | 183.7ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 48,000 | 2026-07-29T09:51:26.8840042+00:00 | 117.9ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 56,000 | 2026-07-29T09:51:26.8953979+00:00 | 127.8ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 58,000 | 2026-07-29T09:51:26.8983531+00:00 | 109.8ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 61,000 | 2026-07-29T09:51:26.905631+00:00 | 209.2ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 68,000 | 2026-07-29T09:51:26.9189646+00:00 | 144.0ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 71,000 | 2026-07-29T09:51:26.9241357+00:00 | 211.9ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 72,000 | 2026-07-29T09:51:26.9437285+00:00 | 192.3ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 78,000 | 2026-07-29T09:51:26.9685234+00:00 | 123.7ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 81,000 | 2026-07-29T09:51:26.9744125+00:00 | 194.2ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 84,000 | 2026-07-29T09:51:26.9994734+00:00 | 170.3ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 86,000 | 2026-07-29T09:51:27.002823+00:00 | 167.0ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 92,000 | 2026-07-29T09:51:27.0281992+00:00 | 255.7ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 97,000 | 2026-07-29T09:51:27.0408276+00:00 | 241.9ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 99,000 | 2026-07-29T09:51:27.0441735+00:00 | 238.4ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 100,000 | 2026-07-29T09:51:27.0582885+00:00 | 224.8ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 102,000 | 2026-07-29T09:51:27.0616313+00:00 | 268.9ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 103,000 | 2026-07-29T09:51:27.0632792+00:00 | 219.3ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 107,000 | 2026-07-29T09:51:27.0718255+00:00 | 240.6ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 108,000 | 2026-07-29T09:51:27.0735437+00:00 | 238.9ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 109,000 | 2026-07-29T09:51:27.0845569+00:00 | 215.8ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 115,000 | 2026-07-29T09:51:27.1148037+00:00 | 216.2ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 119,000 | 2026-07-29T09:51:27.1296942+00:00 | 240.5ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 121,000 | 2026-07-29T09:51:27.1322509+00:00 | 253.2ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 123,000 | 2026-07-29T09:51:27.1391428+00:00 | 231.1ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 125,000 | 2026-07-29T09:51:27.1423844+00:00 | 217.7ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 126,000 | 2026-07-29T09:51:27.1439764+00:00 | 255.5ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 132,000 | 2026-07-29T09:51:27.1769313+00:00 | 266.4ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 140,000 | 2026-07-29T09:51:27.2832563+00:00 | 142.7ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 141,000 | 2026-07-29T09:51:27.2854926+00:00 | 157.9ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 162,000 | 2026-07-29T09:51:27.3586842+00:00 | 141.8ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 164,000 | 2026-07-29T09:51:27.361977+00:00 | 129.6ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 170,000 | 2026-07-29T09:51:27.3929182+00:00 | 162.0ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 172,000 | 2026-07-29T09:51:27.3972808+00:00 | 150.3ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 176,000 | 2026-07-29T09:51:27.4041746+00:00 | 128.3ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 177,000 | 2026-07-29T09:51:27.4058677+00:00 | 108.9ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 180,000 | 2026-07-29T09:51:27.4269594+00:00 | 204.1ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 182,000 | 2026-07-29T09:51:27.4456721+00:00 | 128.6ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 190,000 | 2026-07-29T09:51:27.4670739+00:00 | 218.6ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 191,000 | 2026-07-29T09:51:27.4698373+00:00 | 165.1ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 192,000 | 2026-07-29T09:51:27.4716465+00:00 | 163.3ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 196,000 | 2026-07-29T09:51:27.5158176+00:00 | 134.7ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 197,000 | 2026-07-29T09:51:27.5203579+00:00 | 128.5ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 202,000 | 2026-07-29T09:51:27.5354295+00:00 | 119.3ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 206,000 | 2026-07-29T09:51:27.5456724+00:00 | 147.8ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 207,000 | 2026-07-29T09:51:27.5492906+00:00 | 128.9ms | GC pause | - | - | 1.0s / 242,851 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 212,000 | 2026-07-29T09:51:27.5667097+00:00 | 142.4ms | GC pause | - | - | 2.0s / 397,554 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 213,000 | 2026-07-29T09:51:27.5699306+00:00 | 164.8ms | GC pause | - | - | 2.0s / 397,554 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 214,000 | 2026-07-29T09:51:27.5795818+00:00 | 138.0ms | GC pause | - | - | 2.0s / 397,554 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 218,000 | 2026-07-29T09:51:27.5961225+00:00 | 121.5ms | GC pause | - | - | 2.0s / 397,554 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 222,000 | 2026-07-29T09:51:27.6144478+00:00 | 128.9ms | GC pause | - | - | 2.0s / 397,554 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 229,000 | 2026-07-29T09:51:27.644538+00:00 | 153.9ms | GC pause | - | - | 2.0s / 397,554 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 230,000 | 2026-07-29T09:51:27.6469487+00:00 | 150.5ms | GC pause | - | - | 2.0s / 397,554 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 233,000 | 2026-07-29T09:51:27.6653177+00:00 | 133.1ms | GC pause | - | - | 2.0s / 397,554 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 236,000 | 2026-07-29T09:51:27.6716949+00:00 | 141.1ms | GC pause | - | - | 2.0s / 397,554 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 239,000 | 2026-07-29T09:51:27.6865574+00:00 | 122.5ms | GC pause | - | - | 2.0s / 397,554 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 240,000 | 2026-07-29T09:51:27.6875342+00:00 | 133.7ms | GC pause | - | - | 2.0s / 397,554 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 243,000 | 2026-07-29T09:51:27.6976198+00:00 | 158.6ms | GC pause | - | - | 2.0s / 397,554 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf | 246,000 | 2026-07-29T09:51:27.7020121+00:00 | 150.6ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 248,000 | 2026-07-29T09:51:27.7068277+00:00 | 107.2ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 249,000 | 2026-07-29T09:51:27.7112471+00:00 | 249.4ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 250,000 | 2026-07-29T09:51:27.7118606+00:00 | 145.5ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 251,000 | 2026-07-29T09:51:27.7131275+00:00 | 158.0ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 253,000 | 2026-07-29T09:51:27.7364656+00:00 | 246.0ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 256,000 | 2026-07-29T09:51:27.7395243+00:00 | 131.6ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 257,000 | 2026-07-29T09:51:27.7415664+00:00 | 116.2ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 258,000 | 2026-07-29T09:51:27.7428094+00:00 | 109.3ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 260,000 | 2026-07-29T09:51:27.7456848+00:00 | 236.0ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 265,000 | 2026-07-29T09:51:27.7519291+00:00 | 128.2ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 266,000 | 2026-07-29T09:51:27.7526881+00:00 | 214.5ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 267,000 | 2026-07-29T09:51:27.7534636+00:00 | 121.8ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 270,000 | 2026-07-29T09:51:27.7701168+00:00 | 231.5ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 271,000 | 2026-07-29T09:51:27.7716277+00:00 | 215.1ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 272,000 | 2026-07-29T09:51:27.7721663+00:00 | 214.6ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 274,000 | 2026-07-29T09:51:27.7734499+00:00 | 221.5ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 275,000 | 2026-07-29T09:51:27.7747014+00:00 | 206.1ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 276,000 | 2026-07-29T09:51:27.7977671+00:00 | 217.6ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 283,000 | 2026-07-29T09:51:27.8103921+00:00 | 256.7ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 285,000 | 2026-07-29T09:51:27.812338+00:00 | 195.8ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 287,000 | 2026-07-29T09:51:27.8158193+00:00 | 186.7ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 288,000 | 2026-07-29T09:51:27.8227243+00:00 | 185.4ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 290,000 | 2026-07-29T09:51:27.8523807+00:00 | 214.7ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 291,000 | 2026-07-29T09:51:27.8566319+00:00 | 181.8ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 292,000 | 2026-07-29T09:51:27.858159+00:00 | 180.3ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 295,000 | 2026-07-29T09:51:27.8622947+00:00 | 153.7ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 300,000 | 2026-07-29T09:51:27.9651831+00:00 | 123.7ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 309,000 | 2026-07-29T09:51:27.9919597+00:00 | 105.9ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 312,000 | 2026-07-29T09:51:28.0020234+00:00 | 101.8ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 314,000 | 2026-07-29T09:51:28.0044766+00:00 | 103.5ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 316,000 | 2026-07-29T09:51:28.0061969+00:00 | 101.8ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 334,000 | 2026-07-29T09:51:28.0723278+00:00 | 138.6ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 341,000 | 2026-07-29T09:51:28.0836703+00:00 | 142.5ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 353,000 | 2026-07-29T09:51:28.1056339+00:00 | 109.8ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 356,000 | 2026-07-29T09:51:28.1103105+00:00 | 145.0ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 360,000 | 2026-07-29T09:51:28.1145962+00:00 | 124.5ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 362,000 | 2026-07-29T09:51:28.1178389+00:00 | 139.1ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 370,000 | 2026-07-29T09:51:28.1384186+00:00 | 111.5ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 376,000 | 2026-07-29T09:51:28.1494091+00:00 | 129.2ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 542,000 | 2026-07-29T09:51:28.4520171+00:00 | 135.5ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 546,000 | 2026-07-29T09:51:28.4555859+00:00 | 140.6ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 549,000 | 2026-07-29T09:51:28.458658+00:00 | 130.7ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 550,000 | 2026-07-29T09:51:28.4592522+00:00 | 147.2ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 551,000 | 2026-07-29T09:51:28.4608557+00:00 | 136.1ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 554,000 | 2026-07-29T09:51:28.4665491+00:00 | 153.1ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 559,000 | 2026-07-29T09:51:28.4702955+00:00 | 136.4ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 560,000 | 2026-07-29T09:51:28.4715383+00:00 | 151.4ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 561,000 | 2026-07-29T09:51:28.4724069+00:00 | 151.0ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 564,000 | 2026-07-29T09:51:28.4756524+00:00 | 157.5ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 566,000 | 2026-07-29T09:51:28.4884311+00:00 | 144.7ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 567,000 | 2026-07-29T09:51:28.4895414+00:00 | 102.3ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 569,000 | 2026-07-29T09:51:28.4908302+00:00 | 152.8ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 571,000 | 2026-07-29T09:51:28.4929976+00:00 | 145.2ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 572,000 | 2026-07-29T09:51:28.4975146+00:00 | 140.7ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 576,000 | 2026-07-29T09:51:28.5112301+00:00 | 138.7ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 577,000 | 2026-07-29T09:51:28.511748+00:00 | 115.3ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 580,000 | 2026-07-29T09:51:28.5197068+00:00 | 147.2ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 581,000 | 2026-07-29T09:51:28.5213509+00:00 | 136.4ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 583,000 | 2026-07-29T09:51:28.5293192+00:00 | 147.3ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 584,000 | 2026-07-29T09:51:28.5317383+00:00 | 126.0ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 589,000 | 2026-07-29T09:51:28.5410371+00:00 | 141.7ms | throughput collapse | - | - | 2.0s / 397,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 599,000 | 2026-07-29T09:51:28.6064172+00:00 | 109.5ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 630,000 | 2026-07-29T09:51:28.6836766+00:00 | 120.5ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 632,000 | 2026-07-29T09:51:28.6871328+00:00 | 112.1ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 633,000 | 2026-07-29T09:51:28.6881505+00:00 | 141.0ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 634,000 | 2026-07-29T09:51:28.6891583+00:00 | 110.1ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 635,000 | 2026-07-29T09:51:28.6901539+00:00 | 110.8ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 637,000 | 2026-07-29T09:51:28.6915507+00:00 | 120.7ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 638,000 | 2026-07-29T09:51:28.6922251+00:00 | 108.8ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 639,000 | 2026-07-29T09:51:28.6928304+00:00 | 141.0ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 640,000 | 2026-07-29T09:51:28.695711+00:00 | 144.4ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 641,000 | 2026-07-29T09:51:28.6962183+00:00 | 117.7ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 643,000 | 2026-07-29T09:51:28.7057175+00:00 | 135.0ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 644,000 | 2026-07-29T09:51:28.7083889+00:00 | 108.1ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 646,000 | 2026-07-29T09:51:28.7178991+00:00 | 116.1ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 648,000 | 2026-07-29T09:51:28.7211685+00:00 | 106.8ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 654,000 | 2026-07-29T09:51:28.7339522+00:00 | 102.4ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 660,000 | 2026-07-29T09:51:28.745257+00:00 | 123.7ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 670,000 | 2026-07-29T09:51:28.7686341+00:00 | 127.6ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 673,000 | 2026-07-29T09:51:28.8015603+00:00 | 110.5ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 679,000 | 2026-07-29T09:51:28.8103367+00:00 | 119.2ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 680,000 | 2026-07-29T09:51:28.8113679+00:00 | 100.1ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 690,000 | 2026-07-29T09:51:28.8437068+00:00 | 120.0ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 700,000 | 2026-07-29T09:51:28.8683759+00:00 | 110.7ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 723,000 | 2026-07-29T09:51:28.9132521+00:00 | 109.8ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 733,000 | 2026-07-29T09:51:28.9466125+00:00 | 105.4ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 743,000 | 2026-07-29T09:51:28.9672471+00:00 | 108.8ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 789,000 | 2026-07-29T09:51:29.078408+00:00 | 151.3ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 799,000 | 2026-07-29T09:51:29.0882181+00:00 | 150.4ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 809,000 | 2026-07-29T09:51:29.1050051+00:00 | 165.3ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 819,000 | 2026-07-29T09:51:29.1275985+00:00 | 170.8ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 829,000 | 2026-07-29T09:51:29.1470433+00:00 | 158.1ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 833,000 | 2026-07-29T09:51:29.1513898+00:00 | 157.8ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 990,000 | 2026-07-29T09:51:29.5112737+00:00 | 107.1ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 993,000 | 2026-07-29T09:51:29.5162705+00:00 | 127.8ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,000,000 | 2026-07-29T09:51:29.5260446+00:00 | 118.1ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,003,000 | 2026-07-29T09:51:29.5287493+00:00 | 115.4ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,019,000 | 2026-07-29T09:51:29.5723146+00:00 | 101.9ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,020,000 | 2026-07-29T09:51:29.5728161+00:00 | 102.1ms | throughput collapse | - | - | 3.0s / 444,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,069,000 | 2026-07-29T09:51:29.6781724+00:00 | 100.5ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,089,000 | 2026-07-29T09:51:29.7045035+00:00 | 118.2ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,099,000 | 2026-07-29T09:51:29.7261687+00:00 | 112.4ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,100,000 | 2026-07-29T09:51:29.7278484+00:00 | 131.3ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,103,000 | 2026-07-29T09:51:29.7298224+00:00 | 108.7ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,113,000 | 2026-07-29T09:51:29.7581504+00:00 | 119.3ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,119,000 | 2026-07-29T09:51:29.780654+00:00 | 103.8ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,140,000 | 2026-07-29T09:51:29.8252697+00:00 | 110.7ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,143,000 | 2026-07-29T09:51:29.8325464+00:00 | 104.1ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,170,000 | 2026-07-29T09:51:29.8901101+00:00 | 105.0ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,183,000 | 2026-07-29T09:51:29.9157002+00:00 | 100.6ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,193,000 | 2026-07-29T09:51:29.9424963+00:00 | 108.2ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,199,000 | 2026-07-29T09:51:29.9503154+00:00 | 112.4ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,200,000 | 2026-07-29T09:51:29.9515959+00:00 | 123.3ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,233,000 | 2026-07-29T09:51:30.0221159+00:00 | 101.4ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,270,000 | 2026-07-29T09:51:30.1040552+00:00 | 101.2ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,319,000 | 2026-07-29T09:51:30.2082463+00:00 | 113.5ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,323,000 | 2026-07-29T09:51:30.2144138+00:00 | 107.4ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,339,000 | 2026-07-29T09:51:30.2387981+00:00 | 111.8ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,443,000 | 2026-07-29T09:51:30.4207104+00:00 | 114.8ms | throughput collapse | - | - | 4.0s / 504,962 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,603,000 | 2026-07-29T09:51:30.7293362+00:00 | 102.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 598,055 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,623,000 | 2026-07-29T09:51:30.7707224+00:00 | 116.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 598,055 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,643,000 | 2026-07-29T09:51:30.8160787+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 598,055 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,649,000 | 2026-07-29T09:51:30.8282094+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 598,055 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,899,000 | 2026-07-29T09:51:31.2480913+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 598,055 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,520,000 | 2026-07-29T09:51:32.2004123+00:00 | 107.6ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 665,336 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,529,000 | 2026-07-29T09:51:32.2214113+00:00 | 108.7ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 665,336 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,824,000 | 2026-07-29T09:51:32.6373788+00:00 | 134.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 692,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,831,000 | 2026-07-29T09:51:32.6491715+00:00 | 175.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 692,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,842,000 | 2026-07-29T09:51:32.6629736+00:00 | 181.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 692,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,846,000 | 2026-07-29T09:51:32.6764671+00:00 | 175.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 692,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,850,000 | 2026-07-29T09:51:32.6790649+00:00 | 143.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 692,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,864,000 | 2026-07-29T09:51:32.7219026+00:00 | 159.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 692,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,867,000 | 2026-07-29T09:51:32.7251097+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 692,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,870,000 | 2026-07-29T09:51:32.7269923+00:00 | 134.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 692,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,553,000 | 2026-07-29T09:51:33.7067323+00:00 | 100.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 762,158 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,559,000 | 2026-07-29T09:51:33.7143897+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 762,158 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,263,000 | 2026-07-29T09:51:34.6311518+00:00 | 115.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 685,311 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,269,000 | 2026-07-29T09:51:34.6403194+00:00 | 107.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 685,311 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,283,000 | 2026-07-29T09:51:34.6492651+00:00 | 132.7ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 685,311 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,290,000 | 2026-07-29T09:51:34.6602895+00:00 | 161.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 685,311 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,313,000 | 2026-07-29T09:51:34.7462699+00:00 | 110.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 685,311 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,318,000 | 2026-07-29T09:51:34.750842+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 685,311 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,323,000 | 2026-07-29T09:51:34.7698343+00:00 | 112.5ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 685,311 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,328,000 | 2026-07-29T09:51:34.7732853+00:00 | 112.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 685,311 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,330,000 | 2026-07-29T09:51:34.7838669+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 685,311 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,360,000 | 2026-07-29T09:51:36.1854504+00:00 | 124.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 666,759 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,369,000 | 2026-07-29T09:51:36.1954319+00:00 | 116.3ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 666,759 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,389,000 | 2026-07-29T09:51:36.2198209+00:00 | 121.8ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 666,759 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,393,000 | 2026-07-29T09:51:36.2347669+00:00 | 106.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 666,759 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,409,000 | 2026-07-29T09:51:36.2606942+00:00 | 112.5ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 666,759 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,667,000 | 2026-07-29T09:51:36.7080928+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 689,147 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,677,000 | 2026-07-29T09:51:36.7192912+00:00 | 115.5ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 689,147 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,678,000 | 2026-07-29T09:51:36.7196208+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 689,147 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,098,000 | 2026-07-29T09:51:38.711214+00:00 | 111.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 713,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,115,000 | 2026-07-29T09:51:38.7295118+00:00 | 117.9ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 713,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,117,000 | 2026-07-29T09:51:38.7302554+00:00 | 116.9ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 713,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,125,000 | 2026-07-29T09:51:38.7434071+00:00 | 110.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 713,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,127,000 | 2026-07-29T09:51:38.749494+00:00 | 111.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 713,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,128,000 | 2026-07-29T09:51:38.7504843+00:00 | 110.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 713,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,138,000 | 2026-07-29T09:51:38.7664703+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 713,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,464,000 | 2026-07-29T09:51:39.2429843+00:00 | 108.1ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 713,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,466,000 | 2026-07-29T09:51:39.2442857+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 713,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,471,000 | 2026-07-29T09:51:39.247695+00:00 | 108.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 713,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,201,000 | 2026-07-29T09:51:40.2039475+00:00 | 124.0ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 738,499 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,202,000 | 2026-07-29T09:51:40.2044637+00:00 | 123.5ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 738,499 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,204,000 | 2026-07-29T09:51:40.2072765+00:00 | 119.0ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 738,499 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,214,000 | 2026-07-29T09:51:40.2256378+00:00 | 122.7ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 738,499 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,221,000 | 2026-07-29T09:51:40.2375339+00:00 | 111.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 738,499 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,222,000 | 2026-07-29T09:51:40.2382765+00:00 | 120.2ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 738,499 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,231,000 | 2026-07-29T09:51:40.250151+00:00 | 116.9ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 738,499 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,234,000 | 2026-07-29T09:51:40.2561635+00:00 | 134.9ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 738,499 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,236,000 | 2026-07-29T09:51:40.2573356+00:00 | 131.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 738,499 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,241,000 | 2026-07-29T09:51:40.2659207+00:00 | 122.5ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 738,499 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,246,000 | 2026-07-29T09:51:40.2702976+00:00 | 120.7ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 738,499 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,245,000 | 2026-07-29T09:51:43.7343698+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 866,218 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,275,000 | 2026-07-29T09:51:43.7650309+00:00 | 108.6ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 866,218 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,637,000 | 2026-07-29T09:51:44.2016824+00:00 | 114.0ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 866,218 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,054,000 | 2026-07-29T09:51:53.7333285+00:00 | 107.6ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 720,340 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,056,000 | 2026-07-29T09:51:53.7362183+00:00 | 104.7ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 720,340 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,502,000 | 2026-07-29T09:51:56.7608534+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 31.0s / 836,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 26,238,000 | 2026-07-29T09:52:01.2293903+00:00 | 110.7ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/succeeded | - | 35.0s / 829,031 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 26,245,000 | 2026-07-29T09:52:01.2367546+00:00 | 109.4ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/succeeded | - | 35.0s / 829,031 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 26,258,000 | 2026-07-29T09:52:01.2530126+00:00 | 105.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/succeeded | - | 35.0s / 829,031 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 29,281,000 | 2026-07-29T09:52:04.7383024+00:00 | 101.0ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/succeeded | - | 39.0s / 733,678 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 29,282,000 | 2026-07-29T09:52:04.738848+00:00 | 100.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/succeeded | - | 39.0s / 733,678 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 29,286,000 | 2026-07-29T09:52:04.742515+00:00 | 103.9ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/succeeded | - | 39.0s / 733,678 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 29,291,000 | 2026-07-29T09:52:04.7475766+00:00 | 100.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/succeeded | - | 39.0s / 733,678 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 29,294,000 | 2026-07-29T09:52:04.7506217+00:00 | 101.3ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/succeeded | - | 39.0s / 733,678 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,748,000 | 2026-07-29T09:52:22.7321546+00:00 | 106.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded | - | 57.0s / 886,996 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 47,919,000 | 2026-07-29T09:52:26.1065162+00:00 | 130.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded | - | 60.0s / 865,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 47,929,000 | 2026-07-29T09:52:26.11522+00:00 | 122.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded | - | 60.0s / 865,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 47,933,000 | 2026-07-29T09:52:26.1179421+00:00 | 122.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded | - | 60.0s / 865,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 47,943,000 | 2026-07-29T09:52:26.1266068+00:00 | 119.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded | - | 60.0s / 865,825 msg/s | Gen2 +0 / pause +0.0ms |
| Confluent | 326,498,000 | 2026-07-29T10:12:19.0539869+00:00 | 100.9ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,503,000 | 2026-07-29T10:12:19.0597804+00:00 | 109.7ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,507,000 | 2026-07-29T10:12:19.0643108+00:00 | 108.9ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,508,000 | 2026-07-29T10:12:19.0652376+00:00 | 108.0ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,510,000 | 2026-07-29T10:12:19.0663359+00:00 | 108.3ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,511,000 | 2026-07-29T10:12:19.0668509+00:00 | 106.9ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,513,000 | 2026-07-29T10:12:19.0696128+00:00 | 105.3ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,515,000 | 2026-07-29T10:12:19.070726+00:00 | 103.4ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,516,000 | 2026-07-29T10:12:19.0725982+00:00 | 101.6ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,517,000 | 2026-07-29T10:12:19.0730932+00:00 | 102.3ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,518,000 | 2026-07-29T10:12:19.0735658+00:00 | 114.2ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,519,000 | 2026-07-29T10:12:19.0741613+00:00 | 100.2ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,520,000 | 2026-07-29T10:12:19.0749818+00:00 | 112.4ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,521,000 | 2026-07-29T10:12:19.0754534+00:00 | 112.5ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,522,000 | 2026-07-29T10:12:19.0759891+00:00 | 110.4ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,523,000 | 2026-07-29T10:12:19.0766234+00:00 | 111.8ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,525,000 | 2026-07-29T10:12:19.0792066+00:00 | 109.7ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,526,000 | 2026-07-29T10:12:19.0797735+00:00 | 109.1ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,527,000 | 2026-07-29T10:12:19.0803374+00:00 | 114.2ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,528,000 | 2026-07-29T10:12:19.081967+00:00 | 112.6ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,529,000 | 2026-07-29T10:12:19.0825612+00:00 | 106.7ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,530,000 | 2026-07-29T10:12:19.0831819+00:00 | 115.6ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,531,000 | 2026-07-29T10:12:19.0840766+00:00 | 110.7ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,532,000 | 2026-07-29T10:12:19.0849253+00:00 | 108.5ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,533,000 | 2026-07-29T10:12:19.086096+00:00 | 113.1ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,535,000 | 2026-07-29T10:12:19.0883946+00:00 | 105.3ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,536,000 | 2026-07-29T10:12:19.0894114+00:00 | 104.3ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,537,000 | 2026-07-29T10:12:19.0906768+00:00 | 108.3ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,538,000 | 2026-07-29T10:12:19.0918238+00:00 | 107.2ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,539,000 | 2026-07-29T10:12:19.0924163+00:00 | 102.6ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,540,000 | 2026-07-29T10:12:19.0933096+00:00 | 108.3ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,541,000 | 2026-07-29T10:12:19.0941375+00:00 | 106.5ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,542,000 | 2026-07-29T10:12:19.094849+00:00 | 105.3ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,543,000 | 2026-07-29T10:12:19.0956866+00:00 | 106.0ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,545,000 | 2026-07-29T10:12:19.0974636+00:00 | 102.2ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,546,000 | 2026-07-29T10:12:19.0985081+00:00 | 101.2ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,547,000 | 2026-07-29T10:12:19.0993041+00:00 | 110.0ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,548,000 | 2026-07-29T10:12:19.1002818+00:00 | 109.1ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,550,000 | 2026-07-29T10:12:19.1027365+00:00 | 107.1ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,551,000 | 2026-07-29T10:12:19.1039362+00:00 | 105.6ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,552,000 | 2026-07-29T10:12:19.1048567+00:00 | 107.3ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,553,000 | 2026-07-29T10:12:19.1057201+00:00 | 108.3ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,557,000 | 2026-07-29T10:12:19.1114647+00:00 | 102.4ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,558,000 | 2026-07-29T10:12:19.1123039+00:00 | 103.5ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,560,000 | 2026-07-29T10:12:19.1146488+00:00 | 100.3ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,561,000 | 2026-07-29T10:12:19.1158576+00:00 | 100.1ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,877,000 | 2026-07-29T10:12:19.5559685+00:00 | 106.3ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,878,000 | 2026-07-29T10:12:19.5571574+00:00 | 105.1ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,880,000 | 2026-07-29T10:12:19.5587801+00:00 | 110.7ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,881,000 | 2026-07-29T10:12:19.5605202+00:00 | 109.0ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,883,000 | 2026-07-29T10:12:19.5622053+00:00 | 107.8ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,887,000 | 2026-07-29T10:12:19.5662831+00:00 | 107.2ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,888,000 | 2026-07-29T10:12:19.567091+00:00 | 106.4ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,890,000 | 2026-07-29T10:12:19.5705349+00:00 | 105.2ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,891,000 | 2026-07-29T10:12:19.5721804+00:00 | 104.5ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,893,000 | 2026-07-29T10:12:19.5749577+00:00 | 111.7ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,897,000 | 2026-07-29T10:12:19.5786971+00:00 | 111.9ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,898,000 | 2026-07-29T10:12:19.5795964+00:00 | 111.0ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,900,000 | 2026-07-29T10:12:19.5835765+00:00 | 114.0ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,901,000 | 2026-07-29T10:12:19.5843607+00:00 | 129.0ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,903,000 | 2026-07-29T10:12:19.5863594+00:00 | 111.4ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,907,000 | 2026-07-29T10:12:19.5899321+00:00 | 124.7ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,908,000 | 2026-07-29T10:12:19.5904972+00:00 | 124.2ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,910,000 | 2026-07-29T10:12:19.5915865+00:00 | 122.8ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,911,000 | 2026-07-29T10:12:19.5922583+00:00 | 129.4ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,913,000 | 2026-07-29T10:12:19.5934674+00:00 | 128.8ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,917,000 | 2026-07-29T10:12:19.5967163+00:00 | 128.0ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,918,000 | 2026-07-29T10:12:19.5975606+00:00 | 127.3ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,920,000 | 2026-07-29T10:12:19.5995367+00:00 | 128.7ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,921,000 | 2026-07-29T10:12:19.6004233+00:00 | 124.7ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,922,000 | 2026-07-29T10:12:19.6012028+00:00 | 113.8ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,923,000 | 2026-07-29T10:12:19.6020611+00:00 | 139.7ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,925,000 | 2026-07-29T10:12:19.6038529+00:00 | 101.6ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,926,000 | 2026-07-29T10:12:19.6047445+00:00 | 100.8ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,927,000 | 2026-07-29T10:12:19.6055991+00:00 | 120.5ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,928,000 | 2026-07-29T10:12:19.606658+00:00 | 120.1ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,930,000 | 2026-07-29T10:12:19.6084115+00:00 | 139.3ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,931,000 | 2026-07-29T10:12:19.6092292+00:00 | 117.8ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,932,000 | 2026-07-29T10:12:19.6101627+00:00 | 110.5ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,933,000 | 2026-07-29T10:12:19.6109021+00:00 | 137.0ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,935,000 | 2026-07-29T10:12:19.6123891+00:00 | 103.5ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,936,000 | 2026-07-29T10:12:19.61313+00:00 | 107.6ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,937,000 | 2026-07-29T10:12:19.6142257+00:00 | 119.2ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,938,000 | 2026-07-29T10:12:19.6151181+00:00 | 118.4ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,939,000 | 2026-07-29T10:12:19.6158952+00:00 | 105.1ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,940,000 | 2026-07-29T10:12:19.6163946+00:00 | 133.4ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,941,000 | 2026-07-29T10:12:19.617211+00:00 | 118.0ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,942,000 | 2026-07-29T10:12:19.6180443+00:00 | 109.8ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,943,000 | 2026-07-29T10:12:19.6189015+00:00 | 137.3ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,945,000 | 2026-07-29T10:12:19.620761+00:00 | 102.4ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,946,000 | 2026-07-29T10:12:19.6216519+00:00 | 101.7ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,947,000 | 2026-07-29T10:12:19.622417+00:00 | 114.0ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,948,000 | 2026-07-29T10:12:19.6231887+00:00 | 113.4ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,949,000 | 2026-07-29T10:12:19.6241459+00:00 | 115.2ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,950,000 | 2026-07-29T10:12:19.6249571+00:00 | 148.0ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,951,000 | 2026-07-29T10:12:19.6257814+00:00 | 116.4ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,952,000 | 2026-07-29T10:12:19.6268356+00:00 | 106.9ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,953,000 | 2026-07-29T10:12:19.627746+00:00 | 149.4ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,955,000 | 2026-07-29T10:12:19.631353+00:00 | 108.3ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,956,000 | 2026-07-29T10:12:19.6321572+00:00 | 107.6ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,957,000 | 2026-07-29T10:12:19.6329642+00:00 | 115.8ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,958,000 | 2026-07-29T10:12:19.633834+00:00 | 115.1ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,959,000 | 2026-07-29T10:12:19.6343158+00:00 | 112.9ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,960,000 | 2026-07-29T10:12:19.6347978+00:00 | 146.6ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,961,000 | 2026-07-29T10:12:19.6353407+00:00 | 113.7ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,963,000 | 2026-07-29T10:12:19.6366036+00:00 | 144.9ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,965,000 | 2026-07-29T10:12:19.6377266+00:00 | 111.4ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,966,000 | 2026-07-29T10:12:19.6382253+00:00 | 111.0ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,967,000 | 2026-07-29T10:12:19.639758+00:00 | 115.4ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,968,000 | 2026-07-29T10:12:19.641456+00:00 | 118.5ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,969,000 | 2026-07-29T10:12:19.6430906+00:00 | 106.3ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,970,000 | 2026-07-29T10:12:19.6448621+00:00 | 138.8ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,971,000 | 2026-07-29T10:12:19.6470566+00:00 | 113.1ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,973,000 | 2026-07-29T10:12:19.6515116+00:00 | 135.0ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,975,000 | 2026-07-29T10:12:19.6549771+00:00 | 100.6ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,977,000 | 2026-07-29T10:12:19.6595174+00:00 | 109.4ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,978,000 | 2026-07-29T10:12:19.661947+00:00 | 107.0ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,980,000 | 2026-07-29T10:12:19.672578+00:00 | 135.0ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,983,000 | 2026-07-29T10:12:19.6802584+00:00 | 135.5ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,990,000 | 2026-07-29T10:12:19.6968018+00:00 | 120.9ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 326,993,000 | 2026-07-29T10:12:19.7081717+00:00 | 109.9ms | GC pause | - | - | 353.2s / 733,858 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 352,297,000 | 2026-07-29T10:12:50.2590592+00:00 | 106.4ms | GC pause | - | - | 384.2s / 804,635 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 352,298,000 | 2026-07-29T10:12:50.2605147+00:00 | 105.0ms | GC pause | - | - | 384.2s / 804,635 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 352,300,000 | 2026-07-29T10:12:50.2647803+00:00 | 102.6ms | GC pause | - | - | 384.2s / 804,635 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 352,301,000 | 2026-07-29T10:12:50.2659664+00:00 | 107.7ms | GC pause | - | - | 384.2s / 804,635 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 352,307,000 | 2026-07-29T10:12:50.2723801+00:00 | 105.2ms | GC pause | - | - | 384.2s / 804,635 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 352,308,000 | 2026-07-29T10:12:50.2735948+00:00 | 104.0ms | GC pause | - | - | 384.2s / 804,635 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 352,310,000 | 2026-07-29T10:12:50.2755589+00:00 | 104.4ms | GC pause | - | - | 384.2s / 804,635 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 352,311,000 | 2026-07-29T10:12:50.2795131+00:00 | 100.9ms | GC pause | - | - | 384.2s / 804,635 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 352,313,000 | 2026-07-29T10:12:50.2827532+00:00 | 100.6ms | GC pause | - | - | 384.2s / 804,635 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 536,451,000 | 2026-07-29T10:16:25.3094644+00:00 | 120.4ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,453,000 | 2026-07-29T10:16:25.3127229+00:00 | 117.5ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,457,000 | 2026-07-29T10:16:25.3199056+00:00 | 116.9ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,458,000 | 2026-07-29T10:16:25.3215137+00:00 | 115.4ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,460,000 | 2026-07-29T10:16:25.3240546+00:00 | 113.2ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,461,000 | 2026-07-29T10:16:25.327157+00:00 | 115.9ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,463,000 | 2026-07-29T10:16:25.3293102+00:00 | 108.3ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,467,000 | 2026-07-29T10:16:25.3348407+00:00 | 108.8ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,468,000 | 2026-07-29T10:16:25.3377477+00:00 | 116.5ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,470,000 | 2026-07-29T10:16:25.340805+00:00 | 105.4ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,471,000 | 2026-07-29T10:16:25.3419136+00:00 | 112.6ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,473,000 | 2026-07-29T10:16:25.3470047+00:00 | 108.4ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,477,000 | 2026-07-29T10:16:25.3519168+00:00 | 106.8ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,478,000 | 2026-07-29T10:16:25.353026+00:00 | 105.7ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,480,000 | 2026-07-29T10:16:25.3577921+00:00 | 118.6ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,481,000 | 2026-07-29T10:16:25.35865+00:00 | 112.7ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,483,000 | 2026-07-29T10:16:25.3605637+00:00 | 116.0ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,487,000 | 2026-07-29T10:16:25.3660191+00:00 | 105.7ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,488,000 | 2026-07-29T10:16:25.3696775+00:00 | 102.1ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,490,000 | 2026-07-29T10:16:25.3733604+00:00 | 104.8ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,491,000 | 2026-07-29T10:16:25.3741051+00:00 | 110.9ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,493,000 | 2026-07-29T10:16:25.3761497+00:00 | 102.3ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,497,000 | 2026-07-29T10:16:25.3801381+00:00 | 112.8ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,498,000 | 2026-07-29T10:16:25.381058+00:00 | 112.0ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,500,000 | 2026-07-29T10:16:25.3822209+00:00 | 104.3ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,501,000 | 2026-07-29T10:16:25.383963+00:00 | 114.1ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,503,000 | 2026-07-29T10:16:25.3855807+00:00 | 101.2ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,507,000 | 2026-07-29T10:16:25.3906977+00:00 | 108.9ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,508,000 | 2026-07-29T10:16:25.3913072+00:00 | 108.4ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,510,000 | 2026-07-29T10:16:25.3925677+00:00 | 106.3ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,511,000 | 2026-07-29T10:16:25.3933493+00:00 | 111.5ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,513,000 | 2026-07-29T10:16:25.3949506+00:00 | 104.2ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,517,000 | 2026-07-29T10:16:25.3998457+00:00 | 110.1ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,518,000 | 2026-07-29T10:16:25.4008023+00:00 | 109.3ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,521,000 | 2026-07-29T10:16:25.4038049+00:00 | 109.2ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,527,000 | 2026-07-29T10:16:25.4101757+00:00 | 106.6ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,528,000 | 2026-07-29T10:16:25.4111777+00:00 | 105.7ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |
| Confluent | 536,531,000 | 2026-07-29T10:16:25.4147821+00:00 | 102.5ms | GC pause | - | - | 599.3s / 778,113 msg/s | Gen2 +0 / pause +66.1ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*409 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.46x less CPU per message** than Confluent.Kafka for producer (acks all), 3 brokers; comparison throughput is 1.20x.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,551,385 | 1,550,322–1,552,449 | 0.98 | 1.30x |
| Confluent | 2 | 1,189,579 | 1,180,489–1,198,739 | 1.56 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.77 | 730.86 | 1,694,951 | 1,701,754 | -4.0% | -0.62% | 1616.43 | 1,694,951 | 0 | 1.30 |
| Dekaf (dekaf-first) | 0.97 | 995.19 | 1,535,590 | 1,552,449 | -1.2% | -0.04% | 1464.45 | 1,535,590 | 0 | 1.49 |
| Dekaf (confluent-first) | 0.99 | 1017.26 | 1,530,184 | 1,550,322 | +5.0% | +0.45% | 1459.30 | 1,530,184 | 0 | 1.52 |
| Confluent (confluent-first) | 1.50 | - | 1,168,060 | 1,198,739 | -11.3% | -1.38% | 1113.95 | 1,168,060 | 0 | 1.75 |
| Confluent (dekaf-first) | 1.62 | - | 1,067,624 | 1,180,489 | +102.0% | +9.04% | 1018.17 | 1,067,624 | 0 | 1.73 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget, Idempotent)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,349,294 | 1499.20 | 1018.18 KB |
| Dekaf | 1 | 1,341,478 | 1490.51 | 1020.52 KB |
| Dekaf (3conn) | 1 | 1,599,137 | 1776.79 | 948.26 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget, Idempotent)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-29T09:51:17.3129069+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 583,870 msg/s |
| Dekaf | 2026-07-29T09:51:44.321058+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1703.4 MB/s | 0/0 | 44,460 | 27.0s / 1,564,294 msg/s |
| Dekaf | 2026-07-29T09:52:12.3291422+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1708.5 MB/s | 1/0 | 94,242 | 55.0s / 1,384,540 msg/s |
| Dekaf | 2026-07-29T09:52:39.3380691+00:00 | 1 | 14.0 MiB / 13.9 MiB | 1744.4 MB/s | 1/0 | 143,994 | 82.0s / 1,641,217 msg/s |
| Dekaf | 2026-07-29T09:53:06.3482257+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1756.2 MB/s | 1/1 | 201,725 | 109.0s / 1,490,590 msg/s |
| Dekaf | 2026-07-29T09:53:33.3560668+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1768.4 MB/s | 1/1 | 247,383 | 136.0s / 1,588,197 msg/s |
| Dekaf | 2026-07-29T09:54:01.3703659+00:00 | 1 | 15.0 MiB / 8.9 MiB | 1768.4 MB/s | 1/1 | 297,432 | 164.0s / 1,584,452 msg/s |
| Dekaf | 2026-07-29T09:54:28.3748738+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1768.4 MB/s | 1/2 | 349,766 | 191.1s / 1,605,574 msg/s |
| Dekaf | 2026-07-29T09:54:55.3823915+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1768.4 MB/s | 1/2 | 406,111 | 218.1s / 1,631,636 msg/s |
| Dekaf | 2026-07-29T09:55:22.3935012+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1768.4 MB/s | 1/2 | 467,805 | 245.1s / 1,636,081 msg/s |
| Dekaf | 2026-07-29T09:55:50.4007806+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1776.5 MB/s | 1/2 | 532,447 | 273.1s / 1,591,043 msg/s |
| Dekaf | 2026-07-29T09:56:17.4077882+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1776.5 MB/s | 1/2 | 590,686 | 300.1s / 1,591,417 msg/s |
| Dekaf | 2026-07-29T09:56:44.4170854+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1776.5 MB/s | 2/2 | 652,351 | 327.1s / 1,609,453 msg/s |
| Dekaf | 2026-07-29T09:57:11.4261683+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1776.5 MB/s | 2/3 | 703,651 | 354.1s / 1,561,711 msg/s |
| Dekaf | 2026-07-29T09:57:39.433744+00:00 | 1 | 12.0 MiB / 5.3 MiB | 1776.5 MB/s | 2/3 | 754,943 | 382.1s / 1,528,953 msg/s |
| Dekaf | 2026-07-29T09:58:06.4366881+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1779.8 MB/s | 2/3 | 796,222 | 409.1s / 1,355,579 msg/s |
| Dekaf | 2026-07-29T09:58:33.4447721+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1779.8 MB/s | 3/3 | 845,101 | 436.1s / 1,431,058 msg/s |
| Dekaf | 2026-07-29T09:59:00.4506442+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1779.8 MB/s | 3/3 | 897,449 | 463.1s / 1,595,150 msg/s |
| Dekaf | 2026-07-29T09:59:28.4577337+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1779.8 MB/s | 4/3 | 951,988 | 491.1s / 1,547,608 msg/s |
| Dekaf | 2026-07-29T09:59:55.4628785+00:00 | 1 | 15.0 MiB / 11.4 MiB | 1779.8 MB/s | 5/3 | 997,857 | 518.2s / 1,529,529 msg/s |
| Dekaf | 2026-07-29T10:00:22.4751633+00:00 | 1 | 15.0 MiB / 12.9 MiB | 1779.8 MB/s | 5/3 | 1,047,196 | 545.2s / 1,569,715 msg/s |
| Dekaf | 2026-07-29T10:00:50.4874675+00:00 | 1 | 16.0 MiB / 13.8 MiB | 1779.8 MB/s | 6/3 | 1,096,360 | 573.2s / 1,537,345 msg/s |
| Dekaf | 2026-07-29T10:01:17.49848+00:00 | 1 | 18.0 MiB / 17.4 MiB | 1785.8 MB/s | 6/3 | 1,149,428 | 600.2s / 1,552,449 msg/s |
| Dekaf | 2026-07-29T10:01:44.5119809+00:00 | 1 | 18.0 MiB / 15.4 MiB | 1785.8 MB/s | 7/3 | 1,189,082 | 627.2s / 1,422,763 msg/s |
| Dekaf | 2026-07-29T10:02:11.5195007+00:00 | 1 | 20.0 MiB / 19.6 MiB | 1785.8 MB/s | 8/3 | 1,232,576 | 654.2s / 1,558,446 msg/s |
| Dekaf | 2026-07-29T10:02:39.5265303+00:00 | 1 | 20.0 MiB / 19.7 MiB | 1785.8 MB/s | 8/3 | 1,278,988 | 682.2s / 1,595,733 msg/s |
| Dekaf | 2026-07-29T10:03:06.5322462+00:00 | 1 | 22.0 MiB / 22.0 MiB | 1785.8 MB/s | 9/3 | 1,328,524 | 709.2s / 1,481,273 msg/s |
| Dekaf | 2026-07-29T10:03:33.5366368+00:00 | 1 | 19.0 MiB / 17.1 MiB | 1785.8 MB/s | 10/3 | 1,378,331 | 736.2s / 1,420,552 msg/s |
| Dekaf | 2026-07-29T10:04:00.5457224+00:00 | 1 | 19.0 MiB / 18.4 MiB | 1785.8 MB/s | 10/4 | 1,427,088 | 763.2s / 1,446,398 msg/s |
| Dekaf | 2026-07-29T10:04:28.5537676+00:00 | 1 | 19.0 MiB / 13.2 MiB | 1785.8 MB/s | 10/4 | 1,480,017 | 791.2s / 1,681,827 msg/s |
| Dekaf | 2026-07-29T10:04:55.5622089+00:00 | 1 | 19.0 MiB / 19.0 MiB | 1785.8 MB/s | 10/5 | 1,535,577 | 818.3s / 1,648,799 msg/s |
| Dekaf | 2026-07-29T10:05:22.5753602+00:00 | 1 | 19.0 MiB / 16.3 MiB | 1785.8 MB/s | 10/6 | 1,584,369 | 845.3s / 1,539,493 msg/s |
| Dekaf | 2026-07-29T10:05:49.5888257+00:00 | 1 | 19.0 MiB / 16.9 MiB | 1785.8 MB/s | 10/6 | 1,629,871 | 872.3s / 1,616,704 msg/s |
| Dekaf | 2026-07-29T10:36:19.15991+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 407,984 msg/s |
| Dekaf | 2026-07-29T10:36:46.1676275+00:00 | 1 | 16.0 MiB / 15.5 MiB | 1618.2 MB/s | 0/0 | 46,792 | 27.0s / 1,467,389 msg/s |
| Dekaf | 2026-07-29T10:37:13.1738393+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1618.2 MB/s | 0/1 | 96,535 | 54.0s / 1,448,853 msg/s |
| Dekaf | 2026-07-29T10:37:40.1881962+00:00 | 1 | 16.0 MiB / 14.8 MiB | 1763.1 MB/s | 0/1 | 151,492 | 81.0s / 1,656,138 msg/s |
| Dekaf | 2026-07-29T10:38:08.1981918+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1763.1 MB/s | 0/1 | 212,907 | 109.0s / 1,522,868 msg/s |
| Dekaf | 2026-07-29T10:38:35.2092643+00:00 | 1 | 18.0 MiB / 17.6 MiB | 1763.1 MB/s | 1/1 | 267,655 | 136.1s / 1,537,493 msg/s |
| Dekaf | 2026-07-29T10:39:02.2186997+00:00 | 1 | 20.0 MiB / 19.7 MiB | 1763.1 MB/s | 1/1 | 317,907 | 163.1s / 1,457,935 msg/s |
| Dekaf | 2026-07-29T10:39:30.2308684+00:00 | 1 | 20.0 MiB / 20.0 MiB | 1763.1 MB/s | 2/1 | 364,339 | 191.1s / 1,593,532 msg/s |
| Dekaf | 2026-07-29T10:39:57.2450023+00:00 | 1 | 22.0 MiB / 21.1 MiB | 1763.1 MB/s | 3/1 | 412,779 | 218.1s / 1,611,464 msg/s |
| Dekaf | 2026-07-29T10:40:24.2585429+00:00 | 1 | 22.0 MiB / 21.7 MiB | 1763.1 MB/s | 3/1 | 454,474 | 245.1s / 1,553,805 msg/s |
| Dekaf | 2026-07-29T10:40:51.2683609+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1789.9 MB/s | 4/1 | 511,041 | 272.1s / 1,569,319 msg/s |
| Dekaf | 2026-07-29T10:41:19.2898664+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1789.9 MB/s | 5/1 | 564,855 | 300.1s / 1,611,571 msg/s |
| Dekaf | 2026-07-29T10:41:46.3027567+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1789.9 MB/s | 6/1 | 616,831 | 327.2s / 1,567,128 msg/s |
| Dekaf | 2026-07-29T10:42:13.3176402+00:00 | 1 | 14.0 MiB / 12.2 MiB | 1789.9 MB/s | 6/1 | 663,440 | 354.2s / 1,600,056 msg/s |
| Dekaf | 2026-07-29T10:42:40.3241452+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1789.9 MB/s | 6/2 | 712,548 | 381.2s / 1,502,558 msg/s |
| Dekaf | 2026-07-29T10:43:08.3396657+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1789.9 MB/s | 6/2 | 759,702 | 409.2s / 1,449,560 msg/s |
| Dekaf | 2026-07-29T10:43:35.3462076+00:00 | 1 | 15.0 MiB / 12.9 MiB | 1789.9 MB/s | 6/2 | 810,218 | 436.2s / 1,508,846 msg/s |
| Dekaf | 2026-07-29T10:44:02.3561549+00:00 | 1 | 14.0 MiB / 13.3 MiB | 1789.9 MB/s | 6/3 | 862,409 | 463.2s / 1,566,718 msg/s |
| Dekaf | 2026-07-29T10:44:29.362939+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1789.9 MB/s | 6/3 | 916,276 | 490.2s / 1,571,253 msg/s |
| Dekaf | 2026-07-29T10:44:57.3766991+00:00 | 1 | 14.0 MiB / 13.9 MiB | 1789.9 MB/s | 6/3 | 975,951 | 518.2s / 1,468,759 msg/s |
| Dekaf | 2026-07-29T10:45:24.3839042+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1789.9 MB/s | 6/3 | 1,020,636 | 545.2s / 1,544,914 msg/s |
| Dekaf | 2026-07-29T10:45:51.3902849+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1789.9 MB/s | 6/3 | 1,069,966 | 572.2s / 1,542,485 msg/s |
| Dekaf | 2026-07-29T10:46:18.3930105+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1789.9 MB/s | 6/4 | 1,120,912 | 599.2s / 1,510,997 msg/s |
| Dekaf | 2026-07-29T10:46:46.3973946+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1789.9 MB/s | 6/4 | 1,172,773 | 627.3s / 1,628,369 msg/s |
| Dekaf | 2026-07-29T10:47:13.4045305+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1789.9 MB/s | 6/4 | 1,232,247 | 654.3s / 1,588,988 msg/s |
| Dekaf | 2026-07-29T10:47:40.4164452+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1789.9 MB/s | 6/4 | 1,290,762 | 681.3s / 1,605,794 msg/s |
| Dekaf | 2026-07-29T10:48:07.4287292+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1789.9 MB/s | 6/4 | 1,346,000 | 708.3s / 1,607,098 msg/s |
| Dekaf | 2026-07-29T10:48:35.4373543+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1789.9 MB/s | 6/4 | 1,407,363 | 736.3s / 1,343,704 msg/s |
| Dekaf | 2026-07-29T10:49:02.4420723+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1789.9 MB/s | 6/4 | 1,465,345 | 763.3s / 1,591,868 msg/s |
| Dekaf | 2026-07-29T10:49:29.4488727+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1789.9 MB/s | 6/4 | 1,522,000 | 790.3s / 1,519,397 msg/s |
| Dekaf | 2026-07-29T10:49:57.4573574+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1789.9 MB/s | 6/4 | 1,588,407 | 818.3s / 1,505,814 msg/s |
| Dekaf | 2026-07-29T10:50:24.4657497+00:00 | 1 | 14.0 MiB / 10.0 MiB | 1789.9 MB/s | 6/5 | 1,652,725 | 845.3s / 1,649,366 msg/s |
| Dekaf | 2026-07-29T10:50:51.4782627+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1789.9 MB/s | 6/5 | 1,713,593 | 872.3s / 1,621,948 msg/s |
| Dekaf | 2026-07-29T10:51:18.491577+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1795.4 MB/s | 6/5 | 1,775,925 | 899.3s / 1,595,446 msg/s |
| Dekaf (3conn) | 2026-07-29T10:51:47.1496287+00:00 | 1 | 16.0 MiB / 2.2 MiB | 1771.0 MB/s | 0/0 | 1,955 | 27.0s / 1,411,322 msg/s |
| Dekaf (3conn) | 2026-07-29T10:52:14.1686433+00:00 | 1 | 14.0 MiB / 5.7 MiB | 1869.1 MB/s | 1/0 | 4,689 | 54.0s / 1,530,331 msg/s |
| Dekaf (3conn) | 2026-07-29T10:52:41.1864262+00:00 | 1 | 14.0 MiB / 2.4 MiB | 2121.9 MB/s | 1/0 | 10,422 | 81.0s / 1,781,997 msg/s |
| Dekaf (3conn) | 2026-07-29T10:53:08.2011119+00:00 | 1 | 12.0 MiB / 7.9 MiB | 2121.9 MB/s | 2/0 | 17,709 | 108.1s / 1,839,042 msg/s |
| Dekaf (3conn) | 2026-07-29T10:53:36.2233817+00:00 | 1 | 12.0 MiB / 9.2 MiB | 2150.3 MB/s | 2/1 | 27,291 | 136.1s / 1,752,258 msg/s |
| Dekaf (3conn) | 2026-07-29T10:54:03.2343199+00:00 | 1 | 12.0 MiB / 10.2 MiB | 2150.3 MB/s | 2/1 | 35,574 | 163.1s / 1,743,808 msg/s |
| Dekaf (3conn) | 2026-07-29T10:54:30.2467268+00:00 | 1 | 12.0 MiB / 2.6 MiB | 2150.3 MB/s | 2/1 | 44,124 | 190.1s / 1,628,526 msg/s |
| Dekaf (3conn) | 2026-07-29T10:54:57.2684103+00:00 | 1 | 13.0 MiB / 3.9 MiB | 2150.3 MB/s | 3/1 | 51,942 | 217.1s / 1,707,820 msg/s |
| Dekaf (3conn) | 2026-07-29T10:55:25.2796447+00:00 | 1 | 13.0 MiB / 9.5 MiB | 2150.3 MB/s | 3/1 | 58,355 | 245.1s / 1,869,626 msg/s |
| Dekaf (3conn) | 2026-07-29T10:55:52.2957858+00:00 | 1 | 14.0 MiB / 6.9 MiB | 2364.8 MB/s | 4/1 | 64,708 | 272.1s / 2,022,536 msg/s |
| Dekaf (3conn) | 2026-07-29T10:56:19.3111495+00:00 | 1 | 15.0 MiB / 10.7 MiB | 2364.8 MB/s | 4/1 | 71,295 | 299.2s / 2,006,156 msg/s |
| Dekaf (3conn) | 2026-07-29T10:56:46.3278751+00:00 | 1 | 15.0 MiB / 8.4 MiB | 2364.8 MB/s | 5/1 | 77,136 | 326.2s / 2,227,900 msg/s |
| Dekaf (3conn) | 2026-07-29T10:57:14.3371002+00:00 | 1 | 16.0 MiB / 9.2 MiB | 2465.0 MB/s | 6/1 | 82,172 | 354.2s / 2,006,852 msg/s |
| Dekaf (3conn) | 2026-07-29T10:57:41.3508459+00:00 | 1 | 16.0 MiB / 12.5 MiB | 2480.0 MB/s | 6/1 | 87,081 | 381.2s / 2,146,433 msg/s |
| Dekaf (3conn) | 2026-07-29T10:58:08.3686349+00:00 | 1 | 16.0 MiB / 16.0 MiB | 2480.0 MB/s | 6/2 | 91,106 | 408.2s / 1,822,210 msg/s |
| Dekaf (3conn) | 2026-07-29T10:58:36.3948393+00:00 | 1 | 16.0 MiB / 16.0 MiB | 2480.0 MB/s | 6/2 | 95,249 | 436.3s / 1,606,749 msg/s |
| Dekaf (3conn) | 2026-07-29T10:59:03.4037075+00:00 | 1 | 14.0 MiB / 0.9 MiB | 2480.0 MB/s | 6/2 | 98,830 | 463.3s / 1,709,198 msg/s |
| Dekaf (3conn) | 2026-07-29T10:59:30.4121408+00:00 | 1 | 16.0 MiB / 1.1 MiB | 2480.0 MB/s | 6/3 | 101,918 | 490.3s / 1,510,515 msg/s |
| Dekaf (3conn) | 2026-07-29T10:59:57.424647+00:00 | 1 | 16.0 MiB / 7.7 MiB | 2480.0 MB/s | 6/3 | 105,118 | 517.3s / 1,686,069 msg/s |
| Dekaf (3conn) | 2026-07-29T11:00:25.4456506+00:00 | 1 | 16.0 MiB / 8.6 MiB | 2480.0 MB/s | 6/3 | 108,891 | 545.3s / 1,600,323 msg/s |
| Dekaf (3conn) | 2026-07-29T11:00:52.4602999+00:00 | 1 | 16.0 MiB / 5.2 MiB | 2480.0 MB/s | 6/3 | 112,108 | 572.3s / 1,678,144 msg/s |
| Dekaf (3conn) | 2026-07-29T11:01:19.4686484+00:00 | 1 | 18.0 MiB / 6.1 MiB | 2480.0 MB/s | 6/3 | 114,719 | 599.4s / 1,810,483 msg/s |
| Dekaf (3conn) | 2026-07-29T11:01:46.492126+00:00 | 1 | 16.0 MiB / 7.9 MiB | 2480.0 MB/s | 6/4 | 117,379 | 626.4s / 1,767,335 msg/s |
| Dekaf (3conn) | 2026-07-29T11:02:14.5167506+00:00 | 1 | 16.0 MiB / 4.6 MiB | 2480.0 MB/s | 6/5 | 120,603 | 654.4s / 1,426,757 msg/s |
| Dekaf (3conn) | 2026-07-29T11:02:41.5286192+00:00 | 1 | 16.0 MiB / 11.8 MiB | 2480.0 MB/s | 6/5 | 124,681 | 681.4s / 1,909,819 msg/s |
| Dekaf (3conn) | 2026-07-29T11:03:08.542502+00:00 | 1 | 16.0 MiB / 3.8 MiB | 2480.0 MB/s | 6/5 | 128,242 | 708.4s / 1,774,258 msg/s |
| Dekaf (3conn) | 2026-07-29T11:03:35.5576475+00:00 | 1 | 16.0 MiB / 10.4 MiB | 2480.0 MB/s | 6/5 | 131,621 | 735.4s / 1,662,480 msg/s |
| Dekaf (3conn) | 2026-07-29T11:04:03.5722472+00:00 | 1 | 16.0 MiB / 5.1 MiB | 2480.0 MB/s | 6/5 | 134,534 | 763.4s / 1,581,589 msg/s |
| Dekaf (3conn) | 2026-07-29T11:04:30.5799875+00:00 | 1 | 16.0 MiB / 9.5 MiB | 2480.0 MB/s | 6/5 | 136,717 | 790.5s / 1,855,213 msg/s |
| Dekaf (3conn) | 2026-07-29T11:04:57.5943031+00:00 | 1 | 16.0 MiB / 9.6 MiB | 2480.0 MB/s | 6/5 | 139,447 | 817.5s / 1,300,477 msg/s |
| Dekaf (3conn) | 2026-07-29T11:05:24.596912+00:00 | 1 | 16.0 MiB / 4.7 MiB | 2480.0 MB/s | 6/5 | 142,886 | 844.5s / 1,468,346 msg/s |
| Dekaf (3conn) | 2026-07-29T11:05:52.6041262+00:00 | 1 | 16.0 MiB / 4.1 MiB | 2480.0 MB/s | 6/5 | 148,180 | 872.5s / 1,018,025 msg/s |
| Dekaf (3conn) | 2026-07-29T11:06:19.6177634+00:00 | 1 | 18.0 MiB / 6.6 MiB | 2480.0 MB/s | 6/5 | 150,645 | 899.5s / 1,626,474 msg/s |
*2,600 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget, Idempotent)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-29T09:51:47.4183481+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 7.2 MiB |
| Dekaf | 2026-07-29T09:52:02.4312389+00:00 | 1 | capacity | succeeded | 15,013ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T09:52:32.4571421+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 11.9 MiB |
| Dekaf | 2026-07-29T09:52:47.4720843+00:00 | 1 | capacity | failed | 15,014ms | 14.0 MiB / 12.0 MiB |
| Dekaf | 2026-07-29T09:53:47.5352004+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 11.0 MiB |
| Dekaf | 2026-07-29T09:54:02.5462862+00:00 | 1 | capacity | failed | 15,011ms | 14.0 MiB / 13.7 MiB |
| Dekaf | 2026-07-29T09:56:02.634338+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T09:56:17.646793+00:00 | 1 | capacity | succeeded | 15,012ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T09:56:47.6698618+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T09:57:02.6843176+00:00 | 1 | capacity | failed | 15,014ms | 12.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-29T09:58:02.7327334+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.0 MiB |
| Dekaf | 2026-07-29T09:58:17.7445891+00:00 | 1 | capacity | succeeded | 15,011ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T09:58:47.7701886+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 11.9 MiB |
| Dekaf | 2026-07-29T09:59:02.7812391+00:00 | 1 | capacity | succeeded | 15,011ms | 14.0 MiB / 12.7 MiB |
| Dekaf | 2026-07-29T09:59:32.8026844+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 3.0 MiB |
| Dekaf | 2026-07-29T09:59:47.8116398+00:00 | 1 | capacity | succeeded | 15,009ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-29T10:00:17.8337805+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 13.3 MiB |
| Dekaf | 2026-07-29T10:00:32.8451107+00:00 | 1 | capacity | succeeded | 15,011ms | 16.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-29T10:01:02.8662343+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-29T10:01:17.8778788+00:00 | 1 | capacity | succeeded | 15,011ms | 18.0 MiB / 17.1 MiB |
| Dekaf | 2026-07-29T10:01:47.8985372+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 16.9 MiB |
| Dekaf | 2026-07-29T10:02:02.9105516+00:00 | 1 | capacity | succeeded | 15,011ms | 20.0 MiB / 19.1 MiB |
| Dekaf | 2026-07-29T10:02:32.9324836+00:00 | 1 | capacity | started | 0ms | 22.0 MiB / 19.6 MiB |
| Dekaf | 2026-07-29T10:02:47.9423856+00:00 | 1 | capacity | succeeded | 15,010ms | 22.0 MiB / 15.2 MiB |
| Dekaf | 2026-07-29T10:03:17.9631761+00:00 | 1 | capacity | started | 0ms | 19.0 MiB / 21.6 MiB |
| Dekaf | 2026-07-29T10:03:32.988134+00:00 | 1 | capacity | succeeded | 15,025ms | 19.0 MiB / 18.1 MiB |
| Dekaf | 2026-07-29T10:03:35.9928259+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 18.1 MiB |
| Dekaf | 2026-07-29T10:03:51.0085584+00:00 | 1 | capacity | failed | 15,015ms | 19.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-29T10:04:21.0312134+00:00 | 1 | capacity | started | 0ms | 21.0 MiB / 18.1 MiB |
| Dekaf | 2026-07-29T10:04:36.0436652+00:00 | 1 | capacity | failed | 15,012ms | 19.0 MiB / 12.2 MiB |
| Dekaf | 2026-07-29T10:05:06.0785394+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 18.1 MiB |
| Dekaf | 2026-07-29T10:05:21.0898847+00:00 | 1 | capacity | failed | 15,011ms | 19.0 MiB / 15.6 MiB |
| Dekaf | 2026-07-29T10:36:49.2651729+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.6 MiB |
| Dekaf | 2026-07-29T10:37:04.2773206+00:00 | 1 | capacity | failed | 15,012ms | 16.0 MiB / 13.3 MiB |
| Dekaf | 2026-07-29T10:38:04.3211803+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-29T10:38:19.3325584+00:00 | 1 | capacity | succeeded | 15,010ms | 18.0 MiB / 18.0 MiB |
| Dekaf | 2026-07-29T10:38:49.3569872+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 17.1 MiB |
| Dekaf | 2026-07-29T10:39:04.3718363+00:00 | 1 | capacity | succeeded | 15,014ms | 20.0 MiB / 19.1 MiB |
| Dekaf | 2026-07-29T10:39:34.3986775+00:00 | 1 | capacity | started | 0ms | 22.0 MiB / 19.1 MiB |
| Dekaf | 2026-07-29T10:39:49.4109144+00:00 | 1 | capacity | succeeded | 15,012ms | 22.0 MiB / 20.8 MiB |
| Dekaf | 2026-07-29T10:40:19.4452604+00:00 | 1 | capacity | started | 0ms | 19.0 MiB / 20.9 MiB |
| Dekaf | 2026-07-29T10:40:34.4568567+00:00 | 1 | capacity | succeeded | 15,011ms | 19.0 MiB / 18.1 MiB |
| Dekaf | 2026-07-29T10:40:37.4591048+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 18.1 MiB |
| Dekaf | 2026-07-29T10:40:52.4701756+00:00 | 1 | capacity | succeeded | 15,011ms | 16.0 MiB / 10.6 MiB |
| Dekaf | 2026-07-29T10:41:22.515587+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-29T10:41:37.5251284+00:00 | 1 | capacity | succeeded | 15,009ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T10:42:07.5504819+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.2 MiB |
| Dekaf | 2026-07-29T10:42:22.5612546+00:00 | 1 | capacity | failed | 15,010ms | 14.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T10:43:22.6109918+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 12.9 MiB |
| Dekaf | 2026-07-29T10:43:37.622407+00:00 | 1 | capacity | failed | 15,011ms | 14.0 MiB / 13.6 MiB |
| Dekaf | 2026-07-29T10:45:37.7096911+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T10:45:52.7229063+00:00 | 1 | capacity | failed | 15,013ms | 14.0 MiB / 10.7 MiB |
| Dekaf | 2026-07-29T10:49:52.9048281+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.5 MiB |
| Dekaf | 2026-07-29T10:50:07.915914+00:00 | 1 | capacity | failed | 15,011ms | 14.0 MiB / 14.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:51:50.25882+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 10.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:52:05.2848831+00:00 | 1 | capacity | succeeded | 15,026ms | 14.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-29T10:52:35.3382843+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 3.3 MiB |
| Dekaf (3conn) | 2026-07-29T10:52:50.3754536+00:00 | 1 | capacity | succeeded | 15,037ms | 12.0 MiB / 9.7 MiB |
| Dekaf (3conn) | 2026-07-29T10:53:20.4290659+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-07-29T10:53:35.4505109+00:00 | 1 | capacity | failed | 15,021ms | 12.0 MiB / 5.3 MiB |
| Dekaf (3conn) | 2026-07-29T10:54:35.5475467+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 4.8 MiB |
| Dekaf (3conn) | 2026-07-29T10:54:50.5729829+00:00 | 1 | capacity | succeeded | 15,025ms | 13.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-29T10:55:20.6226588+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 9.6 MiB |
| Dekaf (3conn) | 2026-07-29T10:55:35.6502952+00:00 | 1 | capacity | succeeded | 15,027ms | 14.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-07-29T10:56:05.6873719+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-07-29T10:56:20.7165642+00:00 | 1 | capacity | succeeded | 15,029ms | 15.0 MiB / 3.9 MiB |
| Dekaf (3conn) | 2026-07-29T10:56:50.7653294+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 3.3 MiB |
| Dekaf (3conn) | 2026-07-29T10:57:05.7948118+00:00 | 1 | capacity | succeeded | 15,029ms | 16.0 MiB / 5.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:57:35.8387173+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 6.7 MiB |
| Dekaf (3conn) | 2026-07-29T10:57:51.0162424+00:00 | 1 | capacity | failed | 15,177ms | 16.0 MiB / 8.2 MiB |
| Dekaf (3conn) | 2026-07-29T10:58:51.1479714+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:59:06.1858646+00:00 | 1 | capacity | failed | 15,037ms | 16.0 MiB / 4.9 MiB |
| Dekaf (3conn) | 2026-07-29T11:01:06.9662007+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 8.0 MiB |
| Dekaf (3conn) | 2026-07-29T11:01:21.9979927+00:00 | 1 | capacity | failed | 15,031ms | 16.0 MiB / 16.1 MiB |
| Dekaf (3conn) | 2026-07-29T11:01:52.2614143+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 2.8 MiB |
| Dekaf (3conn) | 2026-07-29T11:02:07.291714+00:00 | 1 | capacity | failed | 15,030ms | 16.0 MiB / 6.9 MiB |
| Dekaf (3conn) | 2026-07-29T11:06:07.6791683+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 4.9 MiB |

## Producer Admission Block Durations - Producer (Fire-and-Forget, Idempotent)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 2,500 |
| Dekaf | 1 | 0.002–0.004ms | 2,784 |
| Dekaf | 1 | 0.004–0.008ms | 7,927 |
| Dekaf | 1 | 0.008–0.016ms | 39,405 |
| Dekaf | 1 | 0.016–0.032ms | 58,861 |
| Dekaf | 1 | 0.032–0.064ms | 57,871 |
| Dekaf | 1 | 0.064–0.128ms | 107,220 |
| Dekaf | 1 | 0.128–0.256ms | 251,637 |
| Dekaf | 1 | 0.256–0.512ms | 243,266 |
| Dekaf | 1 | 0.512–1.024ms | 34,591 |
| Dekaf | 1 | 1.024–2.048ms | 4,373 |
| Dekaf | 1 | 2.048–4.096ms | 3,888 |
| Dekaf | 1 | 4.096–8.192ms | 750 |
| Dekaf | 1 | 8.192–16.384ms | 42 |
| Dekaf | 1 | 32.768–65.536ms | 2 |
| Dekaf | 1 | 0.001–0.002ms | 2,476 |
| Dekaf | 1 | 0.002–0.004ms | 2,729 |
| Dekaf | 1 | 0.004–0.008ms | 9,084 |
| Dekaf | 1 | 0.008–0.016ms | 46,268 |
| Dekaf | 1 | 0.016–0.032ms | 66,193 |
| Dekaf | 1 | 0.032–0.064ms | 59,961 |
| Dekaf | 1 | 0.064–0.128ms | 108,828 |
| Dekaf | 1 | 0.128–0.256ms | 257,266 |
| Dekaf | 1 | 0.256–0.512ms | 256,379 |
| Dekaf | 1 | 0.512–1.024ms | 42,769 |
| Dekaf | 1 | 1.024–2.048ms | 5,117 |
| Dekaf | 1 | 2.048–4.096ms | 3,825 |
| Dekaf | 1 | 4.096–8.192ms | 722 |
| Dekaf | 1 | 8.192–16.384ms | 50 |
| Dekaf | 1 | 16.384–32.768ms | 4 |
| Dekaf (3conn) | 1 | 0.001–0.002ms | 46 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 67 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 176 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 514 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 1,678 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 4,664 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 4,265 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 7,547 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 9,385 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 7,836 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 4,766 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 1,608 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 353 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 32 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 1 |
| Dekaf (3conn) | 1 | 131.072–262.144ms | 17 |

## Delivery Latency Outliers - Producer (Fire-and-Forget, Idempotent)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 17,321,000 | 2026-07-29T10:06:32.8773365+00:00 | 128.6ms | GC pause | - | - | 16.0s / 632,387 msg/s | Gen2 +0 / pause +120.0ms |
| Confluent | 17,322,000 | 2026-07-29T10:06:32.8780013+00:00 | 102.7ms | GC pause | - | - | 16.0s / 632,387 msg/s | Gen2 +0 / pause +120.0ms |
| Confluent | 17,325,000 | 2026-07-29T10:06:32.8811543+00:00 | 116.4ms | GC pause | - | - | 16.0s / 632,387 msg/s | Gen2 +0 / pause +120.0ms |
| Confluent | 17,337,000 | 2026-07-29T10:06:32.9029032+00:00 | 122.9ms | GC pause | - | - | 16.0s / 632,387 msg/s | Gen2 +0 / pause +120.0ms |
| Confluent | 17,338,000 | 2026-07-29T10:06:32.9045666+00:00 | 121.4ms | GC pause | - | - | 16.0s / 632,387 msg/s | Gen2 +0 / pause +120.0ms |
| Confluent | 53,151,000 | 2026-07-29T10:07:40.269783+00:00 | 147.1ms | GC pause | - | - | 83.1s / 735,744 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 53,162,000 | 2026-07-29T10:07:40.2882588+00:00 | 132.0ms | GC pause | - | - | 83.1s / 735,744 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 53,165,000 | 2026-07-29T10:07:40.2954221+00:00 | 121.2ms | GC pause | - | - | 83.1s / 735,744 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 53,198,000 | 2026-07-29T10:07:40.3434289+00:00 | 154.5ms | GC pause | - | - | 83.1s / 735,744 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 53,202,000 | 2026-07-29T10:07:40.3521891+00:00 | 129.9ms | GC pause | - | - | 83.1s / 735,744 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 53,230,000 | 2026-07-29T10:07:40.3833864+00:00 | 146.8ms | GC pause | - | - | 83.1s / 735,744 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 53,272,000 | 2026-07-29T10:07:40.4274058+00:00 | 163.0ms | GC pause | - | - | 83.1s / 735,744 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 53,275,000 | 2026-07-29T10:07:40.4340373+00:00 | 152.6ms | GC pause | - | - | 83.1s / 735,744 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 53,283,000 | 2026-07-29T10:07:40.4531013+00:00 | 153.7ms | GC pause | - | - | 83.1s / 735,744 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 53,284,000 | 2026-07-29T10:07:40.4560645+00:00 | 138.4ms | GC pause | - | - | 83.1s / 735,744 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 53,302,000 | 2026-07-29T10:07:40.4900599+00:00 | 144.1ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +209.6ms |
| Confluent | 53,304,000 | 2026-07-29T10:07:40.492411+00:00 | 134.5ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +209.6ms |
| Confluent | 53,310,000 | 2026-07-29T10:07:40.5001539+00:00 | 134.6ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +209.6ms |
| Confluent | 53,322,000 | 2026-07-29T10:07:40.5127783+00:00 | 152.9ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +209.6ms |
| Confluent | 53,323,000 | 2026-07-29T10:07:40.5143018+00:00 | 144.5ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +209.6ms |
| Confluent | 53,330,000 | 2026-07-29T10:07:40.5229026+00:00 | 143.0ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +209.6ms |
| Confluent | 53,341,000 | 2026-07-29T10:07:40.5514001+00:00 | 157.3ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +209.6ms |
| Confluent | 53,353,000 | 2026-07-29T10:07:40.5742065+00:00 | 121.5ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +209.6ms |
| Confluent | 53,358,000 | 2026-07-29T10:07:40.5825002+00:00 | 147.3ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +209.6ms |
| Confluent | 53,374,000 | 2026-07-29T10:07:40.615516+00:00 | 103.3ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,401,000 | 2026-07-29T10:07:40.6874728+00:00 | 146.2ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,407,000 | 2026-07-29T10:07:40.7036196+00:00 | 143.7ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,408,000 | 2026-07-29T10:07:40.7055585+00:00 | 141.9ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,418,000 | 2026-07-29T10:07:40.7263434+00:00 | 145.6ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,435,000 | 2026-07-29T10:07:40.7572546+00:00 | 100.5ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,447,000 | 2026-07-29T10:07:40.7730071+00:00 | 159.8ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,472,000 | 2026-07-29T10:07:40.8034873+00:00 | 153.9ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,477,000 | 2026-07-29T10:07:40.8089333+00:00 | 204.0ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,486,000 | 2026-07-29T10:07:40.8223295+00:00 | 159.6ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,488,000 | 2026-07-29T10:07:40.825144+00:00 | 205.2ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,496,000 | 2026-07-29T10:07:40.8393936+00:00 | 172.8ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,503,000 | 2026-07-29T10:07:40.8482066+00:00 | 168.3ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,508,000 | 2026-07-29T10:07:40.8524666+00:00 | 218.0ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,528,000 | 2026-07-29T10:07:40.8678536+00:00 | 230.5ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,530,000 | 2026-07-29T10:07:40.8693138+00:00 | 196.4ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,533,000 | 2026-07-29T10:07:40.8717096+00:00 | 194.1ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,540,000 | 2026-07-29T10:07:40.8789761+00:00 | 195.4ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,544,000 | 2026-07-29T10:07:40.8820137+00:00 | 193.8ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,552,000 | 2026-07-29T10:07:40.8884623+00:00 | 213.1ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,554,000 | 2026-07-29T10:07:40.890235+00:00 | 205.8ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,566,000 | 2026-07-29T10:07:40.8999335+00:00 | 217.0ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,567,000 | 2026-07-29T10:07:40.9006479+00:00 | 264.2ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,574,000 | 2026-07-29T10:07:40.9051599+00:00 | 221.3ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,580,000 | 2026-07-29T10:07:40.9088676+00:00 | 232.8ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,582,000 | 2026-07-29T10:07:40.9141227+00:00 | 234.7ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,602,000 | 2026-07-29T10:07:40.945867+00:00 | 232.5ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,604,000 | 2026-07-29T10:07:40.9485646+00:00 | 221.5ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,606,000 | 2026-07-29T10:07:40.9540448+00:00 | 225.9ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,608,000 | 2026-07-29T10:07:40.956776+00:00 | 274.2ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,616,000 | 2026-07-29T10:07:40.9702593+00:00 | 228.5ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,620,000 | 2026-07-29T10:07:40.9755201+00:00 | 231.4ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,623,000 | 2026-07-29T10:07:40.9792319+00:00 | 234.3ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,624,000 | 2026-07-29T10:07:40.9810064+00:00 | 226.8ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,635,000 | 2026-07-29T10:07:40.9938932+00:00 | 229.4ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,648,000 | 2026-07-29T10:07:41.0208277+00:00 | 250.5ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,663,000 | 2026-07-29T10:07:41.0392329+00:00 | 222.5ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,664,000 | 2026-07-29T10:07:41.0400681+00:00 | 213.8ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,667,000 | 2026-07-29T10:07:41.0469162+00:00 | 254.1ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,671,000 | 2026-07-29T10:07:41.0514469+00:00 | 258.7ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,675,000 | 2026-07-29T10:07:41.0568034+00:00 | 220.1ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,684,000 | 2026-07-29T10:07:41.0737155+00:00 | 217.7ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,694,000 | 2026-07-29T10:07:41.0983924+00:00 | 197.9ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,724,000 | 2026-07-29T10:07:41.1465793+00:00 | 197.1ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,730,000 | 2026-07-29T10:07:41.1591784+00:00 | 206.3ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,733,000 | 2026-07-29T10:07:41.1685678+00:00 | 200.2ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,735,000 | 2026-07-29T10:07:41.1745841+00:00 | 195.0ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,738,000 | 2026-07-29T10:07:41.18137+00:00 | 230.4ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,741,000 | 2026-07-29T10:07:41.1861894+00:00 | 225.8ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,762,000 | 2026-07-29T10:07:41.2377555+00:00 | 169.2ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,763,000 | 2026-07-29T10:07:41.2390129+00:00 | 161.5ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,769,000 | 2026-07-29T10:07:41.2494294+00:00 | 161.7ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,781,000 | 2026-07-29T10:07:41.2666793+00:00 | 205.0ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,786,000 | 2026-07-29T10:07:41.2734661+00:00 | 153.5ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,789,000 | 2026-07-29T10:07:41.276978+00:00 | 158.4ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,792,000 | 2026-07-29T10:07:41.283706+00:00 | 167.1ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,794,000 | 2026-07-29T10:07:41.2859061+00:00 | 149.3ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,796,000 | 2026-07-29T10:07:41.2919234+00:00 | 151.8ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,833,000 | 2026-07-29T10:07:41.3924847+00:00 | 112.8ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,836,000 | 2026-07-29T10:07:41.3967575+00:00 | 114.9ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,838,000 | 2026-07-29T10:07:41.3991194+00:00 | 177.4ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,839,000 | 2026-07-29T10:07:41.4012267+00:00 | 111.1ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,864,000 | 2026-07-29T10:07:41.4374215+00:00 | 108.7ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,870,000 | 2026-07-29T10:07:41.4478754+00:00 | 126.5ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,873,000 | 2026-07-29T10:07:41.4536384+00:00 | 127.2ms | GC pause | - | - | 84.1s / 584,106 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 53,888,000 | 2026-07-29T10:07:41.49414+00:00 | 186.9ms | GC pause | - | - | 85.1s / 625,839 msg/s | Gen2 +0 / pause +204.7ms |
| Confluent | 53,902,000 | 2026-07-29T10:07:41.5298752+00:00 | 122.5ms | GC pause | - | - | 85.1s / 625,839 msg/s | Gen2 +0 / pause +204.7ms |
| Confluent | 53,906,000 | 2026-07-29T10:07:41.5386711+00:00 | 106.1ms | GC pause | - | - | 85.1s / 625,839 msg/s | Gen2 +0 / pause +204.7ms |
| Confluent | 53,924,000 | 2026-07-29T10:07:41.5669429+00:00 | 101.3ms | GC pause | - | - | 85.1s / 625,839 msg/s | Gen2 +0 / pause +204.7ms |
| Confluent | 53,933,000 | 2026-07-29T10:07:41.5846824+00:00 | 112.1ms | GC pause | - | - | 85.1s / 625,839 msg/s | Gen2 +0 / pause +204.7ms |
| Confluent | 53,937,000 | 2026-07-29T10:07:41.5895498+00:00 | 169.9ms | GC pause | - | - | 85.1s / 625,839 msg/s | Gen2 +0 / pause +204.7ms |
| Confluent | 53,940,000 | 2026-07-29T10:07:41.5925743+00:00 | 114.5ms | GC pause | - | - | 85.1s / 625,839 msg/s | Gen2 +0 / pause +204.7ms |
| Confluent | 53,950,000 | 2026-07-29T10:07:41.6045387+00:00 | 121.7ms | GC pause | - | - | 85.1s / 625,839 msg/s | Gen2 +0 / pause +204.7ms |
| Confluent | 53,954,000 | 2026-07-29T10:07:41.6091164+00:00 | 108.3ms | GC pause | - | - | 85.1s / 625,839 msg/s | Gen2 +0 / pause +204.7ms |
| Confluent | 53,957,000 | 2026-07-29T10:07:41.6157272+00:00 | 182.7ms | GC pause | - | - | 85.1s / 625,839 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 53,965,000 | 2026-07-29T10:07:41.6326871+00:00 | 114.8ms | GC pause | - | - | 85.1s / 625,839 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 53,970,000 | 2026-07-29T10:07:41.6420475+00:00 | 111.2ms | GC pause | - | - | 85.1s / 625,839 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 53,977,000 | 2026-07-29T10:07:41.6551985+00:00 | 170.2ms | GC pause | - | - | 85.1s / 625,839 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 53,991,000 | 2026-07-29T10:07:41.6856194+00:00 | 159.5ms | GC pause | - | - | 85.1s / 625,839 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 54,047,000 | 2026-07-29T10:07:41.7961491+00:00 | 115.9ms | GC pause | - | - | 85.1s / 625,839 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 55,511,000 | 2026-07-29T10:07:44.0826674+00:00 | 106.3ms | GC pause | - | - | 87.1s / 513,252 msg/s | Gen2 +0 / pause +176.0ms |
| Confluent | 61,266,000 | 2026-07-29T10:07:55.5347596+00:00 | 112.0ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +351.0ms |
| Confluent | 61,287,000 | 2026-07-29T10:07:55.573781+00:00 | 125.0ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +351.0ms |
| Confluent | 61,291,000 | 2026-07-29T10:07:55.5807921+00:00 | 118.6ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +351.0ms |
| Confluent | 61,292,000 | 2026-07-29T10:07:55.5824217+00:00 | 112.6ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +351.0ms |
| Confluent | 61,308,000 | 2026-07-29T10:07:55.6095525+00:00 | 131.4ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +351.0ms |
| Confluent | 61,317,000 | 2026-07-29T10:07:55.6257542+00:00 | 141.0ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +351.0ms |
| Confluent | 61,321,000 | 2026-07-29T10:07:55.6332351+00:00 | 135.9ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 61,343,000 | 2026-07-29T10:07:55.6661769+00:00 | 127.6ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 61,348,000 | 2026-07-29T10:07:55.6752848+00:00 | 141.9ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 61,355,000 | 2026-07-29T10:07:55.6908736+00:00 | 133.8ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 61,382,000 | 2026-07-29T10:07:55.7664268+00:00 | 105.8ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 61,387,000 | 2026-07-29T10:07:55.7802419+00:00 | 133.7ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 61,402,000 | 2026-07-29T10:07:55.8106534+00:00 | 116.9ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 61,413,000 | 2026-07-29T10:07:55.8317458+00:00 | 123.7ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 61,427,000 | 2026-07-29T10:07:55.8566257+00:00 | 133.2ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 61,441,000 | 2026-07-29T10:07:55.9002946+00:00 | 115.9ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 61,468,000 | 2026-07-29T10:07:55.9736626+00:00 | 112.2ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 61,491,000 | 2026-07-29T10:07:56.0154832+00:00 | 121.9ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 61,506,000 | 2026-07-29T10:07:56.055877+00:00 | 105.6ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 61,511,000 | 2026-07-29T10:07:56.0655031+00:00 | 118.7ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 61,517,000 | 2026-07-29T10:07:56.0813835+00:00 | 112.6ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 61,528,000 | 2026-07-29T10:07:56.1071054+00:00 | 110.8ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 61,558,000 | 2026-07-29T10:07:56.1826888+00:00 | 107.4ms | GC pause | - | - | 99.1s / 430,392 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 69,041,000 | 2026-07-29T10:08:08.9755993+00:00 | 116.8ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,046,000 | 2026-07-29T10:08:08.9830847+00:00 | 108.0ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,053,000 | 2026-07-29T10:08:08.9983491+00:00 | 115.1ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,063,000 | 2026-07-29T10:08:09.0328576+00:00 | 108.9ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,076,000 | 2026-07-29T10:08:09.0609025+00:00 | 100.2ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,099,000 | 2026-07-29T10:08:09.1094503+00:00 | 125.4ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,101,000 | 2026-07-29T10:08:09.1119569+00:00 | 144.2ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,113,000 | 2026-07-29T10:08:09.1343736+00:00 | 145.8ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,114,000 | 2026-07-29T10:08:09.1353566+00:00 | 146.4ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,115,000 | 2026-07-29T10:08:09.1362875+00:00 | 147.3ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,123,000 | 2026-07-29T10:08:09.1634492+00:00 | 145.4ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,137,000 | 2026-07-29T10:08:09.1903558+00:00 | 159.7ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,138,000 | 2026-07-29T10:08:09.1914856+00:00 | 158.7ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,150,000 | 2026-07-29T10:08:09.2246986+00:00 | 132.6ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,164,000 | 2026-07-29T10:08:09.2547755+00:00 | 131.6ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,167,000 | 2026-07-29T10:08:09.2613158+00:00 | 139.9ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,168,000 | 2026-07-29T10:08:09.2634621+00:00 | 137.8ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,176,000 | 2026-07-29T10:08:09.2909242+00:00 | 110.0ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,184,000 | 2026-07-29T10:08:09.3077794+00:00 | 107.0ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,200,000 | 2026-07-29T10:08:09.336506+00:00 | 100.3ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,267,000 | 2026-07-29T10:08:09.4904901+00:00 | 101.9ms | GC pause | - | - | 112.1s / 510,639 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 69,287,000 | 2026-07-29T10:08:09.5387755+00:00 | 107.4ms | GC pause | - | - | 113.1s / 407,287 msg/s | Gen2 +0 / pause +278.7ms |
| Confluent | 69,340,000 | 2026-07-29T10:08:09.6594632+00:00 | 100.4ms | GC pause | - | - | 113.1s / 407,287 msg/s | Gen2 +0 / pause +184.5ms |
| Confluent | 69,341,000 | 2026-07-29T10:08:09.6623012+00:00 | 122.0ms | GC pause | - | - | 113.1s / 407,287 msg/s | Gen2 +0 / pause +184.5ms |
| Confluent | 69,347,000 | 2026-07-29T10:08:09.6730363+00:00 | 128.3ms | GC pause | - | - | 113.1s / 407,287 msg/s | Gen2 +0 / pause +184.5ms |
| Confluent | 69,388,000 | 2026-07-29T10:08:09.780366+00:00 | 131.1ms | GC pause | - | - | 113.1s / 407,287 msg/s | Gen2 +0 / pause +184.5ms |
| Confluent | 69,401,000 | 2026-07-29T10:08:09.8093641+00:00 | 144.3ms | GC pause | - | - | 113.1s / 407,287 msg/s | Gen2 +0 / pause +184.5ms |
| Confluent | 69,410,000 | 2026-07-29T10:08:09.8330607+00:00 | 122.0ms | GC pause | - | - | 113.1s / 407,287 msg/s | Gen2 +0 / pause +184.5ms |
| Confluent | 69,417,000 | 2026-07-29T10:08:09.8545181+00:00 | 133.5ms | GC pause | - | - | 113.1s / 407,287 msg/s | Gen2 +0 / pause +184.5ms |
| Confluent | 69,427,000 | 2026-07-29T10:08:09.8665635+00:00 | 163.3ms | GC pause | - | - | 113.1s / 407,287 msg/s | Gen2 +0 / pause +184.5ms |
| Confluent | 69,447,000 | 2026-07-29T10:08:09.9095272+00:00 | 146.9ms | GC pause | - | - | 113.1s / 407,287 msg/s | Gen2 +0 / pause +184.5ms |
| Confluent | 69,460,000 | 2026-07-29T10:08:09.9299798+00:00 | 131.7ms | GC pause | - | - | 113.1s / 407,287 msg/s | Gen2 +0 / pause +184.5ms |
| Confluent | 69,493,000 | 2026-07-29T10:08:10.037903+00:00 | 101.8ms | GC pause | - | - | 113.1s / 407,287 msg/s | Gen2 +0 / pause +184.5ms |
| Confluent | 71,328,000 | 2026-07-29T10:08:14.1323066+00:00 | 101.4ms | GC pause | - | - | 117.1s / 489,302 msg/s | Gen2 +0 / pause +151.1ms |
| Confluent | 71,747,000 | 2026-07-29T10:08:15.021094+00:00 | 117.8ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,751,000 | 2026-07-29T10:08:15.0272793+00:00 | 112.1ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,759,000 | 2026-07-29T10:08:15.0371132+00:00 | 117.0ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,764,000 | 2026-07-29T10:08:15.0470325+00:00 | 140.7ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,786,000 | 2026-07-29T10:08:15.0861641+00:00 | 130.0ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,793,000 | 2026-07-29T10:08:15.1013945+00:00 | 137.4ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,806,000 | 2026-07-29T10:08:15.1294234+00:00 | 132.5ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,821,000 | 2026-07-29T10:08:15.1508832+00:00 | 158.8ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,829,000 | 2026-07-29T10:08:15.1614143+00:00 | 145.6ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,832,000 | 2026-07-29T10:08:15.1661852+00:00 | 148.3ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,834,000 | 2026-07-29T10:08:15.1689671+00:00 | 168.7ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,846,000 | 2026-07-29T10:08:15.1893718+00:00 | 149.4ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,852,000 | 2026-07-29T10:08:15.2002823+00:00 | 157.3ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,861,000 | 2026-07-29T10:08:15.2140861+00:00 | 186.3ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,868,000 | 2026-07-29T10:08:15.2271474+00:00 | 184.7ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,869,000 | 2026-07-29T10:08:15.2282539+00:00 | 157.8ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,884,000 | 2026-07-29T10:08:15.2632849+00:00 | 171.1ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,887,000 | 2026-07-29T10:08:15.2716491+00:00 | 182.3ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,899,000 | 2026-07-29T10:08:15.2958644+00:00 | 154.1ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,910,000 | 2026-07-29T10:08:15.3143331+00:00 | 183.5ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,939,000 | 2026-07-29T10:08:15.3673467+00:00 | 162.6ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,942,000 | 2026-07-29T10:08:15.3761208+00:00 | 161.8ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,948,000 | 2026-07-29T10:08:15.3869745+00:00 | 191.2ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,952,000 | 2026-07-29T10:08:15.3963236+00:00 | 165.6ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,957,000 | 2026-07-29T10:08:15.4052957+00:00 | 187.3ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,962,000 | 2026-07-29T10:08:15.415309+00:00 | 167.0ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,964,000 | 2026-07-29T10:08:15.4191986+00:00 | 166.7ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,965,000 | 2026-07-29T10:08:15.4215132+00:00 | 153.0ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,969,000 | 2026-07-29T10:08:15.430356+00:00 | 161.7ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,971,000 | 2026-07-29T10:08:15.4369127+00:00 | 194.2ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,974,000 | 2026-07-29T10:08:15.4461969+00:00 | 165.3ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,978,000 | 2026-07-29T10:08:15.455085+00:00 | 187.7ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,984,000 | 2026-07-29T10:08:15.4753875+00:00 | 165.7ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,989,000 | 2026-07-29T10:08:15.4868938+00:00 | 154.6ms | GC pause | - | - | 118.1s / 506,960 msg/s | Gen2 +0 / pause +153.5ms |
| Confluent | 71,996,000 | 2026-07-29T10:08:15.5091965+00:00 | 143.6ms | GC pause | - | - | 119.1s / 414,567 msg/s | Gen2 +0 / pause +304.7ms |
| Confluent | 71,997,000 | 2026-07-29T10:08:15.5108118+00:00 | 167.4ms | GC pause | - | - | 119.1s / 414,567 msg/s | Gen2 +0 / pause +304.7ms |
| Confluent | 72,000,000 | 2026-07-29T10:08:15.5193302+00:00 | 142.6ms | GC pause | - | - | 119.1s / 414,567 msg/s | Gen2 +0 / pause +304.7ms |
| Confluent | 72,010,000 | 2026-07-29T10:08:15.5391753+00:00 | 147.2ms | GC pause | - | - | 119.1s / 414,567 msg/s | Gen2 +0 / pause +304.7ms |
| Confluent | 72,021,000 | 2026-07-29T10:08:15.5852099+00:00 | 146.3ms | GC pause | - | - | 119.1s / 414,567 msg/s | Gen2 +0 / pause +304.7ms |
| Confluent | 72,023,000 | 2026-07-29T10:08:15.5895744+00:00 | 120.9ms | GC pause | - | - | 119.1s / 414,567 msg/s | Gen2 +0 / pause +304.7ms |
| Confluent | 72,042,000 | 2026-07-29T10:08:15.6375701+00:00 | 110.9ms | GC pause | - | - | 119.1s / 414,567 msg/s | Gen2 +0 / pause +304.7ms |
| Confluent | 72,053,000 | 2026-07-29T10:08:15.6627315+00:00 | 110.1ms | GC pause | - | - | 119.1s / 414,567 msg/s | Gen2 +0 / pause +151.2ms |
| Confluent | 72,055,000 | 2026-07-29T10:08:15.6664856+00:00 | 108.8ms | GC pause | - | - | 119.1s / 414,567 msg/s | Gen2 +0 / pause +151.2ms |
| Confluent | 72,056,000 | 2026-07-29T10:08:15.6676801+00:00 | 107.7ms | GC pause | - | - | 119.1s / 414,567 msg/s | Gen2 +0 / pause +151.2ms |
| Confluent | 72,071,000 | 2026-07-29T10:08:15.7039416+00:00 | 131.2ms | GC pause | - | - | 119.1s / 414,567 msg/s | Gen2 +0 / pause +151.2ms |
| Confluent | 72,081,000 | 2026-07-29T10:08:15.7446875+00:00 | 130.5ms | GC pause | - | - | 119.1s / 414,567 msg/s | Gen2 +0 / pause +151.2ms |
| Confluent | 81,929,000 | 2026-07-29T10:08:30.7737287+00:00 | 101.4ms | GC pause | - | - | 134.1s / 561,437 msg/s | Gen2 +0 / pause +149.2ms |
| Confluent | 81,935,000 | 2026-07-29T10:08:30.7831961+00:00 | 113.2ms | GC pause | - | - | 134.1s / 561,437 msg/s | Gen2 +0 / pause +149.2ms |
| Confluent | 81,946,000 | 2026-07-29T10:08:30.7996472+00:00 | 109.7ms | GC pause | - | - | 134.1s / 561,437 msg/s | Gen2 +0 / pause +149.2ms |
| Confluent | 81,990,000 | 2026-07-29T10:08:30.905697+00:00 | 101.6ms | GC pause | - | - | 134.1s / 561,437 msg/s | Gen2 +0 / pause +149.2ms |
| Confluent | 82,018,000 | 2026-07-29T10:08:30.9401724+00:00 | 103.8ms | GC pause | - | - | 134.1s / 561,437 msg/s | Gen2 +0 / pause +149.2ms |
| Confluent | 82,020,000 | 2026-07-29T10:08:30.941961+00:00 | 105.8ms | GC pause | - | - | 134.1s / 561,437 msg/s | Gen2 +0 / pause +149.2ms |
| Confluent | 82,021,000 | 2026-07-29T10:08:30.9431838+00:00 | 100.8ms | GC pause | - | - | 134.1s / 561,437 msg/s | Gen2 +0 / pause +149.2ms |
| Confluent | 82,033,000 | 2026-07-29T10:08:30.9588832+00:00 | 110.9ms | GC pause | - | - | 134.1s / 561,437 msg/s | Gen2 +0 / pause +149.2ms |
| Confluent | 82,078,000 | 2026-07-29T10:08:31.039264+00:00 | 114.7ms | GC pause | - | - | 134.1s / 561,437 msg/s | Gen2 +0 / pause +149.2ms |
| Confluent | 82,082,000 | 2026-07-29T10:08:31.0492614+00:00 | 109.2ms | GC pause | - | - | 134.1s / 561,437 msg/s | Gen2 +0 / pause +149.2ms |
| Confluent | 82,087,000 | 2026-07-29T10:08:31.0607692+00:00 | 104.7ms | GC pause | - | - | 134.1s / 561,437 msg/s | Gen2 +0 / pause +149.2ms |
| Confluent | 82,092,000 | 2026-07-29T10:08:31.0717172+00:00 | 115.4ms | GC pause | - | - | 134.1s / 561,437 msg/s | Gen2 +0 / pause +149.2ms |
| Confluent | 82,158,000 | 2026-07-29T10:08:31.2098099+00:00 | 107.6ms | GC pause | - | - | 134.1s / 561,437 msg/s | Gen2 +0 / pause +149.2ms |
| Confluent | 103,271,000 | 2026-07-29T10:09:03.7204816+00:00 | 103.2ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,299,000 | 2026-07-29T10:09:03.7648165+00:00 | 100.3ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,315,000 | 2026-07-29T10:09:03.7817737+00:00 | 100.6ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,336,000 | 2026-07-29T10:09:03.8028362+00:00 | 121.2ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,340,000 | 2026-07-29T10:09:03.8065278+00:00 | 120.8ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,348,000 | 2026-07-29T10:09:03.8152649+00:00 | 138.5ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,353,000 | 2026-07-29T10:09:03.8204793+00:00 | 128.9ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,370,000 | 2026-07-29T10:09:03.8367746+00:00 | 151.9ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,372,000 | 2026-07-29T10:09:03.8386652+00:00 | 149.5ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,377,000 | 2026-07-29T10:09:03.8456313+00:00 | 173.8ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,381,000 | 2026-07-29T10:09:03.850313+00:00 | 180.4ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,382,000 | 2026-07-29T10:09:03.8516807+00:00 | 166.5ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,407,000 | 2026-07-29T10:09:03.8842872+00:00 | 185.0ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,410,000 | 2026-07-29T10:09:03.8881677+00:00 | 175.6ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,438,000 | 2026-07-29T10:09:03.942206+00:00 | 182.0ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,447,000 | 2026-07-29T10:09:03.9620159+00:00 | 173.4ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,461,000 | 2026-07-29T10:09:03.9986897+00:00 | 175.3ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,477,000 | 2026-07-29T10:09:04.059716+00:00 | 140.5ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,499,000 | 2026-07-29T10:09:04.1122478+00:00 | 105.0ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,548,000 | 2026-07-29T10:09:04.2137293+00:00 | 117.0ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 103,581,000 | 2026-07-29T10:09:04.2898082+00:00 | 100.4ms | GC pause | - | - | 167.1s / 560,596 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 106,590,000 | 2026-07-29T10:09:09.3722895+00:00 | 102.0ms | GC pause | - | - | 172.1s / 502,073 msg/s | Gen2 +0 / pause +136.7ms |
| Confluent | 110,806,000 | 2026-07-29T10:09:16.2888824+00:00 | 107.8ms | GC pause | - | - | 179.1s / 447,520 msg/s | Gen2 +0 / pause +176.1ms |
| Confluent | 110,818,000 | 2026-07-29T10:09:16.3042303+00:00 | 112.1ms | GC pause | - | - | 179.1s / 447,520 msg/s | Gen2 +0 / pause +176.1ms |
| Confluent | 110,820,000 | 2026-07-29T10:09:16.3076528+00:00 | 109.7ms | GC pause | - | - | 179.1s / 447,520 msg/s | Gen2 +0 / pause +176.1ms |
| Confluent | 110,828,000 | 2026-07-29T10:09:16.3248708+00:00 | 119.0ms | GC pause | - | - | 179.1s / 447,520 msg/s | Gen2 +0 / pause +176.1ms |
| Confluent | 110,833,000 | 2026-07-29T10:09:16.3365576+00:00 | 108.7ms | GC pause | - | - | 179.1s / 447,520 msg/s | Gen2 +0 / pause +176.1ms |
| Confluent | 110,843,000 | 2026-07-29T10:09:16.3529511+00:00 | 114.9ms | GC pause | - | - | 179.1s / 447,520 msg/s | Gen2 +0 / pause +176.1ms |
| Confluent | 110,856,000 | 2026-07-29T10:09:16.3749221+00:00 | 106.8ms | GC pause | - | - | 179.1s / 447,520 msg/s | Gen2 +0 / pause +176.1ms |
| Confluent | 110,857,000 | 2026-07-29T10:09:16.376409+00:00 | 119.4ms | GC pause | - | - | 179.1s / 447,520 msg/s | Gen2 +0 / pause +176.1ms |
| Confluent | 110,859,000 | 2026-07-29T10:09:16.38102+00:00 | 112.8ms | GC pause | - | - | 179.1s / 447,520 msg/s | Gen2 +0 / pause +176.1ms |
| Confluent | 110,861,000 | 2026-07-29T10:09:16.394996+00:00 | 111.9ms | GC pause | - | - | 179.1s / 447,520 msg/s | Gen2 +0 / pause +176.1ms |
| Confluent | 119,708,000 | 2026-07-29T10:09:31.1061309+00:00 | 105.9ms | GC pause | - | - | 194.1s / 621,442 msg/s | Gen2 +0 / pause +112.5ms |
| Confluent | 119,711,000 | 2026-07-29T10:09:31.1113142+00:00 | 118.0ms | GC pause | - | - | 194.1s / 621,442 msg/s | Gen2 +0 / pause +112.5ms |
| Confluent | 300,641,000 | 2026-07-29T10:12:42.0688974+00:00 | 116.9ms | GC pause | - | - | 385.3s / 612,262 msg/s | Gen2 +0 / pause +289.6ms |
| Confluent | 412,877,000 | 2026-07-29T10:26:42.1784048+00:00 | 102.6ms | GC pause | - | - | 324.3s / 1,141,678 msg/s | Gen2 +0 / pause +155.9ms |
| Confluent | 430,888,000 | 2026-07-29T10:26:55.4270212+00:00 | 115.8ms | GC pause | - | - | 337.3s / 1,329,536 msg/s | Gen2 +0 / pause +72.3ms |
| Confluent | 430,933,000 | 2026-07-29T10:26:55.4734478+00:00 | 107.7ms | GC pause | - | - | 337.3s / 1,329,536 msg/s | Gen2 +0 / pause +72.3ms |
| Confluent | 436,500,000 | 2026-07-29T10:26:59.4752467+00:00 | 104.7ms | GC pause | - | - | 341.3s / 1,154,138 msg/s | Gen2 +0 / pause +103.8ms |
| Confluent | 436,503,000 | 2026-07-29T10:26:59.4776825+00:00 | 102.5ms | GC pause | - | - | 341.3s / 1,154,138 msg/s | Gen2 +0 / pause +103.8ms |
| Confluent | 436,781,000 | 2026-07-29T10:26:59.7119343+00:00 | 103.8ms | GC pause | - | - | 341.3s / 1,154,138 msg/s | Gen2 +0 / pause +103.8ms |
| Confluent | 436,868,000 | 2026-07-29T10:26:59.7846982+00:00 | 108.7ms | GC pause | - | - | 341.3s / 1,154,138 msg/s | Gen2 +0 / pause +103.8ms |
| Confluent | 436,941,000 | 2026-07-29T10:26:59.835702+00:00 | 118.2ms | GC pause | - | - | 341.3s / 1,154,138 msg/s | Gen2 +0 / pause +103.8ms |
| Confluent | 437,033,000 | 2026-07-29T10:26:59.912358+00:00 | 111.9ms | GC pause | - | - | 341.3s / 1,154,138 msg/s | Gen2 +0 / pause +103.8ms |
| Confluent | 437,063,000 | 2026-07-29T10:26:59.9376636+00:00 | 114.3ms | GC pause | - | - | 341.3s / 1,154,138 msg/s | Gen2 +0 / pause +103.8ms |
| Confluent | 437,118,000 | 2026-07-29T10:26:59.9884829+00:00 | 141.5ms | GC pause | - | - | 341.3s / 1,154,138 msg/s | Gen2 +0 / pause +103.8ms |
| Confluent | 437,138,000 | 2026-07-29T10:27:00.0081263+00:00 | 139.1ms | GC pause | - | - | 341.3s / 1,154,138 msg/s | Gen2 +0 / pause +103.8ms |
| Confluent | 437,328,000 | 2026-07-29T10:27:00.1978583+00:00 | 133.3ms | GC pause | - | - | 342.3s / 959,093 msg/s | Gen2 +0 / pause +195.5ms |
| Confluent | 437,357,000 | 2026-07-29T10:27:00.2218389+00:00 | 150.8ms | GC pause | - | - | 342.3s / 959,093 msg/s | Gen2 +0 / pause +195.5ms |
| Confluent | 438,163,000 | 2026-07-29T10:27:01.0489056+00:00 | 109.1ms | GC pause | - | - | 342.3s / 959,093 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 438,169,000 | 2026-07-29T10:27:01.0520114+00:00 | 102.4ms | GC pause | - | - | 342.3s / 959,093 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 438,183,000 | 2026-07-29T10:27:01.059517+00:00 | 116.2ms | GC pause | - | - | 342.3s / 959,093 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 438,188,000 | 2026-07-29T10:27:01.0621575+00:00 | 140.9ms | GC pause | - | - | 342.3s / 959,093 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 438,213,000 | 2026-07-29T10:27:01.0835447+00:00 | 125.0ms | GC pause | - | - | 342.3s / 959,093 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 438,248,000 | 2026-07-29T10:27:01.1190232+00:00 | 123.4ms | GC pause | - | - | 342.3s / 959,093 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 438,270,000 | 2026-07-29T10:27:01.1353464+00:00 | 130.2ms | GC pause | - | - | 342.3s / 959,093 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 442,030,000 | 2026-07-29T10:27:04.3101571+00:00 | 112.4ms | GC pause | - | - | 346.3s / 1,088,541 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 442,041,000 | 2026-07-29T10:27:04.31749+00:00 | 106.6ms | GC pause | - | - | 346.3s / 1,088,541 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 442,070,000 | 2026-07-29T10:27:04.33641+00:00 | 102.0ms | GC pause | - | - | 346.3s / 1,088,541 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 484,448,000 | 2026-07-29T10:27:35.9178258+00:00 | 113.7ms | GC pause | - | - | 377.3s / 1,285,051 msg/s | Gen2 +0 / pause +65.4ms |
| Confluent | 484,508,000 | 2026-07-29T10:27:35.9664546+00:00 | 112.5ms | GC pause | - | - | 377.3s / 1,285,051 msg/s | Gen2 +0 / pause +65.4ms |
| Confluent | 484,538,000 | 2026-07-29T10:27:35.9910666+00:00 | 114.0ms | GC pause | - | - | 377.3s / 1,285,051 msg/s | Gen2 +0 / pause +65.4ms |
| Confluent | 484,571,000 | 2026-07-29T10:27:36.0238384+00:00 | 100.3ms | GC pause | - | - | 377.3s / 1,285,051 msg/s | Gen2 +0 / pause +65.4ms |
| Confluent | 485,747,000 | 2026-07-29T10:27:36.9789173+00:00 | 100.7ms | GC pause | - | - | 378.3s / 1,255,105 msg/s | Gen2 +0 / pause +90.6ms |
| Confluent | 485,768,000 | 2026-07-29T10:27:36.993188+00:00 | 101.8ms | GC pause | - | - | 378.3s / 1,255,105 msg/s | Gen2 +0 / pause +90.6ms |
| Confluent | 551,438,000 | 2026-07-29T10:28:37.4302803+00:00 | 102.2ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 551,609,000 | 2026-07-29T10:28:37.5383586+00:00 | 105.2ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 551,629,000 | 2026-07-29T10:28:37.5611262+00:00 | 101.8ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 551,637,000 | 2026-07-29T10:28:37.5704495+00:00 | 111.0ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 551,713,000 | 2026-07-29T10:28:37.6289164+00:00 | 102.1ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 551,719,000 | 2026-07-29T10:28:37.633567+00:00 | 100.8ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 551,727,000 | 2026-07-29T10:28:37.63982+00:00 | 113.7ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 551,738,000 | 2026-07-29T10:28:37.6500585+00:00 | 111.6ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 551,741,000 | 2026-07-29T10:28:37.652044+00:00 | 109.7ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 551,747,000 | 2026-07-29T10:28:37.6561232+00:00 | 110.9ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 551,771,000 | 2026-07-29T10:28:37.6726604+00:00 | 114.0ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 551,777,000 | 2026-07-29T10:28:37.6766302+00:00 | 115.1ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 551,821,000 | 2026-07-29T10:28:37.7073347+00:00 | 120.5ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 551,887,000 | 2026-07-29T10:28:37.7692225+00:00 | 111.7ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 551,981,000 | 2026-07-29T10:28:37.8286914+00:00 | 117.4ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 552,078,000 | 2026-07-29T10:28:37.8913709+00:00 | 156.3ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 552,092,000 | 2026-07-29T10:28:37.8989752+00:00 | 109.4ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 552,101,000 | 2026-07-29T10:28:37.9120308+00:00 | 152.5ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 552,136,000 | 2026-07-29T10:28:37.9496842+00:00 | 112.1ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 552,157,000 | 2026-07-29T10:28:37.9694871+00:00 | 136.2ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 552,197,000 | 2026-07-29T10:28:38.0172483+00:00 | 120.0ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 552,211,000 | 2026-07-29T10:28:38.0326442+00:00 | 123.1ms | GC pause | - | - | 439.4s / 1,190,735 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 569,156,000 | 2026-07-29T10:28:52.2150793+00:00 | 100.6ms | GC pause | - | - | 453.4s / 1,254,884 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 569,183,000 | 2026-07-29T10:28:52.2434545+00:00 | 108.8ms | GC pause | - | - | 453.4s / 1,254,884 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 601,261,000 | 2026-07-29T10:29:22.4500355+00:00 | 102.6ms | GC pause | - | - | 484.4s / 969,060 msg/s | Gen2 +0 / pause +106.7ms |
| Confluent | 610,287,000 | 2026-07-29T10:29:30.8275704+00:00 | 100.8ms | GC pause | - | - | 492.4s / 1,120,185 msg/s | Gen2 +0 / pause +83.0ms |
| Confluent | 610,428,000 | 2026-07-29T10:29:30.9389852+00:00 | 102.6ms | GC pause | - | - | 492.4s / 1,120,185 msg/s | Gen2 +0 / pause +83.0ms |
| Confluent | 610,431,000 | 2026-07-29T10:29:30.9411463+00:00 | 100.5ms | GC pause | - | - | 492.4s / 1,120,185 msg/s | Gen2 +0 / pause +83.0ms |
| Confluent | 610,521,000 | 2026-07-29T10:29:31.0111685+00:00 | 116.6ms | GC pause | - | - | 492.4s / 1,120,185 msg/s | Gen2 +0 / pause +83.0ms |
| Confluent | 610,708,000 | 2026-07-29T10:29:31.1602564+00:00 | 110.6ms | GC pause | - | - | 492.4s / 1,120,185 msg/s | Gen2 +0 / pause +83.0ms |
| Confluent | 610,711,000 | 2026-07-29T10:29:31.1618606+00:00 | 109.2ms | GC pause | - | - | 492.4s / 1,120,185 msg/s | Gen2 +0 / pause +83.0ms |
| Confluent | 610,807,000 | 2026-07-29T10:29:31.2899443+00:00 | 106.0ms | GC pause | - | - | 493.4s / 1,129,418 msg/s | Gen2 +0 / pause +182.2ms |
| Confluent | 610,991,000 | 2026-07-29T10:29:31.4763948+00:00 | 126.9ms | GC pause | - | - | 493.4s / 1,129,418 msg/s | Gen2 +0 / pause +99.1ms |
| Confluent | 611,038,000 | 2026-07-29T10:29:31.5210093+00:00 | 128.7ms | GC pause | - | - | 493.4s / 1,129,418 msg/s | Gen2 +0 / pause +99.1ms |
| Confluent | 611,057,000 | 2026-07-29T10:29:31.5351541+00:00 | 137.8ms | GC pause | - | - | 493.4s / 1,129,418 msg/s | Gen2 +0 / pause +99.1ms |
| Confluent | 618,074,000 | 2026-07-29T10:29:37.8845944+00:00 | 104.5ms | GC pause | - | - | 499.4s / 1,320,791 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 618,089,000 | 2026-07-29T10:29:37.8942569+00:00 | 113.0ms | GC pause | - | - | 499.4s / 1,320,791 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 618,151,000 | 2026-07-29T10:29:37.9335809+00:00 | 140.6ms | GC pause | - | - | 499.4s / 1,320,791 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 618,220,000 | 2026-07-29T10:29:37.9785492+00:00 | 137.7ms | GC pause | - | - | 499.4s / 1,320,791 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 618,231,000 | 2026-07-29T10:29:37.986521+00:00 | 156.7ms | GC pause | - | - | 499.4s / 1,320,791 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 618,273,000 | 2026-07-29T10:29:38.019046+00:00 | 140.0ms | GC pause | - | - | 499.4s / 1,320,791 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 618,315,000 | 2026-07-29T10:29:38.0455953+00:00 | 156.4ms | GC pause | - | - | 499.4s / 1,320,791 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 618,381,000 | 2026-07-29T10:29:38.0902014+00:00 | 179.6ms | GC pause | - | - | 499.4s / 1,320,791 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 618,383,000 | 2026-07-29T10:29:38.0914332+00:00 | 159.1ms | GC pause | - | - | 499.4s / 1,320,791 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 618,421,000 | 2026-07-29T10:29:38.1264624+00:00 | 178.0ms | GC pause | - | - | 499.4s / 1,320,791 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 618,440,000 | 2026-07-29T10:29:38.1505558+00:00 | 151.5ms | GC pause | - | - | 499.4s / 1,320,791 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 618,448,000 | 2026-07-29T10:29:38.1607449+00:00 | 171.3ms | GC pause | - | - | 499.4s / 1,320,791 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 618,508,000 | 2026-07-29T10:29:38.2244674+00:00 | 157.1ms | GC pause | - | - | 499.4s / 1,320,791 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 618,515,000 | 2026-07-29T10:29:38.2292275+00:00 | 132.4ms | GC pause | - | - | 499.4s / 1,320,791 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 618,562,000 | 2026-07-29T10:29:38.2686818+00:00 | 117.2ms | GC pause | - | - | 499.4s / 1,320,791 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 618,564,000 | 2026-07-29T10:29:38.270085+00:00 | 130.8ms | GC pause | - | - | 500.4s / 1,077,076 msg/s | Gen2 +0 / pause +146.9ms |
| Confluent | 618,572,000 | 2026-07-29T10:29:38.2754574+00:00 | 111.7ms | GC pause | - | - | 499.4s / 1,320,791 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 618,573,000 | 2026-07-29T10:29:38.2760035+00:00 | 128.1ms | GC pause | - | - | 500.4s / 1,077,076 msg/s | Gen2 +0 / pause +146.9ms |
| Confluent | 618,595,000 | 2026-07-29T10:29:38.2953455+00:00 | 121.6ms | GC pause | - | - | 500.4s / 1,077,076 msg/s | Gen2 +0 / pause +146.9ms |
| Confluent | 618,604,000 | 2026-07-29T10:29:38.3014008+00:00 | 127.6ms | GC pause | - | - | 500.4s / 1,077,076 msg/s | Gen2 +0 / pause +146.9ms |
| Confluent | 618,610,000 | 2026-07-29T10:29:38.3056442+00:00 | 130.6ms | GC pause | - | - | 500.4s / 1,077,076 msg/s | Gen2 +0 / pause +146.9ms |
| Confluent | 618,627,000 | 2026-07-29T10:29:38.3155322+00:00 | 157.9ms | GC pause | - | - | 500.4s / 1,077,076 msg/s | Gen2 +0 / pause +146.9ms |
| Confluent | 618,663,000 | 2026-07-29T10:29:38.3430407+00:00 | 140.6ms | GC pause | - | - | 500.4s / 1,077,076 msg/s | Gen2 +0 / pause +146.9ms |
| Confluent | 618,677,000 | 2026-07-29T10:29:38.3539592+00:00 | 156.9ms | GC pause | - | - | 500.4s / 1,077,076 msg/s | Gen2 +0 / pause +146.9ms |
| Confluent | 618,686,000 | 2026-07-29T10:29:38.361181+00:00 | 138.2ms | GC pause | - | - | 500.4s / 1,077,076 msg/s | Gen2 +0 / pause +146.9ms |
| Confluent | 618,703,000 | 2026-07-29T10:29:38.3754035+00:00 | 136.0ms | GC pause | - | - | 500.4s / 1,077,076 msg/s | Gen2 +0 / pause +146.9ms |
| Confluent | 618,748,000 | 2026-07-29T10:29:38.4378495+00:00 | 129.1ms | GC pause | - | - | 500.4s / 1,077,076 msg/s | Gen2 +0 / pause +73.1ms |
| Confluent | 618,749,000 | 2026-07-29T10:29:38.4384184+00:00 | 106.6ms | GC pause | - | - | 500.4s / 1,077,076 msg/s | Gen2 +0 / pause +73.1ms |
| Confluent | 618,867,000 | 2026-07-29T10:29:38.5474338+00:00 | 114.3ms | GC pause | - | - | 500.4s / 1,077,076 msg/s | Gen2 +0 / pause +73.1ms |
| Confluent | 619,087,000 | 2026-07-29T10:29:38.721811+00:00 | 106.4ms | GC pause | - | - | 500.4s / 1,077,076 msg/s | Gen2 +0 / pause +73.1ms |
| Confluent | 620,947,000 | 2026-07-29T10:29:40.2829672+00:00 | 130.4ms | GC pause | - | - | 502.4s / 1,180,318 msg/s | Gen2 +0 / pause +181.4ms |
| Confluent | 620,960,000 | 2026-07-29T10:29:40.2918127+00:00 | 113.7ms | GC pause | - | - | 502.4s / 1,180,318 msg/s | Gen2 +0 / pause +181.4ms |
| Confluent | 620,975,000 | 2026-07-29T10:29:40.306915+00:00 | 112.9ms | GC pause | - | - | 502.4s / 1,180,318 msg/s | Gen2 +0 / pause +181.4ms |
| Confluent | 621,000,000 | 2026-07-29T10:29:40.3233178+00:00 | 108.7ms | GC pause | - | - | 502.4s / 1,180,318 msg/s | Gen2 +0 / pause +181.4ms |
| Confluent | 622,767,000 | 2026-07-29T10:29:41.860533+00:00 | 107.6ms | GC pause | - | - | 503.4s / 1,045,469 msg/s | Gen2 +0 / pause +91.3ms |
| Confluent | 622,816,000 | 2026-07-29T10:29:41.8973048+00:00 | 111.6ms | GC pause | - | - | 503.4s / 1,045,469 msg/s | Gen2 +0 / pause +91.3ms |
| Confluent | 622,839,000 | 2026-07-29T10:29:41.9248375+00:00 | 100.8ms | GC pause | - | - | 503.4s / 1,045,469 msg/s | Gen2 +0 / pause +91.3ms |
| Confluent | 622,858,000 | 2026-07-29T10:29:41.9598407+00:00 | 110.8ms | GC pause | - | - | 503.4s / 1,045,469 msg/s | Gen2 +0 / pause +91.3ms |
| Confluent | 634,009,000 | 2026-07-29T10:29:51.8902644+00:00 | 110.0ms | GC pause | - | - | 513.4s / 1,020,687 msg/s | Gen2 +0 / pause +118.5ms |
| Confluent | 634,024,000 | 2026-07-29T10:29:51.8984896+00:00 | 110.5ms | GC pause | - | - | 513.4s / 1,020,687 msg/s | Gen2 +0 / pause +118.5ms |
| Confluent | 634,029,000 | 2026-07-29T10:29:51.9010572+00:00 | 112.9ms | GC pause | - | - | 513.4s / 1,020,687 msg/s | Gen2 +0 / pause +118.5ms |
| Confluent | 634,057,000 | 2026-07-29T10:29:51.9161352+00:00 | 130.7ms | GC pause | - | - | 513.4s / 1,020,687 msg/s | Gen2 +0 / pause +118.5ms |
| Confluent | 634,078,000 | 2026-07-29T10:29:51.9285004+00:00 | 133.1ms | GC pause | - | - | 513.4s / 1,020,687 msg/s | Gen2 +0 / pause +118.5ms |
| Confluent | 634,086,000 | 2026-07-29T10:29:51.9330147+00:00 | 123.8ms | GC pause | - | - | 513.4s / 1,020,687 msg/s | Gen2 +0 / pause +118.5ms |
| Confluent | 634,089,000 | 2026-07-29T10:29:51.9344686+00:00 | 122.4ms | GC pause | - | - | 513.4s / 1,020,687 msg/s | Gen2 +0 / pause +118.5ms |
| Confluent | 634,127,000 | 2026-07-29T10:29:51.9588604+00:00 | 148.2ms | GC pause | - | - | 513.4s / 1,020,687 msg/s | Gen2 +0 / pause +118.5ms |
| Confluent | 634,130,000 | 2026-07-29T10:29:51.9609943+00:00 | 129.7ms | GC pause | - | - | 513.4s / 1,020,687 msg/s | Gen2 +0 / pause +118.5ms |
| Confluent | 634,154,000 | 2026-07-29T10:29:51.977299+00:00 | 139.0ms | GC pause | - | - | 513.4s / 1,020,687 msg/s | Gen2 +0 / pause +118.5ms |
| Confluent | 634,160,000 | 2026-07-29T10:29:51.9878271+00:00 | 140.9ms | GC pause | - | - | 513.4s / 1,020,687 msg/s | Gen2 +0 / pause +118.5ms |
| Confluent | 634,249,000 | 2026-07-29T10:29:52.079413+00:00 | 130.2ms | GC pause | - | - | 513.4s / 1,020,687 msg/s | Gen2 +0 / pause +118.5ms |
| Confluent | 634,251,000 | 2026-07-29T10:29:52.0815438+00:00 | 138.2ms | GC pause | - | - | 513.4s / 1,020,687 msg/s | Gen2 +0 / pause +118.5ms |
| Confluent | 634,258,000 | 2026-07-29T10:29:52.086619+00:00 | 138.0ms | GC pause | - | - | 513.4s / 1,020,687 msg/s | Gen2 +0 / pause +118.5ms |
| Confluent | 634,264,000 | 2026-07-29T10:29:52.0903584+00:00 | 124.4ms | GC pause | - | - | 513.4s / 1,020,687 msg/s | Gen2 +0 / pause +118.5ms |
| Confluent | 634,266,000 | 2026-07-29T10:29:52.091566+00:00 | 125.0ms | GC pause | - | - | 513.4s / 1,020,687 msg/s | Gen2 +0 / pause +118.5ms |
| Confluent | 634,288,000 | 2026-07-29T10:29:52.1285139+00:00 | 133.4ms | GC pause | - | - | 513.4s / 1,020,687 msg/s | Gen2 +0 / pause +118.5ms |
| Confluent | 635,338,000 | 2026-07-29T10:29:52.985718+00:00 | 115.4ms | GC pause | - | - | 514.4s / 1,305,432 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 635,377,000 | 2026-07-29T10:29:53.0127393+00:00 | 119.4ms | GC pause | - | - | 514.4s / 1,305,432 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 653,467,000 | 2026-07-29T10:30:12.3037781+00:00 | 109.2ms | GC pause | - | - | 533.4s / 1,024,459 msg/s | Gen2 +0 / pause +97.6ms |
| Confluent | 653,588,000 | 2026-07-29T10:30:12.4178646+00:00 | 109.2ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +166.6ms |
| Confluent | 653,658,000 | 2026-07-29T10:30:12.4631895+00:00 | 133.6ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 653,671,000 | 2026-07-29T10:30:12.4706617+00:00 | 137.1ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 653,705,000 | 2026-07-29T10:30:12.4949569+00:00 | 127.9ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 653,741,000 | 2026-07-29T10:30:12.5228184+00:00 | 153.9ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 653,748,000 | 2026-07-29T10:30:12.5296945+00:00 | 155.0ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 653,762,000 | 2026-07-29T10:30:12.5397289+00:00 | 122.0ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 653,772,000 | 2026-07-29T10:30:12.5463043+00:00 | 126.9ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 653,789,000 | 2026-07-29T10:30:12.5605741+00:00 | 135.4ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 653,799,000 | 2026-07-29T10:30:12.5800851+00:00 | 125.6ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 653,817,000 | 2026-07-29T10:30:12.6023285+00:00 | 165.3ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 653,838,000 | 2026-07-29T10:30:12.6317272+00:00 | 144.5ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 653,848,000 | 2026-07-29T10:30:12.6399573+00:00 | 146.7ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 653,884,000 | 2026-07-29T10:30:12.6721014+00:00 | 117.7ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 653,906,000 | 2026-07-29T10:30:12.6889727+00:00 | 129.7ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 653,920,000 | 2026-07-29T10:30:12.6979287+00:00 | 134.4ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 653,930,000 | 2026-07-29T10:30:12.7054469+00:00 | 138.9ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 653,983,000 | 2026-07-29T10:30:12.7795097+00:00 | 117.3ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,012,000 | 2026-07-29T10:30:12.8009806+00:00 | 103.5ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,028,000 | 2026-07-29T10:30:12.8157406+00:00 | 163.4ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,030,000 | 2026-07-29T10:30:12.8167978+00:00 | 121.1ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,055,000 | 2026-07-29T10:30:12.8338394+00:00 | 144.0ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,065,000 | 2026-07-29T10:30:12.8417626+00:00 | 167.5ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,091,000 | 2026-07-29T10:30:12.8605586+00:00 | 200.0ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,096,000 | 2026-07-29T10:30:12.8640908+00:00 | 170.7ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,102,000 | 2026-07-29T10:30:12.8690526+00:00 | 155.4ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,134,000 | 2026-07-29T10:30:12.8943635+00:00 | 169.9ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,143,000 | 2026-07-29T10:30:12.9019893+00:00 | 178.9ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,146,000 | 2026-07-29T10:30:12.9046232+00:00 | 178.1ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,181,000 | 2026-07-29T10:30:12.9301015+00:00 | 224.6ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,193,000 | 2026-07-29T10:30:12.9393669+00:00 | 188.4ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,245,000 | 2026-07-29T10:30:12.9759101+00:00 | 206.3ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,348,000 | 2026-07-29T10:30:13.1130519+00:00 | 205.1ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,402,000 | 2026-07-29T10:30:13.1786333+00:00 | 128.1ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,415,000 | 2026-07-29T10:30:13.1899699+00:00 | 147.1ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,429,000 | 2026-07-29T10:30:13.2024402+00:00 | 143.8ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,445,000 | 2026-07-29T10:30:13.2170742+00:00 | 147.4ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,476,000 | 2026-07-29T10:30:13.2509249+00:00 | 140.9ms | GC pause | - | - | 534.4s / 1,027,031 msg/s | Gen2 +0 / pause +69.0ms |
| Confluent | 654,528,000 | 2026-07-29T10:30:13.310186+00:00 | 186.7ms | GC pause | - | - | 535.4s / 732,001 msg/s | Gen2 +0 / pause +161.7ms |
| Confluent | 654,587,000 | 2026-07-29T10:30:13.3722286+00:00 | 183.8ms | GC pause | - | - | 535.4s / 732,001 msg/s | Gen2 +0 / pause +161.7ms |
| Confluent | 654,589,000 | 2026-07-29T10:30:13.3735756+00:00 | 147.6ms | GC pause | - | - | 535.4s / 732,001 msg/s | Gen2 +0 / pause +161.7ms |
| Confluent | 654,605,000 | 2026-07-29T10:30:13.3938598+00:00 | 143.1ms | GC pause | - | - | 535.4s / 732,001 msg/s | Gen2 +0 / pause +161.7ms |
| Confluent | 663,017,000 | 2026-07-29T10:30:22.9395522+00:00 | 115.7ms | GC pause | - | - | 544.5s / 349,017 msg/s | Gen2 +0 / pause +144.5ms |
| Confluent | 663,037,000 | 2026-07-29T10:30:23.0009223+00:00 | 109.1ms | GC pause | - | - | 544.5s / 349,017 msg/s | Gen2 +0 / pause +144.5ms |
| Confluent | 663,471,000 | 2026-07-29T10:30:24.2850199+00:00 | 123.5ms | GC pause | - | - | 545.5s / 392,858 msg/s | Gen2 +0 / pause +173.7ms |
| Confluent | 663,504,000 | 2026-07-29T10:30:24.3732615+00:00 | 139.6ms | GC pause | - | - | 546.5s / 362,391 msg/s | Gen2 +0 / pause +348.7ms |
| Confluent | 663,544,000 | 2026-07-29T10:30:24.4520228+00:00 | 222.6ms | GC pause | - | - | 546.5s / 362,391 msg/s | Gen2 +0 / pause +175.0ms |
| Confluent | 663,550,000 | 2026-07-29T10:30:24.4653451+00:00 | 223.7ms | GC pause | - | - | 546.5s / 362,391 msg/s | Gen2 +0 / pause +175.0ms |
| Confluent | 663,557,000 | 2026-07-29T10:30:24.4839135+00:00 | 250.3ms | GC pause | - | - | 546.5s / 362,391 msg/s | Gen2 +0 / pause +175.0ms |
| Confluent | 663,642,000 | 2026-07-29T10:30:24.7349715+00:00 | 208.3ms | GC pause | - | - | 546.5s / 362,391 msg/s | Gen2 +0 / pause +175.0ms |
| Confluent | 663,643,000 | 2026-07-29T10:30:24.7361417+00:00 | 219.9ms | GC pause | - | - | 546.5s / 362,391 msg/s | Gen2 +0 / pause +175.0ms |
| Confluent | 663,657,000 | 2026-07-29T10:30:24.7683579+00:00 | 239.7ms | GC pause | - | - | 546.5s / 362,391 msg/s | Gen2 +0 / pause +175.0ms |
| Confluent | 663,686,000 | 2026-07-29T10:30:24.821062+00:00 | 226.9ms | GC pause | - | - | 546.5s / 362,391 msg/s | Gen2 +0 / pause +175.0ms |
| Confluent | 663,733,000 | 2026-07-29T10:30:24.9285059+00:00 | 201.5ms | GC pause | - | - | 546.5s / 362,391 msg/s | Gen2 +0 / pause +175.0ms |
| Confluent | 663,767,000 | 2026-07-29T10:30:25.0172904+00:00 | 203.3ms | GC pause | - | - | 546.5s / 362,391 msg/s | Gen2 +0 / pause +175.0ms |
| Confluent | 663,799,000 | 2026-07-29T10:30:25.1162596+00:00 | 131.6ms | GC pause | - | - | 546.5s / 362,391 msg/s | Gen2 +0 / pause +175.0ms |
| Confluent | 663,806,000 | 2026-07-29T10:30:25.1381144+00:00 | 118.3ms | GC pause | - | - | 546.5s / 362,391 msg/s | Gen2 +0 / pause +175.0ms |
| Confluent | 663,826,000 | 2026-07-29T10:30:25.1778075+00:00 | 109.1ms | GC pause | - | - | 546.5s / 362,391 msg/s | Gen2 +0 / pause +175.0ms |
| Confluent | 676,447,000 | 2026-07-29T10:30:39.2842601+00:00 | 118.2ms | GC pause | - | - | 560.5s / 1,059,933 msg/s | Gen2 +0 / pause +107.7ms |
| Confluent | 676,459,000 | 2026-07-29T10:30:39.2923055+00:00 | 101.1ms | GC pause | - | - | 560.5s / 1,059,933 msg/s | Gen2 +0 / pause +107.7ms |
| Confluent | 676,473,000 | 2026-07-29T10:30:39.3000823+00:00 | 119.4ms | GC pause | - | - | 560.5s / 1,059,933 msg/s | Gen2 +0 / pause +107.7ms |
| Confluent | 676,522,000 | 2026-07-29T10:30:39.3354847+00:00 | 122.2ms | GC pause | - | - | 560.5s / 1,059,933 msg/s | Gen2 +0 / pause +107.7ms |
| Confluent | 676,528,000 | 2026-07-29T10:30:39.3405071+00:00 | 149.9ms | GC pause | - | - | 561.5s / 675,229 msg/s | Gen2 +0 / pause +239.9ms |
| Confluent | 676,529,000 | 2026-07-29T10:30:39.3412295+00:00 | 123.8ms | GC pause | - | - | 560.5s / 1,059,933 msg/s | Gen2 +0 / pause +107.7ms |
| Confluent | 676,567,000 | 2026-07-29T10:30:39.3953325+00:00 | 171.7ms | GC pause | - | - | 561.5s / 675,229 msg/s | Gen2 +0 / pause +239.9ms |
| Confluent | 676,593,000 | 2026-07-29T10:30:39.4550425+00:00 | 125.7ms | GC pause | - | - | 561.5s / 675,229 msg/s | Gen2 +0 / pause +239.9ms |
| Confluent | 676,608,000 | 2026-07-29T10:30:39.4834766+00:00 | 115.5ms | GC pause | - | - | 561.5s / 675,229 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 687,148,000 | 2026-07-29T10:30:52.9300613+00:00 | 113.8ms | GC pause | - | - | 574.5s / 958,571 msg/s | Gen2 +0 / pause +99.5ms |
| Confluent | 687,168,000 | 2026-07-29T10:30:52.9460597+00:00 | 130.1ms | GC pause | - | - | 574.5s / 958,571 msg/s | Gen2 +0 / pause +99.5ms |
| Confluent | 687,206,000 | 2026-07-29T10:30:52.9758886+00:00 | 117.6ms | GC pause | - | - | 574.5s / 958,571 msg/s | Gen2 +0 / pause +99.5ms |
| Confluent | 687,207,000 | 2026-07-29T10:30:52.976573+00:00 | 125.7ms | GC pause | - | - | 574.5s / 958,571 msg/s | Gen2 +0 / pause +99.5ms |
| Confluent | 727,521,000 | 2026-07-29T10:31:33.0884562+00:00 | 105.2ms | GC pause | - | - | 614.5s / 1,217,922 msg/s | Gen2 +0 / pause +81.4ms |
| Confluent | 727,534,000 | 2026-07-29T10:31:33.0990371+00:00 | 112.6ms | GC pause | - | - | 614.5s / 1,217,922 msg/s | Gen2 +0 / pause +81.4ms |
| Confluent | 727,575,000 | 2026-07-29T10:31:33.1331563+00:00 | 110.3ms | GC pause | - | - | 614.5s / 1,217,922 msg/s | Gen2 +0 / pause +81.4ms |
| Confluent | 727,585,000 | 2026-07-29T10:31:33.1442198+00:00 | 112.9ms | GC pause | - | - | 614.5s / 1,217,922 msg/s | Gen2 +0 / pause +81.4ms |
| Confluent | 728,489,000 | 2026-07-29T10:31:33.9081327+00:00 | 101.6ms | GC pause | - | - | 615.5s / 1,219,056 msg/s | Gen2 +0 / pause +95.4ms |
| Confluent | 728,500,000 | 2026-07-29T10:31:33.9148044+00:00 | 116.1ms | GC pause | - | - | 615.5s / 1,219,056 msg/s | Gen2 +0 / pause +95.4ms |
| Confluent | 728,518,000 | 2026-07-29T10:31:33.9295631+00:00 | 130.5ms | GC pause | - | - | 615.5s / 1,219,056 msg/s | Gen2 +0 / pause +95.4ms |
| Confluent | 728,519,000 | 2026-07-29T10:31:33.9301545+00:00 | 110.7ms | GC pause | - | - | 615.5s / 1,219,056 msg/s | Gen2 +0 / pause +95.4ms |
| Confluent | 728,522,000 | 2026-07-29T10:31:33.932086+00:00 | 106.1ms | GC pause | - | - | 615.5s / 1,219,056 msg/s | Gen2 +0 / pause +95.4ms |
| Confluent | 728,528,000 | 2026-07-29T10:31:33.9358066+00:00 | 139.1ms | GC pause | - | - | 615.5s / 1,219,056 msg/s | Gen2 +0 / pause +95.4ms |
| Confluent | 728,587,000 | 2026-07-29T10:31:33.9783085+00:00 | 135.5ms | GC pause | - | - | 615.5s / 1,219,056 msg/s | Gen2 +0 / pause +95.4ms |
| Confluent | 728,589,000 | 2026-07-29T10:31:33.9807003+00:00 | 119.0ms | GC pause | - | - | 615.5s / 1,219,056 msg/s | Gen2 +0 / pause +95.4ms |
| Confluent | 728,619,000 | 2026-07-29T10:31:34.0162488+00:00 | 114.9ms | GC pause | - | - | 615.5s / 1,219,056 msg/s | Gen2 +0 / pause +95.4ms |
| Confluent | 728,634,000 | 2026-07-29T10:31:34.0308523+00:00 | 101.5ms | GC pause | - | - | 615.5s / 1,219,056 msg/s | Gen2 +0 / pause +95.4ms |
| Confluent | 728,644,000 | 2026-07-29T10:31:34.0377807+00:00 | 106.5ms | GC pause | - | - | 615.5s / 1,219,056 msg/s | Gen2 +0 / pause +95.4ms |
| Confluent | 728,645,000 | 2026-07-29T10:31:34.0385961+00:00 | 110.5ms | GC pause | - | - | 615.5s / 1,219,056 msg/s | Gen2 +0 / pause +95.4ms |
| Confluent | 728,671,000 | 2026-07-29T10:31:34.0568556+00:00 | 112.5ms | GC pause | - | - | 615.5s / 1,219,056 msg/s | Gen2 +0 / pause +95.4ms |
| Confluent | 728,721,000 | 2026-07-29T10:31:34.1028211+00:00 | 107.5ms | GC pause | - | - | 615.5s / 1,219,056 msg/s | Gen2 +0 / pause +95.4ms |
| Confluent | 788,137,000 | 2026-07-29T10:32:24.1108406+00:00 | 106.0ms | GC pause | - | - | 665.6s / 977,753 msg/s | Gen2 +0 / pause +125.1ms |
| Confluent | 811,626,000 | 2026-07-29T10:32:44.4215675+00:00 | 108.9ms | GC pause | - | - | 685.6s / 1,125,703 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 811,651,000 | 2026-07-29T10:32:44.4393232+00:00 | 137.6ms | GC pause | - | - | 686.6s / 815,630 msg/s | Gen2 +0 / pause +198.2ms |
| Confluent | 811,656,000 | 2026-07-29T10:32:44.4424572+00:00 | 122.6ms | GC pause | - | - | 686.6s / 815,630 msg/s | Gen2 +0 / pause +198.2ms |
| Confluent | 811,669,000 | 2026-07-29T10:32:44.4499341+00:00 | 126.2ms | GC pause | - | - | 686.6s / 815,630 msg/s | Gen2 +0 / pause +198.2ms |
| Confluent | 819,578,000 | 2026-07-29T10:32:50.9476295+00:00 | 103.5ms | GC pause | - | - | 692.6s / 1,257,216 msg/s | Gen2 +0 / pause +83.9ms |
| Confluent | 819,581,000 | 2026-07-29T10:32:50.9493199+00:00 | 103.4ms | GC pause | - | - | 692.6s / 1,257,216 msg/s | Gen2 +0 / pause +83.9ms |
| Confluent | 819,588,000 | 2026-07-29T10:32:50.9541345+00:00 | 103.7ms | GC pause | - | - | 692.6s / 1,257,216 msg/s | Gen2 +0 / pause +83.9ms |
| Confluent | 819,598,000 | 2026-07-29T10:32:50.960295+00:00 | 107.8ms | GC pause | - | - | 692.6s / 1,257,216 msg/s | Gen2 +0 / pause +83.9ms |
| Confluent | 819,668,000 | 2026-07-29T10:32:51.0069907+00:00 | 112.9ms | GC pause | - | - | 692.6s / 1,257,216 msg/s | Gen2 +0 / pause +83.9ms |
| Confluent | 819,707,000 | 2026-07-29T10:32:51.0400806+00:00 | 116.2ms | GC pause | - | - | 692.6s / 1,257,216 msg/s | Gen2 +0 / pause +83.9ms |
| Confluent | 819,758,000 | 2026-07-29T10:32:51.0930013+00:00 | 129.3ms | GC pause | - | - | 692.6s / 1,257,216 msg/s | Gen2 +0 / pause +83.9ms |
| Confluent | 819,762,000 | 2026-07-29T10:32:51.0959314+00:00 | 101.9ms | GC pause | - | - | 692.6s / 1,257,216 msg/s | Gen2 +0 / pause +83.9ms |
| Confluent | 819,778,000 | 2026-07-29T10:32:51.1074387+00:00 | 125.7ms | GC pause | - | - | 692.6s / 1,257,216 msg/s | Gen2 +0 / pause +83.9ms |
| Confluent | 819,802,000 | 2026-07-29T10:32:51.1227082+00:00 | 114.1ms | GC pause | - | - | 692.6s / 1,257,216 msg/s | Gen2 +0 / pause +83.9ms |
| Confluent | 819,858,000 | 2026-07-29T10:32:51.1899009+00:00 | 100.7ms | GC pause | - | - | 692.6s / 1,257,216 msg/s | Gen2 +0 / pause +83.9ms |
| Confluent | 841,543,000 | 2026-07-29T10:33:10.3657231+00:00 | 102.8ms | GC pause | - | - | 711.6s / 1,207,583 msg/s | Gen2 +0 / pause +80.6ms |
| Confluent | 844,380,000 | 2026-07-29T10:33:12.8475478+00:00 | 101.2ms | GC pause | - | - | 714.6s / 1,085,563 msg/s | Gen2 +0 / pause +112.3ms |
| Confluent | 844,404,000 | 2026-07-29T10:33:12.8651528+00:00 | 104.8ms | GC pause | - | - | 714.6s / 1,085,563 msg/s | Gen2 +0 / pause +112.3ms |
| Confluent | 844,492,000 | 2026-07-29T10:33:12.9612172+00:00 | 108.9ms | GC pause | - | - | 714.6s / 1,085,563 msg/s | Gen2 +0 / pause +112.3ms |
| Confluent | 844,658,000 | 2026-07-29T10:33:13.1283821+00:00 | 129.6ms | GC pause | - | - | 714.6s / 1,085,563 msg/s | Gen2 +0 / pause +112.3ms |
| Confluent | 844,668,000 | 2026-07-29T10:33:13.1443525+00:00 | 118.6ms | GC pause | - | - | 714.6s / 1,085,563 msg/s | Gen2 +0 / pause +112.3ms |
| Confluent | 878,437,000 | 2026-07-29T10:33:43.5786655+00:00 | 106.3ms | GC pause | - | - | 745.6s / 1,070,557 msg/s | Gen2 +0 / pause +169.5ms |
| Confluent | 878,438,000 | 2026-07-29T10:33:43.5791296+00:00 | 106.9ms | GC pause | - | - | 745.6s / 1,070,557 msg/s | Gen2 +0 / pause +169.5ms |
| Confluent | 878,448,000 | 2026-07-29T10:33:43.5881293+00:00 | 104.6ms | GC pause | - | - | 745.6s / 1,070,557 msg/s | Gen2 +0 / pause +169.5ms |
| Confluent | 878,457,000 | 2026-07-29T10:33:43.5951802+00:00 | 104.9ms | GC pause | - | - | 745.6s / 1,070,557 msg/s | Gen2 +0 / pause +169.5ms |
| Confluent | 878,478,000 | 2026-07-29T10:33:43.6104191+00:00 | 110.2ms | GC pause | - | - | 745.6s / 1,070,557 msg/s | Gen2 +0 / pause +79.4ms |
| Confluent | 878,488,000 | 2026-07-29T10:33:43.6168162+00:00 | 109.3ms | GC pause | - | - | 745.6s / 1,070,557 msg/s | Gen2 +0 / pause +79.4ms |
| Confluent | 878,637,000 | 2026-07-29T10:33:43.754382+00:00 | 100.2ms | GC pause | - | - | 745.6s / 1,070,557 msg/s | Gen2 +0 / pause +79.4ms |
| Confluent | 878,761,000 | 2026-07-29T10:33:43.8758833+00:00 | 125.4ms | GC pause | - | - | 745.6s / 1,070,557 msg/s | Gen2 +0 / pause +79.4ms |
| Confluent | 878,877,000 | 2026-07-29T10:33:44.0033718+00:00 | 111.3ms | GC pause | - | - | 745.6s / 1,070,557 msg/s | Gen2 +0 / pause +79.4ms |
| Confluent | 878,931,000 | 2026-07-29T10:33:44.0525292+00:00 | 105.5ms | GC pause | - | - | 745.6s / 1,070,557 msg/s | Gen2 +0 / pause +79.4ms |
| Confluent | 878,958,000 | 2026-07-29T10:33:44.0741867+00:00 | 105.1ms | GC pause | - | - | 745.6s / 1,070,557 msg/s | Gen2 +0 / pause +79.4ms |
| Confluent | 878,981,000 | 2026-07-29T10:33:44.0902466+00:00 | 104.3ms | GC pause | - | - | 745.6s / 1,070,557 msg/s | Gen2 +0 / pause +79.4ms |
| Confluent | 878,998,000 | 2026-07-29T10:33:44.1000614+00:00 | 107.2ms | GC pause | - | - | 745.6s / 1,070,557 msg/s | Gen2 +0 / pause +79.4ms |
| Confluent | 879,247,000 | 2026-07-29T10:33:44.31109+00:00 | 104.7ms | GC pause | - | - | 745.6s / 1,070,557 msg/s | Gen2 +0 / pause +79.4ms |
| Confluent | 879,301,000 | 2026-07-29T10:33:44.3512249+00:00 | 107.2ms | GC pause | - | - | 745.6s / 1,070,557 msg/s | Gen2 +0 / pause +79.4ms |
| Confluent | 921,748,000 | 2026-07-29T10:34:21.5449833+00:00 | 110.8ms | GC pause | - | - | 783.6s / 853,027 msg/s | Gen2 +0 / pause +212.5ms |
| Confluent | 921,761,000 | 2026-07-29T10:34:21.5606223+00:00 | 113.8ms | GC pause | - | - | 783.6s / 853,027 msg/s | Gen2 +0 / pause +212.5ms |
| Confluent | 938,788,000 | 2026-07-29T10:34:37.7590292+00:00 | 100.8ms | GC pause | - | - | 799.7s / 1,034,872 msg/s | Gen2 +0 / pause +111.3ms |
| Confluent | 947,588,000 | 2026-07-29T10:34:47.1285272+00:00 | 139.9ms | GC pause | - | - | 808.7s / 556,013 msg/s | Gen2 +0 / pause +192.6ms |
| Confluent | 947,605,000 | 2026-07-29T10:34:47.151177+00:00 | 275.7ms | GC pause | - | - | 808.7s / 556,013 msg/s | Gen2 +0 / pause +192.6ms |
| Confluent | 947,615,000 | 2026-07-29T10:34:47.1779386+00:00 | 277.9ms | GC pause | - | - | 808.7s / 556,013 msg/s | Gen2 +0 / pause +192.6ms |
| Confluent | 947,639,000 | 2026-07-29T10:34:47.2460859+00:00 | 234.0ms | GC pause | - | - | 808.7s / 556,013 msg/s | Gen2 +0 / pause +192.6ms |
| Dekaf | 255,642,000 | 2026-07-29T10:39:09.943644+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 171.1s / 1,137,709 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 306,321,000 | 2026-07-29T10:39:43.9072023+00:00 | 159.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 205.1s / 1,005,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 306,322,000 | 2026-07-29T10:39:43.9077988+00:00 | 158.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 205.1s / 1,005,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 306,331,000 | 2026-07-29T10:39:43.9191867+00:00 | 172.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 205.1s / 1,005,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 306,332,000 | 2026-07-29T10:39:43.9199657+00:00 | 171.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 205.1s / 1,005,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 306,341,000 | 2026-07-29T10:39:43.9368884+00:00 | 181.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 205.1s / 1,005,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 306,342,000 | 2026-07-29T10:39:43.9382799+00:00 | 180.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 205.1s / 1,005,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 306,351,000 | 2026-07-29T10:39:43.9504907+00:00 | 174.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 205.1s / 1,005,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 306,352,000 | 2026-07-29T10:39:43.9519808+00:00 | 173.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 205.1s / 1,005,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 306,361,000 | 2026-07-29T10:39:43.9651283+00:00 | 162.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 205.1s / 1,005,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 306,362,000 | 2026-07-29T10:39:43.9654135+00:00 | 162.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 205.1s / 1,005,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 306,371,000 | 2026-07-29T10:39:44.0122487+00:00 | 171.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 205.1s / 1,005,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 306,372,000 | 2026-07-29T10:39:44.0134367+00:00 | 170.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 205.1s / 1,005,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 306,381,000 | 2026-07-29T10:39:44.0666723+00:00 | 126.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 205.1s / 1,005,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 306,382,000 | 2026-07-29T10:39:44.0670983+00:00 | 125.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 205.1s / 1,005,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 357,882,000 | 2026-07-29T10:40:17.0568959+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 238.1s / 1,361,740 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 357,891,000 | 2026-07-29T10:40:17.0641168+00:00 | 108.1ms | broker/backlog (no scale or GC event) | - | - | 238.1s / 1,361,740 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 357,892,000 | 2026-07-29T10:40:17.0674041+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 238.1s / 1,361,740 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 357,921,000 | 2026-07-29T10:40:17.1036958+00:00 | 105.3ms | broker/backlog (no scale or GC event) | - | - | 238.1s / 1,361,740 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 357,922,000 | 2026-07-29T10:40:17.1072801+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 238.1s / 1,361,740 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 357,931,000 | 2026-07-29T10:40:17.1163231+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 238.1s / 1,361,740 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 357,932,000 | 2026-07-29T10:40:17.1167224+00:00 | 112.2ms | broker/backlog (no scale or GC event) | - | - | 238.1s / 1,361,740 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 357,941,000 | 2026-07-29T10:40:17.1330023+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 238.1s / 1,361,740 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 357,942,000 | 2026-07-29T10:40:17.1342663+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 238.1s / 1,361,740 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 357,981,000 | 2026-07-29T10:40:17.1862139+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 238.1s / 1,361,740 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 357,982,000 | 2026-07-29T10:40:17.1870357+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 238.1s / 1,361,740 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 357,991,000 | 2026-07-29T10:40:17.199683+00:00 | 104.4ms | broker/backlog (no scale or GC event) | - | - | 238.1s / 1,361,740 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 357,992,000 | 2026-07-29T10:40:17.2009385+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 238.1s / 1,361,740 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 358,011,000 | 2026-07-29T10:40:17.2313777+00:00 | 110.0ms | broker/backlog (no scale or GC event) | - | - | 238.1s / 1,361,740 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 358,012,000 | 2026-07-29T10:40:17.2330282+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 238.1s / 1,361,740 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 358,021,000 | 2026-07-29T10:40:17.2472716+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 238.1s / 1,361,740 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 358,022,000 | 2026-07-29T10:40:17.2476856+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 238.1s / 1,361,740 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 358,031,000 | 2026-07-29T10:40:17.2595265+00:00 | 108.8ms | broker/backlog (no scale or GC event) | - | - | 239.1s / 1,262,158 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 358,032,000 | 2026-07-29T10:40:17.2637557+00:00 | 104.6ms | broker/backlog (no scale or GC event) | - | - | 239.1s / 1,262,158 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 358,041,000 | 2026-07-29T10:40:17.2745026+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 239.1s / 1,262,158 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 358,051,000 | 2026-07-29T10:40:17.2881719+00:00 | 112.4ms | broker/backlog (no scale or GC event) | - | - | 239.1s / 1,262,158 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 358,052,000 | 2026-07-29T10:40:17.2903528+00:00 | 110.2ms | broker/backlog (no scale or GC event) | - | - | 239.1s / 1,262,158 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 358,061,000 | 2026-07-29T10:40:17.2991844+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 239.1s / 1,262,158 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 358,062,000 | 2026-07-29T10:40:17.3044897+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 239.1s / 1,262,158 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 358,081,000 | 2026-07-29T10:40:17.3319413+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 239.1s / 1,262,158 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 358,082,000 | 2026-07-29T10:40:17.3332663+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 239.1s / 1,262,158 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 358,091,000 | 2026-07-29T10:40:17.3465501+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 239.1s / 1,262,158 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 358,101,000 | 2026-07-29T10:40:17.3628736+00:00 | 100.7ms | broker/backlog (no scale or GC event) | - | - | 239.1s / 1,262,158 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 581,091,000 | 2026-07-29T10:42:43.9718237+00:00 | 108.6ms | broker/backlog (no scale or GC event) | - | - | 385.2s / 861,293 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 581,092,000 | 2026-07-29T10:42:43.975196+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 385.2s / 861,293 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 581,101,000 | 2026-07-29T10:42:43.9953286+00:00 | 113.2ms | broker/backlog (no scale or GC event) | - | - | 385.2s / 861,293 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 581,102,000 | 2026-07-29T10:42:43.9958602+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 385.2s / 861,293 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 419,773,000 | 2026-07-29T10:55:32.3953102+00:00 | 219.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 253.1s / 1,378,247 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 419,779,000 | 2026-07-29T10:55:32.3974289+00:00 | 219.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 253.1s / 1,378,247 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 419,780,000 | 2026-07-29T10:55:32.3976605+00:00 | 218.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 253.1s / 1,378,247 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 419,786,000 | 2026-07-29T10:55:32.3997515+00:00 | 218.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 253.1s / 1,378,247 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 419,789,000 | 2026-07-29T10:55:32.400625+00:00 | 217.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 253.1s / 1,378,247 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 419,790,000 | 2026-07-29T10:55:32.4012268+00:00 | 218.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 253.1s / 1,378,247 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 419,791,000 | 2026-07-29T10:55:32.4018402+00:00 | 216.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 253.1s / 1,378,247 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 419,792,000 | 2026-07-29T10:55:32.4020572+00:00 | 216.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 253.1s / 1,378,247 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 419,793,000 | 2026-07-29T10:55:32.4022705+00:00 | 216.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 253.1s / 1,378,247 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 419,794,000 | 2026-07-29T10:55:32.4024751+00:00 | 216.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 253.1s / 1,378,247 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 419,795,000 | 2026-07-29T10:55:32.4038199+00:00 | 211.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 253.1s / 1,378,247 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 586,003,000 | 2026-07-29T10:57:00.3054934+00:00 | 220.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 341.2s / 1,993,094 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 586,006,000 | 2026-07-29T10:57:00.3086904+00:00 | 219.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 341.2s / 1,993,094 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 586,007,000 | 2026-07-29T10:57:00.3099604+00:00 | 221.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 341.2s / 1,993,094 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 586,008,000 | 2026-07-29T10:57:00.310156+00:00 | 218.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 341.2s / 1,993,094 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 586,009,000 | 2026-07-29T10:57:00.3115612+00:00 | 216.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 341.2s / 1,993,094 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 586,010,000 | 2026-07-29T10:57:00.3117785+00:00 | 219.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 341.2s / 1,993,094 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 586,011,000 | 2026-07-29T10:57:00.3119923+00:00 | 219.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 341.2s / 1,993,094 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 586,012,000 | 2026-07-29T10:57:00.31232+00:00 | 218.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 341.2s / 1,993,094 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 586,013,000 | 2026-07-29T10:57:00.3127777+00:00 | 218.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 341.2s / 1,993,094 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 586,014,000 | 2026-07-29T10:57:00.3129785+00:00 | 218.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 341.2s / 1,993,094 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 586,015,000 | 2026-07-29T10:57:00.3133012+00:00 | 217.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 341.2s / 1,993,094 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 586,016,000 | 2026-07-29T10:57:00.3137614+00:00 | 217.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 341.2s / 1,993,094 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 586,017,000 | 2026-07-29T10:57:00.3139661+00:00 | 217.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 341.2s / 1,993,094 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 586,018,000 | 2026-07-29T10:57:00.3144135+00:00 | 216.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 341.2s / 1,993,094 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 586,019,000 | 2026-07-29T10:57:00.314758+00:00 | 216.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 341.2s / 1,993,094 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 588,143,000 | 2026-07-29T10:57:01.4703555+00:00 | 223.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 342.2s / 1,722,840 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 588,149,000 | 2026-07-29T10:57:01.4724714+00:00 | 222.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 342.2s / 1,722,840 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 588,150,000 | 2026-07-29T10:57:01.4726917+00:00 | 223.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 342.2s / 1,722,840 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 588,153,000 | 2026-07-29T10:57:01.4736846+00:00 | 222.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 342.2s / 1,722,840 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 588,154,000 | 2026-07-29T10:57:01.474136+00:00 | 224.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 342.2s / 1,722,840 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 588,156,000 | 2026-07-29T10:57:01.4748927+00:00 | 224.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 342.2s / 1,722,840 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 588,157,000 | 2026-07-29T10:57:01.4752273+00:00 | 223.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 342.2s / 1,722,840 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 588,158,000 | 2026-07-29T10:57:01.4755541+00:00 | 219.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 342.2s / 1,722,840 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 588,160,000 | 2026-07-29T10:57:01.4762839+00:00 | 222.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 342.2s / 1,722,840 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 588,161,000 | 2026-07-29T10:57:01.476737+00:00 | 220.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 342.2s / 1,722,840 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 588,162,000 | 2026-07-29T10:57:01.4769376+00:00 | 220.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 342.2s / 1,722,840 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 588,163,000 | 2026-07-29T10:57:01.477255+00:00 | 221.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 342.2s / 1,722,840 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 588,164,000 | 2026-07-29T10:57:01.477575+00:00 | 221.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 342.2s / 1,722,840 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 588,165,000 | 2026-07-29T10:57:01.4779098+00:00 | 220.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 342.2s / 1,722,840 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 588,166,000 | 2026-07-29T10:57:01.4782339+00:00 | 220.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 342.2s / 1,722,840 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 664,423,000 | 2026-07-29T10:57:37.761876+00:00 | 224.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 378.2s / 1,684,195 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 664,429,000 | 2026-07-29T10:57:37.7638869+00:00 | 231.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 378.2s / 1,684,195 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 664,430,000 | 2026-07-29T10:57:37.764097+00:00 | 231.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 378.2s / 1,684,195 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 664,433,000 | 2026-07-29T10:57:37.7653521+00:00 | 232.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 378.2s / 1,684,195 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 664,434,000 | 2026-07-29T10:57:37.765749+00:00 | 235.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 378.2s / 1,684,195 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 664,436,000 | 2026-07-29T10:57:37.7662816+00:00 | 235.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 378.2s / 1,684,195 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 664,439,000 | 2026-07-29T10:57:37.7684194+00:00 | 230.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 378.2s / 1,684,195 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 664,440,000 | 2026-07-29T10:57:37.7687748+00:00 | 232.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 378.2s / 1,684,195 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 664,442,000 | 2026-07-29T10:57:37.7691948+00:00 | 231.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 378.2s / 1,684,195 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 664,443,000 | 2026-07-29T10:57:37.7694115+00:00 | 229.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 378.2s / 1,684,195 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 664,444,000 | 2026-07-29T10:57:37.7696282+00:00 | 232.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 378.2s / 1,684,195 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 664,445,000 | 2026-07-29T10:57:37.7701015+00:00 | 225.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 378.2s / 1,684,195 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 664,446,000 | 2026-07-29T10:57:37.7708223+00:00 | 232.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 378.2s / 1,684,195 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 664,447,000 | 2026-07-29T10:57:37.7710368+00:00 | 235.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 378.2s / 1,684,195 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 664,448,000 | 2026-07-29T10:57:37.7712411+00:00 | 227.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 378.2s / 1,684,195 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 664,449,000 | 2026-07-29T10:57:37.7714478+00:00 | 228.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 378.2s / 1,684,195 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,709,000 | 2026-07-29T10:58:05.8153862+00:00 | 216.2ms | broker/backlog (no scale or GC event) | - | - | 406.2s / 1,531,844 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,710,000 | 2026-07-29T10:58:05.8156593+00:00 | 215.1ms | broker/backlog (no scale or GC event) | - | - | 406.2s / 1,531,844 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,713,000 | 2026-07-29T10:58:05.8169655+00:00 | 214.6ms | broker/backlog (no scale or GC event) | - | - | 406.2s / 1,531,844 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,716,000 | 2026-07-29T10:58:05.8179155+00:00 | 216.4ms | broker/backlog (no scale or GC event) | - | - | 406.2s / 1,531,844 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,717,000 | 2026-07-29T10:58:05.8183813+00:00 | 214.8ms | broker/backlog (no scale or GC event) | - | - | 406.2s / 1,531,844 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,719,000 | 2026-07-29T10:58:05.8210765+00:00 | 213.3ms | broker/backlog (no scale or GC event) | - | - | 406.2s / 1,531,844 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,720,000 | 2026-07-29T10:58:05.8215048+00:00 | 213.3ms | broker/backlog (no scale or GC event) | - | - | 406.2s / 1,531,844 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,721,000 | 2026-07-29T10:58:05.8216439+00:00 | 212.7ms | broker/backlog (no scale or GC event) | - | - | 406.2s / 1,531,844 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,722,000 | 2026-07-29T10:58:05.8218513+00:00 | 212.5ms | broker/backlog (no scale or GC event) | - | - | 406.2s / 1,531,844 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,723,000 | 2026-07-29T10:58:05.8223254+00:00 | 212.0ms | broker/backlog (no scale or GC event) | - | - | 406.2s / 1,531,844 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,724,000 | 2026-07-29T10:58:05.8225366+00:00 | 212.0ms | broker/backlog (no scale or GC event) | - | - | 406.2s / 1,531,844 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,725,000 | 2026-07-29T10:58:05.8228853+00:00 | 212.6ms | broker/backlog (no scale or GC event) | - | - | 406.2s / 1,531,844 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,726,000 | 2026-07-29T10:58:05.8233584+00:00 | 212.1ms | broker/backlog (no scale or GC event) | - | - | 406.2s / 1,531,844 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,727,000 | 2026-07-29T10:58:05.8237057+00:00 | 212.4ms | broker/backlog (no scale or GC event) | - | - | 406.2s / 1,531,844 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 784,066,000 | 2026-07-29T10:58:42.4251548+00:00 | 218.7ms | broker/backlog (no scale or GC event) | - | - | 443.3s / 1,514,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 784,067,000 | 2026-07-29T10:58:42.4254475+00:00 | 218.5ms | broker/backlog (no scale or GC event) | - | - | 443.3s / 1,514,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 784,069,000 | 2026-07-29T10:58:42.4262079+00:00 | 212.9ms | broker/backlog (no scale or GC event) | - | - | 443.3s / 1,514,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 784,070,000 | 2026-07-29T10:58:42.4275678+00:00 | 216.3ms | broker/backlog (no scale or GC event) | - | - | 443.3s / 1,514,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 784,071,000 | 2026-07-29T10:58:42.428102+00:00 | 215.7ms | broker/backlog (no scale or GC event) | - | - | 443.3s / 1,514,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 784,072,000 | 2026-07-29T10:58:42.42859+00:00 | 215.3ms | broker/backlog (no scale or GC event) | - | - | 443.3s / 1,514,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 784,073,000 | 2026-07-29T10:58:42.4288561+00:00 | 212.6ms | broker/backlog (no scale or GC event) | - | - | 443.3s / 1,514,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 784,074,000 | 2026-07-29T10:58:42.429129+00:00 | 214.8ms | broker/backlog (no scale or GC event) | - | - | 443.3s / 1,514,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 784,076,000 | 2026-07-29T10:58:42.429878+00:00 | 214.0ms | broker/backlog (no scale or GC event) | - | - | 443.3s / 1,514,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 784,077,000 | 2026-07-29T10:58:42.4308044+00:00 | 213.1ms | broker/backlog (no scale or GC event) | - | - | 443.3s / 1,514,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 784,078,000 | 2026-07-29T10:58:42.4310641+00:00 | 212.8ms | broker/backlog (no scale or GC event) | - | - | 443.3s / 1,514,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 784,079,000 | 2026-07-29T10:58:42.4314134+00:00 | 212.5ms | broker/backlog (no scale or GC event) | - | - | 443.3s / 1,514,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 784,080,000 | 2026-07-29T10:58:42.4316323+00:00 | 212.3ms | broker/backlog (no scale or GC event) | - | - | 443.3s / 1,514,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,179,000 | 2026-07-29T10:59:00.9584779+00:00 | 223.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 461.3s / 1,179,830 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,180,000 | 2026-07-29T10:59:00.9588472+00:00 | 224.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 461.3s / 1,179,830 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,181,000 | 2026-07-29T10:59:00.9602742+00:00 | 226.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 461.3s / 1,179,830 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,182,000 | 2026-07-29T10:59:00.9606408+00:00 | 226.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 461.3s / 1,179,830 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,183,000 | 2026-07-29T10:59:00.9610128+00:00 | 224.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 461.3s / 1,179,830 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,184,000 | 2026-07-29T10:59:00.9612393+00:00 | 226.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 461.3s / 1,179,830 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,185,000 | 2026-07-29T10:59:00.9615982+00:00 | 225.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 461.3s / 1,179,830 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,186,000 | 2026-07-29T10:59:00.9619599+00:00 | 226.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 461.3s / 1,179,830 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,187,000 | 2026-07-29T10:59:00.9624388+00:00 | 225.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 461.3s / 1,179,830 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,188,000 | 2026-07-29T10:59:00.9628074+00:00 | 224.3ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 461.3s / 1,179,830 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,189,000 | 2026-07-29T10:59:00.9631518+00:00 | 224.3ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 461.3s / 1,179,830 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,190,000 | 2026-07-29T10:59:00.9633621+00:00 | 224.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 461.3s / 1,179,830 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,191,000 | 2026-07-29T10:59:00.9637238+00:00 | 224.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 461.3s / 1,179,830 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,192,000 | 2026-07-29T10:59:00.9640686+00:00 | 224.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 461.3s / 1,179,830 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,193,000 | 2026-07-29T10:59:00.9644285+00:00 | 223.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 461.3s / 1,179,830 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,194,000 | 2026-07-29T10:59:00.9649334+00:00 | 224.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 461.3s / 1,179,830 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,195,000 | 2026-07-29T10:59:00.9652765+00:00 | 224.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 461.3s / 1,179,830 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,340,000 | 2026-07-29T10:59:07.1731995+00:00 | 216.5ms | broker/backlog (no scale or GC event) | - | - | 467.3s / 1,429,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,343,000 | 2026-07-29T10:59:07.1758628+00:00 | 215.7ms | broker/backlog (no scale or GC event) | - | - | 467.3s / 1,429,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,345,000 | 2026-07-29T10:59:07.1773462+00:00 | 213.7ms | broker/backlog (no scale or GC event) | - | - | 467.3s / 1,429,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,346,000 | 2026-07-29T10:59:07.177706+00:00 | 219.0ms | broker/backlog (no scale or GC event) | - | - | 467.3s / 1,429,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,347,000 | 2026-07-29T10:59:07.177938+00:00 | 218.7ms | broker/backlog (no scale or GC event) | - | - | 467.3s / 1,429,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,348,000 | 2026-07-29T10:59:07.1792262+00:00 | 213.0ms | broker/backlog (no scale or GC event) | - | - | 467.3s / 1,429,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,349,000 | 2026-07-29T10:59:07.1795781+00:00 | 213.0ms | broker/backlog (no scale or GC event) | - | - | 467.3s / 1,429,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,350,000 | 2026-07-29T10:59:07.1798023+00:00 | 215.8ms | broker/backlog (no scale or GC event) | - | - | 467.3s / 1,429,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,351,000 | 2026-07-29T10:59:07.1801727+00:00 | 216.5ms | broker/backlog (no scale or GC event) | - | - | 467.3s / 1,429,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,352,000 | 2026-07-29T10:59:07.1806455+00:00 | 216.0ms | broker/backlog (no scale or GC event) | - | - | 467.3s / 1,429,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,353,000 | 2026-07-29T10:59:07.1808483+00:00 | 211.7ms | broker/backlog (no scale or GC event) | - | - | 467.3s / 1,429,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,354,000 | 2026-07-29T10:59:07.1813156+00:00 | 216.7ms | broker/backlog (no scale or GC event) | - | - | 467.3s / 1,429,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,355,000 | 2026-07-29T10:59:07.1816688+00:00 | 212.8ms | broker/backlog (no scale or GC event) | - | - | 467.3s / 1,429,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,356,000 | 2026-07-29T10:59:07.1818689+00:00 | 216.1ms | broker/backlog (no scale or GC event) | - | - | 467.3s / 1,429,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,357,000 | 2026-07-29T10:59:07.182081+00:00 | 215.3ms | broker/backlog (no scale or GC event) | - | - | 467.3s / 1,429,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,358,000 | 2026-07-29T10:59:07.1825564+00:00 | 211.9ms | broker/backlog (no scale or GC event) | - | - | 467.3s / 1,429,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 851,667,000 | 2026-07-29T10:59:22.3667647+00:00 | 215.0ms | broker/backlog (no scale or GC event) | - | - | 483.3s / 1,394,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 851,668,000 | 2026-07-29T10:59:22.3669767+00:00 | 215.5ms | broker/backlog (no scale or GC event) | - | - | 483.3s / 1,394,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 851,675,000 | 2026-07-29T10:59:22.3708681+00:00 | 214.3ms | broker/backlog (no scale or GC event) | - | - | 483.3s / 1,394,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 851,677,000 | 2026-07-29T10:59:22.3718219+00:00 | 216.2ms | broker/backlog (no scale or GC event) | - | - | 483.3s / 1,394,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 851,685,000 | 2026-07-29T10:59:22.3759291+00:00 | 210.9ms | broker/backlog (no scale or GC event) | - | - | 483.3s / 1,394,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 851,687,000 | 2026-07-29T10:59:22.3770056+00:00 | 212.3ms | broker/backlog (no scale or GC event) | - | - | 483.3s / 1,394,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 851,691,000 | 2026-07-29T10:59:22.3784944+00:00 | 205.8ms | broker/backlog (no scale or GC event) | - | - | 483.3s / 1,394,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 851,692,000 | 2026-07-29T10:59:22.3788651+00:00 | 205.4ms | broker/backlog (no scale or GC event) | - | - | 483.3s / 1,394,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 851,695,000 | 2026-07-29T10:59:22.3798867+00:00 | 206.9ms | broker/backlog (no scale or GC event) | - | - | 483.3s / 1,394,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 851,696,000 | 2026-07-29T10:59:22.3802129+00:00 | 206.6ms | broker/backlog (no scale or GC event) | - | - | 483.3s / 1,394,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 851,697,000 | 2026-07-29T10:59:22.3814006+00:00 | 208.0ms | broker/backlog (no scale or GC event) | - | - | 483.3s / 1,394,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 851,698,000 | 2026-07-29T10:59:22.3932925+00:00 | 196.7ms | broker/backlog (no scale or GC event) | - | - | 483.3s / 1,394,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 919,280,000 | 2026-07-29T11:00:03.4970007+00:00 | 220.5ms | broker/backlog (no scale or GC event) | - | - | 524.3s / 1,481,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 919,283,000 | 2026-07-29T11:00:03.4994357+00:00 | 221.7ms | broker/backlog (no scale or GC event) | - | - | 524.3s / 1,481,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 919,284,000 | 2026-07-29T11:00:03.4999067+00:00 | 220.9ms | broker/backlog (no scale or GC event) | - | - | 524.3s / 1,481,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 919,285,000 | 2026-07-29T11:00:03.5002171+00:00 | 220.5ms | broker/backlog (no scale or GC event) | - | - | 524.3s / 1,481,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 919,286,000 | 2026-07-29T11:00:03.5015849+00:00 | 219.2ms | broker/backlog (no scale or GC event) | - | - | 524.3s / 1,481,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 919,287,000 | 2026-07-29T11:00:03.5018805+00:00 | 218.9ms | broker/backlog (no scale or GC event) | - | - | 524.3s / 1,481,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 919,288,000 | 2026-07-29T11:00:03.5032086+00:00 | 217.6ms | broker/backlog (no scale or GC event) | - | - | 524.3s / 1,481,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 919,289,000 | 2026-07-29T11:00:03.5035905+00:00 | 217.2ms | broker/backlog (no scale or GC event) | - | - | 524.3s / 1,481,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 919,291,000 | 2026-07-29T11:00:03.5040999+00:00 | 216.7ms | broker/backlog (no scale or GC event) | - | - | 524.3s / 1,481,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 919,292,000 | 2026-07-29T11:00:03.5055408+00:00 | 215.2ms | broker/backlog (no scale or GC event) | - | - | 524.3s / 1,481,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 919,293,000 | 2026-07-29T11:00:03.5057992+00:00 | 215.0ms | broker/backlog (no scale or GC event) | - | - | 524.3s / 1,481,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 919,294,000 | 2026-07-29T11:00:03.5064445+00:00 | 216.9ms | broker/backlog (no scale or GC event) | - | - | 524.3s / 1,481,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 919,296,000 | 2026-07-29T11:00:03.5070737+00:00 | 216.2ms | broker/backlog (no scale or GC event) | - | - | 524.3s / 1,481,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 919,297,000 | 2026-07-29T11:00:03.5073193+00:00 | 215.2ms | broker/backlog (no scale or GC event) | - | - | 524.3s / 1,481,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 919,298,000 | 2026-07-29T11:00:03.5078283+00:00 | 215.3ms | broker/backlog (no scale or GC event) | - | - | 524.3s / 1,481,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 954,457,000 | 2026-07-29T11:00:23.8624115+00:00 | 223.8ms | broker/backlog (no scale or GC event) | - | - | 544.3s / 1,209,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 954,461,000 | 2026-07-29T11:00:23.8659686+00:00 | 224.5ms | broker/backlog (no scale or GC event) | - | - | 544.3s / 1,209,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 954,462,000 | 2026-07-29T11:00:23.8661881+00:00 | 224.3ms | broker/backlog (no scale or GC event) | - | - | 544.3s / 1,209,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 954,463,000 | 2026-07-29T11:00:23.8663999+00:00 | 224.1ms | broker/backlog (no scale or GC event) | - | - | 544.3s / 1,209,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 954,464,000 | 2026-07-29T11:00:23.8666238+00:00 | 224.0ms | broker/backlog (no scale or GC event) | - | - | 544.3s / 1,209,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 954,465,000 | 2026-07-29T11:00:23.8669489+00:00 | 222.0ms | broker/backlog (no scale or GC event) | - | - | 544.3s / 1,209,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 954,466,000 | 2026-07-29T11:00:23.8671832+00:00 | 223.4ms | broker/backlog (no scale or GC event) | - | - | 544.3s / 1,209,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 954,467,000 | 2026-07-29T11:00:23.8684431+00:00 | 224.3ms | broker/backlog (no scale or GC event) | - | - | 544.3s / 1,209,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 954,468,000 | 2026-07-29T11:00:23.8688606+00:00 | 221.7ms | broker/backlog (no scale or GC event) | - | - | 544.3s / 1,209,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 954,469,000 | 2026-07-29T11:00:23.869162+00:00 | 221.4ms | broker/backlog (no scale or GC event) | - | - | 544.3s / 1,209,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 954,470,000 | 2026-07-29T11:00:23.8693762+00:00 | 221.2ms | broker/backlog (no scale or GC event) | - | - | 544.3s / 1,209,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 954,471,000 | 2026-07-29T11:00:23.8696111+00:00 | 221.0ms | broker/backlog (no scale or GC event) | - | - | 544.3s / 1,209,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 954,472,000 | 2026-07-29T11:00:23.8698392+00:00 | 220.7ms | broker/backlog (no scale or GC event) | - | - | 544.3s / 1,209,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 954,473,000 | 2026-07-29T11:00:23.8706178+00:00 | 222.1ms | broker/backlog (no scale or GC event) | - | - | 544.3s / 1,209,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 954,474,000 | 2026-07-29T11:00:23.8711176+00:00 | 223.4ms | broker/backlog (no scale or GC event) | - | - | 544.3s / 1,209,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 954,475,000 | 2026-07-29T11:00:23.871346+00:00 | 221.4ms | broker/backlog (no scale or GC event) | - | - | 544.3s / 1,209,054 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,016,935,000 | 2026-07-29T11:01:02.7388289+00:00 | 221.4ms | broker/backlog (no scale or GC event) | - | - | 583.4s / 1,216,653 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,016,937,000 | 2026-07-29T11:01:02.7396988+00:00 | 218.2ms | broker/backlog (no scale or GC event) | - | - | 583.4s / 1,216,653 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,016,938,000 | 2026-07-29T11:01:02.7407267+00:00 | 221.8ms | broker/backlog (no scale or GC event) | - | - | 583.4s / 1,216,653 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,016,940,000 | 2026-07-29T11:01:02.7413728+00:00 | 221.1ms | broker/backlog (no scale or GC event) | - | - | 583.4s / 1,216,653 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,016,941,000 | 2026-07-29T11:01:02.741706+00:00 | 220.8ms | broker/backlog (no scale or GC event) | - | - | 583.4s / 1,216,653 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,016,942,000 | 2026-07-29T11:01:02.7431163+00:00 | 219.4ms | broker/backlog (no scale or GC event) | - | - | 583.4s / 1,216,653 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,016,943,000 | 2026-07-29T11:01:02.743549+00:00 | 215.7ms | broker/backlog (no scale or GC event) | - | - | 583.4s / 1,216,653 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,016,944,000 | 2026-07-29T11:01:02.7450879+00:00 | 218.2ms | broker/backlog (no scale or GC event) | - | - | 583.4s / 1,216,653 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,016,945,000 | 2026-07-29T11:01:02.7455522+00:00 | 217.0ms | broker/backlog (no scale or GC event) | - | - | 583.4s / 1,216,653 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,016,946,000 | 2026-07-29T11:01:02.7458602+00:00 | 217.4ms | broker/backlog (no scale or GC event) | - | - | 583.4s / 1,216,653 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,016,948,000 | 2026-07-29T11:01:02.7464498+00:00 | 216.1ms | broker/backlog (no scale or GC event) | - | - | 583.4s / 1,216,653 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,016,949,000 | 2026-07-29T11:01:02.7467531+00:00 | 214.2ms | broker/backlog (no scale or GC event) | - | - | 583.4s / 1,216,653 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,016,950,000 | 2026-07-29T11:01:02.7477322+00:00 | 216.9ms | broker/backlog (no scale or GC event) | - | - | 583.4s / 1,216,653 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,016,951,000 | 2026-07-29T11:01:02.748138+00:00 | 215.3ms | broker/backlog (no scale or GC event) | - | - | 583.4s / 1,216,653 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,016,952,000 | 2026-07-29T11:01:02.7483837+00:00 | 215.1ms | broker/backlog (no scale or GC event) | - | - | 583.4s / 1,216,653 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,804,000 | 2026-07-29T11:01:09.5262841+00:00 | 218.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 590.4s / 1,208,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,805,000 | 2026-07-29T11:01:09.5288689+00:00 | 217.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 590.4s / 1,208,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,806,000 | 2026-07-29T11:01:09.5292712+00:00 | 217.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 590.4s / 1,208,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,807,000 | 2026-07-29T11:01:09.5296617+00:00 | 217.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 590.4s / 1,208,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,808,000 | 2026-07-29T11:01:09.530043+00:00 | 216.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 590.4s / 1,208,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,809,000 | 2026-07-29T11:01:09.5304217+00:00 | 212.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 590.4s / 1,208,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,810,000 | 2026-07-29T11:01:09.5333849+00:00 | 213.3ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 590.4s / 1,208,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,811,000 | 2026-07-29T11:01:09.5341588+00:00 | 212.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 590.4s / 1,208,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,812,000 | 2026-07-29T11:01:09.5345311+00:00 | 212.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 590.4s / 1,208,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,813,000 | 2026-07-29T11:01:09.5348556+00:00 | 211.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 590.4s / 1,208,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,814,000 | 2026-07-29T11:01:09.5350978+00:00 | 212.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 590.4s / 1,208,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,815,000 | 2026-07-29T11:01:09.5353484+00:00 | 211.3ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 590.4s / 1,208,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,816,000 | 2026-07-29T11:01:09.5362154+00:00 | 211.3ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 590.4s / 1,208,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,817,000 | 2026-07-29T11:01:09.536488+00:00 | 212.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 590.4s / 1,208,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,818,000 | 2026-07-29T11:01:09.5371437+00:00 | 215.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 590.4s / 1,208,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,072,360,000 | 2026-07-29T11:01:37.9337593+00:00 | 224.9ms | broker/backlog (no scale or GC event) | - | - | 618.4s / 1,202,892 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,072,363,000 | 2026-07-29T11:01:37.9367624+00:00 | 224.4ms | broker/backlog (no scale or GC event) | - | - | 618.4s / 1,202,892 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,072,365,000 | 2026-07-29T11:01:37.9385351+00:00 | 221.4ms | broker/backlog (no scale or GC event) | - | - | 618.4s / 1,202,892 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,072,366,000 | 2026-07-29T11:01:37.9398871+00:00 | 221.2ms | broker/backlog (no scale or GC event) | - | - | 618.4s / 1,202,892 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,072,367,000 | 2026-07-29T11:01:37.9423059+00:00 | 224.0ms | broker/backlog (no scale or GC event) | - | - | 618.4s / 1,202,892 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,072,368,000 | 2026-07-29T11:01:37.9437001+00:00 | 217.8ms | broker/backlog (no scale or GC event) | - | - | 618.4s / 1,202,892 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,072,369,000 | 2026-07-29T11:01:37.9441798+00:00 | 217.8ms | broker/backlog (no scale or GC event) | - | - | 618.4s / 1,202,892 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,072,370,000 | 2026-07-29T11:01:37.9445383+00:00 | 221.0ms | broker/backlog (no scale or GC event) | - | - | 618.4s / 1,202,892 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,072,371,000 | 2026-07-29T11:01:37.9449679+00:00 | 221.3ms | broker/backlog (no scale or GC event) | - | - | 618.4s / 1,202,892 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,072,372,000 | 2026-07-29T11:01:37.9453925+00:00 | 220.9ms | broker/backlog (no scale or GC event) | - | - | 618.4s / 1,202,892 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,072,373,000 | 2026-07-29T11:01:37.9460076+00:00 | 218.4ms | broker/backlog (no scale or GC event) | - | - | 618.4s / 1,202,892 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,072,374,000 | 2026-07-29T11:01:37.9464299+00:00 | 219.9ms | broker/backlog (no scale or GC event) | - | - | 618.4s / 1,202,892 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,072,375,000 | 2026-07-29T11:01:37.9468761+00:00 | 217.5ms | broker/backlog (no scale or GC event) | - | - | 618.4s / 1,202,892 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,072,376,000 | 2026-07-29T11:01:37.9471212+00:00 | 219.2ms | broker/backlog (no scale or GC event) | - | - | 618.4s / 1,202,892 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,072,378,000 | 2026-07-29T11:01:37.9479869+00:00 | 216.4ms | broker/backlog (no scale or GC event) | - | - | 618.4s / 1,202,892 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,078,707,000 | 2026-07-29T11:01:42.0250389+00:00 | 220.4ms | broker/backlog (no scale or GC event) | - | - | 622.4s / 1,296,272 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,078,708,000 | 2026-07-29T11:01:42.0254635+00:00 | 220.6ms | broker/backlog (no scale or GC event) | - | - | 622.4s / 1,296,272 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,078,709,000 | 2026-07-29T11:01:42.0269479+00:00 | 220.4ms | broker/backlog (no scale or GC event) | - | - | 622.4s / 1,296,272 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,078,711,000 | 2026-07-29T11:01:42.0309243+00:00 | 221.5ms | broker/backlog (no scale or GC event) | - | - | 622.4s / 1,296,272 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,078,712,000 | 2026-07-29T11:01:42.0311687+00:00 | 221.3ms | broker/backlog (no scale or GC event) | - | - | 622.4s / 1,296,272 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,078,713,000 | 2026-07-29T11:01:42.031417+00:00 | 216.4ms | broker/backlog (no scale or GC event) | - | - | 622.4s / 1,296,272 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,078,714,000 | 2026-07-29T11:01:42.0317417+00:00 | 223.3ms | broker/backlog (no scale or GC event) | - | - | 622.4s / 1,296,272 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,078,715,000 | 2026-07-29T11:01:42.0321256+00:00 | 216.1ms | broker/backlog (no scale or GC event) | - | - | 622.4s / 1,296,272 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,078,716,000 | 2026-07-29T11:01:42.032681+00:00 | 222.5ms | broker/backlog (no scale or GC event) | - | - | 622.4s / 1,296,272 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,078,717,000 | 2026-07-29T11:01:42.0333872+00:00 | 221.7ms | broker/backlog (no scale or GC event) | - | - | 622.4s / 1,296,272 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,078,718,000 | 2026-07-29T11:01:42.0336726+00:00 | 217.0ms | broker/backlog (no scale or GC event) | - | - | 622.4s / 1,296,272 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,078,719,000 | 2026-07-29T11:01:42.0339156+00:00 | 215.1ms | broker/backlog (no scale or GC event) | - | - | 622.4s / 1,296,272 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,078,720,000 | 2026-07-29T11:01:42.0341537+00:00 | 218.3ms | broker/backlog (no scale or GC event) | - | - | 622.4s / 1,296,272 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,078,721,000 | 2026-07-29T11:01:42.0343924+00:00 | 220.6ms | broker/backlog (no scale or GC event) | - | - | 622.4s / 1,296,272 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,078,722,000 | 2026-07-29T11:01:42.0349435+00:00 | 220.1ms | broker/backlog (no scale or GC event) | - | - | 622.4s / 1,296,272 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,199,735,000 | 2026-07-29T11:02:53.9074695+00:00 | 220.7ms | broker/backlog (no scale or GC event) | - | - | 694.4s / 1,380,170 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,199,737,000 | 2026-07-29T11:02:53.9082515+00:00 | 219.8ms | broker/backlog (no scale or GC event) | - | - | 694.4s / 1,380,170 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,199,738,000 | 2026-07-29T11:02:53.9085914+00:00 | 219.6ms | broker/backlog (no scale or GC event) | - | - | 694.4s / 1,380,170 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,199,745,000 | 2026-07-29T11:02:53.9123426+00:00 | 220.1ms | broker/backlog (no scale or GC event) | - | - | 694.4s / 1,380,170 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,199,747,000 | 2026-07-29T11:02:53.9140559+00:00 | 218.4ms | broker/backlog (no scale or GC event) | - | - | 694.4s / 1,380,170 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,199,749,000 | 2026-07-29T11:02:53.9158634+00:00 | 212.2ms | broker/backlog (no scale or GC event) | - | - | 694.4s / 1,380,170 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,199,750,000 | 2026-07-29T11:02:53.9160631+00:00 | 212.5ms | broker/backlog (no scale or GC event) | - | - | 694.4s / 1,380,170 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,199,751,000 | 2026-07-29T11:02:53.9174791+00:00 | 212.2ms | broker/backlog (no scale or GC event) | - | - | 694.4s / 1,380,170 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,199,752,000 | 2026-07-29T11:02:53.9177017+00:00 | 212.0ms | broker/backlog (no scale or GC event) | - | - | 694.4s / 1,380,170 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,199,753,000 | 2026-07-29T11:02:53.9179114+00:00 | 213.3ms | broker/backlog (no scale or GC event) | - | - | 694.4s / 1,380,170 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,199,754,000 | 2026-07-29T11:02:53.918524+00:00 | 213.9ms | broker/backlog (no scale or GC event) | - | - | 694.4s / 1,380,170 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,199,755,000 | 2026-07-29T11:02:53.9188915+00:00 | 214.5ms | broker/backlog (no scale or GC event) | - | - | 694.4s / 1,380,170 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,256,992,000 | 2026-07-29T11:03:27.9205773+00:00 | 214.9ms | broker/backlog (no scale or GC event) | - | - | 728.4s / 1,456,194 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,256,993,000 | 2026-07-29T11:03:27.9230592+00:00 | 213.8ms | broker/backlog (no scale or GC event) | - | - | 728.4s / 1,456,194 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,256,994,000 | 2026-07-29T11:03:27.9244493+00:00 | 215.3ms | broker/backlog (no scale or GC event) | - | - | 728.4s / 1,456,194 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,256,996,000 | 2026-07-29T11:03:27.9250416+00:00 | 214.7ms | broker/backlog (no scale or GC event) | - | - | 728.4s / 1,456,194 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,256,998,000 | 2026-07-29T11:03:27.9267291+00:00 | 209.4ms | broker/backlog (no scale or GC event) | - | - | 728.4s / 1,456,194 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,256,999,000 | 2026-07-29T11:03:27.9275759+00:00 | 210.6ms | broker/backlog (no scale or GC event) | - | - | 728.4s / 1,456,194 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,257,000,000 | 2026-07-29T11:03:27.928005+00:00 | 211.8ms | broker/backlog (no scale or GC event) | - | - | 728.4s / 1,456,194 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,257,001,000 | 2026-07-29T11:03:27.9283341+00:00 | 211.4ms | broker/backlog (no scale or GC event) | - | - | 728.4s / 1,456,194 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,257,003,000 | 2026-07-29T11:03:27.9289143+00:00 | 209.2ms | broker/backlog (no scale or GC event) | - | - | 728.4s / 1,456,194 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,257,002,000 | 2026-07-29T11:03:27.9289745+00:00 | 210.7ms | broker/backlog (no scale or GC event) | - | - | 728.4s / 1,456,194 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,257,004,000 | 2026-07-29T11:03:27.9293396+00:00 | 211.7ms | broker/backlog (no scale or GC event) | - | - | 728.4s / 1,456,194 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,257,005,000 | 2026-07-29T11:03:27.9301254+00:00 | 210.9ms | broker/backlog (no scale or GC event) | - | - | 728.4s / 1,456,194 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,413,279,000 | 2026-07-29T11:05:09.1403767+00:00 | 219.3ms | broker/backlog (no scale or GC event) | - | - | 829.5s / 1,058,429 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,413,280,000 | 2026-07-29T11:05:09.1408711+00:00 | 217.8ms | broker/backlog (no scale or GC event) | - | - | 829.5s / 1,058,429 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,413,283,000 | 2026-07-29T11:05:09.1424081+00:00 | 217.3ms | broker/backlog (no scale or GC event) | - | - | 829.5s / 1,058,429 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,413,285,000 | 2026-07-29T11:05:09.1458733+00:00 | 213.8ms | broker/backlog (no scale or GC event) | - | - | 829.5s / 1,058,429 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,413,286,000 | 2026-07-29T11:05:09.1474015+00:00 | 216.8ms | broker/backlog (no scale or GC event) | - | - | 829.5s / 1,058,429 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,413,287,000 | 2026-07-29T11:05:09.1478371+00:00 | 216.9ms | broker/backlog (no scale or GC event) | - | - | 829.5s / 1,058,429 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,413,289,000 | 2026-07-29T11:05:09.14887+00:00 | 211.3ms | broker/backlog (no scale or GC event) | - | - | 829.5s / 1,058,429 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,413,290,000 | 2026-07-29T11:05:09.149684+00:00 | 214.9ms | broker/backlog (no scale or GC event) | - | - | 829.5s / 1,058,429 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,413,291,000 | 2026-07-29T11:05:09.1504344+00:00 | 213.8ms | broker/backlog (no scale or GC event) | - | - | 829.5s / 1,058,429 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,413,292,000 | 2026-07-29T11:05:09.1510073+00:00 | 213.2ms | broker/backlog (no scale or GC event) | - | - | 829.5s / 1,058,429 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,413,293,000 | 2026-07-29T11:05:09.1513739+00:00 | 212.8ms | broker/backlog (no scale or GC event) | - | - | 829.5s / 1,058,429 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,413,294,000 | 2026-07-29T11:05:09.1519675+00:00 | 212.8ms | broker/backlog (no scale or GC event) | - | - | 829.5s / 1,058,429 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*7,855 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.59x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent); comparison throughput is 1.30x.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.29 | 1279.04 | 1,041,130 | 1,052,382 | -0.6% | -0.06% | 992.90 | 1,041,130 | 0 | 1.34 |
| Dekaf | 1.63 | 1599.15 | 793,847 | 797,397 | +28.1% | +2.83% | 757.07 | 793,847 | 0 | 1.29 |
| Confluent | 2.30 | - | 701,327 | 704,925 | +1.6% | +0.24% | 668.84 | 701,327 | 0 | 1.62 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 242,490 | 269.43 | 970.94 KB |
| Dekaf | 2 | 245,619 | 272.90 | 990.37 KB |
| Dekaf | 3 | 240,227 | 266.91 | 963.82 KB |
| Dekaf (3conn) | 1 | 309,703 | 344.11 | 986.19 KB |
| Dekaf (3conn) | 2 | 323,049 | 358.94 | 991.30 KB |
| Dekaf (3conn) | 3 | 309,811 | 344.23 | 987.05 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-29T09:51:30.7462408+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 170,095 msg/s |
| Dekaf | 2026-07-29T09:51:48.7661097+00:00 | 3 | 16.0 MiB / 14.7 MiB | 197.6 MB/s | 0/0 | 300 | 18.0s / 566,472 msg/s |
| Dekaf | 2026-07-29T09:52:07.7881411+00:00 | 1 | 16.0 MiB / 15.2 MiB | 242.8 MB/s | 0/0 | 1,797 | 37.0s / 677,416 msg/s |
| Dekaf | 2026-07-29T09:52:25.8011519+00:00 | 1 | 14.0 MiB / 14.0 MiB | 263.6 MB/s | 1/0 | 3,981 | 55.0s / 722,263 msg/s |
| Dekaf | 2026-07-29T09:52:43.8104672+00:00 | 2 | 12.0 MiB / 8.9 MiB | 284.9 MB/s | 2/0 | 19,506 | 73.1s / 649,755 msg/s |
| Dekaf | 2026-07-29T09:53:01.8140592+00:00 | 2 | 10.0 MiB / 9.5 MiB | 285.9 MB/s | 3/0 | 24,014 | 91.1s / 661,835 msg/s |
| Dekaf | 2026-07-29T09:53:19.820072+00:00 | 3 | 8.0 MiB / 2.1 MiB | 289.3 MB/s | 4/0 | 5,274 | 109.1s / 742,633 msg/s |
| Dekaf | 2026-07-29T09:53:37.8260869+00:00 | 3 | 8.0 MiB / 4.6 MiB | 289.3 MB/s | 4/1 | 5,930 | 127.1s / 752,852 msg/s |
| Dekaf | 2026-07-29T09:53:56.8408784+00:00 | 1 | 8.0 MiB / 2.9 MiB | 293.0 MB/s | 4/1 | 11,899 | 146.1s / 693,024 msg/s |
| Dekaf | 2026-07-29T09:54:14.8451539+00:00 | 1 | 8.0 MiB / 1.8 MiB | 293.0 MB/s | 4/2 | 12,749 | 164.1s / 733,788 msg/s |
| Dekaf | 2026-07-29T09:54:32.8535415+00:00 | 2 | 8.0 MiB / 3.9 MiB | 308.4 MB/s | 4/2 | 37,701 | 182.1s / 843,215 msg/s |
| Dekaf | 2026-07-29T09:54:50.861644+00:00 | 2 | 8.0 MiB / 7.4 MiB | 314.1 MB/s | 4/3 | 40,122 | 200.1s / 660,047 msg/s |
| Dekaf | 2026-07-29T09:55:08.8690301+00:00 | 3 | 8.0 MiB / 1.9 MiB | 295.7 MB/s | 4/2 | 9,113 | 218.1s / 746,702 msg/s |
| Dekaf | 2026-07-29T09:55:26.8732363+00:00 | 3 | 8.0 MiB / 7.0 MiB | 299.9 MB/s | 4/2 | 9,881 | 236.1s / 757,718 msg/s |
| Dekaf | 2026-07-29T09:55:45.8833364+00:00 | 1 | 8.0 MiB / 8.0 MiB | 316.2 MB/s | 4/2 | 18,996 | 255.2s / 797,209 msg/s |
| Dekaf | 2026-07-29T09:56:03.8909086+00:00 | 1 | 8.0 MiB / 2.5 MiB | 316.2 MB/s | 4/2 | 20,219 | 273.2s / 727,910 msg/s |
| Dekaf | 2026-07-29T09:56:21.8960715+00:00 | 2 | 6.0 MiB / 1.8 MiB | 317.4 MB/s | 5/4 | 54,349 | 291.2s / 659,742 msg/s |
| Dekaf | 2026-07-29T09:56:39.9022013+00:00 | 2 | 7.0 MiB / 4.7 MiB | 317.4 MB/s | 5/5 | 57,168 | 309.2s / 717,725 msg/s |
| Dekaf | 2026-07-29T09:56:57.9090418+00:00 | 3 | 8.0 MiB / 3.7 MiB | 306.8 MB/s | 4/4 | 13,541 | 327.2s / 753,919 msg/s |
| Dekaf | 2026-07-29T09:57:15.9182371+00:00 | 3 | 8.0 MiB / 2.8 MiB | 306.8 MB/s | 4/4 | 13,845 | 345.2s / 729,290 msg/s |
| Dekaf | 2026-07-29T09:57:34.9288815+00:00 | 1 | 7.0 MiB / 2.7 MiB | 316.2 MB/s | 5/3 | 26,244 | 364.2s / 767,711 msg/s |
| Dekaf | 2026-07-29T09:57:52.9342545+00:00 | 1 | 7.0 MiB / 5.5 MiB | 316.9 MB/s | 5/3 | 28,327 | 382.2s / 886,977 msg/s |
| Dekaf | 2026-07-29T09:58:10.9417226+00:00 | 2 | 7.0 MiB / 5.4 MiB | 328.1 MB/s | 5/6 | 72,605 | 400.2s / 764,539 msg/s |
| Dekaf | 2026-07-29T09:58:28.9455702+00:00 | 2 | 7.0 MiB / 1.7 MiB | 328.1 MB/s | 5/6 | 76,735 | 418.3s / 736,598 msg/s |
| Dekaf | 2026-07-29T09:58:46.9529523+00:00 | 3 | 8.0 MiB / 4.6 MiB | 312.8 MB/s | 4/4 | 15,441 | 436.3s / 799,993 msg/s |
| Dekaf | 2026-07-29T09:59:04.9619747+00:00 | 3 | 8.0 MiB / 4.2 MiB | 312.8 MB/s | 4/4 | 15,767 | 454.3s / 828,860 msg/s |
| Dekaf | 2026-07-29T09:59:23.9704562+00:00 | 1 | 5.0 MiB / 4.3 MiB | 320.5 MB/s | 6/5 | 50,525 | 473.3s / 859,103 msg/s |
| Dekaf | 2026-07-29T09:59:41.9760426+00:00 | 1 | 6.0 MiB / 5.6 MiB | 320.5 MB/s | 6/6 | 54,072 | 491.3s / 836,462 msg/s |
| Dekaf | 2026-07-29T09:59:59.9844946+00:00 | 2 | 7.0 MiB / 4.7 MiB | 328.1 MB/s | 5/6 | 96,449 | 509.3s / 814,034 msg/s |
| Dekaf | 2026-07-29T10:00:17.9971699+00:00 | 2 | 6.0 MiB / 6.0 MiB | 333.5 MB/s | 6/6 | 101,773 | 527.3s / 818,759 msg/s |
| Dekaf | 2026-07-29T10:00:36.0080524+00:00 | 3 | 8.0 MiB / 3.4 MiB | 344.5 MB/s | 4/4 | 18,731 | 545.3s / 790,759 msg/s |
| Dekaf | 2026-07-29T10:00:54.0211958+00:00 | 3 | 8.0 MiB / 4.7 MiB | 344.5 MB/s | 4/4 | 19,280 | 563.3s / 783,838 msg/s |
| Dekaf | 2026-07-29T10:01:13.0329357+00:00 | 1 | 6.0 MiB / 5.7 MiB | 342.9 MB/s | 6/8 | 74,464 | 582.3s / 901,098 msg/s |
| Dekaf | 2026-07-29T10:01:31.0403266+00:00 | 1 | 6.0 MiB / 4.1 MiB | 342.9 MB/s | 6/8 | 78,590 | 600.3s / 952,686 msg/s |
| Dekaf | 2026-07-29T10:01:49.0516707+00:00 | 2 | 6.0 MiB / 5.8 MiB | 362.5 MB/s | 6/8 | 129,999 | 618.3s / 891,653 msg/s |
| Dekaf | 2026-07-29T10:02:07.0564806+00:00 | 2 | 6.0 MiB / 4.6 MiB | 362.5 MB/s | 6/9 | 135,273 | 636.4s / 945,349 msg/s |
| Dekaf | 2026-07-29T10:02:25.0654344+00:00 | 3 | 8.0 MiB / 3.0 MiB | 344.5 MB/s | 4/5 | 21,056 | 654.4s / 835,165 msg/s |
| Dekaf | 2026-07-29T10:02:43.0691181+00:00 | 3 | 8.0 MiB / 3.7 MiB | 344.5 MB/s | 4/5 | 21,708 | 672.4s / 908,678 msg/s |
| Dekaf | 2026-07-29T10:03:02.0749612+00:00 | 1 | 6.0 MiB / 3.6 MiB | 342.9 MB/s | 8/9 | 97,046 | 691.4s / 823,118 msg/s |
| Dekaf | 2026-07-29T10:03:20.0840259+00:00 | 1 | 6.0 MiB / 5.6 MiB | 342.9 MB/s | 8/9 | 99,900 | 709.4s / 883,696 msg/s |
| Dekaf | 2026-07-29T10:03:38.0957191+00:00 | 2 | 7.0 MiB / 6.5 MiB | 362.5 MB/s | 7/10 | 155,653 | 727.4s / 910,905 msg/s |
| Dekaf | 2026-07-29T10:03:56.1040945+00:00 | 2 | 6.0 MiB / 3.0 MiB | 362.5 MB/s | 7/10 | 159,236 | 745.4s / 943,092 msg/s |
| Dekaf | 2026-07-29T10:04:14.112057+00:00 | 3 | 8.0 MiB / 5.2 MiB | 344.5 MB/s | 4/5 | 23,399 | 763.4s / 864,319 msg/s |
| Dekaf | 2026-07-29T10:04:32.1202859+00:00 | 3 | 8.0 MiB / 2.8 MiB | 344.5 MB/s | 4/5 | 23,737 | 781.4s / 937,275 msg/s |
| Dekaf | 2026-07-29T10:04:51.1367538+00:00 | 1 | 6.0 MiB / 2.0 MiB | 347.4 MB/s | 10/10 | 121,635 | 800.4s / 898,635 msg/s |
| Dekaf | 2026-07-29T10:05:09.1455905+00:00 | 1 | 6.0 MiB / 4.4 MiB | 347.4 MB/s | 10/10 | 124,538 | 818.4s / 979,279 msg/s |
| Dekaf | 2026-07-29T10:05:27.1481131+00:00 | 2 | 5.0 MiB / 5.0 MiB | 377.0 MB/s | 9/11 | 186,504 | 836.4s / 865,402 msg/s |
| Dekaf | 2026-07-29T10:05:45.1590499+00:00 | 2 | 4.0 MiB / 1.8 MiB | 377.0 MB/s | 10/11 | 196,337 | 854.5s / 889,351 msg/s |
| Dekaf | 2026-07-29T10:06:03.1691188+00:00 | 3 | 10.0 MiB / 2.1 MiB | 356.2 MB/s | 5/5 | 24,917 | 872.5s / 983,809 msg/s |
| Dekaf | 2026-07-29T10:06:21.1810555+00:00 | 3 | 10.0 MiB / 3.6 MiB | 356.2 MB/s | 6/5 | 24,919 | 890.5s / 946,137 msg/s |
| Dekaf (3conn) | 2026-07-29T10:21:52.8055076+00:00 | 3 | 16.0 MiB / 1.5 MiB | 317.2 MB/s | 0/0 | 1,903 | 9.0s / 865,248 msg/s |
| Dekaf (3conn) | 2026-07-29T10:22:10.8151545+00:00 | 3 | 16.0 MiB / 5.1 MiB | 392.3 MB/s | 0/0 | 3,459 | 27.0s / 1,081,090 msg/s |
| Dekaf (3conn) | 2026-07-29T10:22:29.8230743+00:00 | 1 | 14.0 MiB / 2.4 MiB | 415.4 MB/s | 1/0 | 4,041 | 46.0s / 1,071,594 msg/s |
| Dekaf (3conn) | 2026-07-29T10:22:47.8332869+00:00 | 1 | 12.0 MiB / 7.5 MiB | 434.5 MB/s | 2/0 | 4,910 | 64.0s / 1,129,765 msg/s |
| Dekaf (3conn) | 2026-07-29T10:23:05.8421976+00:00 | 2 | 12.0 MiB / 11.1 MiB | 455.8 MB/s | 2/1 | 9,492 | 82.1s / 1,037,421 msg/s |
| Dekaf (3conn) | 2026-07-29T10:23:23.8526439+00:00 | 2 | 12.0 MiB / 4.1 MiB | 455.8 MB/s | 2/1 | 10,557 | 100.1s / 987,714 msg/s |
| Dekaf (3conn) | 2026-07-29T10:23:41.862291+00:00 | 3 | 12.0 MiB / 7.8 MiB | 446.3 MB/s | 2/2 | 9,470 | 118.1s / 1,116,980 msg/s |
| Dekaf (3conn) | 2026-07-29T10:23:59.8728261+00:00 | 3 | 12.0 MiB / 4.6 MiB | 446.3 MB/s | 2/2 | 10,058 | 136.1s / 930,679 msg/s |
| Dekaf (3conn) | 2026-07-29T10:24:18.8808861+00:00 | 1 | 12.0 MiB / 6.8 MiB | 434.5 MB/s | 2/2 | 9,448 | 155.1s / 1,111,873 msg/s |
| Dekaf (3conn) | 2026-07-29T10:24:36.8870232+00:00 | 1 | 10.0 MiB / 2.1 MiB | 434.5 MB/s | 3/2 | 10,667 | 173.1s / 803,577 msg/s |
| Dekaf (3conn) | 2026-07-29T10:24:54.8971007+00:00 | 2 | 12.0 MiB / 8.1 MiB | 460.1 MB/s | 2/3 | 15,459 | 191.2s / 1,135,698 msg/s |
| Dekaf (3conn) | 2026-07-29T10:25:12.9073061+00:00 | 2 | 12.0 MiB / 1.7 MiB | 460.1 MB/s | 2/3 | 16,051 | 209.2s / 1,090,500 msg/s |
| Dekaf (3conn) | 2026-07-29T10:25:30.9168039+00:00 | 3 | 12.0 MiB / 1.7 MiB | 446.3 MB/s | 2/2 | 12,341 | 227.2s / 1,076,625 msg/s |
| Dekaf (3conn) | 2026-07-29T10:25:48.926179+00:00 | 3 | 12.0 MiB / 3.1 MiB | 446.3 MB/s | 2/2 | 12,719 | 245.2s / 1,113,822 msg/s |
| Dekaf (3conn) | 2026-07-29T10:26:07.9316951+00:00 | 1 | 8.0 MiB / 2.8 MiB | 434.5 MB/s | 4/4 | 22,422 | 264.2s / 1,047,933 msg/s |
| Dekaf (3conn) | 2026-07-29T10:26:25.9358188+00:00 | 1 | 8.0 MiB / 4.9 MiB | 434.5 MB/s | 4/4 | 24,398 | 282.2s / 1,052,689 msg/s |
| Dekaf (3conn) | 2026-07-29T10:26:43.9450613+00:00 | 2 | 12.0 MiB / 3.7 MiB | 460.1 MB/s | 2/3 | 19,170 | 300.2s / 1,067,321 msg/s |
| Dekaf (3conn) | 2026-07-29T10:27:01.9565278+00:00 | 2 | 12.0 MiB / 7.0 MiB | 460.1 MB/s | 2/3 | 19,705 | 318.2s / 1,068,648 msg/s |
| Dekaf (3conn) | 2026-07-29T10:27:19.9780418+00:00 | 3 | 12.0 MiB / 2.3 MiB | 446.3 MB/s | 2/3 | 14,685 | 336.3s / 1,090,866 msg/s |
| Dekaf (3conn) | 2026-07-29T10:27:37.9898982+00:00 | 3 | 12.0 MiB / 6.6 MiB | 446.3 MB/s | 2/3 | 15,166 | 354.3s / 1,142,976 msg/s |
| Dekaf (3conn) | 2026-07-29T10:27:57.0048756+00:00 | 1 | 9.0 MiB / 5.0 MiB | 434.5 MB/s | 4/4 | 33,727 | 373.3s / 1,125,003 msg/s |
| Dekaf (3conn) | 2026-07-29T10:28:15.0170158+00:00 | 1 | 9.0 MiB / 7.9 MiB | 434.5 MB/s | 5/4 | 35,335 | 391.3s / 1,146,561 msg/s |
| Dekaf (3conn) | 2026-07-29T10:28:33.0250283+00:00 | 2 | 12.0 MiB / 4.7 MiB | 460.1 MB/s | 2/3 | 24,483 | 409.3s / 1,141,638 msg/s |
| Dekaf (3conn) | 2026-07-29T10:28:51.0281607+00:00 | 2 | 12.0 MiB / 3.1 MiB | 460.1 MB/s | 2/4 | 25,711 | 427.3s / 1,120,600 msg/s |
| Dekaf (3conn) | 2026-07-29T10:29:09.0374766+00:00 | 3 | 12.0 MiB / 5.1 MiB | 446.3 MB/s | 2/3 | 19,270 | 445.3s / 978,965 msg/s |
| Dekaf (3conn) | 2026-07-29T10:29:27.0465633+00:00 | 3 | 12.0 MiB / 2.7 MiB | 446.3 MB/s | 2/3 | 19,997 | 463.3s / 1,140,189 msg/s |
| Dekaf (3conn) | 2026-07-29T10:29:46.0511794+00:00 | 1 | 9.0 MiB / 3.2 MiB | 445.7 MB/s | 6/5 | 48,895 | 482.3s / 1,209,074 msg/s |
| Dekaf (3conn) | 2026-07-29T10:30:04.0578044+00:00 | 1 | 9.0 MiB / 4.0 MiB | 445.7 MB/s | 7/5 | 51,993 | 500.3s / 1,231,343 msg/s |
| Dekaf (3conn) | 2026-07-29T10:30:22.0661262+00:00 | 2 | 12.0 MiB / 9.6 MiB | 473.4 MB/s | 2/4 | 31,630 | 518.4s / 1,161,071 msg/s |
| Dekaf (3conn) | 2026-07-29T10:30:40.0770155+00:00 | 2 | 12.0 MiB / 5.3 MiB | 473.4 MB/s | 2/4 | 32,355 | 536.4s / 1,157,034 msg/s |
| Dekaf (3conn) | 2026-07-29T10:30:58.0860387+00:00 | 3 | 7.0 MiB / 3.1 MiB | 446.8 MB/s | 5/3 | 29,015 | 554.4s / 1,145,215 msg/s |
| Dekaf (3conn) | 2026-07-29T10:31:16.0980242+00:00 | 3 | 7.0 MiB / 5.2 MiB | 446.8 MB/s | 5/4 | 32,368 | 572.4s / 1,105,412 msg/s |
| Dekaf (3conn) | 2026-07-29T10:31:35.1092922+00:00 | 1 | 9.0 MiB / 7.3 MiB | 445.7 MB/s | 9/5 | 58,147 | 591.4s / 1,064,670 msg/s |
| Dekaf (3conn) | 2026-07-29T10:31:53.1169811+00:00 | 1 | 9.0 MiB / 9.0 MiB | 445.7 MB/s | 10/5 | 59,530 | 609.4s / 825,144 msg/s |
| Dekaf (3conn) | 2026-07-29T10:32:11.123306+00:00 | 2 | 12.0 MiB / 9.0 MiB | 473.4 MB/s | 2/4 | 36,015 | 627.4s / 1,165,880 msg/s |
| Dekaf (3conn) | 2026-07-29T10:32:29.1342766+00:00 | 2 | 12.0 MiB / 8.2 MiB | 473.4 MB/s | 2/4 | 36,827 | 645.4s / 1,145,259 msg/s |
| Dekaf (3conn) | 2026-07-29T10:32:47.1412281+00:00 | 3 | 9.0 MiB / 3.5 MiB | 446.8 MB/s | 7/4 | 43,326 | 663.4s / 1,002,854 msg/s |
| Dekaf (3conn) | 2026-07-29T10:33:05.1510511+00:00 | 3 | 7.0 MiB / 5.5 MiB | 446.8 MB/s | 7/4 | 45,179 | 681.4s / 977,420 msg/s |
| Dekaf (3conn) | 2026-07-29T10:33:24.1582029+00:00 | 1 | 7.0 MiB / 3.8 MiB | 445.7 MB/s | 10/6 | 67,762 | 700.5s / 867,650 msg/s |
| Dekaf (3conn) | 2026-07-29T10:33:42.1682078+00:00 | 1 | 8.0 MiB / 2.7 MiB | 445.7 MB/s | 11/6 | 70,707 | 718.5s / 1,039,196 msg/s |
| Dekaf (3conn) | 2026-07-29T10:34:00.1751173+00:00 | 2 | 10.0 MiB / 9.8 MiB | 473.4 MB/s | 3/5 | 41,687 | 736.5s / 1,128,855 msg/s |
| Dekaf (3conn) | 2026-07-29T10:34:18.1880225+00:00 | 2 | 10.0 MiB / 7.1 MiB | 473.4 MB/s | 3/6 | 43,646 | 754.5s / 1,117,617 msg/s |
| Dekaf (3conn) | 2026-07-29T10:34:36.196118+00:00 | 3 | 8.0 MiB / 7.7 MiB | 446.8 MB/s | 8/5 | 54,453 | 772.5s / 1,180,444 msg/s |
| Dekaf (3conn) | 2026-07-29T10:34:54.2031962+00:00 | 3 | 8.0 MiB / 5.1 MiB | 446.8 MB/s | 9/5 | 56,659 | 790.5s / 1,047,374 msg/s |
| Dekaf (3conn) | 2026-07-29T10:35:13.2239795+00:00 | 1 | 7.0 MiB / 7.0 MiB | 445.7 MB/s | 11/9 | 87,992 | 809.5s / 949,254 msg/s |
| Dekaf (3conn) | 2026-07-29T10:35:31.2346972+00:00 | 1 | 7.0 MiB / 7.0 MiB | 445.7 MB/s | 11/9 | 92,518 | 827.5s / 1,092,067 msg/s |
| Dekaf (3conn) | 2026-07-29T10:35:49.2494701+00:00 | 2 | 11.0 MiB / 9.5 MiB | 473.4 MB/s | 4/7 | 47,277 | 845.5s / 920,889 msg/s |
| Dekaf (3conn) | 2026-07-29T10:36:07.2606521+00:00 | 2 | 11.0 MiB / 5.7 MiB | 473.4 MB/s | 4/7 | 48,125 | 863.5s / 946,341 msg/s |
| Dekaf (3conn) | 2026-07-29T10:36:25.2732728+00:00 | 3 | 8.0 MiB / 6.8 MiB | 446.8 MB/s | 9/6 | 65,795 | 881.6s / 714,123 msg/s |
| Dekaf (3conn) | 2026-07-29T10:36:43.287684+00:00 | 3 | 8.0 MiB / 7.3 MiB | 446.8 MB/s | 9/6 | 67,671 | 899.6s / 800,876 msg/s |
*5,296 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-29T09:52:01.1265966+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 5.4 MiB |
| Dekaf | 2026-07-29T09:52:01.2713908+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 11.5 MiB |
| Dekaf | 2026-07-29T09:52:16.3164646+00:00 | 2 | capacity | succeeded | 15,131ms | 14.0 MiB / 12.3 MiB |
| Dekaf | 2026-07-29T09:52:19.2057547+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T09:52:19.3881166+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-29T09:52:34.4723821+00:00 | 3 | capacity | succeeded | 15,084ms | 12.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-29T09:52:37.4271529+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 8.6 MiB |
| Dekaf | 2026-07-29T09:52:52.4176248+00:00 | 1 | capacity | succeeded | 15,076ms | 10.0 MiB / 2.8 MiB |
| Dekaf | 2026-07-29T09:52:52.5709047+00:00 | 3 | capacity | succeeded | 15,086ms | 10.0 MiB / 4.0 MiB |
| Dekaf | 2026-07-29T09:52:55.5203879+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 7.6 MiB |
| Dekaf | 2026-07-29T09:53:10.5161204+00:00 | 1 | capacity | succeeded | 15,083ms | 8.0 MiB / 3.0 MiB |
| Dekaf | 2026-07-29T09:53:10.6776786+00:00 | 3 | capacity | succeeded | 15,093ms | 8.0 MiB / 2.2 MiB |
| Dekaf | 2026-07-29T09:53:13.6230863+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 4.4 MiB |
| Dekaf | 2026-07-29T09:53:26.2677332+00:00 | 3 | capacity | failed | 12,574ms | 8.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-29T09:53:56.3981326+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 1.8 MiB |
| Dekaf | 2026-07-29T09:53:58.7701874+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-29T09:54:13.8420534+00:00 | 1 | capacity | failed | 15,072ms | 8.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-29T09:54:44.1341988+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-29T09:55:17.2959308+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 5.3 MiB |
| Dekaf | 2026-07-29T09:55:58.4508273+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 1.0 MiB |
| Dekaf | 2026-07-29T09:56:02.496745+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-29T09:56:17.5893649+00:00 | 2 | capacity | succeeded | 15,092ms | 7.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-29T09:56:29.4928069+00:00 | 1 | capacity | succeeded | 15,076ms | 7.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-29T09:56:35.6862668+00:00 | 2 | capacity | failed | 15,078ms | 7.0 MiB / 3.8 MiB |
| Dekaf | 2026-07-29T09:56:47.5785423+00:00 | 1 | capacity | failed | 15,073ms | 7.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-29T09:57:47.8622044+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-29T09:58:02.9183357+00:00 | 1 | capacity | succeeded | 15,056ms | 6.0 MiB / 1.8 MiB |
| Dekaf | 2026-07-29T09:58:08.4530425+00:00 | 1 | capacity | failed | 2,520ms | 6.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-29T09:58:53.6673035+00:00 | 1 | capacity | failed | 15,082ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-29T09:59:38.8735148+00:00 | 1 | capacity | failed | 15,052ms | 6.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-29T10:00:06.5786699+00:00 | 2 | capacity | succeeded | 15,062ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T10:00:09.5893031+00:00 | 2 | capacity | started | 0ms | 5.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-29T10:00:47.2761444+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 5.5 MiB |
| Dekaf | 2026-07-29T10:00:54.8139439+00:00 | 2 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T10:01:08.2901872+00:00 | 1 | capacity | failed | 15,085ms | 6.0 MiB / 4.0 MiB |
| Dekaf | 2026-07-29T10:01:38.4252655+00:00 | 1 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T10:01:53.4705403+00:00 | 1 | capacity | failed | 15,045ms | 6.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-29T10:02:23.5959229+00:00 | 1 | capacity | started | 0ms | 5.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-29T10:02:38.6477377+00:00 | 1 | capacity | succeeded | 15,052ms | 5.0 MiB / 4.0 MiB |
| Dekaf | 2026-07-29T10:02:41.6574035+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 3.5 MiB |
| Dekaf | 2026-07-29T10:03:10.3818711+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 2.5 MiB |
| Dekaf | 2026-07-29T10:03:41.9062176+00:00 | 1 | capacity | succeeded | 15,052ms | 5.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-29T10:03:44.9155374+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 0.5 MiB |
| Dekaf | 2026-07-29T10:03:59.9729567+00:00 | 1 | capacity | failed | 15,057ms | 5.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-29T10:04:02.5637633+00:00 | 2 | capacity | failed | 1,504ms | 6.0 MiB / 2.3 MiB |
| Dekaf | 2026-07-29T10:04:45.1685768+00:00 | 1 | capacity | succeeded | 15,070ms | 6.0 MiB / 1.6 MiB |
| Dekaf | 2026-07-29T10:05:03.4003025+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 0.8 MiB |
| Dekaf | 2026-07-29T10:05:17.8720174+00:00 | 2 | capacity | succeeded | 15,047ms | 5.0 MiB / 3.0 MiB |
| Dekaf | 2026-07-29T10:05:20.8797974+00:00 | 2 | capacity | started | 0ms | 4.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-29T10:05:35.9270979+00:00 | 2 | capacity | succeeded | 15,047ms | 4.0 MiB / 0.4 MiB |
| Dekaf | 2026-07-29T10:05:53.9871158+00:00 | 2 | capacity | failed | 15,049ms | 4.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-29T10:06:03.6289191+00:00 | 3 | capacity | succeeded | 15,052ms | 10.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-29T10:22:13.9838859+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-29T10:22:14.095368+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 13.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:22:29.0880331+00:00 | 1 | capacity | succeeded | 15,051ms | 14.0 MiB / 0.9 MiB |
| Dekaf (3conn) | 2026-07-29T10:22:32.0453995+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:22:32.1734015+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-29T10:22:47.1455683+00:00 | 1 | capacity | succeeded | 15,044ms | 12.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:22:50.1080129+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 4.5 MiB |
| Dekaf (3conn) | 2026-07-29T10:23:05.1616975+00:00 | 2 | capacity | failed | 15,053ms | 12.0 MiB / 6.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:23:05.3479257+00:00 | 3 | capacity | failed | 15,052ms | 12.0 MiB / 8.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:23:35.3614227+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:23:37.4756532+00:00 | 3 | capacity | failed | 2,004ms | 12.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-07-29T10:23:50.3733802+00:00 | 2 | capacity | failed | 15,051ms | 12.0 MiB / 8.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:24:20.4691988+00:00 | 2 | capacity | started | 0ms | 13.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:24:26.5828018+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-07-29T10:24:41.6414406+00:00 | 1 | capacity | succeeded | 15,059ms | 8.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:24:59.768071+00:00 | 1 | capacity | failed | 15,114ms | 8.0 MiB / 4.7 MiB |
| Dekaf (3conn) | 2026-07-29T10:25:44.932376+00:00 | 1 | capacity | failed | 15,058ms | 8.0 MiB / 3.7 MiB |
| Dekaf (3conn) | 2026-07-29T10:27:45.4912127+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 5.9 MiB |
| Dekaf (3conn) | 2026-07-29T10:28:30.6318116+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-07-29T10:28:45.7308419+00:00 | 1 | capacity | failed | 15,099ms | 9.0 MiB / 9.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:29:15.8335047+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:29:33.9134173+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-07-29T10:29:54.0540492+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:30:12.1245829+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 4.0 MiB |
| Dekaf (3conn) | 2026-07-29T10:30:27.1876419+00:00 | 3 | capacity | succeeded | 15,063ms | 8.0 MiB / 6.9 MiB |
| Dekaf (3conn) | 2026-07-29T10:30:45.2810469+00:00 | 3 | capacity | succeeded | 15,054ms | 7.0 MiB / 3.9 MiB |
| Dekaf (3conn) | 2026-07-29T10:31:01.3304943+00:00 | 3 | capacity | failed | 13,040ms | 7.0 MiB / 7.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:31:19.3351279+00:00 | 1 | capacity | succeeded | 15,065ms | 8.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-29T10:31:31.4579124+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:31:46.5155093+00:00 | 3 | capacity | succeeded | 15,057ms | 8.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-07-29T10:32:16.6501733+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:32:31.74387+00:00 | 3 | capacity | succeeded | 15,092ms | 9.0 MiB / 8.5 MiB |
| Dekaf (3conn) | 2026-07-29T10:33:01.8305834+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 6.7 MiB |
| Dekaf (3conn) | 2026-07-29T10:33:16.8963784+00:00 | 3 | capacity | failed | 15,065ms | 9.0 MiB / 2.5 MiB |
| Dekaf (3conn) | 2026-07-29T10:33:37.9210679+00:00 | 1 | capacity | succeeded | 15,077ms | 7.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-07-29T10:33:49.4381425+00:00 | 2 | capacity | succeeded | 15,054ms | 10.0 MiB / 5.3 MiB |
| Dekaf (3conn) | 2026-07-29T10:33:56.0105789+00:00 | 1 | capacity | failed | 15,072ms | 7.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:34:17.2405467+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:34:27.6245877+00:00 | 1 | capacity | failed | 1,507ms | 7.0 MiB / 4.8 MiB |
| Dekaf (3conn) | 2026-07-29T10:34:35.3058551+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:34:50.3479322+00:00 | 3 | capacity | succeeded | 15,042ms | 8.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-29T10:34:57.7889731+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 4.3 MiB |
| Dekaf (3conn) | 2026-07-29T10:35:20.4631181+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 3.0 MiB |
| Dekaf (3conn) | 2026-07-29T10:35:31.4375849+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 5.2 MiB |
| Dekaf (3conn) | 2026-07-29T10:35:46.5168787+00:00 | 1 | capacity | failed | 15,079ms | 7.0 MiB / 4.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:36:11.6071668+00:00 | 2 | capacity | succeeded | 15,069ms | 9.0 MiB / 4.1 MiB |
| Dekaf (3conn) | 2026-07-29T10:36:29.7413899+00:00 | 2 | capacity | succeeded | 15,116ms | 7.0 MiB / 3.3 MiB |
| Dekaf (3conn) | 2026-07-29T10:36:33.8481732+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 6.7 MiB |
*110 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf (3conn) | 1 | 0.001–0.002ms | 19 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 21 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 91 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 247 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 563 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 866 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 1,209 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 1,258 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 2,086 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 3,446 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 3,957 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 3,532 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 2,077 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 939 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 436 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 53 |
| Dekaf (3conn) | 2 | 0.001–0.002ms | 7 |
| Dekaf (3conn) | 2 | 0.002–0.004ms | 3 |
| Dekaf (3conn) | 2 | 0.004–0.008ms | 38 |
| Dekaf (3conn) | 2 | 0.008–0.016ms | 149 |
| Dekaf (3conn) | 2 | 0.016–0.032ms | 348 |
| Dekaf (3conn) | 2 | 0.032–0.064ms | 526 |
| Dekaf (3conn) | 2 | 0.064–0.128ms | 698 |
| Dekaf (3conn) | 2 | 0.128–0.256ms | 817 |
| Dekaf (3conn) | 2 | 0.256–0.512ms | 1,292 |
| Dekaf (3conn) | 2 | 0.512–1.024ms | 2,032 |
| Dekaf (3conn) | 2 | 1.024–2.048ms | 2,312 |
| Dekaf (3conn) | 2 | 2.048–4.096ms | 1,900 |
| Dekaf (3conn) | 2 | 4.096–8.192ms | 1,076 |
| Dekaf (3conn) | 2 | 8.192–16.384ms | 406 |
| Dekaf (3conn) | 2 | 16.384–32.768ms | 152 |
| Dekaf (3conn) | 2 | 32.768–65.536ms | 28 |
| Dekaf (3conn) | 3 | 0.001–0.002ms | 12 |
| Dekaf (3conn) | 3 | 0.002–0.004ms | 13 |
| Dekaf (3conn) | 3 | 0.004–0.008ms | 47 |
| Dekaf (3conn) | 3 | 0.008–0.016ms | 176 |
| Dekaf (3conn) | 3 | 0.016–0.032ms | 407 |
| Dekaf (3conn) | 3 | 0.032–0.064ms | 655 |
| Dekaf (3conn) | 3 | 0.064–0.128ms | 800 |
| Dekaf (3conn) | 3 | 0.128–0.256ms | 851 |
| Dekaf (3conn) | 3 | 0.256–0.512ms | 1,454 |
| Dekaf (3conn) | 3 | 0.512–1.024ms | 2,336 |
| Dekaf (3conn) | 3 | 1.024–2.048ms | 2,785 |
| Dekaf (3conn) | 3 | 2.048–4.096ms | 2,337 |
| Dekaf (3conn) | 3 | 4.096–8.192ms | 1,293 |
| Dekaf (3conn) | 3 | 8.192–16.384ms | 537 |
| Dekaf (3conn) | 3 | 16.384–32.768ms | 214 |
| Dekaf (3conn) | 3 | 32.768–65.536ms | 31 |
| Dekaf | 1 | 0.001–0.002ms | 29 |
| Dekaf | 1 | 0.002–0.004ms | 32 |
| Dekaf | 1 | 0.004–0.008ms | 104 |
| Dekaf | 1 | 0.008–0.016ms | 256 |
| Dekaf | 1 | 0.016–0.032ms | 634 |
| Dekaf | 1 | 0.032–0.064ms | 1,191 |
| Dekaf | 1 | 0.064–0.128ms | 1,753 |
| Dekaf | 1 | 0.128–0.256ms | 2,490 |
| Dekaf | 1 | 0.256–0.512ms | 4,322 |
| Dekaf | 1 | 0.512–1.024ms | 6,554 |
| Dekaf | 1 | 1.024–2.048ms | 7,109 |
| Dekaf | 1 | 2.048–4.096ms | 4,628 |
| Dekaf | 1 | 4.096–8.192ms | 2,298 |
| Dekaf | 1 | 8.192–16.384ms | 797 |
| Dekaf | 1 | 16.384–32.768ms | 158 |
| Dekaf | 1 | 32.768–65.536ms | 3 |
| Dekaf | 1 | 131.072–262.144ms | 1 |
| Dekaf | 2 | 0.001–0.002ms | 46 |
| Dekaf | 2 | 0.002–0.004ms | 55 |
| Dekaf | 2 | 0.004–0.008ms | 128 |
| Dekaf | 2 | 0.008–0.016ms | 451 |
| Dekaf | 2 | 0.016–0.032ms | 1,165 |
| Dekaf | 2 | 0.032–0.064ms | 2,267 |
| Dekaf | 2 | 0.064–0.128ms | 3,377 |
| Dekaf | 2 | 0.128–0.256ms | 4,498 |
| Dekaf | 2 | 0.256–0.512ms | 8,623 |
| Dekaf | 2 | 0.512–1.024ms | 13,130 |
| Dekaf | 2 | 1.024–2.048ms | 13,193 |
| Dekaf | 2 | 2.048–4.096ms | 7,267 |
| Dekaf | 2 | 4.096–8.192ms | 3,099 |
| Dekaf | 2 | 8.192–16.384ms | 802 |
| Dekaf | 2 | 16.384–32.768ms | 155 |
| Dekaf | 2 | 32.768–65.536ms | 9 |
| Dekaf | 2 | 65.536–131.072ms | 4 |
| Dekaf | 2 | 131.072–262.144ms | 1 |
| Dekaf | 3 | 0.001–0.002ms | 7 |
| Dekaf | 3 | 0.002–0.004ms | 8 |
| Dekaf | 3 | 0.004–0.008ms | 20 |
| Dekaf | 3 | 0.008–0.016ms | 55 |
| Dekaf | 3 | 0.016–0.032ms | 165 |
| Dekaf | 3 | 0.032–0.064ms | 306 |
| Dekaf | 3 | 0.064–0.128ms | 453 |
| Dekaf | 3 | 0.128–0.256ms | 483 |
| Dekaf | 3 | 0.256–0.512ms | 914 |
| Dekaf | 3 | 0.512–1.024ms | 1,378 |
| Dekaf | 3 | 1.024–2.048ms | 1,448 |
| Dekaf | 3 | 2.048–4.096ms | 968 |
| Dekaf | 3 | 4.096–8.192ms | 495 |
| Dekaf | 3 | 8.192–16.384ms | 167 |
| Dekaf | 3 | 16.384–32.768ms | 36 |
| Dekaf | 3 | 32.768–65.536ms | 1 |

## Delivery Latency Outliers - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 21,000 | 2026-07-29T09:51:30.8983143+00:00 | 102.3ms | GC pause | - | - | 1.0s / 170,095 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf | 53,000 | 2026-07-29T09:51:30.9867043+00:00 | 350.7ms | GC pause | - | - | 1.0s / 170,095 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf | 70,000 | 2026-07-29T09:51:31.0391569+00:00 | 176.6ms | GC pause | - | - | 1.0s / 170,095 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf | 91,000 | 2026-07-29T09:51:31.1660432+00:00 | 135.9ms | GC pause | - | - | 1.0s / 170,095 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf | 97,000 | 2026-07-29T09:51:31.2107578+00:00 | 446.7ms | GC pause | - | - | 1.0s / 170,095 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf | 99,000 | 2026-07-29T09:51:31.2171388+00:00 | 572.3ms | GC pause | - | - | 1.0s / 170,095 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf | 126,000 | 2026-07-29T09:51:31.5175904+00:00 | 198.2ms | GC pause | - | - | 1.0s / 170,095 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf | 132,000 | 2026-07-29T09:51:31.571614+00:00 | 150.4ms | GC pause | - | - | 1.0s / 170,095 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf | 148,000 | 2026-07-29T09:51:31.6714484+00:00 | 112.0ms | GC pause | - | - | 1.0s / 170,095 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf | 200,000 | 2026-07-29T09:51:32.0171208+00:00 | 106.2ms | throughput collapse | - | - | 2.0s / 237,060 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 256,000 | 2026-07-29T09:51:32.1994669+00:00 | 191.5ms | throughput collapse | - | - | 2.0s / 237,060 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 276,000 | 2026-07-29T09:51:32.264014+00:00 | 209.6ms | throughput collapse | - | - | 2.0s / 237,060 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 339,000 | 2026-07-29T09:51:32.5807862+00:00 | 227.4ms | throughput collapse | - | - | 2.0s / 237,060 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 350,000 | 2026-07-29T09:51:32.6143108+00:00 | 112.7ms | throughput collapse | - | - | 2.0s / 237,060 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 373,000 | 2026-07-29T09:51:32.7003157+00:00 | 204.3ms | throughput collapse | - | - | 3.0s / 322,683 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 444,000 | 2026-07-29T09:51:32.9183967+00:00 | 125.8ms | throughput collapse | - | - | 3.0s / 322,683 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 453,000 | 2026-07-29T09:51:32.9412504+00:00 | 360.9ms | throughput collapse | - | - | 3.0s / 322,683 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 489,000 | 2026-07-29T09:51:33.1304413+00:00 | 286.5ms | throughput collapse | - | - | 3.0s / 322,683 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 510,000 | 2026-07-29T09:51:33.2256466+00:00 | 124.9ms | throughput collapse | - | - | 3.0s / 322,683 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 549,000 | 2026-07-29T09:51:33.3869421+00:00 | 141.0ms | throughput collapse | - | - | 3.0s / 322,683 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 619,000 | 2026-07-29T09:51:33.5388949+00:00 | 139.1ms | throughput collapse | - | - | 3.0s / 322,683 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 750,000 | 2026-07-29T09:51:33.8532292+00:00 | 121.0ms | throughput collapse | - | - | 4.0s / 353,268 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 763,000 | 2026-07-29T09:51:33.9151642+00:00 | 195.9ms | throughput collapse | - | - | 4.0s / 353,268 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 783,000 | 2026-07-29T09:51:33.9758764+00:00 | 188.7ms | throughput collapse | - | - | 4.0s / 353,268 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 909,000 | 2026-07-29T09:51:34.2945193+00:00 | 137.0ms | throughput collapse | - | - | 4.0s / 353,268 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,059,000 | 2026-07-29T09:51:34.7567411+00:00 | 168.8ms | throughput collapse | - | - | 5.0s / 374,746 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,063,000 | 2026-07-29T09:51:34.7699565+00:00 | 162.4ms | throughput collapse | - | - | 5.0s / 374,746 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,073,000 | 2026-07-29T09:51:34.7888845+00:00 | 159.2ms | throughput collapse | - | - | 5.0s / 374,746 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,129,000 | 2026-07-29T09:51:34.9284807+00:00 | 173.7ms | throughput collapse | - | - | 5.0s / 374,746 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,169,000 | 2026-07-29T09:51:35.0183925+00:00 | 170.1ms | throughput collapse | - | - | 5.0s / 374,746 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,179,000 | 2026-07-29T09:51:35.0396621+00:00 | 230.0ms | throughput collapse | - | - | 5.0s / 374,746 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,199,000 | 2026-07-29T09:51:35.1016163+00:00 | 211.7ms | throughput collapse | - | - | 5.0s / 374,746 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,249,000 | 2026-07-29T09:51:35.2725141+00:00 | 184.8ms | throughput collapse | - | - | 5.0s / 374,746 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,359,000 | 2026-07-29T09:51:35.5504313+00:00 | 159.6ms | throughput collapse | - | - | 5.0s / 374,746 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,469,000 | 2026-07-29T09:51:35.832183+00:00 | 169.0ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 471,428 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,509,000 | 2026-07-29T09:51:35.9318733+00:00 | 153.0ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 471,428 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,559,000 | 2026-07-29T09:51:36.0441673+00:00 | 136.9ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 471,428 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,609,000 | 2026-07-29T09:51:36.1473814+00:00 | 158.6ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 471,428 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,723,000 | 2026-07-29T09:51:36.398504+00:00 | 124.5ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 471,428 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,879,000 | 2026-07-29T09:51:36.6974136+00:00 | 149.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 461,886 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,942,000 | 2026-07-29T09:51:36.8345599+00:00 | 122.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 461,886 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,013,000 | 2026-07-29T09:51:36.9987582+00:00 | 160.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 461,886 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,129,000 | 2026-07-29T09:51:37.301853+00:00 | 128.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 461,886 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,193,000 | 2026-07-29T09:51:37.4159737+00:00 | 120.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 461,886 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,199,000 | 2026-07-29T09:51:37.4291365+00:00 | 127.3ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 461,886 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,209,000 | 2026-07-29T09:51:37.4506097+00:00 | 119.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 461,886 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,379,000 | 2026-07-29T09:51:37.7890194+00:00 | 162.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 432,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,423,000 | 2026-07-29T09:51:37.8836438+00:00 | 153.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 432,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,439,000 | 2026-07-29T09:51:37.9312373+00:00 | 141.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 432,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,459,000 | 2026-07-29T09:51:37.975793+00:00 | 138.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 432,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,569,000 | 2026-07-29T09:51:38.2307731+00:00 | 116.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 432,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,703,000 | 2026-07-29T09:51:38.4877855+00:00 | 155.9ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 432,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,713,000 | 2026-07-29T09:51:38.5016845+00:00 | 161.3ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 432,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,769,000 | 2026-07-29T09:51:38.6357245+00:00 | 211.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 457,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,913,000 | 2026-07-29T09:51:39.0346735+00:00 | 108.3ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 457,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,919,000 | 2026-07-29T09:51:39.0507919+00:00 | 110.5ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 457,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,983,000 | 2026-07-29T09:51:39.1699004+00:00 | 154.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 457,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,019,000 | 2026-07-29T09:51:39.2672484+00:00 | 122.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 457,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,043,000 | 2026-07-29T09:51:39.3084424+00:00 | 120.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 457,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,099,000 | 2026-07-29T09:51:39.413657+00:00 | 133.3ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 457,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,109,000 | 2026-07-29T09:51:39.4294219+00:00 | 138.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 457,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,123,000 | 2026-07-29T09:51:39.4562139+00:00 | 131.7ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 457,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,193,000 | 2026-07-29T09:51:39.594645+00:00 | 174.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 457,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,223,000 | 2026-07-29T09:51:39.6607343+00:00 | 172.2ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 443,516 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,249,000 | 2026-07-29T09:51:39.7397972+00:00 | 160.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 443,516 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,313,000 | 2026-07-29T09:51:39.8883526+00:00 | 142.8ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 443,516 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,383,000 | 2026-07-29T09:51:40.0631035+00:00 | 182.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 443,516 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,437,000 | 2026-07-29T09:51:40.2043327+00:00 | 108.2ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 443,516 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,447,000 | 2026-07-29T09:51:40.2181759+00:00 | 105.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 443,516 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,493,000 | 2026-07-29T09:51:40.336368+00:00 | 140.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 443,516 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,523,000 | 2026-07-29T09:51:40.4062034+00:00 | 125.3ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 443,516 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,613,000 | 2026-07-29T09:51:40.5832134+00:00 | 122.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 443,516 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,703,000 | 2026-07-29T09:51:40.7719627+00:00 | 131.5ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 447,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,709,000 | 2026-07-29T09:51:40.7825046+00:00 | 128.0ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 447,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,723,000 | 2026-07-29T09:51:40.8096106+00:00 | 121.6ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 447,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,893,000 | 2026-07-29T09:51:41.092656+00:00 | 115.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 447,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,993,000 | 2026-07-29T09:51:41.2992797+00:00 | 126.3ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 447,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,046,000 | 2026-07-29T09:51:41.416512+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 447,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,085,000 | 2026-07-29T09:51:41.5560274+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 447,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,111,000 | 2026-07-29T09:51:41.6243221+00:00 | 186.5ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 447,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,193,000 | 2026-07-29T09:51:41.861933+00:00 | 197.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 446,354 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,469,000 | 2026-07-29T09:51:42.5101914+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 446,354 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,539,000 | 2026-07-29T09:51:42.6272607+00:00 | 165.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 446,354 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,569,000 | 2026-07-29T09:51:42.6896403+00:00 | 157.1ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 490,221 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,619,000 | 2026-07-29T09:51:42.8177743+00:00 | 109.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 490,221 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,713,000 | 2026-07-29T09:51:42.9792296+00:00 | 119.9ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 490,221 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,739,000 | 2026-07-29T09:51:43.0271293+00:00 | 124.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 490,221 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,753,000 | 2026-07-29T09:51:43.0518274+00:00 | 117.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 490,221 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,799,000 | 2026-07-29T09:51:43.136663+00:00 | 161.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 490,221 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,823,000 | 2026-07-29T09:51:43.1783083+00:00 | 152.8ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 490,221 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,839,000 | 2026-07-29T09:51:43.228265+00:00 | 125.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 490,221 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,969,000 | 2026-07-29T09:51:43.4688561+00:00 | 148.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 490,221 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,039,000 | 2026-07-29T09:51:43.6248605+00:00 | 183.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 490,221 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,149,000 | 2026-07-29T09:51:43.9024829+00:00 | 110.0ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 495,461 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,159,000 | 2026-07-29T09:51:43.9164319+00:00 | 124.4ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 495,461 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,263,000 | 2026-07-29T09:51:44.0898509+00:00 | 120.2ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 495,461 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,473,000 | 2026-07-29T09:51:44.5200189+00:00 | 128.6ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 495,461 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,489,000 | 2026-07-29T09:51:44.5546809+00:00 | 134.4ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 495,461 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,693,000 | 2026-07-29T09:51:45.0066347+00:00 | 120.1ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 507,395 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,703,000 | 2026-07-29T09:51:45.0289056+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 507,395 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,722,000 | 2026-07-29T09:51:45.0629823+00:00 | 133.8ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 507,395 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,741,000 | 2026-07-29T09:51:45.0867742+00:00 | 189.0ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 507,395 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,785,000 | 2026-07-29T09:51:45.1726728+00:00 | 107.1ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 507,395 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,791,000 | 2026-07-29T09:51:45.1786391+00:00 | 167.5ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 507,395 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,871,000 | 2026-07-29T09:51:45.3671993+00:00 | 121.4ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 507,395 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,933,000 | 2026-07-29T09:51:45.4855696+00:00 | 108.8ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 507,395 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,943,000 | 2026-07-29T09:51:45.4991099+00:00 | 109.6ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 507,395 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,001,000 | 2026-07-29T09:51:45.6062264+00:00 | 136.1ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 507,395 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,041,000 | 2026-07-29T09:51:45.6644169+00:00 | 154.5ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 507,395 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,051,000 | 2026-07-29T09:51:45.6772632+00:00 | 150.0ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 507,395 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,059,000 | 2026-07-29T09:51:45.6928854+00:00 | 112.5ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 507,395 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,121,000 | 2026-07-29T09:51:45.8324534+00:00 | 115.0ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 454,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,122,000 | 2026-07-29T09:51:45.8330931+00:00 | 114.3ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 454,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,179,000 | 2026-07-29T09:51:45.9292786+00:00 | 108.5ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 454,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,202,000 | 2026-07-29T09:51:45.9663459+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 454,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,232,000 | 2026-07-29T09:51:46.0186904+00:00 | 104.7ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 454,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,259,000 | 2026-07-29T09:51:46.0639282+00:00 | 128.1ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 454,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,289,000 | 2026-07-29T09:51:46.1186195+00:00 | 178.7ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 454,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,312,000 | 2026-07-29T09:51:46.1596552+00:00 | 125.2ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 454,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,359,000 | 2026-07-29T09:51:46.2965762+00:00 | 147.5ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 454,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,459,000 | 2026-07-29T09:51:46.5241782+00:00 | 168.7ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 454,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,523,000 | 2026-07-29T09:51:46.6933441+00:00 | 157.5ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 544,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,639,000 | 2026-07-29T09:51:46.9559188+00:00 | 109.1ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 544,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,679,000 | 2026-07-29T09:51:47.0262995+00:00 | 107.3ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 544,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,697,000 | 2026-07-29T09:51:47.06152+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 544,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,873,000 | 2026-07-29T09:51:47.3873227+00:00 | 112.7ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 544,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,879,000 | 2026-07-29T09:51:47.3945791+00:00 | 116.9ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 544,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,983,000 | 2026-07-29T09:51:47.5592531+00:00 | 107.5ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 544,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,023,000 | 2026-07-29T09:51:47.6274636+00:00 | 142.9ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 544,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,029,000 | 2026-07-29T09:51:47.6356128+00:00 | 150.6ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 544,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,179,000 | 2026-07-29T09:51:47.9299136+00:00 | 123.0ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 566,472 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,253,000 | 2026-07-29T09:51:48.0694377+00:00 | 114.9ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 566,472 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,263,000 | 2026-07-29T09:51:48.0869841+00:00 | 111.9ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 566,472 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,309,000 | 2026-07-29T09:51:48.168516+00:00 | 128.4ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 566,472 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,313,000 | 2026-07-29T09:51:48.178068+00:00 | 125.3ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 566,472 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,343,000 | 2026-07-29T09:51:48.2312864+00:00 | 127.6ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 566,472 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,363,000 | 2026-07-29T09:51:48.267102+00:00 | 116.5ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 566,472 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,423,000 | 2026-07-29T09:51:48.371973+00:00 | 115.6ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 566,472 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,433,000 | 2026-07-29T09:51:48.387072+00:00 | 123.2ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 566,472 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,523,000 | 2026-07-29T09:51:48.5408945+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 566,472 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,732,000 | 2026-07-29T09:51:48.914083+00:00 | 103.5ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 475,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,742,000 | 2026-07-29T09:51:48.9212493+00:00 | 116.6ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 475,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,875,000 | 2026-07-29T09:51:49.1630483+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 475,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,891,000 | 2026-07-29T09:51:49.1868198+00:00 | 207.6ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 475,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,032,000 | 2026-07-29T09:51:49.5321294+00:00 | 129.3ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 475,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,052,000 | 2026-07-29T09:51:49.5652342+00:00 | 143.8ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 475,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,072,000 | 2026-07-29T09:51:49.6088303+00:00 | 171.6ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 475,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,082,000 | 2026-07-29T09:51:49.6285054+00:00 | 170.0ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 475,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,107,000 | 2026-07-29T09:51:49.6802467+00:00 | 178.7ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 517,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,331,000 | 2026-07-29T09:51:50.1430568+00:00 | 132.1ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 517,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,352,000 | 2026-07-29T09:51:50.1793008+00:00 | 156.9ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 517,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,442,000 | 2026-07-29T09:51:50.3894891+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 517,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,591,000 | 2026-07-29T09:51:50.6570578+00:00 | 111.1ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 517,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,637,000 | 2026-07-29T09:51:50.7655233+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 610,368 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,717,000 | 2026-07-29T09:51:50.8986031+00:00 | 110.6ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 610,368 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,747,000 | 2026-07-29T09:51:50.9391765+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 610,368 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,901,000 | 2026-07-29T09:51:51.1706375+00:00 | 115.8ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 610,368 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,962,000 | 2026-07-29T09:51:51.2984495+00:00 | 110.0ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 610,368 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,027,000 | 2026-07-29T09:51:51.4012693+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 610,368 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,217,000 | 2026-07-29T09:51:51.6884495+00:00 | 134.0ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 610,368 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,237,000 | 2026-07-29T09:51:51.7305869+00:00 | 129.4ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 579,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,247,000 | 2026-07-29T09:51:51.7659465+00:00 | 127.9ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 579,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,521,000 | 2026-07-29T09:51:52.2200451+00:00 | 112.7ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 579,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,581,000 | 2026-07-29T09:51:52.3210881+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 579,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,632,000 | 2026-07-29T09:51:52.3955784+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 579,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,662,000 | 2026-07-29T09:51:52.4378326+00:00 | 114.2ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 579,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,692,000 | 2026-07-29T09:51:52.4833712+00:00 | 138.1ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 579,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,741,000 | 2026-07-29T09:51:52.589644+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 579,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,799,000 | 2026-07-29T09:51:52.6842245+00:00 | 110.5ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 579,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,879,000 | 2026-07-29T09:51:52.8448925+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 578,176 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,909,000 | 2026-07-29T09:51:52.894004+00:00 | 114.1ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 578,176 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,939,000 | 2026-07-29T09:51:52.9410793+00:00 | 126.3ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 578,176 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,053,000 | 2026-07-29T09:51:53.1503471+00:00 | 139.8ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 578,176 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,069,000 | 2026-07-29T09:51:53.1738727+00:00 | 143.5ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 578,176 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,089,000 | 2026-07-29T09:51:53.2136023+00:00 | 125.2ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 578,176 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,143,000 | 2026-07-29T09:51:53.3256699+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 578,176 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,429,000 | 2026-07-29T09:51:53.8038573+00:00 | 114.0ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 658,423 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,439,000 | 2026-07-29T09:51:53.8183593+00:00 | 109.8ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 658,423 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,449,000 | 2026-07-29T09:51:53.8352108+00:00 | 108.5ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 658,423 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,943,000 | 2026-07-29T09:51:54.5815659+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 658,423 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,283,000 | 2026-07-29T09:51:55.0959401+00:00 | 170.0ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 565,634 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,329,000 | 2026-07-29T09:51:55.1801099+00:00 | 162.9ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 565,634 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,553,000 | 2026-07-29T09:51:55.5757684+00:00 | 109.5ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 565,634 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,599,000 | 2026-07-29T09:51:55.6473129+00:00 | 166.9ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 565,634 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,640,000 | 2026-07-29T09:51:55.7374292+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 644,608 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,703,000 | 2026-07-29T09:51:55.8932216+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 644,608 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,839,000 | 2026-07-29T09:51:56.1009458+00:00 | 115.0ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 644,608 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,853,000 | 2026-07-29T09:51:56.1238151+00:00 | 133.0ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 644,608 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,873,000 | 2026-07-29T09:51:56.1561285+00:00 | 120.5ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 644,608 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,173,000 | 2026-07-29T09:51:56.5985005+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 644,608 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,229,000 | 2026-07-29T09:51:56.6797982+00:00 | 125.7ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 644,608 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,549,000 | 2026-07-29T09:51:57.1818971+00:00 | 130.1ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 648,172 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,179,000 | 2026-07-29T09:51:58.1309674+00:00 | 125.2ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 680,592 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,219,000 | 2026-07-29T09:51:58.2000492+00:00 | 121.3ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 680,592 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,239,000 | 2026-07-29T09:51:58.2371416+00:00 | 113.1ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 680,592 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,543,000 | 2026-07-29T09:51:58.663329+00:00 | 119.1ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 680,592 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,919,000 | 2026-07-29T09:51:59.2170435+00:00 | 109.2ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 661,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,189,000 | 2026-07-29T09:51:59.6511073+00:00 | 116.9ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 661,601 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,509,000 | 2026-07-29T09:52:00.1236567+00:00 | 129.6ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 656,414 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,513,000 | 2026-07-29T09:52:00.1293637+00:00 | 142.7ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 656,414 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,889,000 | 2026-07-29T09:52:00.7052452+00:00 | 116.9ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 656,414 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,903,000 | 2026-07-29T09:52:00.727833+00:00 | 107.3ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 656,414 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,569,000 | 2026-07-29T09:52:04.6828169+00:00 | 112.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 34.0s / 705,272 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,292,000 | 2026-07-29T09:52:05.7546343+00:00 | 107.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 36.0s / 663,755 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,562,000 | 2026-07-29T09:52:06.1597564+00:00 | 137.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 36.0s / 663,755 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,632,000 | 2026-07-29T09:52:07.7581424+00:00 | 105.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 38.0s / 671,141 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,633,000 | 2026-07-29T09:52:07.7593122+00:00 | 117.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 38.0s / 671,141 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,639,000 | 2026-07-29T09:52:07.7729751+00:00 | 110.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 38.0s / 671,141 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,909,000 | 2026-07-29T09:52:08.1585232+00:00 | 102.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 38.0s / 671,141 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,249,000 | 2026-07-29T09:52:08.6446858+00:00 | 132.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 38.0s / 671,141 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,252,000 | 2026-07-29T09:52:08.6500426+00:00 | 138.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 38.0s / 671,141 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,259,000 | 2026-07-29T09:52:08.6653959+00:00 | 126.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 38.0s / 671,141 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,351,000 | 2026-07-29T09:52:08.8418443+00:00 | 104.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 39.0s / 631,989 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,542,000 | 2026-07-29T09:52:09.1093781+00:00 | 145.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 39.0s / 631,989 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,571,000 | 2026-07-29T09:52:09.1525595+00:00 | 144.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 39.0s / 631,989 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,621,000 | 2026-07-29T09:52:09.2743033+00:00 | 112.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 39.0s / 631,989 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,662,000 | 2026-07-29T09:52:09.3427737+00:00 | 119.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 39.0s / 631,989 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,871,000 | 2026-07-29T09:52:09.6519751+00:00 | 122.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 39.0s / 631,989 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,882,000 | 2026-07-29T09:52:09.6708169+00:00 | 118.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 39.0s / 631,989 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,891,000 | 2026-07-29T09:52:09.6805459+00:00 | 126.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 39.0s / 631,989 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,162,000 | 2026-07-29T09:52:10.0905291+00:00 | 137.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 40.0s / 578,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,232,000 | 2026-07-29T09:52:10.2143893+00:00 | 113.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 40.0s / 578,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,361,000 | 2026-07-29T09:52:10.4127519+00:00 | 119.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 40.0s / 578,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,381,000 | 2026-07-29T09:52:10.4428551+00:00 | 135.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 40.0s / 578,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,392,000 | 2026-07-29T09:52:10.464119+00:00 | 145.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 40.0s / 578,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,402,000 | 2026-07-29T09:52:10.486465+00:00 | 145.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 40.0s / 578,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,441,000 | 2026-07-29T09:52:10.5715293+00:00 | 156.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 40.0s / 578,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,442,000 | 2026-07-29T09:52:10.5739292+00:00 | 154.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 40.0s / 578,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,482,000 | 2026-07-29T09:52:10.6405364+00:00 | 179.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 40.0s / 578,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,771,000 | 2026-07-29T09:52:11.1691127+00:00 | 137.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 41.0s / 646,267 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,962,000 | 2026-07-29T09:52:11.462473+00:00 | 106.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 41.0s / 646,267 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,022,000 | 2026-07-29T09:52:11.5587277+00:00 | 107.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 41.0s / 646,267 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,101,000 | 2026-07-29T09:52:11.6764717+00:00 | 122.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 41.0s / 646,267 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,427,000 | 2026-07-29T09:52:12.1620495+00:00 | 114.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 42.0s / 613,828 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,441,000 | 2026-07-29T09:52:12.1956726+00:00 | 150.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 42.0s / 613,828 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 23,027,000 | 2026-07-29T09:52:13.1681911+00:00 | 108.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 43.0s / 627,354 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 23,399,000 | 2026-07-29T09:52:13.7253582+00:00 | 123.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 44.0s / 595,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 23,642,000 | 2026-07-29T09:52:14.1315059+00:00 | 104.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 44.0s / 595,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 23,673,000 | 2026-07-29T09:52:14.1924865+00:00 | 133.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 44.0s / 595,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,319,000 | 2026-07-29T09:52:15.2061384+00:00 | 105.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 45.0s / 668,834 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,609,000 | 2026-07-29T09:52:15.6380366+00:00 | 139.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 45.0s / 668,834 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,949,000 | 2026-07-29T09:52:16.1817881+00:00 | 101.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 46.0s / 689,016 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 26,109,000 | 2026-07-29T09:52:17.8822381+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 48.0s / 699,629 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,253,000 | 2026-07-29T09:52:23.799035+00:00 | 102.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 54.0s / 678,152 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,537,000 | 2026-07-29T09:52:24.2099586+00:00 | 114.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 54.0s / 678,152 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,547,000 | 2026-07-29T09:52:24.2314196+00:00 | 104.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 54.0s / 678,152 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 31,592,000 | 2026-07-29T09:52:25.6927858+00:00 | 109.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 55.0s / 722,263 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 31,602,000 | 2026-07-29T09:52:25.7002716+00:00 | 110.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 55.0s / 722,263 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,902,000 | 2026-07-29T09:52:27.6962219+00:00 | 132.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 57.0s / 634,176 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,217,000 | 2026-07-29T09:52:28.2241009+00:00 | 101.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 58.0s / 657,569 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,869,000 | 2026-07-29T09:52:29.1701126+00:00 | 108.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 59.0s / 688,253 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 35,193,000 | 2026-07-29T09:52:31.1596113+00:00 | 100.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 61.0s / 640,777 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 38,593,000 | 2026-07-29T09:52:36.1719441+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 66.1s / 668,189 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,633,000 | 2026-07-29T09:52:45.1875707+00:00 | 101.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 75.1s / 675,423 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 45,277,000 | 2026-07-29T09:52:46.1894917+00:00 | 101.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 76.1s / 652,740 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 77,470,000 | 2026-07-29T09:53:31.6602256+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 121.1s / 676,935 msg/s | Gen2 +0 / pause +0.0ms |
| Confluent | 6,960,000 | 2026-07-29T10:06:41.1790477+00:00 | 154.1ms | GC pause | - | - | 10.0s / 749,058 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 6,963,000 | 2026-07-29T10:06:41.1808054+00:00 | 152.4ms | GC pause | - | - | 10.0s / 749,058 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 6,970,000 | 2026-07-29T10:06:41.1952281+00:00 | 159.4ms | GC pause | - | - | 10.0s / 749,058 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 6,972,000 | 2026-07-29T10:06:41.1964043+00:00 | 149.2ms | GC pause | - | - | 10.0s / 749,058 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 6,973,000 | 2026-07-29T10:06:41.2032434+00:00 | 157.3ms | GC pause | - | - | 10.0s / 749,058 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 6,980,000 | 2026-07-29T10:06:41.2197765+00:00 | 141.6ms | GC pause | - | - | 10.0s / 749,058 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 6,982,000 | 2026-07-29T10:06:41.2231925+00:00 | 138.6ms | GC pause | - | - | 10.0s / 749,058 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 6,983,000 | 2026-07-29T10:06:41.2238655+00:00 | 141.8ms | GC pause | - | - | 10.0s / 749,058 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 6,990,000 | 2026-07-29T10:06:41.2342508+00:00 | 142.0ms | GC pause | - | - | 10.0s / 749,058 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 6,992,000 | 2026-07-29T10:06:41.2356551+00:00 | 131.1ms | GC pause | - | - | 10.0s / 749,058 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 6,993,000 | 2026-07-29T10:06:41.2396615+00:00 | 139.2ms | GC pause | - | - | 10.0s / 749,058 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 7,000,000 | 2026-07-29T10:06:41.2536782+00:00 | 132.8ms | GC pause | - | - | 10.0s / 749,058 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 7,002,000 | 2026-07-29T10:06:41.2623653+00:00 | 117.1ms | GC pause | - | - | 10.0s / 749,058 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 7,003,000 | 2026-07-29T10:06:41.2634175+00:00 | 123.1ms | GC pause | - | - | 10.0s / 749,058 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 7,010,000 | 2026-07-29T10:06:41.2861844+00:00 | 108.3ms | GC pause | - | - | 10.0s / 749,058 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 7,012,000 | 2026-07-29T10:06:41.2943831+00:00 | 105.9ms | GC pause | - | - | 10.0s / 749,058 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 7,013,000 | 2026-07-29T10:06:41.2955174+00:00 | 109.9ms | GC pause | - | - | 10.0s / 749,058 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 50,464,000 | 2026-07-29T10:07:43.1333876+00:00 | 112.1ms | GC pause | - | - | 72.0s / 631,692 msg/s | Gen2 +0 / pause +115.7ms |
| Confluent | 97,372,000 | 2026-07-29T10:08:45.6813386+00:00 | 103.4ms | GC pause | - | - | 135.1s / 636,859 msg/s | Gen2 +0 / pause +151.5ms |
| Confluent | 188,894,000 | 2026-07-29T10:10:47.4899814+00:00 | 124.0ms | GC pause | - | - | 256.2s / 527,121 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 188,895,000 | 2026-07-29T10:10:47.4910048+00:00 | 108.9ms | GC pause | - | - | 256.2s / 527,121 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 188,896,000 | 2026-07-29T10:10:47.4920375+00:00 | 107.9ms | GC pause | - | - | 256.2s / 527,121 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 188,897,000 | 2026-07-29T10:10:47.5019629+00:00 | 102.4ms | GC pause | - | - | 256.2s / 527,121 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 211,082,000 | 2026-07-29T10:11:18.6816103+00:00 | 149.6ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,090,000 | 2026-07-29T10:11:18.6922282+00:00 | 161.0ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,092,000 | 2026-07-29T10:11:18.6952759+00:00 | 150.1ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,093,000 | 2026-07-29T10:11:18.6970631+00:00 | 156.3ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,100,000 | 2026-07-29T10:11:18.7149417+00:00 | 148.9ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,102,000 | 2026-07-29T10:11:18.7169211+00:00 | 149.3ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,103,000 | 2026-07-29T10:11:18.7247862+00:00 | 143.4ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,110,000 | 2026-07-29T10:11:18.7369157+00:00 | 138.6ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,112,000 | 2026-07-29T10:11:18.7389617+00:00 | 139.0ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,113,000 | 2026-07-29T10:11:18.7454346+00:00 | 130.2ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,120,000 | 2026-07-29T10:11:18.756339+00:00 | 129.0ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,122,000 | 2026-07-29T10:11:18.7630752+00:00 | 119.3ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,123,000 | 2026-07-29T10:11:18.7651489+00:00 | 120.3ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,130,000 | 2026-07-29T10:11:18.7807787+00:00 | 111.8ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,132,000 | 2026-07-29T10:11:18.7830147+00:00 | 112.4ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,133,000 | 2026-07-29T10:11:18.7841119+00:00 | 115.0ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,140,000 | 2026-07-29T10:11:18.8016463+00:00 | 102.0ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,142,000 | 2026-07-29T10:11:18.803374+00:00 | 102.9ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 211,143,000 | 2026-07-29T10:11:18.8074409+00:00 | 100.4ms | GC pause | - | - | 288.2s / 633,208 msg/s | Gen2 +0 / pause +88.6ms |
| Confluent | 252,407,000 | 2026-07-29T10:12:20.7031654+00:00 | 103.7ms | GC pause | - | - | 350.2s / 457,609 msg/s | Gen2 +0 / pause +165.1ms |
| Confluent | 269,854,000 | 2026-07-29T10:12:51.3778024+00:00 | 162.5ms | GC pause | - | - | 380.3s / 415,187 msg/s | Gen2 +0 / pause +113.5ms |
| Confluent | 269,855,000 | 2026-07-29T10:12:51.3799307+00:00 | 142.3ms | GC pause | - | - | 380.3s / 415,187 msg/s | Gen2 +0 / pause +113.5ms |
| Confluent | 269,856,000 | 2026-07-29T10:12:51.3903037+00:00 | 131.9ms | GC pause | - | - | 380.3s / 415,187 msg/s | Gen2 +0 / pause +113.5ms |
| Confluent | 269,857,000 | 2026-07-29T10:12:51.3918361+00:00 | 136.9ms | GC pause | - | - | 380.3s / 415,187 msg/s | Gen2 +0 / pause +113.5ms |
| Confluent | 269,858,000 | 2026-07-29T10:12:51.3929902+00:00 | 135.8ms | GC pause | - | - | 380.3s / 415,187 msg/s | Gen2 +0 / pause +113.5ms |
| Confluent | 269,859,000 | 2026-07-29T10:12:51.3942946+00:00 | 128.2ms | GC pause | - | - | 380.3s / 415,187 msg/s | Gen2 +0 / pause +113.5ms |
| Confluent | 269,861,000 | 2026-07-29T10:12:51.4119045+00:00 | 127.4ms | GC pause | - | - | 380.3s / 415,187 msg/s | Gen2 +0 / pause +113.5ms |
| Confluent | 269,864,000 | 2026-07-29T10:12:51.4191161+00:00 | 133.0ms | GC pause | - | - | 380.3s / 415,187 msg/s | Gen2 +0 / pause +113.5ms |
| Confluent | 269,865,000 | 2026-07-29T10:12:51.4266106+00:00 | 110.8ms | GC pause | - | - | 380.3s / 415,187 msg/s | Gen2 +0 / pause +113.5ms |
| Confluent | 269,866,000 | 2026-07-29T10:12:51.4311922+00:00 | 106.3ms | GC pause | - | - | 380.3s / 415,187 msg/s | Gen2 +0 / pause +113.5ms |
| Confluent | 269,867,000 | 2026-07-29T10:12:51.4391461+00:00 | 112.5ms | GC pause | - | - | 380.3s / 415,187 msg/s | Gen2 +0 / pause +113.5ms |
| Confluent | 269,868,000 | 2026-07-29T10:12:51.4404071+00:00 | 111.3ms | GC pause | - | - | 380.3s / 415,187 msg/s | Gen2 +0 / pause +113.5ms |
| Confluent | 269,869,000 | 2026-07-29T10:12:51.4453508+00:00 | 102.5ms | GC pause | - | - | 380.3s / 415,187 msg/s | Gen2 +0 / pause +113.5ms |
| Confluent | 269,871,000 | 2026-07-29T10:12:51.4477725+00:00 | 104.0ms | GC pause | - | - | 380.3s / 415,187 msg/s | Gen2 +0 / pause +113.5ms |
| Confluent | 269,874,000 | 2026-07-29T10:12:51.4648268+00:00 | 104.9ms | GC pause | - | - | 380.3s / 415,187 msg/s | Gen2 +0 / pause +113.5ms |
| Confluent | 269,878,000 | 2026-07-29T10:12:51.4748927+00:00 | 102.9ms | GC pause | - | - | 380.3s / 415,187 msg/s | Gen2 +0 / pause +113.5ms |
| Confluent | 287,055,000 | 2026-07-29T10:13:22.5939433+00:00 | 105.9ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,056,000 | 2026-07-29T10:13:22.5948212+00:00 | 105.1ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,058,000 | 2026-07-29T10:13:22.59704+00:00 | 105.7ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,059,000 | 2026-07-29T10:13:22.5978468+00:00 | 108.7ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,061,000 | 2026-07-29T10:13:22.6012175+00:00 | 101.6ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,065,000 | 2026-07-29T10:13:22.6085246+00:00 | 123.8ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,066,000 | 2026-07-29T10:13:22.609342+00:00 | 123.0ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,067,000 | 2026-07-29T10:13:22.6101705+00:00 | 117.2ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,068,000 | 2026-07-29T10:13:22.6110717+00:00 | 116.3ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,069,000 | 2026-07-29T10:13:22.6170937+00:00 | 115.3ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,071,000 | 2026-07-29T10:13:22.6186901+00:00 | 116.0ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,075,000 | 2026-07-29T10:13:22.6269716+00:00 | 114.4ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,076,000 | 2026-07-29T10:13:22.6277957+00:00 | 121.8ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,077,000 | 2026-07-29T10:13:22.6308944+00:00 | 113.5ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,078,000 | 2026-07-29T10:13:22.6318916+00:00 | 112.5ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,079,000 | 2026-07-29T10:13:22.6333699+00:00 | 116.2ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,081,000 | 2026-07-29T10:13:22.6348481+00:00 | 123.2ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,085,000 | 2026-07-29T10:13:22.6429325+00:00 | 115.3ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,086,000 | 2026-07-29T10:13:22.6440205+00:00 | 114.3ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,087,000 | 2026-07-29T10:13:22.6448206+00:00 | 117.2ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,088,000 | 2026-07-29T10:13:22.6456524+00:00 | 116.4ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 287,089,000 | 2026-07-29T10:13:22.6467469+00:00 | 117.7ms | GC pause | - | - | 411.3s / 490,743 msg/s | Gen2 +0 / pause +105.0ms |
| Confluent | 299,247,000 | 2026-07-29T10:13:42.1804655+00:00 | 104.4ms | GC pause | - | - | 431.3s / 522,414 msg/s | Gen2 +0 / pause +133.9ms |
| Confluent | 299,248,000 | 2026-07-29T10:13:42.181372+00:00 | 103.6ms | GC pause | - | - | 431.3s / 522,414 msg/s | Gen2 +0 / pause +133.9ms |
| Confluent | 299,257,000 | 2026-07-29T10:13:42.1970701+00:00 | 101.3ms | GC pause | - | - | 431.3s / 522,414 msg/s | Gen2 +0 / pause +133.9ms |
| Confluent | 299,258,000 | 2026-07-29T10:13:42.201425+00:00 | 116.3ms | GC pause | - | - | 431.3s / 522,414 msg/s | Gen2 +0 / pause +133.9ms |
| Confluent | 311,475,000 | 2026-07-29T10:14:02.6813316+00:00 | 122.8ms | GC pause | - | - | 451.4s / 548,041 msg/s | Gen2 +0 / pause +168.8ms |
| Confluent | 311,476,000 | 2026-07-29T10:14:02.6834692+00:00 | 120.8ms | GC pause | - | - | 451.4s / 548,041 msg/s | Gen2 +0 / pause +168.8ms |
| Confluent | 311,477,000 | 2026-07-29T10:14:02.6843693+00:00 | 118.8ms | GC pause | - | - | 451.4s / 548,041 msg/s | Gen2 +0 / pause +168.8ms |
| Confluent | 311,478,000 | 2026-07-29T10:14:02.6893268+00:00 | 115.6ms | GC pause | - | - | 451.4s / 548,041 msg/s | Gen2 +0 / pause +168.8ms |
| Confluent | 311,479,000 | 2026-07-29T10:14:02.6901698+00:00 | 114.2ms | GC pause | - | - | 451.4s / 548,041 msg/s | Gen2 +0 / pause +168.8ms |
| Confluent | 311,480,000 | 2026-07-29T10:14:02.6915082+00:00 | 112.1ms | GC pause | - | - | 451.4s / 548,041 msg/s | Gen2 +0 / pause +168.8ms |
| Confluent | 311,481,000 | 2026-07-29T10:14:02.6922715+00:00 | 112.9ms | GC pause | - | - | 451.4s / 548,041 msg/s | Gen2 +0 / pause +168.8ms |
| Confluent | 311,482,000 | 2026-07-29T10:14:02.6930084+00:00 | 111.6ms | GC pause | - | - | 451.4s / 548,041 msg/s | Gen2 +0 / pause +168.8ms |
| Confluent | 311,483,000 | 2026-07-29T10:14:02.6996183+00:00 | 104.1ms | GC pause | - | - | 451.4s / 548,041 msg/s | Gen2 +0 / pause +168.8ms |
| Confluent | 311,484,000 | 2026-07-29T10:14:02.7014745+00:00 | 103.8ms | GC pause | - | - | 451.4s / 548,041 msg/s | Gen2 +0 / pause +168.8ms |
| Confluent | 311,485,000 | 2026-07-29T10:14:02.7022561+00:00 | 103.9ms | GC pause | - | - | 451.4s / 548,041 msg/s | Gen2 +0 / pause +168.8ms |
| Confluent | 311,486,000 | 2026-07-29T10:14:02.7029942+00:00 | 104.2ms | GC pause | - | - | 451.4s / 548,041 msg/s | Gen2 +0 / pause +168.8ms |
| Dekaf (3conn) | 45,000 | 2026-07-29T10:21:43.9535028+00:00 | 105.3ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 55,000 | 2026-07-29T10:21:43.9656289+00:00 | 118.2ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 67,000 | 2026-07-29T10:21:43.9814846+00:00 | 199.8ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 75,000 | 2026-07-29T10:21:44.0082371+00:00 | 139.1ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 77,000 | 2026-07-29T10:21:44.0105362+00:00 | 207.9ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 78,000 | 2026-07-29T10:21:44.0117841+00:00 | 135.6ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 85,000 | 2026-07-29T10:21:44.02376+00:00 | 136.0ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 87,000 | 2026-07-29T10:21:44.0333572+00:00 | 197.0ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 95,000 | 2026-07-29T10:21:44.0610411+00:00 | 118.8ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 97,000 | 2026-07-29T10:21:44.0713717+00:00 | 192.7ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 98,000 | 2026-07-29T10:21:44.0727449+00:00 | 107.1ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 107,000 | 2026-07-29T10:21:44.1355039+00:00 | 152.1ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 117,000 | 2026-07-29T10:21:44.1566742+00:00 | 135.8ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 137,000 | 2026-07-29T10:21:44.1948536+00:00 | 137.1ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 177,000 | 2026-07-29T10:21:44.298208+00:00 | 117.8ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 184,000 | 2026-07-29T10:21:44.3095406+00:00 | 151.7ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 186,000 | 2026-07-29T10:21:44.3113742+00:00 | 149.9ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 187,000 | 2026-07-29T10:21:44.3126935+00:00 | 132.6ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 190,000 | 2026-07-29T10:21:44.3159868+00:00 | 150.5ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 197,000 | 2026-07-29T10:21:44.3262756+00:00 | 124.5ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 204,000 | 2026-07-29T10:21:44.3371776+00:00 | 158.3ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 210,000 | 2026-07-29T10:21:44.3496033+00:00 | 148.6ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 214,000 | 2026-07-29T10:21:44.3549287+00:00 | 147.6ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 217,000 | 2026-07-29T10:21:44.360706+00:00 | 113.6ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 219,000 | 2026-07-29T10:21:44.3641151+00:00 | 101.6ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 224,000 | 2026-07-29T10:21:44.3758648+00:00 | 169.1ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 227,000 | 2026-07-29T10:21:44.3827101+00:00 | 156.5ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 230,000 | 2026-07-29T10:21:44.3868991+00:00 | 128.1ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 236,000 | 2026-07-29T10:21:44.4619807+00:00 | 105.9ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 244,000 | 2026-07-29T10:21:44.4743708+00:00 | 108.2ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 246,000 | 2026-07-29T10:21:44.4775544+00:00 | 105.0ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 256,000 | 2026-07-29T10:21:44.4973766+00:00 | 103.2ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 263,000 | 2026-07-29T10:21:44.504842+00:00 | 131.7ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 264,000 | 2026-07-29T10:21:44.50697+00:00 | 107.1ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 266,000 | 2026-07-29T10:21:44.5098575+00:00 | 104.2ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 273,000 | 2026-07-29T10:21:44.5208924+00:00 | 179.6ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 276,000 | 2026-07-29T10:21:44.5262802+00:00 | 104.5ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 279,000 | 2026-07-29T10:21:44.5299369+00:00 | 180.0ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 281,000 | 2026-07-29T10:21:44.5338578+00:00 | 159.4ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 282,000 | 2026-07-29T10:21:44.5347668+00:00 | 158.5ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 283,000 | 2026-07-29T10:21:44.5368716+00:00 | 173.0ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 284,000 | 2026-07-29T10:21:44.538264+00:00 | 101.2ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 287,000 | 2026-07-29T10:21:44.5425708+00:00 | 131.9ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 291,000 | 2026-07-29T10:21:44.5475178+00:00 | 266.5ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 293,000 | 2026-07-29T10:21:44.5514304+00:00 | 172.3ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 301,000 | 2026-07-29T10:21:44.5708865+00:00 | 292.0ms | GC pause | - | - | 2.0s / 599,704 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 303,000 | 2026-07-29T10:21:44.5743967+00:00 | 168.1ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 307,000 | 2026-07-29T10:21:44.5838596+00:00 | 158.4ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 308,000 | 2026-07-29T10:21:44.5853192+00:00 | 113.3ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 309,000 | 2026-07-29T10:21:44.5863717+00:00 | 176.5ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 313,000 | 2026-07-29T10:21:44.5951234+00:00 | 167.7ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 321,000 | 2026-07-29T10:21:44.691908+00:00 | 199.2ms | GC pause | - | - | 2.0s / 599,704 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 322,000 | 2026-07-29T10:21:44.6936451+00:00 | 197.4ms | GC pause | - | - | 2.0s / 599,704 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 323,000 | 2026-07-29T10:21:44.6954141+00:00 | 161.0ms | GC pause | - | - | 1.0s / 356,701 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 329,000 | 2026-07-29T10:21:44.7112989+00:00 | 153.0ms | GC pause | - | - | 2.0s / 599,704 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 333,000 | 2026-07-29T10:21:44.7193374+00:00 | 161.2ms | GC pause | - | - | 2.0s / 599,704 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 339,000 | 2026-07-29T10:21:44.7415197+00:00 | 148.8ms | GC pause | - | - | 2.0s / 599,704 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 349,000 | 2026-07-29T10:21:44.7650138+00:00 | 164.9ms | GC pause | - | - | 2.0s / 599,704 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 351,000 | 2026-07-29T10:21:44.81697+00:00 | 165.9ms | GC pause | - | - | 2.0s / 599,704 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 352,000 | 2026-07-29T10:21:44.8189781+00:00 | 163.9ms | GC pause | - | - | 2.0s / 599,704 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 359,000 | 2026-07-29T10:21:44.8645246+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 599,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 361,000 | 2026-07-29T10:21:44.8687797+00:00 | 124.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 599,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 362,000 | 2026-07-29T10:21:44.8714589+00:00 | 139.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 599,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 372,000 | 2026-07-29T10:21:44.9001922+00:00 | 121.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 599,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 381,000 | 2026-07-29T10:21:44.921384+00:00 | 125.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 599,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 392,000 | 2026-07-29T10:21:44.9599047+00:00 | 119.3ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 599,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 402,000 | 2026-07-29T10:21:44.9825745+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 599,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 411,000 | 2026-07-29T10:21:45.0011364+00:00 | 110.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 599,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 412,000 | 2026-07-29T10:21:45.0035349+00:00 | 108.3ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 599,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 482,000 | 2026-07-29T10:21:45.1537516+00:00 | 131.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 599,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 492,000 | 2026-07-29T10:21:45.1652899+00:00 | 126.3ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 599,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 502,000 | 2026-07-29T10:21:45.1772978+00:00 | 128.3ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 599,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,084,000 | 2026-07-29T10:21:46.0260125+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,086,000 | 2026-07-29T10:21:46.0270959+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,094,000 | 2026-07-29T10:21:46.0348311+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,106,000 | 2026-07-29T10:21:46.0467477+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,134,000 | 2026-07-29T10:21:46.0851639+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,156,000 | 2026-07-29T10:21:46.1368673+00:00 | 114.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,164,000 | 2026-07-29T10:21:46.145967+00:00 | 113.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,166,000 | 2026-07-29T10:21:46.1493338+00:00 | 110.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,174,000 | 2026-07-29T10:21:46.156271+00:00 | 108.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,184,000 | 2026-07-29T10:21:46.1692114+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,186,000 | 2026-07-29T10:21:46.1710152+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,334,000 | 2026-07-29T10:21:46.3847392+00:00 | 105.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,506,000 | 2026-07-29T10:21:46.6377752+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,514,000 | 2026-07-29T10:21:46.6489889+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,516,000 | 2026-07-29T10:21:46.6500114+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,524,000 | 2026-07-29T10:21:46.6602414+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,526,000 | 2026-07-29T10:21:46.6612123+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,534,000 | 2026-07-29T10:21:46.6718291+00:00 | 106.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,538,000 | 2026-07-29T10:21:46.6781227+00:00 | 130.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,545,000 | 2026-07-29T10:21:46.6908335+00:00 | 128.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,547,000 | 2026-07-29T10:21:46.6927633+00:00 | 124.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,548,000 | 2026-07-29T10:21:46.6969885+00:00 | 122.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,557,000 | 2026-07-29T10:21:46.7074909+00:00 | 150.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,558,000 | 2026-07-29T10:21:46.7156957+00:00 | 116.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,565,000 | 2026-07-29T10:21:46.7298377+00:00 | 108.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,568,000 | 2026-07-29T10:21:46.7386908+00:00 | 103.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,578,000 | 2026-07-29T10:21:46.7522421+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 662,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,585,000 | 2026-07-29T10:21:46.767132+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,228 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,824,000 | 2026-07-29T10:21:47.1059778+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,228 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,836,000 | 2026-07-29T10:21:47.1172266+00:00 | 115.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,228 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,854,000 | 2026-07-29T10:21:47.1390987+00:00 | 108.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,228 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,856,000 | 2026-07-29T10:21:47.140659+00:00 | 107.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,228 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,857,000 | 2026-07-29T10:21:47.1413852+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,228 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,864,000 | 2026-07-29T10:21:47.1469686+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,228 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,866,000 | 2026-07-29T10:21:47.1480592+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,228 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,867,000 | 2026-07-29T10:21:47.1584112+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,228 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,877,000 | 2026-07-29T10:21:47.1678805+00:00 | 107.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,228 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,926,000 | 2026-07-29T10:21:47.2579665+00:00 | 103.5ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,228 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,934,000 | 2026-07-29T10:21:47.2672038+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,228 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,936,000 | 2026-07-29T10:21:47.2691143+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,228 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,542,000 | 2026-07-29T10:21:49.1145005+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 846,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,992,000 | 2026-07-29T10:21:49.6682006+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 846,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,851,000 | 2026-07-29T10:21:50.6679898+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 831,037 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,861,000 | 2026-07-29T10:21:50.6864337+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 831,037 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,647,000 | 2026-07-29T10:21:51.6340826+00:00 | 120.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 841,084 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,657,000 | 2026-07-29T10:21:51.6476218+00:00 | 123.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 841,084 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,677,000 | 2026-07-29T10:21:51.6678924+00:00 | 123.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 841,084 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,084,000 | 2026-07-29T10:21:52.1065176+00:00 | 109.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 865,248 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,096,000 | 2026-07-29T10:21:52.1157258+00:00 | 117.5ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 865,248 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,114,000 | 2026-07-29T10:21:52.1364355+00:00 | 117.6ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 865,248 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,116,000 | 2026-07-29T10:21:52.1395211+00:00 | 114.6ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 865,248 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,126,000 | 2026-07-29T10:21:52.15644+00:00 | 113.7ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 865,248 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,136,000 | 2026-07-29T10:21:52.1706002+00:00 | 107.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 865,248 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,417,000 | 2026-07-29T10:21:52.5014329+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 865,248 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,847,000 | 2026-07-29T10:21:54.1226721+00:00 | 111.9ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 839,762 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,857,000 | 2026-07-29T10:21:54.1349592+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 839,762 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,867,000 | 2026-07-29T10:21:54.1441618+00:00 | 111.1ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 839,762 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,877,000 | 2026-07-29T10:21:54.1535843+00:00 | 111.0ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 839,762 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,247,000 | 2026-07-29T10:21:54.6269874+00:00 | 111.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 839,762 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,257,000 | 2026-07-29T10:21:54.6359659+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 839,762 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,277,000 | 2026-07-29T10:21:54.6606979+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 839,762 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,356,000 | 2026-07-29T10:21:54.7768125+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 877,106 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,374,000 | 2026-07-29T10:21:54.7928536+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 877,106 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,376,000 | 2026-07-29T10:21:54.7943853+00:00 | 105.7ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 877,106 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,054,000 | 2026-07-29T10:21:56.6450725+00:00 | 113.9ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 947,160 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,056,000 | 2026-07-29T10:21:56.6469767+00:00 | 112.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 947,160 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,064,000 | 2026-07-29T10:21:56.6532202+00:00 | 114.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 947,160 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,076,000 | 2026-07-29T10:21:56.671835+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 947,160 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,084,000 | 2026-07-29T10:21:56.6871265+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 947,160 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,086,000 | 2026-07-29T10:21:56.6886008+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 947,160 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,351,000 | 2026-07-29T10:22:01.6811476+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 1,078,066 msg/s | Gen2 +0 / pause +2.6ms |
| Dekaf (3conn) | 15,352,000 | 2026-07-29T10:22:01.6818758+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 1,078,066 msg/s | Gen2 +0 / pause +2.6ms |
| Dekaf (3conn) | 22,991,000 | 2026-07-29T10:22:09.1253881+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 1,058,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 23,001,000 | 2026-07-29T10:22:09.1316578+00:00 | 108.1ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 1,058,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 23,002,000 | 2026-07-29T10:22:09.1322587+00:00 | 107.5ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 1,058,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 23,011,000 | 2026-07-29T10:22:09.1428319+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 1,058,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 23,012,000 | 2026-07-29T10:22:09.1438122+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 1,058,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 27,736,000 | 2026-07-29T10:22:13.67325+00:00 | 109.8ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 921,095 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 33,202,000 | 2026-07-29T10:22:19.10214+00:00 | 116.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 36.0s / 1,041,070 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 33,211,000 | 2026-07-29T10:22:19.1142748+00:00 | 109.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 36.0s / 1,041,070 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 33,212,000 | 2026-07-29T10:22:19.1148865+00:00 | 109.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 36.0s / 1,041,070 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 33,216,000 | 2026-07-29T10:22:19.1238047+00:00 | 110.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 36.0s / 1,041,070 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 34,344,000 | 2026-07-29T10:22:20.1809553+00:00 | 103.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 37.0s / 1,103,500 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 34,354,000 | 2026-07-29T10:22:20.1882975+00:00 | 107.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 37.0s / 1,103,500 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 34,356,000 | 2026-07-29T10:22:20.1892433+00:00 | 106.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 37.0s / 1,103,500 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 39,624,000 | 2026-07-29T10:22:25.1128252+00:00 | 105.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 42.0s / 1,032,177 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 39,626,000 | 2026-07-29T10:22:25.1138462+00:00 | 104.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 42.0s / 1,032,177 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 39,644,000 | 2026-07-29T10:22:25.1317866+00:00 | 102.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 42.0s / 1,032,177 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 39,647,000 | 2026-07-29T10:22:25.1362683+00:00 | 103.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 42.0s / 1,032,177 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 54,881,000 | 2026-07-29T10:22:38.6464729+00:00 | 107.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 55.0s / 857,642 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 54,891,000 | 2026-07-29T10:22:38.6505499+00:00 | 122.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 55.0s / 857,642 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 54,901,000 | 2026-07-29T10:22:38.6636631+00:00 | 113.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 55.0s / 857,642 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 54,911,000 | 2026-07-29T10:22:38.6879198+00:00 | 106.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 55.0s / 857,642 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 55,754,000 | 2026-07-29T10:22:39.6578871+00:00 | 116.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 55,756,000 | 2026-07-29T10:22:39.659719+00:00 | 114.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 55,757,000 | 2026-07-29T10:22:39.6601505+00:00 | 102.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 55,764,000 | 2026-07-29T10:22:39.6633065+00:00 | 120.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 55,766,000 | 2026-07-29T10:22:39.6639166+00:00 | 119.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 55,767,000 | 2026-07-29T10:22:39.664617+00:00 | 111.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 55,769,000 | 2026-07-29T10:22:39.6656086+00:00 | 110.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 55,770,000 | 2026-07-29T10:22:39.6659205+00:00 | 110.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 55,773,000 | 2026-07-29T10:22:39.6673206+00:00 | 117.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 55,774,000 | 2026-07-29T10:22:39.6678564+00:00 | 123.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 55,777,000 | 2026-07-29T10:22:39.6690398+00:00 | 130.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 55,779,000 | 2026-07-29T10:22:39.6698388+00:00 | 121.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 55,780,000 | 2026-07-29T10:22:39.6704221+00:00 | 117.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 55,781,000 | 2026-07-29T10:22:39.6709594+00:00 | 101.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 55,783,000 | 2026-07-29T10:22:39.671945+00:00 | 119.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 55,784,000 | 2026-07-29T10:22:39.6722793+00:00 | 132.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 55,786,000 | 2026-07-29T10:22:39.6736484+00:00 | 131.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 55,788,000 | 2026-07-29T10:22:39.6866759+00:00 | 103.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 56.0s / 901,949 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 67,984,000 | 2026-07-29T10:22:51.1475899+00:00 | 113.3ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed, 3:capacity/failed | - | 68.0s / 749,525 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 67,986,000 | 2026-07-29T10:22:51.1522799+00:00 | 108.6ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed, 3:capacity/failed | - | 68.0s / 749,525 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 129,509,000 | 2026-07-29T10:23:51.1486288+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 128.1s / 847,175 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 129,513,000 | 2026-07-29T10:23:51.1525366+00:00 | 107.4ms | broker/backlog (no scale or GC event) | - | - | 128.1s / 847,175 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 129,519,000 | 2026-07-29T10:23:51.1654583+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 128.1s / 847,175 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 280,883,000 | 2026-07-29T10:26:21.8505731+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 278.2s / 978,745 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 280,889,000 | 2026-07-29T10:26:21.8542185+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 278.2s / 978,745 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 280,893,000 | 2026-07-29T10:26:21.8564048+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 278.2s / 978,745 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 280,903,000 | 2026-07-29T10:26:21.8648957+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 278.2s / 978,745 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 280,904,000 | 2026-07-29T10:26:21.8652295+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 278.2s / 978,745 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 280,912,000 | 2026-07-29T10:26:21.8768044+00:00 | 109.2ms | broker/backlog (no scale or GC event) | - | - | 278.2s / 978,745 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 280,913,000 | 2026-07-29T10:26:21.8776204+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 278.2s / 978,745 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 280,916,000 | 2026-07-29T10:26:21.8789682+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 278.2s / 978,745 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 280,919,000 | 2026-07-29T10:26:21.8808383+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 278.2s / 978,745 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 321,349,000 | 2026-07-29T10:27:02.6664918+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 319.2s / 991,739 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 321,353,000 | 2026-07-29T10:27:02.6712704+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 319.2s / 991,739 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 373,911,000 | 2026-07-29T10:27:51.6737319+00:00 | 105.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 368.3s / 1,037,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 373,914,000 | 2026-07-29T10:27:51.6756472+00:00 | 112.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 368.3s / 1,037,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 373,916,000 | 2026-07-29T10:27:51.6812079+00:00 | 107.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 368.3s / 1,037,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 387,332,000 | 2026-07-29T10:28:03.6653345+00:00 | 109.5ms | broker/backlog (no scale or GC event) | - | - | 380.3s / 1,029,762 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 387,341,000 | 2026-07-29T10:28:03.6797175+00:00 | 103.5ms | broker/backlog (no scale or GC event) | - | - | 380.3s / 1,029,762 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 431,541,000 | 2026-07-29T10:28:42.1495113+00:00 | 100.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/failed | - | 419.3s / 1,167,041 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 431,551,000 | 2026-07-29T10:28:42.1616999+00:00 | 100.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/failed | - | 419.3s / 1,167,041 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 460,721,000 | 2026-07-29T10:29:07.1274255+00:00 | 107.0ms | broker/backlog (no scale or GC event) | - | - | 444.3s / 1,069,580 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 460,731,000 | 2026-07-29T10:29:07.1386395+00:00 | 108.5ms | broker/backlog (no scale or GC event) | - | - | 444.3s / 1,069,580 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 460,732,000 | 2026-07-29T10:29:07.1403565+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 444.3s / 1,069,580 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 460,741,000 | 2026-07-29T10:29:07.1510409+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 444.3s / 1,069,580 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 460,742,000 | 2026-07-29T10:29:07.1512937+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 444.3s / 1,069,580 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 460,761,000 | 2026-07-29T10:29:07.1789709+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 444.3s / 1,069,580 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 493,923,000 | 2026-07-29T10:29:35.6654177+00:00 | 107.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 472.3s / 1,123,144 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 493,931,000 | 2026-07-29T10:29:35.6710915+00:00 | 112.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 472.3s / 1,123,144 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 493,932,000 | 2026-07-29T10:29:35.6724518+00:00 | 110.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 472.3s / 1,123,144 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 493,933,000 | 2026-07-29T10:29:35.6755198+00:00 | 101.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 472.3s / 1,123,144 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 555,113,000 | 2026-07-29T10:30:29.6735125+00:00 | 106.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 526.4s / 1,018,469 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 555,119,000 | 2026-07-29T10:30:29.6852297+00:00 | 103.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 526.4s / 1,018,469 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 642,307,000 | 2026-07-29T10:31:52.6458467+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 609.4s / 825,144 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 651,179,000 | 2026-07-29T10:32:01.1403544+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 617.4s / 998,463 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,784,000 | 2026-07-29T10:33:05.161472+00:00 | 107.1ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 681.4s / 977,420 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,786,000 | 2026-07-29T10:33:05.16244+00:00 | 106.2ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 681.4s / 977,420 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,788,000 | 2026-07-29T10:33:05.1639159+00:00 | 106.0ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 681.4s / 977,420 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 719,789,000 | 2026-07-29T10:33:05.1648925+00:00 | 105.2ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 681.4s / 977,420 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 734,147,000 | 2026-07-29T10:33:19.6720545+00:00 | 108.3ms | broker/backlog (no scale or GC event) | - | - | 696.4s / 927,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 734,151,000 | 2026-07-29T10:33:19.6751584+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 696.4s / 927,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 734,152,000 | 2026-07-29T10:33:19.6755781+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 696.4s / 927,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 734,157,000 | 2026-07-29T10:33:19.6786231+00:00 | 126.6ms | broker/backlog (no scale or GC event) | - | - | 696.4s / 927,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 734,161,000 | 2026-07-29T10:33:19.6797191+00:00 | 110.8ms | broker/backlog (no scale or GC event) | - | - | 696.4s / 927,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 734,164,000 | 2026-07-29T10:33:19.68134+00:00 | 109.2ms | broker/backlog (no scale or GC event) | - | - | 696.4s / 927,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 734,165,000 | 2026-07-29T10:33:19.6815545+00:00 | 120.2ms | broker/backlog (no scale or GC event) | - | - | 696.4s / 927,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 734,167,000 | 2026-07-29T10:33:19.7072018+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 696.4s / 927,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 738,995,000 | 2026-07-29T10:33:25.1834607+00:00 | 113.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 701.4s / 906,069 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 739,891,000 | 2026-07-29T10:33:26.1603353+00:00 | 102.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 702.5s / 915,014 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 739,892,000 | 2026-07-29T10:33:26.1608611+00:00 | 101.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 702.5s / 915,014 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 739,898,000 | 2026-07-29T10:33:26.1688092+00:00 | 121.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 702.5s / 915,014 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 739,902,000 | 2026-07-29T10:33:26.1748918+00:00 | 111.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 702.5s / 915,014 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 739,905,000 | 2026-07-29T10:33:26.1768392+00:00 | 116.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 702.5s / 915,014 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 739,907,000 | 2026-07-29T10:33:26.1837665+00:00 | 106.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 702.5s / 915,014 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 739,911,000 | 2026-07-29T10:33:26.1903011+00:00 | 102.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 702.5s / 915,014 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 740,757,000 | 2026-07-29T10:33:27.1679198+00:00 | 101.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 703.5s / 877,817 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 740,768,000 | 2026-07-29T10:33:27.1769229+00:00 | 102.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 703.5s / 877,817 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 745,643,000 | 2026-07-29T10:33:32.167984+00:00 | 115.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 708.5s / 919,791 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 745,650,000 | 2026-07-29T10:33:32.1769712+00:00 | 107.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 708.5s / 919,791 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 745,653,000 | 2026-07-29T10:33:32.1878604+00:00 | 104.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 708.5s / 919,791 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 840,227,000 | 2026-07-29T10:35:01.1429725+00:00 | 110.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 797.5s / 921,288 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 852,029,000 | 2026-07-29T10:35:13.1688461+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 809.5s / 949,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 852,030,000 | 2026-07-29T10:35:13.1693757+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 809.5s / 949,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 852,033,000 | 2026-07-29T10:35:13.1747024+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 809.5s / 949,254 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 868,370,000 | 2026-07-29T10:35:29.6531311+00:00 | 106.5ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 826.5s / 1,034,511 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 902,170,000 | 2026-07-29T10:36:04.6464993+00:00 | 100.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 861.5s / 749,651 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 923,416,000 | 2026-07-29T10:36:28.177194+00:00 | 105.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 884.6s / 836,113 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 924,770,000 | 2026-07-29T10:36:29.667891+00:00 | 101.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 886.6s / 839,152 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 924,777,000 | 2026-07-29T10:36:29.6784687+00:00 | 103.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 886.6s / 839,152 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 924,780,000 | 2026-07-29T10:36:29.6793245+00:00 | 102.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 886.6s / 839,152 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 926,538,000 | 2026-07-29T10:36:31.6654875+00:00 | 111.7ms | broker/backlog (no scale or GC event) | - | - | 888.6s / 768,824 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*3,481 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.41x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers; comparison throughput is 1.13x.
:::

## Producer → Consumer Round-Trip Steady State Throughput (15 minutes, 128B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.38 | 3542.15 | 897,706 | 1,333,409 | +45.3% | +286.19% | 109.58 | 897,706 | 0 | 1.24 |
| Confluent | 2.46 | - | 122,330 | 994,733 | +17.7% | +70.56% | 14.93 | 122,330 | 0 | 0.30 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer → Consumer Round-Trip Steady State

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 7,701 | 494.56 | 489.46 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer → Consumer Round-Trip Steady State

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-29T09:51:26.327812+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 364,239 msg/s |
| Dekaf | 2026-07-29T09:51:27.3277331+00:00 | 1 | 16.0 MiB / 1.2 MiB | 64.7 MB/s | 0/0 | 0 | 1.0s / 364,239 msg/s |
| Dekaf | 2026-07-29T09:51:28.3319469+00:00 | 1 | 16.0 MiB / 1.9 MiB | 71.5 MB/s | 0/0 | 0 | 2.0s / 697,411 msg/s |
| Dekaf | 2026-07-29T09:51:29.336633+00:00 | 1 | 16.0 MiB / 4.3 MiB | 225.2 MB/s | 0/0 | 0 | 3.0s / 1,246,750 msg/s |
| Dekaf | 2026-07-29T09:51:30.3378315+00:00 | 1 | 16.0 MiB / 2.3 MiB | 300.1 MB/s | 0/0 | 0 | 4.0s / 1,405,122 msg/s |
| Dekaf | 2026-07-29T09:51:31.3389075+00:00 | 1 | 16.0 MiB / 3.3 MiB | 300.1 MB/s | 0/0 | 0 | 5.0s / 1,353,690 msg/s |
| Dekaf | 2026-07-29T09:51:32.3425116+00:00 | 1 | 16.0 MiB / 1.8 MiB | 300.1 MB/s | 0/0 | 0 | 6.0s / 1,296,563 msg/s |
| Dekaf | 2026-07-29T09:51:33.3456102+00:00 | 1 | 16.0 MiB / 1.1 MiB | 300.1 MB/s | 0/0 | 0 | 7.0s / 1,333,409 msg/s |
| Dekaf | 2026-07-29T09:51:34.3456866+00:00 | 1 | 16.0 MiB / 1.9 MiB | 300.1 MB/s | 0/0 | 0 | 8.0s / 1,306,632 msg/s |
| Dekaf | 2026-07-29T09:51:35.347461+00:00 | 1 | 16.0 MiB / 1.4 MiB | 300.1 MB/s | 0/0 | 0 | 9.0s / 1,391,568 msg/s |
| Dekaf | 2026-07-29T09:51:36.34905+00:00 | 1 | 16.0 MiB / 1.0 MiB | 300.1 MB/s | 0/0 | 0 | 10.0s / 1,172,585 msg/s |
| Dekaf | 2026-07-29T09:51:37.3498137+00:00 | 1 | 16.0 MiB / 1.3 MiB | 300.1 MB/s | 0/0 | 0 | 11.0s / 1,483,588 msg/s |
| Dekaf | 2026-07-29T09:51:38.3505678+00:00 | 1 | 16.0 MiB / 1.5 MiB | 321.2 MB/s | 0/0 | 0 | 12.0s / 1,561,803 msg/s |
| Dekaf | 2026-07-29T09:51:39.3523849+00:00 | 1 | 16.0 MiB / 2.2 MiB | 321.2 MB/s | 0/0 | 0 | 13.0s / 1,530,197 msg/s |
| Dekaf | 2026-07-29T09:51:40.3546271+00:00 | 1 | 16.0 MiB / 1.8 MiB | 321.2 MB/s | 0/0 | 0 | 14.0s / 1,464,021 msg/s |
| Dekaf | 2026-07-29T09:51:41.3564484+00:00 | 1 | 16.0 MiB / 2.1 MiB | 321.2 MB/s | 0/0 | 0 | 15.0s / 1,321,618 msg/s |

### Round-Trip Validation

| Client | Expected | Consumed | Missing | Duplicates | Corrupt | Out of Order | Wrong Partition | Unexpected | Timed Out | Result |
|--------|----------|----------|---------|------------|---------|--------------|-----------------|------------|-----------|--------|
| Confluent | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |
| Dekaf | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |

:::tip
**Dekaf uses 1.79x less CPU per message** than Confluent.Kafka for producer → consumer round-trip steady state; comparison throughput is 1.34x.
:::

## Producer (Transactional EOS), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 449.55 | 449.54 | 246 | 332 | +2.5% | +0.27% | 0.23 | 327 | 0 | 0.15 |
| Confluent | 303.64 | - | 123 | 166 | +7.3% | +0.67% | 0.12 | 165 | 0 | 0.05 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Transactional EOS), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 98,528 | 109.44 | 1.16 KB |
| Dekaf | 2 | 98,160 | 109.03 | 1.16 KB |
| Dekaf | 3 | 98,118 | 108.98 | 1.16 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Transactional EOS), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-29T09:51:29.8450921+00:00 | 3 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 140 msg/s |
| Dekaf | 2026-07-29T09:51:38.8596527+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.1 MB/s | 0/0 | 0 | 9.0s / 246 msg/s |
| Dekaf | 2026-07-29T09:51:47.8955525+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.1 MB/s | 0/0 | 0 | 18.0s / 269 msg/s |
| Dekaf | 2026-07-29T09:51:57.9382598+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 28.0s / 293 msg/s |
| Dekaf | 2026-07-29T09:52:06.9420538+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.1 MB/s | 0/0 | 0 | 37.0s / 293 msg/s |
| Dekaf | 2026-07-29T09:52:15.9468111+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 46.0s / 265 msg/s |
| Dekaf | 2026-07-29T09:52:24.952942+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 55.0s / 295 msg/s |
| Dekaf | 2026-07-29T09:52:33.956028+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 64.0s / 298 msg/s |
| Dekaf | 2026-07-29T09:52:42.9606161+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 73.0s / 321 msg/s |
| Dekaf | 2026-07-29T09:52:51.9682272+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 82.0s / 321 msg/s |
| Dekaf | 2026-07-29T09:53:00.9700923+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.1 MB/s | 0/0 | 0 | 91.0s / 326 msg/s |
| Dekaf | 2026-07-29T09:53:09.977651+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 100.0s / 300 msg/s |
| Dekaf | 2026-07-29T09:53:19.0140436+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.1 MB/s | 0/0 | 0 | 109.0s / 322 msg/s |
| Dekaf | 2026-07-29T09:53:28.0457494+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 118.0s / 322 msg/s |
| Dekaf | 2026-07-29T09:53:37.0489744+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 127.0s / 318 msg/s |
| Dekaf | 2026-07-29T09:53:47.0611826+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 137.0s / 332 msg/s |
| Dekaf | 2026-07-29T09:53:56.0843284+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 146.0s / 356 msg/s |
| Dekaf | 2026-07-29T09:54:05.0864364+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 155.0s / 351 msg/s |
| Dekaf | 2026-07-29T09:54:14.0999016+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 164.0s / 347 msg/s |
| Dekaf | 2026-07-29T09:54:23.1032762+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 173.0s / 346 msg/s |
| Dekaf | 2026-07-29T09:54:32.1060452+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 182.0s / 349 msg/s |
| Dekaf | 2026-07-29T09:54:41.1207669+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 191.0s / 347 msg/s |
| Dekaf | 2026-07-29T09:54:50.1389468+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 200.0s / 325 msg/s |
| Dekaf | 2026-07-29T09:54:59.1438143+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 209.0s / 342 msg/s |
| Dekaf | 2026-07-29T09:55:08.1626971+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 218.0s / 339 msg/s |
| Dekaf | 2026-07-29T09:55:17.1819317+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 227.0s / 340 msg/s |
| Dekaf | 2026-07-29T09:55:26.1959838+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 236.0s / 348 msg/s |
| Dekaf | 2026-07-29T09:55:36.200823+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 246.0s / 351 msg/s |
| Dekaf | 2026-07-29T09:55:45.2090323+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 255.0s / 347 msg/s |
| Dekaf | 2026-07-29T09:55:54.2124143+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 264.0s / 355 msg/s |
| Dekaf | 2026-07-29T09:56:03.2173487+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 273.0s / 349 msg/s |
| Dekaf | 2026-07-29T09:56:12.2222171+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 282.0s / 322 msg/s |
| Dekaf | 2026-07-29T09:56:21.2252263+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 291.0s / 338 msg/s |
| Dekaf | 2026-07-29T09:56:30.2305882+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 300.0s / 345 msg/s |
| Dekaf | 2026-07-29T09:56:39.2349826+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 309.0s / 358 msg/s |
| Dekaf | 2026-07-29T09:56:48.2393807+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 318.1s / 335 msg/s |
| Dekaf | 2026-07-29T09:56:57.2452786+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 327.1s / 308 msg/s |
| Dekaf | 2026-07-29T09:57:06.2608288+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 336.1s / 324 msg/s |
| Dekaf | 2026-07-29T09:57:15.2653369+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 345.1s / 341 msg/s |
| Dekaf | 2026-07-29T09:57:24.2850543+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 354.1s / 335 msg/s |
| Dekaf | 2026-07-29T09:57:34.2972517+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 364.1s / 315 msg/s |
| Dekaf | 2026-07-29T09:57:43.3005915+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 373.1s / 323 msg/s |
| Dekaf | 2026-07-29T09:57:52.3035997+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 382.1s / 324 msg/s |
| Dekaf | 2026-07-29T09:58:01.32215+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 391.1s / 326 msg/s |
| Dekaf | 2026-07-29T09:58:10.3438481+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 400.1s / 329 msg/s |
| Dekaf | 2026-07-29T09:58:19.3497992+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 409.1s / 326 msg/s |
| Dekaf | 2026-07-29T09:58:28.3525893+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 418.1s / 339 msg/s |
| Dekaf | 2026-07-29T09:58:37.3691316+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 427.1s / 350 msg/s |
| Dekaf | 2026-07-29T09:58:46.3757179+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 436.1s / 348 msg/s |
| Dekaf | 2026-07-29T09:58:55.3829099+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 445.1s / 326 msg/s |
| Dekaf | 2026-07-29T09:59:04.386644+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 454.1s / 328 msg/s |
| Dekaf | 2026-07-29T09:59:13.3918884+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 463.1s / 330 msg/s |
| Dekaf | 2026-07-29T09:59:23.4131825+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 473.1s / 343 msg/s |
| Dekaf | 2026-07-29T09:59:32.4355299+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 482.1s / 341 msg/s |
| Dekaf | 2026-07-29T09:59:41.4611369+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 491.1s / 330 msg/s |
| Dekaf | 2026-07-29T09:59:50.4828463+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 500.1s / 350 msg/s |
| Dekaf | 2026-07-29T09:59:59.5156849+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 510.1s / 338 msg/s |
| Dekaf | 2026-07-29T10:00:08.5191439+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 519.1s / 311 msg/s |
| Dekaf | 2026-07-29T10:00:17.5267511+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 528.1s / 321 msg/s |
| Dekaf | 2026-07-29T10:00:26.5423395+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 537.1s / 338 msg/s |
| Dekaf | 2026-07-29T10:00:35.5532987+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 546.1s / 348 msg/s |
| Dekaf | 2026-07-29T10:00:44.5685111+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 555.1s / 348 msg/s |
| Dekaf | 2026-07-29T10:00:53.5823999+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 564.1s / 320 msg/s |
| Dekaf | 2026-07-29T10:01:02.5847188+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 573.1s / 329 msg/s |
| Dekaf | 2026-07-29T10:01:12.608958+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 583.1s / 335 msg/s |
| Dekaf | 2026-07-29T10:01:21.6401514+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 592.1s / 330 msg/s |
| Dekaf | 2026-07-29T10:01:30.6456057+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 601.1s / 316 msg/s |
| Dekaf | 2026-07-29T10:01:39.6516033+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 610.1s / 329 msg/s |
| Dekaf | 2026-07-29T10:01:48.6571051+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 619.1s / 350 msg/s |
| Dekaf | 2026-07-29T10:01:57.6607304+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 628.1s / 326 msg/s |
| Dekaf | 2026-07-29T10:02:06.6640155+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 637.1s / 336 msg/s |
| Dekaf | 2026-07-29T10:02:15.6691239+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 646.1s / 323 msg/s |
| Dekaf | 2026-07-29T10:02:24.6715972+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 655.1s / 327 msg/s |
| Dekaf | 2026-07-29T10:02:33.7026962+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 664.1s / 342 msg/s |
| Dekaf | 2026-07-29T10:02:42.7127064+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 673.1s / 340 msg/s |
| Dekaf | 2026-07-29T10:02:51.7451516+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 682.1s / 341 msg/s |
| Dekaf | 2026-07-29T10:03:00.7506333+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 691.1s / 341 msg/s |
| Dekaf | 2026-07-29T10:03:10.7817854+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 701.1s / 339 msg/s |
| Dekaf | 2026-07-29T10:03:19.7908613+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 710.1s / 347 msg/s |
| Dekaf | 2026-07-29T10:03:28.8140868+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 719.1s / 344 msg/s |
| Dekaf | 2026-07-29T10:03:37.8191504+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 728.1s / 352 msg/s |
| Dekaf | 2026-07-29T10:03:46.8229362+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 737.1s / 348 msg/s |
| Dekaf | 2026-07-29T10:03:55.8247243+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 746.1s / 343 msg/s |
| Dekaf | 2026-07-29T10:04:04.8340374+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 755.1s / 339 msg/s |
| Dekaf | 2026-07-29T10:04:13.8385213+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 764.1s / 335 msg/s |
| Dekaf | 2026-07-29T10:04:22.842406+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 773.1s / 341 msg/s |
| Dekaf | 2026-07-29T10:04:31.8449018+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 782.1s / 340 msg/s |
| Dekaf | 2026-07-29T10:04:40.8474182+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 791.1s / 342 msg/s |
| Dekaf | 2026-07-29T10:04:49.8610545+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 800.1s / 338 msg/s |
| Dekaf | 2026-07-29T10:04:59.8649383+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 810.1s / 327 msg/s |
| Dekaf | 2026-07-29T10:05:08.8697607+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 819.1s / 302 msg/s |
| Dekaf | 2026-07-29T10:05:17.8787097+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 828.1s / 309 msg/s |
| Dekaf | 2026-07-29T10:05:26.881623+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 837.1s / 307 msg/s |
| Dekaf | 2026-07-29T10:05:35.8907642+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 846.1s / 303 msg/s |
| Dekaf | 2026-07-29T10:05:44.8938107+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 855.2s / 305 msg/s |
| Dekaf | 2026-07-29T10:05:53.9053613+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 864.2s / 304 msg/s |
| Dekaf | 2026-07-29T10:06:02.9103339+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 873.2s / 314 msg/s |
| Dekaf | 2026-07-29T10:06:11.9182582+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 882.2s / 295 msg/s |
| Dekaf | 2026-07-29T10:06:20.9221281+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 891.2s / 314 msg/s |
| Dekaf | 2026-07-29T10:06:29.9255451+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 899.2s / 314 msg/s |
*2,598 budget sample(s) omitted; rows sampled across the full timeline.*

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 148,100 | 111,100 | 37,000 | 111,100 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 294,800 | 221,100 | 73,700 | 221,100 | 0 | 0 | 0 | 0 | 0 | PASS |

:::note
Confluent.Kafka uses 1.48x less CPU per message for producer (transactional eos), 3 brokers; comparison throughput is 2.00x.
:::

## Consumer Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.88 | - | 1,545,004 | 1,542,794 | +8.4% | +0.66% | 1473.43 | - | 0 | 1.37 |
| Confluent | 1.34 | - | 1,116,946 | 1,138,704 | +4.3% | +0.50% | 1065.20 | - | 0 | 1.50 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

:::tip
**Dekaf uses 1.51x less CPU per message** than Confluent.Kafka for consumer; comparison throughput is 1.35x.
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.85 | - | 1,629,307 | 1,616,470 | -12.1% | -1.03% | 1553.83 | - | 0 | 1.38 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.47 | - | 3,309,487 | 3,357,964 | +16.6% | +1.69% | 3156.17 | - | 0 | 1.57 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.43 | - | 3,582,269 | 3,604,054 | +17.4% | +1.55% | 3416.32 | - | 0 | 1.56 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 19743 | 101 | 0 | 2284.37 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 188833 | 1 | 1 | 1009.00 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 226532 | 34 | 1 | 1110.83 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 138940 | 1 | 1 | 666.57 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 225087 | 1 | 1 | 1172.65 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 216493 | 6 | 1 | 1060.76 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 202388 | 1 | 1 | 971.90 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 239059 | 21 | 1 | 1153.11 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 243559 | 1 | 1 | 1261.54 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 159581 | 1 | 1 | 757.55 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 4753 | 7 | 5 | 21.41 GB | 1.13 KB |
| Confluent | Producer (Transactional EOS), 3 Brokers | 101 | 1 | 1 | 246.73 MB | 1.71 KB |
| Dekaf | Consumer | 23096 | 24 | 3 | 2621.68 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 24316 | 3 | 2 | 2765.01 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 3 | 1 | 0 | 461.60 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 9 | 4 | 2 | 909.73 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 358 | 2 | 2 | 161.72 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 355 | 2 | 2 | 1.31 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 159 | 3 | 2 | 677.68 MB | 1 B |
| Dekaf | Producer (Acks All) | 394 | 2 | 2 | 111.62 MB | 0 B |
| Dekaf | Producer (Acks All) | 380 | 3 | 2 | 1.41 GB | 1 B |
| Dekaf | Producer (Acks All), 3 Brokers | 178 | 2 | 2 | 858.88 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 405 | 2 | 2 | 1.53 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 394 | 2 | 2 | 133.29 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 148 | 3 | 2 | 596.40 MB | 1 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 1196 | 5 | 2 | 5.64 GB | 306 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 52 | 6 | 0 | 181.12 MB | 644 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 265 | 2 | 2 | 1.08 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 179 | 3 | 2 | 759.93 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 345 | 1 | 1 | 1.35 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 208 | 2 | 1 | 960.25 MB | 1 B |

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
